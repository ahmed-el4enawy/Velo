// Velo Chrome Extension Background Service Worker
chrome.runtime.onInstalled.addListener(() => {
  chrome.contextMenus.create({
    id: "velo-download-link",
    title: "Download with Velo",
    contexts: ["link", "video", "audio"]
  });
});

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

function sendToVeloHost(message) {
  chrome.runtime.sendNativeMessage("com.velo.native_host", message, (response) => {
    if (chrome.runtime.lastError) {
      console.warn("Failed to reach Velo Native Host:", chrome.runtime.lastError.message);
    } else {
      console.log("Velo Host response:", response);
    }
  });
}
