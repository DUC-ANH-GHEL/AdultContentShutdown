function getManagedConfig() {
  return new Promise((resolve) => {
    if (!chrome.storage?.managed) {
      resolve({});
      return;
    }

    chrome.storage.managed.get(["healthUrl"], (items) => {
      if (chrome.runtime.lastError) {
        resolve({});
        return;
      }

      resolve(items || {});
    });
  });
}

async function refreshStatus() {
  const statusElement = document.getElementById("serviceStatus");
  const managed = await getManagedConfig();
  const healthUrl = managed.healthUrl || RULES.healthUrl;

  try {
    const response = await fetch(healthUrl, { method: "GET" });
    if (!response.ok) {
      statusElement.textContent = "Local service: offline";
      return;
    }

    statusElement.textContent = "Local service: online";
  } catch {
    statusElement.textContent = "Local service: offline";
  }
}

refreshStatus();
