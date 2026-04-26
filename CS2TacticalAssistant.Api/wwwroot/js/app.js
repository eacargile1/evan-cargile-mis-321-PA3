const api = (path, opts = {}) =>
  fetch(path, { headers: { "Content-Type": "application/json", ...opts.headers }, ...opts });

const state = { lastAssistant: "", lastSources: [], lastTools: [] };

function el(html) {
  const t = document.createElement("template");
  t.innerHTML = html.trim();
  return t.content.firstElementChild;
}

function escapeHtml(s) {
  if (s == null) return "";
  return String(s).replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
}

function setStatus(text, ok = true) {
  const n = document.getElementById("app-status");
  if (!n) return;
  n.textContent = text;
  n.style.color = ok ? "var(--muted)" : "var(--err, #f85149)";
}

async function parseErrorBody(res) {
  const text = await res.text();
  let msg = res.statusText || "Request failed";
  try {
    const j = JSON.parse(text);
    if (j && (j.error || j.title || j.detail)) msg = j.error || j.detail || j.title;
    if (j && j.errors) msg = String(msg) + " " + JSON.stringify(j.errors);
  } catch {
    if (text && text.length < 300) msg = text;
  }
  return msg;
}

function showLoading(el, text = "Loading…") {
  if (!el) return;
  el.setAttribute("data-state", "loading");
  el.className = (el.className + " is-loading").trim();
  el.innerHTML = `<span class="muted small">${escapeHtml(text)}</span>`;
}

function appendBubble(role, text, extra = "") {
  const log = document.getElementById("chat-log");
  if (!log) return;
  const row = el(
    `<div class="msg-row msg-row--${role}"><div class="bubble ${role}">${escapeHtml(text)}${extra}</div></div>`
  );
  log.appendChild(row);
  log.scrollTop = log.scrollHeight;
}

async function sendChat(message) {
  const btn = document.getElementById("send-btn");
  btn.disabled = true;
  appendBubble("user", message);
  const errEl = document.getElementById("chat-error");
  if (errEl) errEl.textContent = "";
  try {
    const res = await api("/api/chat", {
      method: "POST",
      body: JSON.stringify({ message, userId: 1 }),
    });
    const data = res.ok ? await res.json() : { __err: await parseErrorBody(res) };
    if (!res.ok) throw new Error(data.__err || "Chat failed");
    state.lastAssistant = data.reply || "";
    state.lastSources = data.sourcesUsed || [];
    state.lastTools = data.toolCallsUsed || [];
    const toolsHtml = state.lastTools.length
      ? `<div class="meta tools-used">Tools: ${state.lastTools.map(escapeHtml).join(", ")}</div>`
      : "";
    appendBubble("assistant", data.reply || "(empty)", toolsHtml);
    renderSources(state.lastSources);
    await loadStrats();
  } catch (e) {
    if (errEl) errEl.textContent = e.message || String(e);
  } finally {
    btn.disabled = false;
  }
}

function renderSources(items) {
  const box = document.getElementById("sources");
  if (!box) return;
  if (!items || !items.length) {
    box.innerHTML = '<p class="empty-hint">No references matched. Try a more specific or longer question so we can find the right playbooks.</p>';
    return;
  }
  box.innerHTML = items
    .map(
      (s) => `<div class="source-item">
        <div><span class="pill">${escapeHtml(s.category)}</span> <strong>${escapeHtml(s.title)}</strong></div>
        <p class="excerpt">${escapeHtml(s.content ? s.content.slice(0, 300) : "")}${s.content && s.content.length > 300 ? "…" : ""}</p>
      </div>`
    )
    .join("");
}

function formatMatchNotes(n) {
  if (n == null) return "";
  const s = String(n).trim();
  if (s.startsWith("{")) {
    try {
      const o = JSON.parse(s);
      if (o && typeof o.error === "string") return o.error;
    } catch {
      /* use raw */
    }
  }
  return s;
}

function formatMatchTime(iso) {
  if (iso == null || iso === "") return "TBC";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "TBC";
  return d.toLocaleString(undefined, {
    timeZone: "UTC",
    month: "short",
    day: "numeric",
    hour: "numeric",
    minute: "2-digit",
    timeZoneName: "short",
  });
}

function renderMatchList(rows) {
  if (!rows || !rows.length) {
    return '<p class="empty-hint">No matches in the list.</p>';
  }
  return `<div class="hltv-match-list" role="list" aria-label="Pro matches from HLTV">
    ${rows
      .map((m) => {
        const note = m.notes
          ? `<p class="hltv-row-note">${escapeHtml(formatMatchNotes(m.notes))}</p>`
          : "";
        const timeStr = formatMatchTime(m.matchTimeUtc);
        const tAttr =
          m.matchTimeUtc && timeStr !== "TBC"
            ? ` datetime="${escapeHtml(typeof m.matchTimeUtc === "string" ? m.matchTimeUtc : new Date(m.matchTimeUtc).toISOString())}"`
            : "";
        const open =
          m.matchPageUrl
            ? `<a href="${escapeHtml(m.matchPageUrl)}" target="_blank" rel="noopener" class="hltv-row-link">View on HLTV →</a>`
            : "";
        return `<article class="hltv-match-row" role="listitem">
          <div class="hltv-row-top">
            <time class="hltv-row-time"${tAttr}${timeStr === "TBC" ? " title='Start time not on feed'" : ""}>${escapeHtml(
              timeStr
            )}</time>
            <div class="hltv-row-pills">
              <span class="pill">${escapeHtml(m.tournament || "Event")}</span>
              <span class="pill pill-status">${escapeHtml(m.status || "—")}</span>
            </div>
          </div>
          <div class="hltv-row-teams">
            <span class="hltv-teams">${escapeHtml(m.teamA || "TBA")}</span>
            <span class="hltv-vs" aria-hidden="true">vs</span>
            <span class="hltv-teams">${escapeHtml(m.teamB || "TBA")}</span>
          </div>
          ${note}
          ${open ? `<div class="hltv-row-foot">${open}</div>` : ""}
        </article>`;
      })
      .join("")}
  </div>`;
}

async function loadMatches() {
  const box = document.getElementById("matches");
  if (!box) return;
  showLoading(box, "Loading HLTV…");
  try {
    const res = await fetch("/api/matches/today");
    if (!res.ok) {
      const err = await parseErrorBody(res);
      box.removeAttribute("data-state");
      box.className = "hltv-matches-out table-wrap";
      box.innerHTML = `<div class="error-box">${escapeHtml(err)}</div>`;
      return;
    }
    const rows = await res.json();
    box.removeAttribute("data-state");
    box.className = "hltv-matches-out table-wrap";
    if (!Array.isArray(rows)) {
      box.innerHTML = '<p class="empty-hint">No rows returned. Is <strong>hltv-bridge</strong> running?</p>';
      return;
    }
    if (rows.length === 0) {
      box.innerHTML =
        '<p class="empty-hint">No <strong>official pro</strong> matches in the <strong>today (UTC)</strong> window. Try again later, or open HLTV for the full week.</p>';
      return;
    }
    const m0 = rows[0];
    const hltvFail =
      m0 &&
      (m0.status === "error" ||
        m0.tournament === "HLTV bridge offline" ||
        m0.tournament === "HLTV fetch failed" ||
        m0.tournament === "HLTV bridge error");
    if (rows.length === 1 && hltvFail) {
      const msg =
        formatMatchNotes(m0.notes) ||
        "The pro schedule needs the HLTV data service running next to this app.";
      box.innerHTML = `<p class="error-box">${escapeHtml(msg)}</p>
        <p class="hltv-retry-hint">In a terminal: <code class="code-inline">cd hltv-bridge</code> then <code class="code-inline">npm install</code> and <code class="code-inline">npm start</code> (listens on port 3847). Restart <code class="code-inline">dotnet run</code> if you changed <code class="code-inline">HLTV_BRIDGE_URL</code>. If it still fails, hltv.org may be rate-limiting—wait 30s and press ↻. <a class="inline-link" href="https://www.hltv.org/matches" rel="noopener" target="_blank">Open HLTV</a> for the full schedule.</p>`;
      return;
    }
    if (rows.length === 1 && m0 && m0.tournament === "No match rows") {
      box.innerHTML = `<p class="empty-hint">${escapeHtml(formatMatchNotes(m0.notes) || "No pro matches in the list right now.")}</p>`;
      return;
    }
    box.innerHTML = renderMatchList(rows);
  } catch (e) {
    box.removeAttribute("data-state");
    box.className = "hltv-matches-out table-wrap";
    box.innerHTML = `<p class="error-box">${escapeHtml(e.message || String(e))}</p>`;
  }
}

function renderLineupResults(rows) {
  if (!rows || !rows.length) {
    return '<p class="empty-hint">No lineups for this search.</p>';
  }
  return `<ul class="lineup-results" aria-label="Lineup results">
    ${rows
      .map(
        (r) => `<li class="lineup-card">
      <div class="lineup-card-top">
        <h3 class="lineup-name">${escapeHtml(r.lineupName)}</h3>
        <span class="pill">${escapeHtml(r.grenadeType)}</span>
      </div>
      <p class="lineup-purpose">${escapeHtml(r.purpose)}</p>
      <div class="lineup-how" role="text">${escapeHtml(r.instructions)}</div>
      <p class="lineup-when"><span class="lineup-when-label">When</span> ${escapeHtml(r.whenToUse)}</p>
    </li>`
      )
      .join("")}
  </ul>`;
}

async function searchLineups() {
  const map = document.getElementById("lu-map")?.value.trim() || "";
  const site = document.getElementById("lu-site")?.value.trim() || "";
  const gt = document.getElementById("lu-gt")?.value.trim() || "smoke";
  const side = document.getElementById("lu-side")?.value.trim() || "T";
  const box = document.getElementById("lineups");
  if (!box) return;
  if (!map) {
    box.innerHTML = '<p class="error-box">Enter a map name.</p>';
    return;
  }
  showLoading(box, "Searching lineups…");
  const q = new URLSearchParams({ map, site, grenade_type: gt, side });
  try {
    const res = await fetch(`/api/lineups/search?${q}`);
    if (!res.ok) {
      box.removeAttribute("data-state");
      box.className = "panel-output";
      box.innerHTML = `<p class="error-box">${escapeHtml(await parseErrorBody(res))}</p>`;
      return;
    }
    const rows = await res.json();
    box.removeAttribute("data-state");
    box.className = "panel-output";
    box.innerHTML = renderLineupResults(rows);
  } catch (e) {
    box.removeAttribute("data-state");
    box.className = "panel-output";
    box.innerHTML = `<p class="error-box">${escapeHtml(e.message || String(e))}</p>`;
  }
}

async function loadStrats() {
  const box = document.getElementById("strats");
  if (!box) return;
  showLoading(box, "Loading strats…");
  try {
    const res = await fetch("/api/strats?userId=1");
    if (!res.ok) {
      box.removeAttribute("data-state");
      box.className = "strats-list";
      box.innerHTML = `<p class="error-box">${escapeHtml(await parseErrorBody(res))}</p>`;
      return;
    }
    const rows = await res.json();
    box.removeAttribute("data-state");
    box.className = "strats-list";
    if (!rows || !rows.length) {
      box.innerHTML = '<p class="empty-hint">Nothing here yet. Get an answer you like, then use <strong>Save to playbook</strong>.</p>';
      return;
    }
    box.innerHTML = rows
      .map(
        (s) => `<article class="strat-card">
          <h3>${escapeHtml(s.title)}</h3>
          <p class="strat-meta">${escapeHtml(new Date(s.createdAt).toLocaleString())}</p>
          <pre>${escapeHtml((s.bodyJson || "").slice(0, 500))}${(s.bodyJson || "").length > 500 ? "…" : ""}</pre>
        </article>`
      )
      .join("");
  } catch (e) {
    box.removeAttribute("data-state");
    box.className = "strats-list";
    box.innerHTML = `<p class="error-box">${escapeHtml(e.message || String(e))}</p>`;
  }
}

async function saveStrat() {
  const title = document.getElementById("strat-title")?.value.trim() || "";
  const notes = document.getElementById("strat-notes")?.value.trim() || "";
  if (!title) {
    setStatus("Title required", false);
    setTimeout(() => setStatus("Ready"), 2500);
    return;
  }
  setStatus("Saving…", true);
  const payload = {
    notes,
    lastAssistant: state.lastAssistant,
    sources: (state.lastSources || []).map((s) => ({ id: s.id, title: s.title })),
    tools: state.lastTools || [],
  };
  try {
    const res = await api("/api/strats/save", {
      method: "POST",
      body: JSON.stringify({ userId: 1, title, payload }),
    });
    if (!res.ok) throw new Error(await parseErrorBody(res));
    document.getElementById("strat-title").value = "";
    document.getElementById("strat-notes").value = "";
    await loadStrats();
    setStatus("Strat saved", true);
  } catch (e) {
    setStatus("Save failed", false);
    window.alert(e.message || String(e));
  } finally {
    setTimeout(() => setStatus("Ready"), 2000);
  }
}

async function explainEconomy() {
  const body = {
    teamMoney: Number(document.getElementById("e-money")?.value || 0),
    lossBonus: Number(document.getElementById("e-loss")?.value || 0),
    side: document.getElementById("e-side")?.value || "T",
    roundNumber: Number(document.getElementById("e-round")?.value || 1),
    weaponsSaved: (document.getElementById("e-saved")?.value || "")
      .split(",")
      .map((s) => s.trim())
      .filter(Boolean),
  };
  const out = document.getElementById("economy-out");
  if (!out) return;
  out.innerHTML = '<span class="muted small">Computing…</span>';
  try {
    const res = await api("/api/economy/explain", { method: "POST", body: JSON.stringify(body) });
    if (!res.ok) throw new Error(await parseErrorBody(res));
    const d = await res.json();
    out.className = "economy-out panel-output";
    out.innerHTML = `<span class="eco-title">${escapeHtml(d.buyRecommendation)}</span>
      <p>${escapeHtml(d.reasoning)}</p>
      <p class="match-note">Risk: <span class="pill pill-status">${escapeHtml(d.riskLevel)}</span></p>
      <p class="match-note">Suggested: ${(d.suggestedWeaponsUtility || []).map(escapeHtml).join(" · ")}</p>`;
  } catch (e) {
    out.innerHTML = `<p class="error-box">${escapeHtml(e.message || String(e))}</p>`;
  }
}

function wire() {
  document.getElementById("send-btn")?.addEventListener("click", () => {
    const input = document.getElementById("chat-input");
    const v = (input && input.value.trim()) || "";
    if (!v) return;
    input.value = "";
    sendChat(v);
  });
  document.getElementById("chat-input")?.addEventListener("keydown", (ev) => {
    if (ev.key === "Enter" && !ev.shiftKey) {
      ev.preventDefault();
      document.getElementById("send-btn")?.click();
    }
  });
  document.querySelectorAll("[data-prompt]").forEach((b) =>
    b.addEventListener("click", () => {
      const p = b.getAttribute("data-prompt");
      if (p) sendChat(p);
    })
  );
  document.getElementById("save-strat-btn")?.addEventListener("click", saveStrat);
  document.getElementById("lineup-search-btn")?.addEventListener("click", () => searchLineups());
  document.getElementById("economy-btn")?.addEventListener("click", () => explainEconomy());
  document.getElementById("refresh-matches")?.addEventListener("click", () => {
    setStatus("Refreshing HLTV…", true);
    loadMatches().then(() => setStatus("Ready"));
  });
}

function renderValveNews(data) {
  if (data && data.error && (!data.items || !data.items.length)) {
    return `<p class="error-box">${escapeHtml(data.error)}</p>
      <p class="valve-fallback small"><a href="https://store.steampowered.com/news/app/730" rel="noopener" target="_blank">View CS2 news on Steam</a> to read official patch notes in your browser.</p>`;
  }
  const items = data && data.items;
  if (!items || !items.length) {
    return '<p class="empty-hint">No patch posts returned right now, or the feed is empty for this request.</p>';
  }
  return `<ol class="valve-updates-list" role="list" start="1" aria-label="Valve updates for CS2">
    ${items
      .map(
        (n) => {
          const show = n.body && String(n.body).trim() ? n.body : n.excerpt || "";
          return `<li class="valve-update-row" role="listitem">
      <p class="valve-update-meta"><span>${escapeHtml(n.postedAt || "—")}</span>${
        n.source ? ` <span class="muted">· ${escapeHtml(n.source)}</span>` : ""
      }</p>
      <h3 class="valve-update-title"><a href="${escapeHtml(n.url)}" target="_blank" rel="noopener">${escapeHtml(
        n.title || "Update"
      )} <span class="valve-external" aria-hidden="true">↗</span></a></h3>
      <div class="valve-body" tabindex="0" aria-label="Valve post text"><pre class="valve-body-pre">${escapeHtml(
        show
      )}</pre></div>
    </li>`;
        }
      )
      .join("")}
  </ol>`;
}

function valveNewsApiUrl() {
  // Same host as this page; don’t use another origin unless the page is there too.
  return "/api/updates/valve-cs2?count=5";
}

function valveLoadErrorHint() {
  const o = typeof location !== "undefined" ? location.origin : "";
  return (
    `Patch notes load from this app’s API at ${o}/api/… — open the same URL you use for dotnet (e.g. the http://localhost:… printed when you run the project).` +
    " Browsers can’t call Steam’s API directly; the .NET app does that for you."
  );
}

async function loadValveNews() {
  const box = document.getElementById("valve-updates");
  if (!box) return;
  showLoading(box, "Loading patch notes…");
  const finish = (data) => {
    box.removeAttribute("data-state");
    box.className = "valve-updates-panel";
    box.innerHTML = renderValveNews(data);
  };
  try {
    const res = await fetch(valveNewsApiUrl());
    const text = await res.text();
    const t = text.trim();
    if (!t.startsWith("{") && !t.startsWith("[")) {
      finish({
        items: [],
        error: `Got a web page instead of JSON. ${valveLoadErrorHint()}`,
      });
      return;
    }
    let data;
    try {
      data = JSON.parse(t);
    } catch {
      finish({ items: [], error: "The app returned data that isn’t valid JSON. Restart the server and try again, or use the Steam link below." });
      return;
    }
    if (!res.ok) {
      const msg = (data && (data.error || data.title || data.detail)) || `Request failed (HTTP ${res.status})`;
      const its = data?.items;
      data = { error: String(msg), items: Array.isArray(its) ? its : [] };
    }
    // Trust server: { items, error? } (Steam is fetched on the server only; no in-browser Steam — CORS blocks it)
    finish(data);
  } catch (e) {
    const m = (e && e.message) || String(e);
    const isNetwork = m === "Failed to fetch" || (e && e.name === "TypeError");
    finish({
      items: [],
      error: isNetwork
        ? `Can’t reach ${location.origin} — ${valveLoadErrorHint()} (${m})`
        : m,
    });
  }
}

function boot() {
  wire();
  setStatus("Ready", true);
  void loadMatches();
  void loadValveNews();
  void loadStrats();
  void searchLineups();
}

boot();
