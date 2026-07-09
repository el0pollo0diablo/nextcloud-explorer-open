"use strict";

const MENU_TITLE = "Ordner im Explorer \u00f6ffnen";
const NC_BUTTON_ATTR = "data-nextcloud-explorer-open-folder";
const FILE_ROW_SELECTOR = [
  "[data-file]",
  "[data-filename]",
  "[data-entryname]",
  "[data-cy-files-list-row]",
  "tr[data-id]",
  "li[data-id]",
  "tr[class*='files-list']",
  "li[class*='files-list']"
].join(", ");

const ACTION_MENU_SELECTOR = [
  "[role='menu']",
  ".popovermenu",
  ".v-popper__popper",
  ".v-popper__inner",
  "[class*='popover']"
].join(", ");

let lastContextTarget = null;
let lastMenuSourceTarget = null;
let injectionTimer = 0;
let floatingMenuButton = null;

document.addEventListener("contextmenu", (event) => {
  lastContextTarget = event.target;
  rememberSourceTarget(event.target);
}, true);

document.addEventListener("click", (event) => {
  const element = toElement(event.target);
  if (!element || element.closest(`[${NC_BUTTON_ATTR}]`)) {
    return;
  }

  rememberSourceTarget(element);
}, true);

document.addEventListener("pointerdown", handleInjectedMenuEvent, true);
document.addEventListener("mousedown", handleInjectedMenuEvent, true);
document.addEventListener("click", handleInjectedMenuEvent, true);

browser.runtime.onMessage.addListener((message) => {
  if (!message || !message.type) {
    return undefined;
  }

  if (message.type === "NC_EXPLORER_GET_CONTEXT") {
    return Promise.resolve(extractContext(lastContextTarget, message.info || {}));
  }

  if (message.type === "NC_EXPLORER_SHOW_ERROR") {
    window.alert(`Nextcloud Explorer Open\n\n${message.message}`);
  }

  return undefined;
});

startNextcloudMenuObserver();

function startNextcloudMenuObserver() {
  injectNextcloudMenuItems();

  const observer = new MutationObserver(scheduleNextcloudMenuInjection);
  observer.observe(document.documentElement, {
    childList: true,
    subtree: true
  });

  window.setInterval(injectNextcloudMenuItems, 250);
}

function scheduleNextcloudMenuInjection() {
  window.clearTimeout(injectionTimer);
  injectionTimer = window.setTimeout(injectNextcloudMenuItems, 60);
}

function injectNextcloudMenuItems() {
  if (!isLikelyNextcloudFilesPage()) {
    hideFloatingMenuButton();
    return;
  }

  let foundMenu = false;
  for (const menu of findActionMenuCandidates()) {
    if (isNextcloudFileActionMenu(menu)) {
      foundMenu = true;
      addNextcloudMenuButton(menu);
    } else if (menu instanceof Element && menu.querySelector(`[${NC_BUTTON_ATTR}]`)) {
      foundMenu = true;
      syncFloatingMenuButton(menu.querySelector(`[${NC_BUTTON_ATTR}]`));
    }
  }

  if (!foundMenu) {
    hideFloatingMenuButton();
  }
}

function findActionMenuCandidates() {
  const candidates = new Set(document.querySelectorAll(ACTION_MENU_SELECTOR));

  for (const element of document.querySelectorAll("div, ul, ol, menu, section")) {
    const text = normalizedText(element);
    if (text.includes("Lokal \u00f6ffnen") &&
        (text.includes("Herunterladen") || text.includes("Umbenennen") || text.includes("Verschieben oder kopieren"))) {
      candidates.add(element);
    }
  }

  return candidates;
}

function isLikelyNextcloudFilesPage() {
  return /\/apps\/files\b/.test(window.location.pathname) ||
    Boolean(document.querySelector("[data-cy-files-list-row], #app-content-files, .files-list"));
}

function isNextcloudFileActionMenu(menu) {
  if (!(menu instanceof Element) || menu.querySelector(`[${NC_BUTTON_ATTR}]`)) {
    return false;
  }

  if (!isVisibleMenu(menu)) {
    return false;
  }

  const text = normalizedText(menu);
  const knownActions = [
    "Lokal \u00f6ffnen",
    "Herunterladen",
    "Umbenennen",
    "Verschieben oder kopieren",
    "Datei l\u00f6schen",
    "In Nextcloud Office \u00f6ffnen"
  ];

  return knownActions.filter((label) => text.includes(label)).length >= 2;
}

function isVisibleMenu(menu) {
  const style = window.getComputedStyle(menu);
  if (style.display === "none" || style.visibility === "hidden" || Number(style.opacity) === 0) {
    return false;
  }

  const rect = menu.getBoundingClientRect();
  return rect.width >= 120 &&
    rect.width <= 560 &&
    rect.height >= 80 &&
    rect.height <= Math.max(220, window.innerHeight);
}

function addNextcloudMenuButton(menu) {
  const anchorControl =
    findMenuControl(menu, "Lokal \u00f6ffnen") ||
    findMenuControl(menu, "Herunterladen") ||
    findMenuControl(menu, "Umbenennen") ||
    menu.querySelector("button, a, [role='menuitem']");

  const anchorItem = anchorControl ? menuItemContainer(anchorControl, menu) : null;
  const item = buildMenuButton(anchorItem || anchorControl);
  item.setAttribute(NC_BUTTON_ATTR, "true");

  item.addEventListener("pointerdown", openFolderFromInjectedMenu, true);
  item.addEventListener("mousedown", openFolderFromInjectedMenu, true);
  item.addEventListener("click", openFolderFromInjectedMenu, true);
  item.addEventListener("keydown", (event) => {
    if (event.key === "Enter" || event.key === " ") {
      openFolderFromInjectedMenu(event);
    }
  }, true);

  if (anchorItem && anchorItem.parentElement) {
    anchorItem.insertAdjacentElement("afterend", item);
  } else {
    menu.appendChild(item);
  }

  syncFloatingMenuButton(item);
}

function handleInjectedMenuEvent(event) {
  const element = toElement(event.target);
  const button = element ? element.closest(`[${NC_BUTTON_ATTR}]`) : null;
  if (!button) {
    return;
  }

  openFolderFromInjectedMenu(event, button);
}

async function openFolderFromInjectedMenu(event, sourceButton) {
  if (event.type === "mousedown" && event.button !== 0) {
    return;
  }
  if (event.type === "pointerdown" && event.button !== 0) {
    return;
  }

  event.preventDefault();
  event.stopPropagation();
  if (typeof event.stopImmediatePropagation === "function") {
    event.stopImmediatePropagation();
  }

  const button = sourceButton || (event.currentTarget instanceof Element ? event.currentTarget : null);
  if (button && button.dataset.ncExplorerOpening === "true") {
    return;
  }
  if (button) {
    button.dataset.ncExplorerOpening = "true";
    button.dataset.ncExplorerPreviousTitle = button.title || "";
    button.title = "\u00d6ffne...";
    button.style.backgroundColor = "rgba(70,120,220,0.24)";
  }

  try {
    showPageNotice("Explorer wird geoeffnet...", false, 1800);
    const context = extractContext(lastMenuSourceTarget || lastContextTarget || event.target, {});
    const response = await browser.runtime.sendMessage({
      type: "NC_EXPLORER_OPEN_FOLDER",
      context
    });

    if (!response || response.ok !== true) {
      throw new Error(response && response.error ? response.error : "Der lokale Helper konnte den Explorer nicht starten.");
    }
  } catch (error) {
    const message = error && error.message ? error.message : String(error);
    showPageNotice(message, true, 10000);
  } finally {
    if (button) {
      window.setTimeout(() => {
        delete button.dataset.ncExplorerOpening;
        button.title = button.dataset.ncExplorerPreviousTitle || MENU_TITLE;
        delete button.dataset.ncExplorerPreviousTitle;
        button.style.backgroundColor = "transparent";
      }, 1200);
    }
  }
}

function showPageNotice(message, isError, durationMs) {
  let notice = document.getElementById("nextcloud-explorer-open-notice");
  if (!notice) {
    notice = document.createElement("div");
    notice.id = "nextcloud-explorer-open-notice";
    notice.style.cssText = [
      "position:fixed",
      "right:18px",
      "top:18px",
      "z-index:2147483647",
      "max-width:420px",
      "padding:12px 14px",
      "border-radius:6px",
      "box-shadow:0 8px 28px rgba(0,0,0,0.35)",
      "color:#fff",
      "font:14px/1.35 system-ui,Segoe UI,sans-serif",
      "white-space:normal"
    ].join(";");
    (document.body || document.documentElement).appendChild(notice);
  }

  notice.textContent = message;
  notice.style.background = isError ? "#8f1f1f" : "#1f5f8f";
  notice.style.display = "block";

  window.clearTimeout(showPageNotice.timer);
  showPageNotice.timer = window.setTimeout(() => {
    notice.style.display = "none";
  }, durationMs);
}

function buildMenuButton(template) {
  const item = document.createElement("button");
  item.type = "button";
  item.setAttribute("role", "menuitem");
  item.setAttribute("aria-label", MENU_TITLE);
  item.title = MENU_TITLE;
  item.style.cssText = [
    "align-items:center",
    "background:transparent",
    "border:0",
    "box-sizing:border-box",
    "color:inherit",
    "cursor:pointer",
    "display:flex",
    "font:inherit",
    "gap:10px",
    "min-height:34px",
    "padding:6px 12px",
    "text-align:left",
    "width:100%"
  ].join(";");

  if (template instanceof Element) {
    const templateStyle = window.getComputedStyle(template);
    item.style.borderRadius = templateStyle.borderRadius;
  }

  item.addEventListener("mouseenter", () => {
    item.style.backgroundColor = "rgba(255,255,255,0.08)";
  });
  item.addEventListener("mouseleave", () => {
    item.style.backgroundColor = "transparent";
  });

  const icon = document.createElement("span");
  icon.setAttribute("aria-hidden", "true");
  icon.textContent = "\u25a1";
  icon.style.cssText = [
    "display:inline-flex",
    "flex:0 0 20px",
    "font-size:18px",
    "justify-content:center",
    "line-height:1"
  ].join(";");

  const label = document.createElement("span");
  label.textContent = MENU_TITLE;
  label.style.cssText = [
    "display:block",
    "overflow:hidden",
    "text-overflow:ellipsis",
    "white-space:nowrap"
  ].join(";");

  item.append(icon, label);
  return item;
}

function syncFloatingMenuButton(source) {
  if (!(source instanceof Element)) {
    hideFloatingMenuButton();
    return;
  }

  const rect = source.getBoundingClientRect();
  if (rect.width <= 0 || rect.height <= 0) {
    hideFloatingMenuButton();
    return;
  }

  const button = ensureFloatingMenuButton();
  button.style.left = `${Math.round(rect.left)}px`;
  button.style.top = `${Math.round(rect.top)}px`;
  button.style.width = `${Math.round(rect.width)}px`;
  button.style.height = `${Math.round(rect.height)}px`;
  button.style.display = "flex";
  try {
    if (typeof button.showPopover === "function" && !button.matches(":popover-open")) {
      button.showPopover();
    }
  } catch (_) {
    // Popover support is best-effort; fixed positioning still works without it.
  }
}

function ensureFloatingMenuButton() {
  if (floatingMenuButton) {
    return floatingMenuButton;
  }

  floatingMenuButton = buildMenuButton(null);
  floatingMenuButton.setAttribute(NC_BUTTON_ATTR, "floating");
  floatingMenuButton.setAttribute("popover", "manual");
  floatingMenuButton.style.position = "fixed";
  floatingMenuButton.style.zIndex = "2147483647";
  floatingMenuButton.style.backgroundColor = "rgb(37,37,37)";
  floatingMenuButton.style.boxShadow = "none";
  floatingMenuButton.style.margin = "0";
  floatingMenuButton.style.pointerEvents = "auto";
  floatingMenuButton.style.userSelect = "none";
  floatingMenuButton.addEventListener("pointerdown", openFolderFromInjectedMenu, true);
  floatingMenuButton.addEventListener("mousedown", openFolderFromInjectedMenu, true);
  floatingMenuButton.addEventListener("mouseup", openFolderFromInjectedMenu, true);
  floatingMenuButton.addEventListener("click", openFolderFromInjectedMenu, true);
  (document.body || document.documentElement).appendChild(floatingMenuButton);
  return floatingMenuButton;
}

function hideFloatingMenuButton() {
  if (floatingMenuButton) {
    try {
      if (typeof floatingMenuButton.hidePopover === "function" && floatingMenuButton.matches(":popover-open")) {
        floatingMenuButton.hidePopover();
      }
    } catch (_) {
      // Ignore popover state errors while the page is changing.
    }
    floatingMenuButton.style.display = "none";
  }
}

function rememberSourceTarget(target) {
  const element = toElement(target);
  if (!element || element.closest(ACTION_MENU_SELECTOR)) {
    return;
  }

  if (getFileRow(element)) {
    lastMenuSourceTarget = element;
  }
}

function extractContext(target, menuInfo) {
  const linkContext = extractFromUrl(menuInfo.linkUrl || "");
  const domContext = target ? extractFromDom(target) : null;
  const pageDir = getCurrentDirectoryFromLocation();

  const item = domContext || linkContext;
  if (!item) {
    if (pageDir !== null) {
      return {
        itemType: "directory",
        itemPath: pageDir,
        folderPath: pageDir
      };
    }

    throw new Error("Der Nextcloud-Dateipfad konnte an dieser Stelle nicht erkannt werden.");
  }

  const itemPath = normalizeAbsolutePath(item.itemPath);
  const itemType = item.itemType || "file";
  const folderPath = itemType === "directory" ? itemPath : dirname(itemPath);

  return {
    itemType,
    itemPath,
    folderPath
  };
}

function extractFromDom(target) {
  const element = toElement(target);
  if (!element) {
    return null;
  }

  const row = getFileRow(element);
  const link = element.closest("a[href]");

  if (row) {
    const fileName =
      getData(row, "file") ||
      getData(row, "filename") ||
      getData(row, "entryname") ||
      textFromCandidate(row.querySelector("[data-cy-files-list-row-name], .files-list__row-name, .files-list__row-name-link, .nametext, .filename, a[title], a"));

    const rowPath = getData(row, "path") || getData(row, "dir") || getData(row, "directory");
    const typeHint = `${getData(row, "type") || ""} ${getData(row, "mimetype") || ""}`.toLowerCase();
    const itemType = /dir|folder|directory|httpd\/unix-directory/.test(typeHint) ? "directory" : "file";

    if (rowPath && looksLikeAbsolutePath(rowPath) && (!fileName || basename(rowPath) === fileName)) {
      return {
        itemType,
        itemPath: rowPath
      };
    }

    if (fileName) {
      const currentDir = rowPath && looksLikeAbsolutePath(rowPath) ? rowPath : getCurrentDirectoryFromLocation();
      return {
        itemType,
        itemPath: joinPath(currentDir || "/", fileName)
      };
    }
  }

  if (link) {
    return extractFromUrl(link.href);
  }

  return null;
}

function getFileRow(element) {
  return element instanceof Element ? element.closest(FILE_ROW_SELECTOR) : null;
}

function extractFromUrl(rawUrl) {
  if (!rawUrl) {
    return null;
  }

  let url;
  try {
    url = new URL(rawUrl, window.location.href);
  } catch (_) {
    return null;
  }

  const path = firstSearchValue(url.searchParams, ["path", "dir"]);
  const files = firstSearchValue(url.searchParams, ["files", "file"]);

  if (path && files && !String(files).includes(",")) {
    return {
      itemType: "file",
      itemPath: joinPath(path, files)
    };
  }

  if (path) {
    return {
      itemType: "directory",
      itemPath: path
    };
  }

  return null;
}

function getCurrentDirectoryFromLocation() {
  const pageUrl = new URL(window.location.href);
  const direct = firstSearchValue(pageUrl.searchParams, ["dir", "path"]);
  if (direct !== null) {
    return normalizeAbsolutePath(direct);
  }

  if (pageUrl.hash) {
    const queryIndex = pageUrl.hash.indexOf("?");
    if (queryIndex !== -1) {
      const hashParams = new URLSearchParams(pageUrl.hash.slice(queryIndex + 1));
      const fromHash = firstSearchValue(hashParams, ["dir", "path"]);
      if (fromHash !== null) {
        return normalizeAbsolutePath(fromHash);
      }
    }
  }

  return null;
}

function cleanClone(element) {
  if (!(element instanceof Element)) {
    return;
  }

  element.removeAttribute("id");
  element.removeAttribute("disabled");
  element.removeAttribute("aria-disabled");

  for (const child of element.querySelectorAll("[id], [disabled], [aria-disabled]")) {
    child.removeAttribute("id");
    child.removeAttribute("disabled");
    child.removeAttribute("aria-disabled");
  }
}

function findMenuControl(menu, label) {
  return Array.from(menu.querySelectorAll("button, a, [role='menuitem']"))
    .find((control) => normalizedText(control).includes(label));
}

function menuItemContainer(control, menu) {
  const listItem = control.closest("li");
  if (listItem && menu.contains(listItem)) {
    return listItem;
  }

  let candidate = control;
  while (candidate && candidate.parentElement && candidate.parentElement !== menu) {
    if (candidate.parentElement.matches("[role='menu'], ul, ol, .popovermenu, .v-popper__inner")) {
      return candidate;
    }
    candidate = candidate.parentElement;
  }

  return candidate || control;
}

function firstClickable(element) {
  if (element.matches("button, a, [role='menuitem']")) {
    return element;
  }

  return element.querySelector("button, a, [role='menuitem']");
}

function replaceVisibleText(element, value) {
  const textNodes = [];
  const walker = document.createTreeWalker(element, NodeFilter.SHOW_TEXT, {
    acceptNode(node) {
      return node.nodeValue.trim()
        ? NodeFilter.FILTER_ACCEPT
        : NodeFilter.FILTER_REJECT;
    }
  });

  while (walker.nextNode()) {
    textNodes.push(walker.currentNode);
  }

  if (textNodes.length > 0) {
    for (const node of textNodes.slice(0, -1)) {
      node.nodeValue = "";
    }
    textNodes[textNodes.length - 1].nodeValue = value;
    return;
  }

  const label = document.createElement("span");
  label.textContent = value;
  element.appendChild(label);
}

function normalizedText(element) {
  return (element.innerText || element.textContent || "").replace(/\s+/g, " ").trim();
}

function toElement(target) {
  return target instanceof Element ? target : target && target.parentElement;
}

function getData(element, name) {
  if (!element) {
    return "";
  }
  return element.getAttribute(`data-${name}`) || element.dataset[toCamel(name)] || "";
}

function toCamel(value) {
  return value.replace(/-([a-z])/g, (_, char) => char.toUpperCase());
}

function textFromCandidate(element) {
  if (!element) {
    return "";
  }
  return (element.getAttribute("title") || element.textContent || "").trim();
}

function firstSearchValue(params, names) {
  for (const name of names) {
    if (params.has(name)) {
      return params.get(name);
    }
  }
  return null;
}

function normalizeAbsolutePath(value) {
  let path = safeDecodeURIComponent(String(value || "/")).replace(/\\/g, "/").trim();
  if (!path.startsWith("/")) {
    path = `/${path}`;
  }
  path = path.replace(/\/+/g, "/");
  if (path.length > 1) {
    path = path.replace(/\/+$/, "");
  }
  return path || "/";
}

function safeDecodeURIComponent(value) {
  try {
    return decodeURIComponent(value);
  } catch (_) {
    return value;
  }
}

function joinPath(parent, child) {
  const left = normalizeAbsolutePath(parent);
  const right = String(child || "").replace(/^\/+/, "").replace(/\/+$/, "");
  if (!right) {
    return left;
  }
  return normalizeAbsolutePath(`${left}/${right}`);
}

function dirname(path) {
  const normalized = normalizeAbsolutePath(path);
  if (normalized === "/") {
    return "/";
  }
  const index = normalized.lastIndexOf("/");
  return index <= 0 ? "/" : normalized.slice(0, index);
}

function basename(path) {
  const normalized = normalizeAbsolutePath(path);
  const index = normalized.lastIndexOf("/");
  return index === -1 ? normalized : normalized.slice(index + 1);
}

function looksLikeAbsolutePath(value) {
  return typeof value === "string" && value.trim().startsWith("/");
}
