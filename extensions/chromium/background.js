// Velo Download Accelerator - Chrome Extension Background Service Worker
const NATIVE_HOST = "com.velo.downloader";

// Initialize default settings and context menus on install
chrome.runtime.onInstalled.addListener(() => {
  chrome.storage.local.get(["autoIntercept"], (result) => {
    if (result.autoIntercept === undefined) {
      chrome.storage.local.set({ autoIntercept: true });
    }
  });

  chrome.contextMenus.create({
    id: "velo-download-link",
    title: "Download with Velo",
    contexts: ["link", "video", "audio", "image"]
  });
});

// Handle Context Menu clicks
chrome.contextMenus.onClicked.addListener((info, tab) => {
  if (info.menuItemId === "velo-download-link") {
    const targetUrl = info.linkUrl || info.srcUrl;
    if (targetUrl) {
      sendToVeloHost({
        action: "enqueue_download",
        url: targetUrl,
        pageUrl: tab?.url || ""
      });
    }
  }
});

// Track active and recently intercepted downloads to prevent loops or duplicates
const interceptedDownloadIds = new Set();
const recentInterceptedUrls = new Map(); // url -> timestamp

function isDuplicateInterception(url) {
  const now = Date.now();
  // Expire entries older than 8 seconds
  for (const [recordedUrl, time] of recentInterceptedUrls.entries()) {
    if (now - time > 8000) {
      recentInterceptedUrls.delete(recordedUrl);
    }
  }

  if (recentInterceptedUrls.has(url)) {
    return true;
  }
  recentInterceptedUrls.set(url, now);
  return false;
}

// Intercept browser downloads like IDM safely
chrome.downloads.onCreated.addListener(async (downloadItem) => {
  const { autoIntercept } = await chrome.storage.local.get("autoIntercept");
  if (!autoIntercept) return;

  const url = downloadItem.finalUrl || downloadItem.url;
  if (!url) return;

  // Skip internal, blob, data, or extension installation files
  if (
    url.startsWith("blob:") ||
    url.startsWith("data:") ||
    url.startsWith("chrome://") ||
    url.startsWith("chrome-extension://") ||
    url.startsWith("edge://") ||
    url.endsWith(".crx")
  ) {
    return;
  }

  // Validate scheme (must be http or https)
  if (!url.startsWith("http://") && !url.startsWith("https://")) {
    return;
  }

  // Prevent duplicate loops if triggered multiple times
  if (interceptedDownloadIds.has(downloadItem.id) || isDuplicateInterception(url)) {
    return;
  }

  interceptedDownloadIds.add(downloadItem.id);

  // Pause Chrome's native download while checking with Velo
  try {
    await chrome.downloads.pause(downloadItem.id);
  } catch (pauseErr) {
    // If pausing fails, the download might already be finished or in progress
    console.debug("Could not pause native download:", pauseErr);
  }

  // Pass to Velo Native Messaging Host and confirm acceptance before canceling
  sendToVeloHost({
    action: "enqueue_download",
    url: url,
    fileName: downloadItem.filename || "",
    fileSize: downloadItem.fileSize || 0
  }, async (res) => {
    if (res && res.success && res.response && res.response.accepted) {
      // Velo confirmed acceptance: safely cancel and erase Chrome native download
      try {
        await chrome.downloads.cancel(downloadItem.id);
        await chrome.downloads.erase({ id: downloadItem.id });
      } catch (err) {
        console.warn("Could not cancel native download after Velo acceptance:", err);
      }
    } else {
      // Velo is unavailable, disconnected, or rejected the download: resume native download
      console.warn("Velo did not accept download, resuming Chrome native download. Details:", res);
      try {
        await chrome.downloads.resume(downloadItem.id);
      } catch (resumeErr) {
        console.debug("Could not resume native download:", resumeErr);
      }
      // Allow re-trying if needed
      interceptedDownloadIds.delete(downloadItem.id);
      recentInterceptedUrls.delete(url);
    }
  });
});

// Listen for messages from popup
chrome.runtime.onMessage.addListener((request, sender, sendResponse) => {
  if (request.action === "send_url") {
    if (!request.url || (!request.url.startsWith("http://") && !request.url.startsWith("https://"))) {
      sendResponse({ success: false, error: "Invalid HTTP/HTTPS URL" });
      return false;
    }

    sendToVeloHost({
      action: "enqueue_download",
      url: request.url
    }, (res) => sendResponse(res));
    return true; // Keep message channel open for async response
  } else if (request.action === "open_app") {
    sendToVeloHost({ action: "open_app" }, (res) => sendResponse(res));
    return true;
  }
});

function sendToVeloHost(message, callback) {
  try {
    chrome.runtime.sendNativeMessage(NATIVE_HOST, message, (response) => {
      if (chrome.runtime.lastError) {
        console.warn("Velo Native Host not reachable:", chrome.runtime.lastError.message);
        if (callback) callback({ success: false, error: chrome.runtime.lastError.message });
      } else {
        console.log("Velo Native Host responded:", response);
        if (callback) callback({ success: true, response });
      }
    });
  } catch (err) {
    console.error("Error communicating with Velo Native Host:", err);
    if (callback) callback({ success: false, error: err.message });
  }
}
