"use strict";

const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");

let menuClickHandler;
let runtimeMessageHandler;
let contentScriptResponse = {};
const nativeMessages = [];

const browser = {
  menus: {
    removeAll: async () => {},
    create: () => {},
    onClicked: {
      addListener: (handler) => {
        menuClickHandler = handler;
      }
    }
  },
  runtime: {
    onMessage: {
      addListener: (handler) => {
        runtimeMessageHandler = handler;
      }
    },
    sendNativeMessage: async (_hostName, message) => {
      nativeMessages.push(message);
      return { ok: true };
    },
    openOptionsPage: async () => {}
  },
  tabs: {
    sendMessage: async (_tabId, message) => {
      if (message.type === "NC_EXPLORER_GET_CONTEXT") {
        return contentScriptResponse;
      }
      return undefined;
    }
  }
};

const backgroundPath = path.join(__dirname, "..", "extension", "background.js");
const source = fs.readFileSync(backgroundPath, "utf8");
vm.runInNewContext(source, { browser, console }, { filename: backgroundPath });

const contentPath = path.join(__dirname, "..", "extension", "content.js");
const contentSource = fs.readFileSync(contentPath, "utf8");
const pageUrlSources = contentSource.match(/pageUrl:\s*window\.location\.href/g) || [];

async function run() {
  assert.equal(typeof menuClickHandler, "function");
  assert.equal(typeof runtimeMessageHandler, "function");
  assert.equal(pageUrlSources.length, 2);

  contentScriptResponse = {
    folderPath: "/Documents",
    pageUrl: "https://cloud.example/apps/files/?dir=/Documents"
  };
  await menuClickHandler(
    { menuItemId: "nextcloud-explorer-open-folder" },
    { id: 7 }
  );
  assert.equal(
    nativeMessages.pop().pageUrl,
    "https://cloud.example/apps/files/?dir=/Documents"
  );

  contentScriptResponse = { folderPath: "/Photos" };
  await menuClickHandler(
    {
      menuItemId: "nextcloud-explorer-open-folder",
      pageUrl: "https://cloud.example/index.php/apps/files/?dir=/Photos"
    },
    { id: 7 }
  );
  assert.equal(
    nativeMessages.pop().pageUrl,
    "https://cloud.example/index.php/apps/files/?dir=/Photos"
  );

  await runtimeMessageHandler(
    {
      type: "NC_EXPLORER_OPEN_FOLDER",
      pageUrl: "https://cloud.example/apps/files/#dir=/Shared",
      context: { folderPath: "/Shared" }
    },
    { tab: { id: 7 } }
  );
  assert.equal(
    nativeMessages.pop().pageUrl,
    "https://cloud.example/apps/files/#dir=/Shared"
  );

  console.log("Extension message routing tests passed.");
}

run().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
