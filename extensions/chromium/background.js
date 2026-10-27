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

// Intercept browser downloads like IDM
chrome.downloads.onCreated.addListener(async (downloadItem) => {
  const { autoIntercept } = await chrome.storage.local.get("autoIntercept");
  if (!autoIntercept) return;

  const url = downloadItem.finalUrl || downloadItem.url;
  if (!url) return;

  // Skip internal, blob, data, or extension installation files
  if (url.startsWith("blob:") || url.startsWith("data:") || url.startsWith("chrome://") || url.endsWith(".crx")) {
    return;
  }

  // Cancel and erase Chrome's native download so Velo handles it
  try {
    await chrome.downloads.cancel(downloadItem.id);
    await chrome.downloads.erase({ id: downloadItem.id });
  } catch (err) {
    console.warn("Could not cancel native download:", err);
  }

  // Pass to Velo Native Messaging Host
  sendToVeloHost({
    action: "enqueue_download",
    url: url,
    fileName: downloadItem.filename || "",
    fileSize: downloadItem.fileSize || 0
  });
});

// Listen for messages from popup
chrome.runtime.onMessage.addListener((request, sender, sendResponse) => {
  if (request.action === "send_url") {
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
