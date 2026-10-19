"use strict";

const body = document.getElementById("clients-body");
const status = document.getElementById("status");
const refreshButton = document.getElementById("refresh");
const lastUpdated = document.getElementById("last-updated");
const screenshotDialog = document.getElementById("screenshot-dialog");
const screenshotStatus = document.getElementById("screenshot-status");
const screenshotImage = document.getElementById("screenshot-image");
const screenshotRetry = document.getElementById("screenshot-retry");
const formatter = new Intl.DateTimeFormat("ru-RU", {
  dateStyle: "short",
  timeStyle: "medium"
});
let refreshTimer;
let loading = false;
let hasData = false;
let connected = false;
let activeScreenshot = null;

function formatTime(value) {
  if (!value) return "—";
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "—" : formatter.format(date);
}

function appendCell(row, value, className = "") {
  const cell = document.createElement("td");
  cell.textContent = value || "—";
  cell.className = className;
  row.append(cell);
  return cell;
}

function showEmpty(message) {
  const row = document.createElement("tr");
  const cell = appendCell(row, message, "empty");
  cell.colSpan = 8;
  body.replaceChildren(row);
}

function renderClients(clients) {
  const fragment = document.createDocumentFragment();
  for (const client of clients) {
    const row = document.createElement("tr");
    const cell = appendCell(row, "");
    const badge = document.createElement("span");
    badge.className = `badge ${client.isOnline ? "online" : "offline"}`;
    badge.textContent = client.isOnline ? "Online" : "Offline";
    cell.replaceChildren(badge);
    appendCell(row, client.domainName);
    appendCell(row, client.machineName);
    appendCell(row, client.remoteIpAddress);
    appendCell(row, client.userName);
    appendCell(row, formatTime(client.lastInputAt), "time");
    appendCell(row, formatTime(client.lastSeenAt), "time");
    const actions = appendCell(row, "");
    const button = document.createElement("button");
    button.type = "button";
    button.className = "screenshot-button";
    button.dataset.clientId = client.clientId;
    button.dataset.online = String(client.isOnline);
    button.textContent = "Скриншот";
    button.setAttribute("aria-label", `Запросить скриншот: ${client.machineName}, ${client.userName}`);
    button.addEventListener("click", () => openScreenshot(client));
    actions.replaceChildren(button);
    fragment.append(row);
  }

  if (clients.length === 0) showEmpty("Клиентов пока нет. Запустите Windows-клиент для подключения.");
  else body.replaceChildren(fragment);

  const online = clients.filter(client => client.isOnline).length;
  document.getElementById("total-count").textContent = clients.length;
  document.getElementById("online-count").textContent = online;
  document.getElementById("offline-count").textContent = clients.length - online;
  updateScreenshotButtons();
}

async function refreshClients() {
  if (loading) return;
  clearTimeout(refreshTimer);
  loading = true;
  refreshButton.disabled = true;
  body.setAttribute("aria-busy", "true");

  try {
    const response = await fetch("/clients", {
      cache: "no-store",
      signal: AbortSignal.timeout(8000)
    });
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const clients = await response.json();
    if (!Array.isArray(clients)) throw new Error("Unexpected client list");

    connected = true;
    renderClients(clients);
    hasData = true;
    lastUpdated.textContent = `Обновлено: ${formatTime(new Date())}`;
    status.className = "notice";
    status.textContent = "Связь с сервером установлена. Обновление каждые 5 секунд.";
  } catch {
    connected = false;
    updateScreenshotButtons();
    status.className = "notice error";
    status.textContent = hasData
      ? "Не удалось обновить список. Показаны последние полученные данные; статусы могут быть устаревшими. Повтор через 5 секунд."
      : "Не удалось загрузить список клиентов. Повтор через 5 секунд.";
    if (!hasData) showEmpty("Нет связи с сервером.");
  } finally {
    loading = false;
    refreshButton.disabled = false;
    body.setAttribute("aria-busy", "false");
    refreshTimer = setTimeout(refreshClients, 5000);
  }
}

function updateScreenshotButtons() {
  for (const button of body.querySelectorAll("button[data-client-id]")) {
    const waiting = activeScreenshot?.waiting && activeScreenshot.client.clientId === button.dataset.clientId;
    button.disabled = !connected || button.dataset.online !== "true" || Boolean(waiting);
    button.textContent = waiting ? "Ожидание…" : "Скриншот";
  }
}

function setScreenshotStatus(message, isError = false) {
  screenshotStatus.textContent = message;
  screenshotStatus.className = isError ? "notice error" : "notice";
}

function disposeScreenshot() {
  if (!activeScreenshot) return;
  clearTimeout(activeScreenshot.timer);
  activeScreenshot.controller.abort();
  if (activeScreenshot.imageUrl) URL.revokeObjectURL(activeScreenshot.imageUrl);
  activeScreenshot = null;
  screenshotImage.onload = null;
  screenshotImage.onerror = null;
  screenshotImage.removeAttribute("src");
  screenshotImage.hidden = true;
  updateScreenshotButtons();
}

function finishScreenshot(session, message, isError = false) {
  if (activeScreenshot !== session) return;
  clearTimeout(session.timer);
  session.waiting = false;
  screenshotRetry.hidden = false;
  setScreenshotStatus(message, isError);
  updateScreenshotButtons();
}

async function screenshotFetch(session, path, options = {}) {
  return fetch(path, {
    ...options,
    cache: "no-store",
    signal: AbortSignal.any([session.controller.signal, AbortSignal.timeout(8000)])
  });
}

async function openScreenshot(client) {
  if (activeScreenshot?.waiting) return;
  disposeScreenshot();
  const session = {
    client,
    controller: new AbortController(),
    waiting: true,
    deadline: Date.now() + 45000,
    timer: null,
    imageUrl: null
  };
  activeScreenshot = session;
  document.getElementById("screenshot-client").textContent = `${client.machineName} · ${client.userName}`;
  screenshotRetry.hidden = true;
  setScreenshotStatus("Создание запроса скриншота…");
  if (!screenshotDialog.open) screenshotDialog.showModal();
  updateScreenshotButtons();

  try {
    const response = await screenshotFetch(session,
      `/clients/${encodeURIComponent(client.clientId)}/screenshots`, { method: "POST" });
    if (activeScreenshot !== session) return;
    if (!response.ok) {
      const messages = {
        404: "Клиент не найден.",
        409: "Клиент сейчас Offline. Дождитесь подключения.",
        503: "Достигнут лимит запросов. Попробуйте позже."
      };
      finishScreenshot(session, messages[response.status] || "Не удалось создать запрос скриншота.", true);
      return;
    }
    const request = await response.json();
    if (typeof request.requestId !== "string" || !request.requestId) throw new Error("Invalid request");
    session.requestId = request.requestId;
    await pollScreenshot(session);
  } catch {
    if (activeScreenshot === session)
      finishScreenshot(session, "Не удалось отправить запрос. Проверьте связь и попробуйте снова.", true);
  }
}

async function pollScreenshot(session) {
  if (activeScreenshot !== session || !session.waiting) return;
  if (Date.now() >= session.deadline) {
    finishScreenshot(session, "Не удалось получить результат за отведённое время. Попробуйте снова.", true);
    return;
  }

  try {
    const path = `/screenshots/${encodeURIComponent(session.requestId)}`;
    const response = await screenshotFetch(session, path);
    if (activeScreenshot !== session) return;
    if (response.status === 404) {
      finishScreenshot(session, "Запрос удалён или сервер был перезапущен. Создайте новый запрос.", true);
      return;
    }
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const request = await response.json();
    if (activeScreenshot !== session) return;

    if (request.state === "Completed") {
      setScreenshotStatus("Загрузка изображения…");
      const image = await screenshotFetch(session, path + "/image");
      if (image.status === 404) {
        finishScreenshot(session, "Изображение больше не хранится на сервере. Создайте новый запрос.", true);
        return;
      }
      if (!image.ok) throw new Error(`HTTP ${image.status}`);
      const blob = await image.blob();
      if (activeScreenshot !== session) return;
      if (blob.type !== "image/png") throw new Error("Invalid image type");
      session.imageUrl = URL.createObjectURL(blob);
      screenshotImage.onload = () => {
        if (activeScreenshot !== session) return;
        screenshotImage.hidden = false;
        finishScreenshot(session, "Скриншот получен.");
      };
      screenshotImage.onerror = () => {
        finishScreenshot(session, "Не удалось отобразить изображение. Запросите новый снимок.", true);
      };
      screenshotImage.src = session.imageUrl;
      return;
    }
    if (request.state === "Failed") {
      finishScreenshot(session, `Клиент не смог сделать снимок: ${request.error || "неизвестная ошибка"}`, true);
      return;
    }
    if (request.state === "Expired") {
      finishScreenshot(session, "Клиент не прислал снимок за 30 секунд. Попробуйте снова.", true);
      return;
    }
    if (request.state !== "Pending") throw new Error("Invalid request state");
    setScreenshotStatus("Ожидание снимка от клиента…");
  } catch {
    if (activeScreenshot !== session) return;
    setScreenshotStatus("Не удалось получить результат. Повторная проверка через 1,5 секунды…", true);
  }

  if (activeScreenshot === session && session.waiting)
    session.timer = setTimeout(() => pollScreenshot(session), 1500);
}

document.getElementById("screenshot-close").addEventListener("click", () => {
  disposeScreenshot();
  screenshotDialog.close();
});
screenshotDialog.addEventListener("cancel", disposeScreenshot);
screenshotDialog.addEventListener("close", disposeScreenshot);
screenshotRetry.addEventListener("click", () => {
  if (activeScreenshot) openScreenshot(activeScreenshot.client);
});
refreshButton.addEventListener("click", refreshClients);
refreshClients();
