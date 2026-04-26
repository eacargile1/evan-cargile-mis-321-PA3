/**
 * Unofficial HLTV data — uses the `hltv` npm package (https://github.com/gigobyte/HLTV).
 * One call to HLTV.getMatches() = 1 request to hltv.org (per package README; respect rate limits / Cloudflare).
 */
import http from "node:http";
import { URL } from "node:url";
import { HLTV } from "hltv";

// PaaS (Render/Railway) often set PORT; HLTV_BRIDGE_PORT is the explicit override.
const port = Number(process.env.PORT || process.env.HLTV_BRIDGE_PORT || 3847);
// Listen on all interfaces in Docker/cloud (override with HLTV_BRIDGE_HOST=127.0.0.1 for local-only).
const host = process.env.HLTV_BRIDGE_HOST || "0.0.0.0";

function toUtcIso(dataUnix) {
  if (dataUnix == null || dataUnix === 0) return new Date().toISOString();
  if (dataUnix instanceof Date) return dataUnix.toISOString();
  if (typeof dataUnix === "string") {
    const p = Date.parse(dataUnix);
    if (!Number.isNaN(p)) return new Date(p).toISOString();
  }
  const n = Number(dataUnix);
  if (Number.isNaN(n)) return new Date().toISOString();
  // hltv.org: upcoming matches are usually Unix *seconds*; if value looks like ms, use as-is
  const ms = n < 1_000_000_000_000 ? n * 1000 : n;
  const d = new Date(ms);
  return Number.isNaN(d.getTime()) ? new Date().toISOString() : d.toISOString();
}

function mapRow(m) {
  const t1 = m.team1?.name || m.title || "TBA";
  const t2 = m.team2?.name || "TBA";
  const eventName = m.event?.name || "Event";
  return {
    id: m.id,
    teamA: t1,
    teamB: t2,
    tournament: eventName,
    matchTimeUtc: m.date != null && m.date !== 0 ? toUtcIso(m.date) : null,
    status: m.live ? "live" : "upcoming",
    notes: m.format ? `Format: ${m.format}${m.stars ? ` · ★${m.stars}` : ""}` : m.stars ? `★${m.stars}` : null,
    matchPageUrl: `https://www.hltv.org/matches/${m.id}/`,
    source: "hltv",
  };
}

async function handle(req, res) {
  res.setHeader("Access-Control-Allow-Origin", "*");
  const u = new URL(req.url || "/", `http://${host}`);

  if (req.method === "OPTIONS") {
    res.setHeader("Access-Control-Allow-Methods", "GET, OPTIONS");
    res.setHeader("Access-Control-Allow-Headers", "Content-Type");
    res.writeHead(204);
    res.end();
    return;
  }

  if (req.method !== "GET" || (u.pathname !== "/matches" && u.pathname !== "/")) {
    res.writeHead(404, { "Content-Type": "application/json" });
    res.end(JSON.stringify({ error: "use GET /matches" }));
    return;
  }

  const delay = (ms) => new Promise((r) => setTimeout(r, ms));
  const attempts = Math.max(1, Number(process.env.HLTV_RETRIES || 3));

  for (let i = 0; i < attempts; i++) {
    try {
      if (i > 0) {
        // hltv.org / Cloudflare often reset long-lived scrapes — back off
        const backoff = 600 * (1 << (i - 1));
        await delay(backoff);
        // eslint-disable-next-line no-console
        console.warn(`hltv: retry ${i + 1}/${attempts} after ECONNRESET / network issue`);
      }
      const list = await HLTV.getMatches();
      const body = JSON.stringify(list.map(mapRow));
      res.writeHead(200, { "Content-Type": "application/json; charset=utf-8" });
      res.end(body);
      return;
    } catch (e) {
      const msg = (e && e.message) || String(e);
      const last = i === attempts - 1;
      if (!last && (msg.includes("ECONNRESET") || msg.includes("ETIMEDOUT") || msg.includes("socket hang up")))
        continue;
      const user =
        msg.includes("ECONNRESET") || msg.includes("ETIMEDOUT")
          ? "hltv.org closed the connection (rate limit / network / anti-bot). Wait 30s and refresh, or run the bridge from hltv-bridge/ again."
          : msg;
      res.writeHead(503, { "Content-Type": "application/json; charset=utf-8" });
      res.end(JSON.stringify({ error: user, code: (e && e.code) || "HLTV_ERR" }));
      return;
    }
  }
}

http.createServer(handle).listen(port, host, () => {
  // eslint-disable-next-line no-console
  console.log(`hltv-bridge listening on http://${host}:${port}  (GET /matches)`);
});
