"use strict";

const input = document.getElementById("webdavBaseUrl");
const status = document.getElementById("status");
const saveButton = document.getElementById("save");

browser.storage.local.get({ webdavBaseUrl: "" }).then((settings) => {
  input.value = settings.webdavBaseUrl || "";
});

saveButton.addEventListener("click", async () => {
  const webdavBaseUrl = input.value.trim();

  if (!isValidWebDavBaseUrl(webdavBaseUrl)) {
    status.textContent = "Bitte eine gueltige http(s)-URL eintragen.";
    return;
  }

  await browser.storage.local.set({ webdavBaseUrl });
  status.textContent = "Gespeichert.";
  window.setTimeout(() => {
    status.textContent = "";
  }, 1800);
});

function isValidWebDavBaseUrl(value) {
  try {
    const url = new URL(value);
    return (url.protocol === "https:" || url.protocol === "http:") &&
      url.pathname.includes("/remote.php/dav/files/");
  } catch (_) {
    return false;
  }
}
