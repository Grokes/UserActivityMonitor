"use strict";

const body = document.getElementById("clients-body");
const status = document.getElementById("status");
const refreshButton = document.getElementById("refresh");
const lastUpdated = document.getElementById("last-updated");
const formatter = new Intl.DateTimeFormat("ru-RU", {
  dateStyle: "short",
  timeStyle: "medium"
});
let refreshTimer;
let loading = false;
let hasData = false;

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
  cell.colSpan = 7;
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
    fragment.append(row);
  }

  if (clients.length === 0) showEmpty("Клиентов пока нет. Запустите Windows-клиент для подключения.");
  else body.replaceChildren(fragment);

  const online = clients.filter(client => client.isOnline).length;
  document.getElementById("total-count").textContent = clients.length;
  document.getElementById("online-count").textContent = online;
  document.getElementById("offline-count").textContent = clients.length - online;
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

    renderClients(clients);
    hasData = true;
    lastUpdated.textContent = `Обновлено: ${formatTime(new Date())}`;
    status.className = "notice";
    status.textContent = "Связь с сервером установлена. Обновление каждые 5 секунд.";
  } catch {
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

refreshButton.addEventListener("click", refreshClients);
refreshClients();
