/**
 * GameHub Analytics -> Google Sheets auto sync.
 * Paste into the Sheet: Extensions > Apps Script, fill CONFIG, run setup() once (grant access).
 * Produces the same layout as the dashboard's Excel export: "Màn Chơi" + one sheet per player.
 */
const CONFIG = {
  apiKey: '',      // AnalyticsConfig.apiKey
  projectId: '',   // AnalyticsConfig.projectId
  gameId: '',      // AnalyticsConfig.gameId (if empty in Unity: Product Name, non [A-Za-z0-9_-] chars -> '-')
  everyMinutes: 5, // 1, 5, 10, 15 or 30. Free Google accounts get ~90 min/day of trigger runtime
};

function setup() {
  ScriptApp.getProjectTriggers()
    .filter(t => t.getHandlerFunction() === 'sync')
    .forEach(t => ScriptApp.deleteTrigger(t));
  ScriptApp.newTrigger('sync').timeBased().everyMinutes(CONFIG.everyMinutes).create();
  sync();
}

function onOpen() {
  SpreadsheetApp.getUi().createMenu('GameHub').addItem('Sync now', 'sync').addToUi();
}

function sync() {
  const names = {};
  query_('players').forEach(d => {
    const id = d.name.split('/players/')[1];
    if (id && !id.includes('/') && d.fields && d.fields.playerName) names[id] = String(val_(d.fields.playerName)).trim();
  });

  // Player stats live at analytics/{gameId}/players/{playerId}/missions/{missionId};
  // analytics/{gameId}/missions/{id} is only the events parent, so it is skipped.
  const rows = [];
  query_('missions').forEach(d => {
    const m = d.name.match(/\/players\/([^/]+)\/missions\/([^/]+)$/);
    if (!m) return;
    const f = d.fields || {};
    rows.push({
      playerId: m[1], missionId: m[2],
      started: num_(f.started), completed: num_(f.completed), failed: num_(f.failed),
      bestTime: num_(f.bestTime), totalPlayTime: num_(f.totalPlayTime),
      lastPlayed: val_(f.lastPlayed) || val_(f.lastPlayed_str) || '',
      rating: Math.min(Math.max(num_(f.rating), 0), 5),
    });
  });

  const byMission = {};
  rows.forEach(r => {
    const a = byMission[r.missionId] = byMission[r.missionId] || { started: 0, completed: 0, failed: 0, total: 0, players: 0, done: 0, best: 0, rSum: 0, rCnt: 0 };
    a.started += r.started; a.completed += r.completed; a.failed += r.failed; a.total += r.totalPlayTime;
    a.players++;
    if (r.completed > 0) a.done++;
    if (r.rating > 0) { a.rSum += r.rating; a.rCnt++; }
    if (r.bestTime > 0 && (a.best <= 0 || r.bestTime < a.best)) a.best = r.bestTime;
  });

  const missionData = Object.keys(byMission).sort(cmp_).map(id => {
    const a = byMission[id];
    const avg = a.completed > 0 ? a.total / a.completed : (a.started > 0 ? a.total / a.started : 0);
    return [id, a.started, a.completed, a.failed, pct_(a.completed, a.completed + a.failed), pct_(a.done, a.players),
            time_(avg), a.best > 0 ? time_(a.best) : '-', a.players, a.rCnt > 0 ? round_(a.rSum / a.rCnt, 1) : '-'];
  });
  write_('Màn Chơi', ['Màn Chơi', 'Số Lượt Bắt Đầu', 'Hoàn Thành (Thắng)', 'Thất Bại (Thua)', 'Tỉ Lệ Thắng (%)',
    'Tỉ Lệ Hoàn Thành (%)', 'Thời Gian TB', 'Kỷ Lục', 'Số Thiết Bị Test', 'Star Rate (TB ★)'], missionData,
    [, , , , PCT, PCT, , , , '0.0']);

  const byPlayer = {};
  rows.forEach(r => (byPlayer[r.playerId] = byPlayer[r.playerId] || []).push(r));
  const label = id => names[id] || id;
  const nameCount = {};
  Object.keys(byPlayer).forEach(id => { const k = label(id).toLowerCase(); nameCount[k] = (nameCount[k] || 0) + 1; });

  Object.keys(byPlayer).sort((a, b) => cmp_(label(a), label(b))).forEach(id => {
    // Same rule as the Excel export: duplicate names get a short id suffix
    const sheetName = nameCount[label(id).toLowerCase()] > 1 ? `${label(id)} (${id.slice(0, 4)})` : label(id);
    const data = byPlayer[id].sort((a, b) => cmp_(a.missionId, b.missionId)).map(r => [
      r.missionId, r.started, r.completed, r.failed, pct_(r.completed, r.completed + r.failed), pct_(r.completed, r.started),
      r.bestTime > 0 ? time_(r.bestTime) : '-', time_(r.totalPlayTime), r.lastPlayed ? new Date(r.lastPlayed) : '-',
      r.rating > 0 ? r.rating : '-']);
    write_(sheetName, ['Màn Chơi', 'Bắt Đầu', 'Thắng', 'Thua', 'Win Rate (%)', 'Hoàn Thành (%)', 'Kỷ Lục',
      'Thời Lượng', 'Lần Chơi Cuối', 'Star Rate (★)'], data, [, , , , PCT, PCT, , , 'dd/MM/yyyy HH:mm:ss']);
  });

  SpreadsheetApp.getActive().getSheetByName('Màn Chơi').getRange('L1').setValue('Cập nhật: ' + new Date().toLocaleString('vi-VN'));
}

function query_(collectionId) {
  const url = `https://firestore.googleapis.com/v1/projects/${CONFIG.projectId}/databases/(default)/documents/analytics/${CONFIG.gameId}:runQuery?key=${CONFIG.apiKey}`;
  const res = UrlFetchApp.fetch(url, {
    method: 'post', contentType: 'application/json', muteHttpExceptions: true,
    payload: JSON.stringify({ structuredQuery: { from: [{ collectionId, allDescendants: true }] } }),
  });
  // Throw instead of writing empty sheets, so a network/rules error never wipes the last good data
  if (res.getResponseCode() !== 200) throw new Error(`Firestore ${res.getResponseCode()}: ${res.getContentText()}`);
  return JSON.parse(res.getContentText()).filter(x => x.document).map(x => x.document);
}

const PCT = '0.0%';

// Only clears sheets this script owns by name; other tabs in the file are left alone.
// formats: optional number format per column (sparse array), e.g. dates need one or Sheets shows the day only.
function write_(name, headers, data, formats) {
  const ss = SpreadsheetApp.getActive();
  const sh = ss.getSheetByName(name) || ss.insertSheet(name);
  sh.getRange(1, 1, Math.max(sh.getMaxRows(), 1), headers.length).clearContent();
  if (sh.getFilter()) sh.getFilter().remove();
  sh.getBandings().forEach(b => b.remove());

  sh.getRange(1, 1, 1, headers.length).setValues([headers]).setFontWeight('bold');
  if (data.length) {
    sh.getRange(2, 1, data.length, headers.length).setValues(data);
    (formats || []).forEach((f, c) => f && sh.getRange(2, c + 1, data.length, 1).setNumberFormat(f));
  }

  // Table look: banded rows + header color, filter dropdowns, fitted columns
  const table = sh.getRange(1, 1, data.length + 1, headers.length);
  table.applyRowBanding(SpreadsheetApp.BandingTheme.BLUE, true, false);
  table.createFilter();
  sh.setFrozenRows(1);
  sh.autoResizeColumns(1, headers.length);
}

function val_(f) {
  if (!f) return null;
  if ('integerValue' in f) return Number(f.integerValue);
  if ('doubleValue' in f) return f.doubleValue;
  if ('stringValue' in f) return f.stringValue;
  if ('timestampValue' in f) return f.timestampValue;
  if ('booleanValue' in f) return f.booleanValue;
  return null;
}
function num_(f) { return Number(val_(f)) || 0; }
function pct_(a, b) { return b > 0 ? a / b : 0; } // fraction; shown as % via PCT format
function round_(x, d) { return Math.round(x * 10 ** d) / 10 ** d; }
function cmp_(a, b) { return a.localeCompare(b, undefined, { sensitivity: 'base', numeric: true }); }

// Mirrors FormatPlayTime in AnalyticsDashboardWindow.cs
function time_(s) {
  if (s <= 0) return '-';
  if (s < 60) return s.toFixed(1) + 's';
  const t = Math.round(s), h = Math.floor(t / 3600), m = Math.floor((t % 3600) / 60), sec = t % 60;
  const p = n => String(n).padStart(2, "0");
  return h > 0 ? `${h}h ${p(m)}m ${p(sec)}s` : `${m}m ${p(sec)}s`;
}
