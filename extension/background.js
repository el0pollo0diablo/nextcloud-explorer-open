"use strict";

const HOST_NAME = "io.github.el0pollo0diablo.nextcloud_explorer_open";
const MENU_ID = "nextcloud-explorer-open-folder";
const MENU_TITLE = "Ordner im Explorer \u00f6ffnen";

createMenu().catch((error) => {
  console.error("Nextcloud Explorer Open: Kontextmenue konnte nicht erstellt werden.", error);
});

async function createMenu() {
  try {
    await browser.menus.removeAll();
  } catch (_) {
    // removeAll can fail during early extension startup; create will be retried.
  }

  browser.menus.create({
    id: MENU_ID,
    title: MENU_TITLE,
    contexts: ["all"],
    documentUrlPatterns: [
      "*://*/index.php/apps/files/*",
      "*://*/*/index.php/apps/files/*",
      "*://*/apps/files/*",
      "*://*/*/apps/files/*"
    ]
  });
}

browser.menus.onClicked.addListener(async (info, tab) => {
  if (info.menuItemId !== MENU_ID || !tab || !tab.id) {
    return;
  }

  try {
    const context = await browser.tabs.sendMessage(tab.id, {
      type: "NC_EXPLORER_GET_CONTEXT",
      info
    });

    const response = await openFolder(context, tab.url);

    if (!response || response.ok !== true) {
      throw new Error(response && response.error ? response.error : "Der lokale Helper konnte den Explorer nicht starten.");
    }
  } catch (error) {
    await showPageError(tab.id, error);
  }
});

browser.runtime.onMessage.addListener((message, sender) => {
  if (!message || message.type !== "NC_EXPLORER_OPEN_FOLDER") {
    return undefined;
  }

  const tab = sender && sender.tab ? sender.tab : {};

  return openFolder(message.context || {}, tab.url || "").then((response) => {
    if (!response || response.ok !== true) {
      return {
        ok: false,
        error: response && response.error ? response.error : "Der lokale Helper konnte den Explorer nicht starten."
      };
    }

    return response;
  }).catch(async (error) => {
    if (tab.id) {
      await showPageError(tab.id, error);
    }

    return {
      ok: false,
      error: error && error.message ? error.message : String(error)
    };
  });
});

async function openFolder(context, pageUrl) {
  const settings = await browser.storage.local.get({
    webdavBaseUrl: ""
  });

  const webdavBaseUrl = String(settings.webdavBaseUrl || "").trim();
  if (!webdavBaseUrl) {
    await browser.runtime.openOptionsPage();
    throw new Error("Bitte zuerst die Nextcloud-WebDAV-Basis-URL in den Erweiterungsoptionen eintragen.");
  }

  return browser.runtime.sendNativeMessage(HOST_NAME, {
    action: "openFolder",
    webdavBaseUrl,
    folderPath: context.folderPath,
    itemPath: context.itemPath,
    itemType: context.itemType,
    pageUrl
  });
}

async function showPageError(tabId, error) {
  const message = error && error.message ? error.message : String(error);
  console.error("Nextcloud Explorer Open:", message);

  try {
    await browser.tabs.sendMessage(tabId, {
      type: "NC_EXPLORER_SHOW_ERROR",
      message
    });
  } catch (_) {
    // If no content script is available, the console entry is the only feedback.
  }
}
