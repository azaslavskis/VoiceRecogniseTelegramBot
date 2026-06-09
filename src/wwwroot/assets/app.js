const settingsEndpoint = "/settings";
const statsEndpoint = "/stats";
const healthEndpoint = "/health";

const botTextFields = [
  "SetLanguageButton",
  "LogButton",
  "AboutButton",
  "MainMenuPrompt",
  "LanguagePrompt",
  "AboutMessage",
  "UnknownCommandMessage",
  "TranscriptionInProgressMessage",
  "TranscriptionResultPrefix",
  "LanguageChangedPrefix",
  "InternalErrorMessage"
];

const state = {
  settings: null,
  stats: null,
  health: null,
  rawJson: "",
  activeView: "dashboard",
  loading: true,
  saving: false,
  dirty: false,
  parseError: ""
};

const content = document.getElementById("content");
const summary = document.getElementById("summary");
const statusText = document.getElementById("statusText");
const dirtyBadge = document.getElementById("dirtyBadge");
const reloadButton = document.getElementById("reloadButton");
const saveButton = document.getElementById("saveButton");
const dashboardTab = document.getElementById("dashboardTab");
const jsonTab = document.getElementById("jsonTab");

reloadButton.addEventListener("click", loadDashboard);
saveButton.addEventListener("click", saveSettings);
dashboardTab.addEventListener("click", () => setView("dashboard"));
jsonTab.addEventListener("click", () => setView("json"));

function setView(view) {
  state.activeView = view;
  dashboardTab.classList.toggle("active", view === "dashboard");
  jsonTab.classList.toggle("active", view === "json");
  render();
}

async function loadDashboard() {
  state.loading = true;
  state.parseError = "";
  setStatus("Loading dashboard", "neutral");
  render();

  try {
    const [settings, stats, health] = await Promise.all([
      getJson(settingsEndpoint),
      getJson(statsEndpoint),
      getJson(healthEndpoint)
    ]);

    state.settings = normalizeSettings(settings);
    state.stats = stats;
    state.health = health;
    state.rawJson = JSON.stringify(state.settings, null, 2);
    state.dirty = false;
    setStatus("Dashboard loaded", "good");
  } catch (error) {
    setStatus(error.message, "bad");
  } finally {
    state.loading = false;
    render();
  }
}

async function getJson(url) {
  const response = await fetch(url, { headers: { Accept: "application/json" } });
  if (!response.ok) throw new Error(`${url} returned ${response.status}`);
  const text = await response.text();
  return text.trim() ? JSON.parse(text) : {};
}

async function saveSettings() {
  if (!state.settings || state.parseError) return;

  state.saving = true;
  setStatus("Saving settings", "neutral");
  render();

  try {
    const response = await fetch(settingsEndpoint, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Accept: "application/json"
      },
      body: JSON.stringify(state.settings, null, 2)
    });

    const responseText = await response.text();
    const payload = responseText.trim() ? JSON.parse(responseText) : {};
    if (!response.ok) throw new Error(payload.error || `Save returned ${response.status}`);

    state.settings = normalizeSettings(payload);
    state.rawJson = JSON.stringify(state.settings, null, 2);
    state.dirty = false;
    setStatus("Settings saved", "good");
  } catch (error) {
    setStatus(error.message, "bad");
  } finally {
    state.saving = false;
    render();
  }
}

function normalizeSettings(settings) {
  return {
    Model: settings.Model ?? "ggml-base",
    Token: settings.Token ?? "",
    WebServer: Boolean(settings.WebServer),
    Lang: Array.isArray(settings.Lang) ? settings.Lang : [],
    DefaultLang: settings.DefaultLang ?? "",
    BotText: settings.BotText ?? {}
  };
}

function setStatus(message, tone) {
  statusText.textContent = message;
  statusText.className = tone === "neutral" ? "" : tone;
}

function markDirty() {
  state.rawJson = JSON.stringify(state.settings, null, 2);
  state.dirty = true;
  setStatus("Unsaved changes", "warn");
  syncControls();
}

function updateSetting(key, value) {
  state.settings = { ...state.settings, [key]: value };
  markDirty();
  render();
}

function updateBotText(key, value) {
  state.settings = {
    ...state.settings,
    BotText: {
      ...state.settings.BotText,
      [key]: value
    }
  };
  markDirty();
}

function updateRawJson(value) {
  state.rawJson = value;
  state.dirty = true;

  try {
    state.settings = normalizeSettings(JSON.parse(value || "{}"));
    state.parseError = "";
    setStatus("Unsaved changes", "warn");
  } catch (error) {
    state.parseError = error.message;
    setStatus("JSON has errors", "bad");
  }

  syncControls();
  const error = document.getElementById("jsonError");
  if (error) {
    error.textContent = state.parseError;
    error.hidden = !state.parseError;
  }
}

function syncControls() {
  reloadButton.disabled = state.loading || state.saving;
  saveButton.disabled = state.loading || state.saving || Boolean(state.parseError) || !state.settings;
  saveButton.textContent = state.saving ? "Saving" : "Save";
  dirtyBadge.hidden = !state.dirty;
}

function render() {
  syncControls();
  renderSummary();

  if (state.loading) {
    content.className = "empty";
    content.textContent = "Loading dashboard...";
    return;
  }

  if (!state.settings) {
    content.className = "empty error-state";
    content.textContent = "Dashboard could not load.";
    return;
  }

  if (state.activeView === "json") {
    renderJsonEditor();
    return;
  }

  renderDashboard();
}

function renderSummary() {
  summary.textContent = "";
  const items = [
    ["Total messages", state.stats?.TotalMessages ?? "-"],
    ["Past 7 days", state.stats?.MessagesPast7Days ?? "-"],
    ["Web UI", state.health?.status ?? "unknown"],
    ["Languages", state.settings?.Lang?.join(", ") || "-"]
  ];

  items.forEach(([label, value]) => {
    const item = document.createElement("article");
    item.className = "metric";
    item.append(createElement("span", label), createElement("strong", String(value)));
    summary.append(item);
  });
}

function renderDashboard() {
  content.className = "dashboard";
  content.textContent = "";

  const settingsPanel = createPanel("Runtime Settings");
  settingsPanel.append(
    createTextField("Model", state.settings.Model, (value) => updateSetting("Model", value)),
    createPasswordField("Telegram Token", state.settings.Token, (value) => updateSetting("Token", value)),
    createLanguageEditor(),
    createToggleField("Web Server", state.settings.WebServer, (value) => updateSetting("WebServer", value))
  );

  const botTextPanel = createPanel("Bot Messages", "wide");
  const textGrid = document.createElement("div");
  textGrid.className = "message-grid";
  botTextFields.forEach((key) => {
    textGrid.append(createTextareaField(titleFromKey(key), state.settings.BotText[key] ?? "", (value) => updateBotText(key, value)));
  });
  botTextPanel.append(textGrid);

  content.append(settingsPanel, botTextPanel);
}

function renderJsonEditor() {
  content.className = "editor";
  content.textContent = "";

  const textarea = document.createElement("textarea");
  textarea.value = state.rawJson;
  textarea.spellcheck = false;
  textarea.addEventListener("input", (event) => updateRawJson(event.currentTarget.value));
  content.append(textarea);

  const error = document.createElement("p");
  error.id = "jsonError";
  error.className = "error";
  error.textContent = state.parseError;
  error.hidden = !state.parseError;
  content.append(error);
}

function createPanel(title, variant = "") {
  const panel = document.createElement("section");
  panel.className = `panel ${variant}`.trim();
  panel.append(createElement("h2", title));
  return panel;
}

function createTextField(label, value, onInput) {
  const field = createFieldShell(label);
  const input = document.createElement("input");
  input.type = "text";
  input.value = value ?? "";
  input.addEventListener("input", (event) => onInput(event.currentTarget.value));
  field.append(input);
  return field;
}

function createPasswordField(label, value, onInput) {
  const field = createFieldShell(label);
  const row = document.createElement("div");
  row.className = "input-row";

  const input = document.createElement("input");
  input.type = "password";
  input.value = value ?? "";
  input.addEventListener("input", (event) => onInput(event.currentTarget.value));

  const button = document.createElement("button");
  button.className = "icon-button subtle";
  button.type = "button";
  button.textContent = "◐";
  button.title = "Show or hide token";
  button.setAttribute("aria-label", "Show or hide token");
  button.addEventListener("click", () => {
    input.type = input.type === "password" ? "text" : "password";
  });

  row.append(input, button);
  field.append(row);
  return field;
}

function createTextareaField(label, value, onInput) {
  const field = createFieldShell(label);
  const textarea = document.createElement("textarea");
  textarea.className = "compact";
  textarea.value = value ?? "";
  textarea.addEventListener("input", (event) => onInput(event.currentTarget.value));
  field.append(textarea);
  return field;
}

function createToggleField(label, value, onToggle) {
  const field = createFieldShell(label);
  const button = document.createElement("button");
  button.type = "button";
  button.className = `toggle${value ? " enabled" : ""}`;
  button.innerHTML = `<span></span>${value ? "Enabled" : "Disabled"}`;
  button.addEventListener("click", () => onToggle(!value));
  field.append(button);
  return field;
}

function createLanguageEditor() {
  const field = createFieldShell("Recognition Languages");
  const chips = document.createElement("div");
  chips.className = "chips";

  state.settings.Lang.forEach((language) => {
    const chip = document.createElement("button");
    chip.type = "button";
    chip.className = language === state.settings.DefaultLang ? "chip selected" : "chip";
    chip.textContent = language;
    chip.title = "Set default language";
    chip.addEventListener("click", () => updateSetting("DefaultLang", language));

    const remove = document.createElement("span");
    remove.textContent = "×";
    remove.title = "Remove language";
    remove.addEventListener("click", (event) => {
      event.stopPropagation();
      removeLanguage(language);
    });

    chip.append(remove);
    chips.append(chip);
  });

  const row = document.createElement("form");
  row.className = "input-row";
  row.addEventListener("submit", (event) => {
    event.preventDefault();
    const input = row.querySelector("input");
    addLanguage(input.value);
    input.value = "";
  });

  const input = document.createElement("input");
  input.placeholder = "Add language code";
  input.maxLength = 8;

  const button = document.createElement("button");
  button.className = "secondary";
  button.type = "submit";
  button.textContent = "Add";

  row.append(input, button);
  field.append(chips, row);
  return field;
}

function addLanguage(value) {
  const language = value.trim().toUpperCase();
  if (!language || state.settings.Lang.includes(language)) return;

  updateSetting("Lang", [...state.settings.Lang, language]);
  if (!state.settings.DefaultLang) updateSetting("DefaultLang", language);
}

function removeLanguage(language) {
  const nextLanguages = state.settings.Lang.filter((item) => item !== language);
  const nextDefault = state.settings.DefaultLang === language ? (nextLanguages[0] ?? "") : state.settings.DefaultLang;
  state.settings = {
    ...state.settings,
    Lang: nextLanguages,
    DefaultLang: nextDefault
  };
  markDirty();
  render();
}

function createFieldShell(label) {
  const field = document.createElement("label");
  field.className = "field";
  field.append(createElement("span", label));
  return field;
}

function createElement(tag, text) {
  const element = document.createElement(tag);
  element.textContent = text;
  return element;
}

function titleFromKey(key) {
  return String(key)
    .replace(/([a-z])([A-Z])/g, "$1 $2")
    .replace(/[_-]+/g, " ")
    .replace(/\b\w/g, (letter) => letter.toUpperCase());
}

loadDashboard();
