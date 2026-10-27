document.addEventListener("DOMContentLoaded", async () => {
  const toggle = document.getElementById("interceptToggle");
  const urlInput = document.getElementById("urlInput");
  const downloadBtn = document.getElementById("downloadBtn");
  const openManagerBtn = document.getElementById("openManagerBtn");
  const feedback = document.getElementById("feedback");

  // Load saved preference
  const { autoIntercept } = await chrome.storage.local.get("autoIntercept");
  toggle.checked = autoIntercept !== false;

  // Toggle change listener
  toggle.addEventListener("change", () => {
    chrome.storage.local.set({ autoIntercept: toggle.checked });
  });

  // Download button
  downloadBtn.addEventListener("click", () => {
    const url = urlInput.value.trim();
    if (!url) return;

    if (!url.startsWith("http://") && !url.startsWith("https://")) {
      feedback.textContent = "Please enter a valid HTTP/HTTPS URL.";
      feedback.className = "feedback error";
      return;
    }

    feedback.textContent = "Sending to Velo...";
    feedback.className = "feedback";
    feedback.style.display = "block";

    chrome.runtime.sendMessage({ action: "send_url", url }, (res) => {
      if (res && res.success) {
        feedback.textContent = "Sent to Velo successfully!";
        feedback.className = "feedback success";
        urlInput.value = "";
      } else {
        feedback.textContent = "Velo host not running or not registered.";
        feedback.className = "feedback error";
      }
    });
  });

  // Open Manager button
  openManagerBtn.addEventListener("click", () => {
    chrome.runtime.sendMessage({ action: "open_app" });
  });
});
