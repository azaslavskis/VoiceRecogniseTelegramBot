const REFRESH_INTERVAL_MS = 10_000;
const LOG_REFRESH_INTERVAL_MS = 5_000;
const LOG_LINES = 300;

const BOT_TEXT_FIELDS = [
  ["SetLanguageButton", "Language button"],
  ["LogButton", "Log button"],
  ["AboutButton", "About button"],
  ["MainMenuPrompt", "Main menu prompt"],
  ["LanguagePrompt", "Language prompt"],
  ["AboutMessage", "About message"],
  ["UnknownCommandMessage", "Unknown command reply"],
  ["TranscriptionInProgressMessage", "Transcription in progress"],
  ["TranscriptionResultPrefix", "Transcription result prefix"],
  ["LanguageChangedPrefix", "Language changed prefix"],
  ["InternalErrorMessage", "Internal error reply"]
];

const BOT_STATES = {
  Online: (bot) => [`@${bot.Username} online`, "good"],
  Connecting: () => ["Connecting to Telegram", "neutral"],
  WaitingForToken: () => ["Waiting for bot token", "warn"],
  Error: () => ["Telegram connection failed", "bad"],
  Stopped: () => ["Bot stopped", "neutral"],
  NotRunning: () => ["Web UI only", "neutral"]
};

const $ = (id) => document.getElementById(id);

const state = {
  saved: null, // settings as last loaded from or saved to the server
  draft: null, // settings being edited
  tokenConfigured: false,
  jsonError: "",
  activeTab: "overview",
  saving: false
};

// ---------------------------------------------------------------- API

async function request(url, options) {
  let response;
  try {
    response = await fetch(url, { headers: { Accept: "application/json" }, ...options });
  } catch {
    throw new Error("The bot's web server is not reachable.");
  }

  const text = await response.text();
  let payload = {};
  try {
    payload = text.trim() ? JSON.parse(text) : {};
  } catch {
    // Non-JSON error pages fall through to the status check below.
  }

  if (!response.ok) throw new Error(payload.Error || `${url} returned ${response.status}`);
  return payload;
}

// ---------------------------------------------------------------- Loading

async function loadAll() {
  $("reloadButton").disabled = true;
  try {
    const [settings] = await Promise.all([request("/settings"), refreshStatus()]);
    applySettings(settings);
    $("loadError").hidden = true;
  } catch (error) {
    $("loadError").textContent = error.message;
    $("loadError").hidden = false;
  } finally {
    $("reloadButton").disabled = false;
  }

  if (state.activeTab === "logs") refreshLogs();
}

function applySettings(payload) {
  state.saved = payload.Settings;
  state.draft = structuredClone(payload.Settings);
  state.tokenConfigured = payload.TokenConfigured;
  state.jsonError = "";
  fillForms();
  renderOverviewSettings();
  syncDirty();
}

/** Refreshes the parts that change on their own: bot status and counters. */
async function refreshStatus() {
  try {
    const [health, stats] = await Promise.all([request("/health"), request("/stats")]);
    renderHealth(health);
    renderStats(stats);
  } catch (error) {
    setBotStatus("Server unreachable", "bad", error.message);
    throw error;
  }
}

async function refreshLogs() {
  const output = $("logOutput");
  try {
    const logs = await request(`/logs?lines=${LOG_LINES}`);
    const pinnedToEnd = output.scrollTop + output.clientHeight >= output.scrollHeight - 24;
    output.textContent = logs.Lines.length ? logs.Lines.join("\n") : "No log messages yet.";
    if (pinnedToEnd) output.scrollTop = output.scrollHeight;
  } catch (error) {
    output.textContent = error.message;
  }
}

// ---------------------------------------------------------------- Overview

function setBotStatus(label, tone, title = "") {
  $("botStatusText").textContent = label;
  $("botStatus").dataset.tone = tone;
  $("botStatus").title = title;
}

function renderHealth(health) {
  const bot = health.Bot ?? { State: "NotRunning" };
  const [label, tone] = (BOT_STATES[bot.State] ?? BOT_STATES.NotRunning)(bot);
  setBotStatus(label, tone, bot.Error ?? "");

  $("versionText").textContent = `Admin · v${health.Version}`;
  $("factBot").textContent = bot.Error ? `${label}: ${bot.Error}` : label;
  $("factStarted").textContent = new Date(health.StartedAtUtc).toLocaleString();
  $("factSettingsPath").textContent = health.SettingsPath;
  $("factStatsPath").textContent = health.StatsPath;
  $("factLogPath").textContent = health.LogPath;
}

function renderOverviewSettings() {
  $("metricModel").textContent = state.saved.Model;
  $("metricLanguages").textContent = state.saved.Lang.join(", ") || "no languages";
}

function renderStats(stats) {
  $("metricMessages").textContent = stats.TotalMessages.toLocaleString();
  $("metricTranscriptions").textContent = stats.TotalTranscriptions.toLocaleString();
  $("metricWeek").textContent = stats.MessagesPast7Days.toLocaleString();
  renderChart(stats.Daily);

  $("chartTableBody").replaceChildren(
    ...stats.Daily.toReversed().map((day) => {
      const row = document.createElement("tr");
      row.append(el("td", formatDay(day.Date, { weekday: "short" })), el("td", day.Messages), el("td", day.Transcriptions));
      return row;
    })
  );
}

function renderChart(days) {
  const chart = $("chart");
  const axisMax = niceMax(Math.max(...days.map((day) => day.Messages)));

  const axis = el("div", "", "chart-axis");
  axis.append(el("span", 0), el("span", axisMax / 2), el("span", axisMax));

  const plot = el("div", "", "chart-plot");
  const labels = el("div", "", "chart-labels");

  if (days.every((day) => day.Messages === 0)) {
    plot.append(el("p", "No messages in this period yet.", "chart-empty"));
  } else {
    days.forEach((day) => {
      const column = el("div", "", "chart-column");
      column.tabIndex = 0;
      column.setAttribute("aria-label", `${formatDay(day.Date)}: ${day.Messages} messages, ${day.Transcriptions} transcriptions`);

      const bar = el("div", "", "chart-bar");
      // Keep non-zero days visible even when one busy day dominates the scale.
      bar.style.height = day.Messages ? `max(${(day.Messages / axisMax) * 100}%, 3px)` : "0";
      column.append(bar);

      const show = () => showChartTooltip(column, day);
      column.addEventListener("pointerenter", show);
      column.addEventListener("focus", show);
      column.addEventListener("pointerleave", hideChartTooltip);
      column.addEventListener("blur", hideChartTooltip);
      plot.append(column);
    });
  }

  days.forEach((day) => labels.append(el("span", formatDay(day.Date, { month: undefined }))));
  chart.replaceChildren(axis, plot, labels);
}

function showChartTooltip(column, day) {
  const tooltip = $("chartTooltip");
  const rect = column.getBoundingClientRect();
  const bar = column.firstElementChild.getBoundingClientRect();

  tooltip.replaceChildren(
    el("strong", formatDay(day.Date, { weekday: "short" })),
    el("span", `${day.Messages.toLocaleString()} messages · ${day.Transcriptions.toLocaleString()} transcriptions`)
  );
  tooltip.hidden = false;

  const halfWidth = tooltip.offsetWidth / 2;
  const left = Math.min(Math.max(rect.left + rect.width / 2, halfWidth + 8), window.innerWidth - halfWidth - 8);
  tooltip.style.left = `${left}px`;
  tooltip.style.top = `${bar.top}px`;
}

function hideChartTooltip() {
  $("chartTooltip").hidden = true;
}

/** Rounds up to an even axis maximum so the midpoint label is a whole number. */
function niceMax(value) {
  if (value <= 4) return 4;
  const magnitude = 10 ** Math.floor(Math.log10(value));
  const step = [1, 2, 4, 5, 10].find((candidate) => candidate * magnitude >= value) * magnitude;
  return step % 2 === 0 ? step : step * 2;
}

function formatDay(isoDate, options = {}) {
  return new Date(`${isoDate}T00:00:00Z`).toLocaleDateString(undefined, {
    day: "numeric",
    month: "short",
    timeZone: "UTC",
    ...options
  });
}

// ---------------------------------------------------------------- Settings forms

function buildBotTextFields() {
  $("botTextFields").replaceChildren(
    ...BOT_TEXT_FIELDS.map(([key, label]) => {
      const field = el("div", "", "field");
      const labelElement = el("label", label);
      labelElement.htmlFor = `botText-${key}`;

      const textarea = document.createElement("textarea");
      textarea.id = `botText-${key}`;
      textarea.addEventListener("input", () => edit((draft) => (draft.BotText[key] = textarea.value)));

      field.append(labelElement, textarea);
      return field;
    })
  );
}

/** Copies the draft into the form controls. */
function fillForms() {
  const draft = state.draft;
  if (!draft) return;

  $("tokenInput").value = draft.Token;
  $("tokenInput").placeholder = state.tokenConfigured ? "Configured — leave empty to keep it" : "123456:ABC-DEF…";
  $("tokenHint").textContent = state.tokenConfigured
    ? "A token is saved. It is never shown here; enter a new one to replace it. The bot reconnects on its own."
    : "No token yet. Create a bot with @BotFather in Telegram and paste its token here; the bot starts as soon as you save.";
  $("modelInput").value = draft.Model;
  $("webServerInput").checked = draft.WebServer;

  BOT_TEXT_FIELDS.forEach(([key]) => {
    $(`botText-${key}`).value = draft.BotText[key] ?? "";
  });

  renderLanguages();
  fillJson();
}

function fillJson() {
  if (!state.draft || state.jsonError) return; // on a JSON error, keep the text the user is still fixing
  $("jsonInput").value = JSON.stringify(state.draft, null, 2);
}

function renderLanguages() {
  const draft = state.draft;

  $("languageList").replaceChildren(
    ...draft.Lang.map((language) => {
      const isDefault = language === draft.DefaultLang;
      const chip = el("li", "", "chip");
      chip.dataset.default = isDefault;

      const select = el("button", language, "chip-select");
      select.type = "button";
      select.setAttribute("aria-pressed", isDefault);
      select.title = isDefault ? "Default language" : `Make ${language} the default`;
      if (isDefault) select.append(el("span", "default", "chip-badge"));
      select.addEventListener("click", () => {
        draft.DefaultLang = language;
        renderLanguages();
        syncDirty();
      });

      const remove = el("button", "×", "chip-remove");
      remove.type = "button";
      remove.setAttribute("aria-label", `Remove ${language}`);
      remove.addEventListener("click", () => {
        draft.Lang = draft.Lang.filter((item) => item !== language);
        if (isDefault) draft.DefaultLang = draft.Lang[0] ?? "";
        renderLanguages();
        syncDirty();
      });

      chip.append(select, remove);
      return chip;
    })
  );
}

function addLanguage(value) {
  const language = value.trim().toUpperCase();
  const draft = state.draft;
  if (!draft || !language || draft.Lang.includes(language)) return;

  draft.Lang.push(language);
  if (!draft.DefaultLang) draft.DefaultLang = language;
  renderLanguages();
  syncDirty();
}

function onJsonInput() {
  if (!state.saved) return;
  const input = $("jsonInput");
  try {
    const parsed = JSON.parse(input.value || "{}");
    if (typeof parsed !== "object" || parsed === null || Array.isArray(parsed)) throw new Error("Settings must be a JSON object.");

    state.draft = {
      ...state.saved,
      ...parsed,
      Token: typeof parsed.Token === "string" ? parsed.Token : "",
      Lang: Array.isArray(parsed.Lang) ? parsed.Lang.map(String) : [],
      BotText: { ...state.saved.BotText, ...parsed.BotText }
    };
    state.jsonError = "";
  } catch (error) {
    state.jsonError = error.message;
  }

  input.setAttribute("aria-invalid", Boolean(state.jsonError));
  $("jsonError").textContent = state.jsonError;
  $("jsonError").hidden = !state.jsonError;
  syncDirty();
}

/** Applies a change to the draft; a no-op until the settings have loaded. */
function edit(change) {
  if (!state.draft) return;
  change(state.draft);
  syncDirty();
}

// ---------------------------------------------------------------- Saving

function isDirty() {
  return Boolean(state.draft) && (Boolean(state.jsonError) || JSON.stringify(state.draft) !== JSON.stringify(state.saved));
}

function syncDirty() {
  $("saveBar").hidden = !isDirty();
  $("saveButton").disabled = state.saving || Boolean(state.jsonError);
  $("saveButton").textContent = state.saving ? "Saving…" : "Save changes";
  $("discardButton").disabled = state.saving;
}

async function save() {
  if (state.saving || state.jsonError || !state.draft) return;

  state.saving = true;
  syncDirty();
  try {
    const payload = await request("/settings", {
      method: "POST",
      headers: { "Content-Type": "application/json", Accept: "application/json" },
      body: JSON.stringify(state.draft)
    });
    applySettings(payload);
    toast("Settings saved");
  } catch (error) {
    toast(error.message, "bad");
  } finally {
    state.saving = false;
    syncDirty();
  }
}

function discard() {
  state.draft = structuredClone(state.saved);
  state.jsonError = "";
  $("jsonInput").removeAttribute("aria-invalid");
  $("jsonError").hidden = true;
  fillForms();
  syncDirty();
}

// ---------------------------------------------------------------- Tabs, toasts, helpers

function selectTab(name, focus = false) {
  state.activeTab = name;
  document.querySelectorAll('[role="tab"]').forEach((tab) => {
    const selected = tab.dataset.tab === name;
    tab.setAttribute("aria-selected", selected);
    tab.tabIndex = selected ? 0 : -1;
    $(`panel-${tab.dataset.tab}`).hidden = !selected;
    if (selected && focus) tab.focus();
  });

  // The form tabs and the JSON tab edit the same draft; bring the one being opened up to date.
  if (name === "json") fillJson();
  else if (!state.jsonError) fillForms();
  if (name === "logs") refreshLogs();
  history.replaceState(null, "", `#${name}`);
}

function toast(message, tone = "neutral") {
  const item = el("div", message, "toast");
  item.dataset.tone = tone;
  $("toasts").append(item);
  setTimeout(() => item.remove(), tone === "bad" ? 7000 : 3000);
}

function el(tag, text = "", className = "") {
  const element = document.createElement(tag);
  element.textContent = text;
  if (className) element.className = className;
  return element;
}

// ---------------------------------------------------------------- Wiring

function bindEvents() {
  const tabs = [...document.querySelectorAll('[role="tab"]')];
  tabs.forEach((tab, index) => {
    tab.addEventListener("click", () => selectTab(tab.dataset.tab));
    tab.addEventListener("keydown", (event) => {
      const offset = { ArrowRight: 1, ArrowLeft: -1 }[event.key];
      if (!offset) return;
      event.preventDefault();
      selectTab(tabs[(index + offset + tabs.length) % tabs.length].dataset.tab, true);
    });
  });

  $("reloadButton").addEventListener("click", () => {
    if (!isDirty() || confirm("Reload and discard unsaved changes?")) loadAll();
  });
  $("saveButton").addEventListener("click", save);
  $("discardButton").addEventListener("click", discard);

  $("tokenInput").addEventListener("input", (event) => edit((draft) => (draft.Token = event.target.value.trim())));
  $("tokenToggle").addEventListener("click", () => {
    const reveal = $("tokenInput").type === "password";
    $("tokenInput").type = reveal ? "text" : "password";
    $("tokenToggle").textContent = reveal ? "Hide" : "Show";
    $("tokenToggle").setAttribute("aria-pressed", reveal);
  });
  $("modelInput").addEventListener("input", (event) => edit((draft) => (draft.Model = event.target.value.trim())));
  $("webServerInput").addEventListener("change", (event) => edit((draft) => (draft.WebServer = event.target.checked)));
  $("languageForm").addEventListener("submit", (event) => {
    event.preventDefault();
    addLanguage($("languageInput").value);
    $("languageInput").value = "";
  });

  $("jsonInput").addEventListener("input", onJsonInput);
  $("logRefreshButton").addEventListener("click", refreshLogs);

  window.addEventListener("beforeunload", (event) => {
    if (isDirty()) event.preventDefault();
  });
  window.addEventListener("scroll", hideChartTooltip, { passive: true });

  setInterval(() => {
    if (!document.hidden) refreshStatus().catch(() => {});
  }, REFRESH_INTERVAL_MS);
  setInterval(() => {
    if (!document.hidden && state.activeTab === "logs" && $("logAutoRefresh").checked) refreshLogs();
  }, LOG_REFRESH_INTERVAL_MS);
}

buildBotTextFields();
bindEvents();

const initialTab = location.hash.slice(1);
if ($(`panel-${initialTab}`)) selectTab(initialTab);

loadAll();
