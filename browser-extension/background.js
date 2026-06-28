importScripts("rules.js");

const CONTENT_QUERY = { type: "ACSG_GET_CONTENT_SNAPSHOT" };
const recentViolationsByTab = new Map();
let runtimeRulesPromise = null;

function normalize(value) {
  return (value || "").toLowerCase();
}

function matchesKnownAdultDomain(hostname) {
  const host = normalize(hostname);
  return RULES.adultDomains.some((domain) => host === domain || host.endsWith(`.${domain}`));
}

function containsKeyword(text, keywords) {
  const normalized = normalize(text);
  return keywords.filter((keyword) => normalized.includes(keyword.toLowerCase()));
}

function isBrowserInternalUrl(url) {
  return !url ||
    url.startsWith("chrome://") ||
    url.startsWith("edge://") ||
    url.startsWith("about:");
}

function createUrlSnapshot(url, title) {
  try {
    const parsed = new URL(url);
    return {
      url,
      host: parsed.hostname,
      title: title || "",
      metaDescription: "",
      metaKeywords: "",
      headings: "",
      bodyText: ""
    };
  } catch {
    return {
      url,
      host: "",
      title: title || "",
      metaDescription: "",
      metaKeywords: "",
      headings: "",
      bodyText: ""
    };
  }
}

function getManagedConfig() {
  return new Promise((resolve) => {
    if (!chrome.storage?.managed) {
      resolve({});
      return;
    }

    chrome.storage.managed.get(["serviceUrl", "healthUrl", "token"], (items) => {
      if (chrome.runtime.lastError) {
        resolve({});
        return;
      }

      resolve(items || {});
    });
  });
}

async function getRuntimeRules() {
  if (!runtimeRulesPromise) {
    runtimeRulesPromise = getManagedConfig().then((managed) => ({
      ...RULES,
      serviceUrl: managed.serviceUrl || RULES.serviceUrl,
      healthUrl: managed.healthUrl || RULES.healthUrl,
      token: managed.token || RULES.token
    }));
  }

  return runtimeRulesPromise;
}

function scoreSnapshot(snapshot) {
  const matchedRules = [];
  let score = 0;

  if (snapshot.host && matchesKnownAdultDomain(snapshot.host)) {
    score += 100;
    matchedRules.push(`known adult domain: ${snapshot.host}`);
  }

  const urlMatches = containsKeyword(snapshot.url, RULES.strongKeywords);
  if (urlMatches.length > 0) {
    score += 60;
    matchedRules.push(`url keywords: ${urlMatches.join(", ")}`);
  }

  const titleMatches = containsKeyword(snapshot.title, RULES.strongKeywords);
  if (titleMatches.length > 0) {
    score += 50;
    matchedRules.push(`title keywords: ${titleMatches.join(", ")}`);
  }

  const metaMatches = [
    ...containsKeyword(snapshot.metaDescription, RULES.adultKeywords),
    ...containsKeyword(snapshot.metaKeywords, RULES.adultKeywords)
  ];
  if (metaMatches.length > 0) {
    score += 40;
    matchedRules.push(`meta keywords: ${Array.from(new Set(metaMatches)).join(", ")}`);
  }

  const bodyMatches = containsKeyword(`${snapshot.headings}\n${snapshot.bodyText}`, RULES.adultKeywords);
  if (bodyMatches.length > 0) {
    const uniqueMatches = Array.from(new Set(bodyMatches));
    const bodyScore = Math.min(uniqueMatches.length * 20, 80);
    score += bodyScore;
    matchedRules.push(`visible text keywords: ${uniqueMatches.join(", ")}`);
  }

  const reason = score >= RULES.minScoreToShutdown ? "adult content score threshold reached" : "adult content suspicion";
  return { score, matchedRules, reason };
}

async function getContentSnapshot(tabId) {
  for (const delay of [0, 250, 1000]) {
    if (delay > 0) {
      await new Promise((resolve) => setTimeout(resolve, delay));
    }

    try {
      const response = await chrome.tabs.sendMessage(tabId, CONTENT_QUERY);
      if (response?.ok) {
        return response.snapshot;
      }
    } catch {
      // The content script may not be injected yet on fast navigation events.
    }
  }

  return null;
}

async function postViolation(payload, runtimeRules) {
  if (!runtimeRules.token) {
    return false;
  }

  try {
    const response = await fetch(runtimeRules.serviceUrl, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "X-Guard-Token": runtimeRules.token
      },
      body: JSON.stringify(payload)
    });
    return response.ok;
  } catch {
    return false;
  }
}

async function evaluateTab(tabId, url, title) {
  if (isBrowserInternalUrl(url)) {
    return;
  }

  const runtimeRules = await getRuntimeRules();
  const now = Date.now();
  const lastViolation = recentViolationsByTab.get(tabId) || 0;
  if (now - lastViolation < runtimeRules.debounceMilliseconds) {
    return;
  }

  const urlSnapshot = createUrlSnapshot(url, title);
  const urlEvaluation = scoreSnapshot(urlSnapshot);
  if (urlEvaluation.score >= runtimeRules.minScoreToShutdown) {
    recentViolationsByTab.set(tabId, now);
    await postViolation({
      url: urlSnapshot.url,
      host: urlSnapshot.host,
      title: urlSnapshot.title,
      reason: urlEvaluation.reason,
      matchedRules: urlEvaluation.matchedRules,
      detectedAt: new Date().toISOString()
    }, runtimeRules);
    return;
  }

  const snapshot = await getContentSnapshot(tabId);
  if (!snapshot) {
    return;
  }

  const evaluation = scoreSnapshot({
    ...urlSnapshot,
    ...snapshot,
    url: snapshot.url || url,
    title: snapshot.title || title
  });

  if (evaluation.score < runtimeRules.minScoreToShutdown) {
    return;
  }

  recentViolationsByTab.set(tabId, now);
  await postViolation({
    url: snapshot.url || url,
    host: snapshot.host || new URL(url).hostname,
    title: snapshot.title || title || "",
    reason: evaluation.reason,
    matchedRules: evaluation.matchedRules,
    detectedAt: new Date().toISOString()
  }, runtimeRules);
}

chrome.tabs.onUpdated.addListener((tabId, changeInfo, tab) => {
  if (changeInfo.status === "complete" || typeof changeInfo.url === "string") {
    void evaluateTab(tabId, changeInfo.url || tab.url || "", tab.title || "");
  }
});

chrome.webNavigation.onCompleted.addListener((details) => {
  if (details.frameId === 0) {
    void chrome.tabs.get(details.tabId).then((tab) => evaluateTab(details.tabId, tab.url || "", tab.title || ""));
  }
});
