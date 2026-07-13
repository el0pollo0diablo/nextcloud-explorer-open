"use strict";

const HOST_NAME = "io.github.el0pollo0diablo.nextcloud_explorer_open";
const status = document.getElementById("status");
const details = document.getElementById("details");
const server = document.getElementById("server");
const username = document.getElementById("username");
const service = document.getElementById("service");
const configureButton = document.getElementById("configure");
const refreshButton = document.getElementById("refresh");
const installerLink = document.getElementById("installer");

refreshButton.addEventListener("click", refreshStatus);

configureButton.addEventListener("click", async () => {
  setBusy(true);
  try {
    await browser.runtime.sendNativeMessage(HOST_NAME, { action: "configure" });
    await refreshStatus();
  } catch (error) {
    showMissingHelper(error);
  } finally {
    setBusy(false);
  }
});

refreshStatus();

async function refreshStatus() {
  setBusy(true);
  status.className = "";
  status.textContent = "Status wird geprueft...";

  try {
    const response = await browser.runtime.sendNativeMessage(HOST_NAME, { action: "getStatus" });
    if (!response || response.ok !== true) {
      throw new Error(response && response.error ? response.error : "Der Windows-Helper antwortet nicht.");
    }

    installerLink.hidden = true;
    configureButton.disabled = false;
    details.hidden = false;
    server.textContent = response.serverUrl || "Noch nicht eingerichtet";
    username.textContent = response.username || "-";
    service.textContent = response.webClientRunning && response.webClientAutomatic ? "Bereit" : "Reparatur erforderlich";

    const ready = response.configured &&
      response.credentialPresent &&
      response.webClientRunning &&
      response.webClientAutomatic;

    status.className = ready ? "success" : "warning";
    status.textContent = ready
      ? "Die Windows-Verbindung ist bereit."
      : "Die Windows-Einrichtung ist noch nicht vollstaendig.";
  } catch (error) {
    showMissingHelper(error);
  } finally {
    setBusy(false);
  }
}

function showMissingHelper(error) {
  const message = error && error.message ? error.message : String(error);
  status.className = "error";
  status.textContent = `Der Windows-Helper ist nicht installiert oder nicht erreichbar. ${message}`;
  details.hidden = true;
  installerLink.hidden = false;
  configureButton.disabled = true;
}

function setBusy(busy) {
  refreshButton.disabled = busy;
  if (!installerLink.hidden) {
    configureButton.disabled = true;
  } else {
    configureButton.disabled = busy;
  }
}
