# Deployment guide — CS2 Tactical Assistant

**First time deploying?** See **[DEPLOY_FIRST_STEPS.md](./DEPLOY_FIRST_STEPS.md)** (Railway/Render vocabulary, MySQL, optional HLTV service, and verification via `/api/health`).

**Local all-in-one:** from repo root, `docker compose up --build` (needs `.env` with at least `OPENAI_API_KEY`; see [docker-compose.yml](./docker-compose.yml) comments).

This API serves the static frontend from `wwwroot/` and exposes `/api/*` on the same origin. Use **`GET /api/health`** to confirm a deploy (JSON `status: ok`).

## Environment variables

Set the same variables as `example.env` on your host:

- `OPENAI_API_KEY`, optional `OPENAI_MODEL`
- `MYSQL_HOST`, `MYSQL_PORT`, `MYSQL_USER`, `MYSQL_PASSWORD`, `MYSQL_DATABASE`

On **Heroku**, add **JawsDB MySQL** (or compatible) and copy credentials into Config Vars. Run `database/schema.sql` then `database/seed.sql` using any MySQL client against the JawsDB URL.

Heroku often injects `PORT`; `Program.cs` binds Kestrel to `http://0.0.0.0:{PORT}` when `PORT` is set.

## Option A — Docker (recommended for .NET on Heroku container / Fly / Railway / Render)

From repo root (where this `Dockerfile` lives):

```bash
docker build -t cs2-tactical .
docker run --rm -p 8080:8080 \
  -e OPENAI_API_KEY=... \
  -e MYSQL_HOST=... -e MYSQL_PORT=3306 \
  -e MYSQL_USER=... -e MYSQL_PASSWORD=... -e MYSQL_DATABASE=cs2_tactical \
  cs2-tactical
```

Open `http://localhost:8080/`.

## Option B — Publish + host on a VM

```bash
dotnet publish CS2TacticalAssistant.Api -c Release -o ./publish
cd publish
export OPENAI_API_KEY=...
export MYSQL_HOST=...
# ... other MYSQL_* vars
dotnet CS2TacticalAssistant.Api.dll
```

Put a reverse proxy (nginx, Caddy) with TLS in front for production.

## Frontend hosting

The UI is static files inside the API. **You do not need a separate frontend host** unless you want a CDN; if you split them later, enable CORS for your static origin and change `fetch("/api/...")` in `wwwroot/js/app.js` to use an absolute API base URL.

## Swapping mock matches for a live API

Implement a new class (e.g. `LiveMatchService : IMatchService`) that calls your provider, then replace the DI registration in `Program.cs`. Keep `pro_matches` as a fallback for demos if you want.
