import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";
import vm from "node:vm";

function loadBackground() {
  const removedTabs = [];
  const postedViolations = [];
  const context = {
    URL,
    Map,
    Date,
    Promise,
    setTimeout,
    fetch: async (_url, options) => {
      postedViolations.push(JSON.parse(options.body));
      return { ok: true };
    },
    chrome: {
      runtime: {},
      storage: { managed: { get: (_keys, callback) => callback({ token: "test-token" }) } },
      tabs: {
        onUpdated: { addListener: () => {} },
        sendMessage: async () => null,
        get: async () => ({ url: "", title: "" }),
        remove: async (tabId) => {
          removedTabs.push(tabId);
        }
      },
      webNavigation: { onCompleted: { addListener: () => {} } }
    }
  };

  context.globalThis = context;
  context.importScripts = (...paths) => {
    for (const path of paths) {
      vm.runInContext(readFileSync(new URL(`../browser-extension/${path}`, import.meta.url), "utf8"), context);
    }
  };

  vm.createContext(context);
  vm.runInContext(readFileSync(new URL("../browser-extension/background.js", import.meta.url), "utf8"), context);
  vm.runInContext("globalThis.__scoreSnapshot = scoreSnapshot;", context);
  vm.runInContext("globalThis.__evaluateTab = evaluateTab;", context);
  return {
    evaluateTab: context.__evaluateTab,
    postedViolations,
    removedTabs,
    scoreSnapshot: context.__scoreSnapshot
  };
}

const background = loadBackground();
const scoreSnapshot = background.scoreSnapshot;

function snapshot(overrides) {
  return {
    url: "",
    host: "",
    title: "",
    metaDescription: "",
    metaKeywords: "",
    headings: "",
    bodyText: "",
    ...overrides
  };
}

test("does not match adult short keywords inside ordinary words", () => {
  const analytics = scoreSnapshot(snapshot({
    url: "https://analytics.google.com/analytics/web/#/p123/reports/intelligenthome",
    host: "analytics.google.com",
    title: "Analytics"
  }));

  assert.equal(analytics.score, 0);
  assert.deepEqual(Array.from(analytics.matchedRules), []);

  const sussex = scoreSnapshot(snapshot({
    url: "https://www.sussex.ac.uk/",
    host: "www.sussex.ac.uk",
    title: "University of Sussex"
  }));

  assert.equal(sussex.score, 0);
  assert.deepEqual(Array.from(sussex.matchedRules), []);
});

test("still matches adult keywords when separated by URL or text boundaries", () => {
  const result = scoreSnapshot(snapshot({
    url: "https://example.invalid/videos/anal-scene",
    host: "example.invalid",
    title: "Video archive"
  }));

  assert.equal(result.score, 60);
  assert.deepEqual(Array.from(result.matchedRules), ["url keywords: anal"]);
});

test("closes the violating tab after reporting an adult page", async () => {
  await background.evaluateTab(42, "https://example.invalid/videos/anal-scene", "Anal video");

  assert.deepEqual(background.removedTabs, [42]);
  assert.equal(background.postedViolations.length, 1);
  assert.equal(background.postedViolations[0].host, "example.invalid");
});
