# HLTV bridge (Node)

Small HTTP server that exposes `GET /matches` for the CS2 Tactical Assistant API. It uses the [`hltv`](https://www.npmjs.com/package/hltv) package ([gigobyte/HLTV](https://github.com/gigobyte/HLTV)) to read **public** hltv.org data — there is **no official HLTV API key**. Too many requests can get you rate-limited or blocked; see the upstream README.

**Full setup (also documented in the repo root `README.md` → “HLTV bridge”).**

## 1. Install (once per clone)

From the **repository root**:

```bash
cd hltv-bridge
npm install
```

## 2. Run (every time you work on the app)

Keep this terminal open next to the .NET app:

```bash
npm start
```

You should see something like:

```text
hltv-bridge listening on http://0.0.0.0:3847  (GET /matches)
```

(`0.0.0.0` = listen on all interfaces; call it from the API at `http://127.0.0.1:3847` as usual. To listen only on loopback, set `HLTV_BRIDGE_HOST=127.0.0.1`.)

**Production:** deploy the repo’s `hltv-bridge/Dockerfile` as a second service, set the platform’s `PORT` (the bridge uses `PORT` or `HLTV_BRIDGE_PORT`), then set `HLTV_BRIDGE_URL` on the API to that service’s public `https` URL. See `DEPLOY_FIRST_STEPS.md`.

## 3. Point the .NET API at this URL

In the **repo root** `.env` (not inside `hltv-bridge/`):

```env
HLTV_BRIDGE_URL=http://127.0.0.1:3847
```

Then start `CS2TacticalAssistant.Api` with `dotnet run` so it loads that variable.

## 4. Verify

- Open [http://127.0.0.1:3847/matches](http://127.0.0.1:3847/matches) — you should get JSON.
- In the site’s **Upcoming (HLTV)** card, use **↻** after the bridge is up.

## Optional environment

| Variable | Default | Meaning |
|----------|---------|---------|
| `HLTV_BRIDGE_PORT` | `3847` | HTTP port for this process |
| `HLTV_BRIDGE_HOST` | `127.0.0.1` | Bind address (use `0.0.0.0` in Docker if needed) |
| `HLTV_RETRIES` | `3` | Retries when hltv.org resets the connection (e.g. ECONNRESET) |
| `HLTV_BRIDGE_URL` | — | Used by **.NET only** (in repo root `.env`); set to `http://127.0.0.1:PORT` matching the bridge |

If you change the port, e.g. `HLTV_BRIDGE_PORT=3848`, set `HLTV_BRIDGE_URL=http://127.0.0.1:3848` in `.env` and restart the API.

## Deploying

Run this process next to the API (second process, sidecar, or small VM) and set `HLTV_BRIDGE_URL` in production to the bridge’s public URL (with a firewall in front).
