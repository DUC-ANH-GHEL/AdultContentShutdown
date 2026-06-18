function extractContentSnapshot() {
  const metaDescription = document.querySelector('meta[name="description"]')?.content ?? "";
  const metaKeywords = document.querySelector('meta[name="keywords"]')?.content ?? "";
  const headings = Array.from(document.querySelectorAll('h1, h2'))
    .map((element) => (element.textContent || "").trim())
    .filter(Boolean)
    .join("\n");

  const bodyText = (document.body?.innerText || document.body?.textContent || "")
    .replace(/\s+/g, " ")
    .trim()
    .slice(0, RULES.visibleTextLimit);

  return {
    url: location.href,
    host: location.hostname,
    title: document.title || "",
    metaDescription,
    metaKeywords,
    headings,
    bodyText
  };
}

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (message?.type !== "ACSG_GET_CONTENT_SNAPSHOT") {
    return false;
  }

  try {
    sendResponse({ ok: true, snapshot: extractContentSnapshot() });
  } catch (error) {
    sendResponse({ ok: false, error: error instanceof Error ? error.message : String(error) });
  }

  return true;
});
