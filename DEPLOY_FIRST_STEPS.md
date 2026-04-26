# First-time deployment (plain English)

This doc is for **you and anyone cloning the repo** who has not used "PaaS" products before. It explains **what the moving parts are**, what **Railway** and **Render** actually do, and what to click through **once**, end to end.

---

## What you are actually doing

- **The app** is one **ASP.NET** program. It serves the **website** and the **API** from a single public URL. There is no separate "frontend host" in the default setup.
- **MySQL** is a **database server** that must run **somewhere** the API can reach (same datacenter is ideal). You create tables once (`database/schema.sql`) and optionally load demo data (`database/seed.sql`).
- **Environment variables** are name/value settings the host injects at runtime (API keys, DB address, etc.). You set them in the **dashboard** of the platform, not in committed files.
- **HLTV pro matches** need a small **separate** Node app (`hltv-bridge`). On your laptop, `http://127.0.0.1:3847` works. **In the cloud, `127.0.0.1` is wrong** — it would point at the API container itself. So for production you either:
  - deploy a **second** service from `hltv-bridge/Dockerfile` and set `HLTV_BRIDGE_URL` to its **https** public URL, or
  - skip HLTV in production (other features still work, including **Steam** patch news from the .NET app).

---

## Vocabulary (30 seconds)

| Term | Meaning |
|------|--------|
| **Git** | The version control that stores the code. |
| **GitHub** | Where the repo may live. Many hosts "deploy from GitHub" with one button. |
| **Repository / repo** | The project folder as stored in git. |
| **Docker** | A standard way to **build the same image** of the app anywhere. This repo’s root `Dockerfile` is the main API image. |
| **Container** | A running instance built from that image. |
| **PaaS** (Platform as a service) | Railway, Render, Fly, etc. — they run your container and give you a **URL**. You pay for compute + sometimes MySQL. |
| **Config / env vars** | Key-value settings (e.g. `OPENAI_API_KEY`, `MYSQL_HOST`) that the app reads on startup. |

---

## What works without extra work

- **OpenAI** chat, **MySQL** RAG, lineups, strats: yes, if MySQL and env are set.
- **Steam** CS2 / Valve news: yes — the **.NET** server fetches it (no browser CORS to Steam from users).
- **HLTV in production**: only if you run the bridge as a **separate** public service and set `HLTV_BRIDGE_URL` (see above).

**Smoke test after any deploy:** open `https://YOUR-APP-HOST/api/health` — you should see JSON with `"status": "ok"`.

---

## Local: Docker Compose (optional but recommended)

From the **repo root** (where `docker-compose.yml` is):

1. Copy `example.env` to **`.env`** in the same folder. Put a real `OPENAI_API_KEY` in `.env` (or compose will start but the chat will fail at runtime).
2. Run: `docker compose up --build`
3. Open `http://localhost:8080/`. HLTV bridge and MySQL are wired for you inside Docker.

To reset the dev database completely: `docker compose down -v` then `up` again (wipes the MySQL volume; schema and seed re-run on first start).

---

## Path A: Railway (walkthrough, minimal jargon)

**Railway** = sign up, connect **GitHub**, let it build the **root `Dockerfile`**, set **env vars**, attach a **MySQL** plugin, run SQL, get a public URL. Names change slightly in the UI over time, but the flow is stable.

### A1 — Account and project

1. Go to [railway.app](https://railway.app) and create an account (e.g. sign in with **GitHub** so repos are easy to pick).
2. **New project** → **Deploy from GitHub** (or **Empty project** and add a **GitHub repo** source).
3. Select **this** repository. Railway will detect a **Dockerfile** at the repo root and use it to build the **API** service.

### A2 — Add MySQL

1. In the same project, **New** → **Database** → **MySQL** (or "MySQL" template).
2. When it is created, open the **MySQL** service and find **Connect** or **Variables**; copy:
   - host, port, user, password, database name.

### A3 — Map env vars on the **API** service (not the DB)

Open your **web/API** service (the one that built from `Dockerfile`) → **Variables** and set (names must match what the app expects — see `example.env`):

| Name | What to put |
|------|-------------|
| `OPENAI_API_KEY` | Your key from the OpenAI dashboard. |
| `OPENAI_MODEL` | e.g. `gpt-4o-mini` |
| `MYSQL_HOST` | From the Railway MySQL "host" (often a hostname, not an IP). |
| `MYSQL_PORT` | Usually `3306` unless the panel says otherwise. |
| `MYSQL_USER` | From MySQL template. |
| `MYSQL_PASSWORD` | From MySQL template. |
| `MYSQL_DATABASE` | Database name you will use. |
| `MYSQL_SSL_MODE` | Start with `Preferred` (default). If connection fails, try `Required` or what Railway documents for their MySQL. |

**Do not** set `HLTV_BRIDGE_URL` to `http://127.0.0.1:...` in production. Either leave default until you add the bridge, or set the **public** URL of the HLTV service (below).

**Note:** Railway often sets **`PORT`**. The app’s `Program.cs` already uses `PORT` to bind, so the platform’s value is the one that wins.

### A4 — Create tables (schema + optional seed)

Use **any** MySQL client: Railway’s web console, TablePlus, DBeaver, or `mysql` CLI.

1. Connect with the same host/user/password as above; select your database.
2. Run the contents of **`database/schema.sql`**, then **`database/seed.sql`**.

If you skip seed, the app can still work; you just have less demo data.

### A5 — (Optional) HLTV bridge as a **second** Railway service

1. **New service** in the same project → **Empty service** (or "Dockerfile from repo").
2. **Root directory** or **build context** should point to **`hltv-bridge`**` folder / use **`hltv-bridge/Dockerfile`**. In Railway, you may set the **Dockerfile path** in service settings to `hltv-bridge/Dockerfile` and the context to the repo root or `hltv-bridge` depending on the UI. Goal: the built image runs `node server.mjs` and **listens** on the port the platform gives you (`PORT` is read by the bridge first).
3. Deploy; copy the service’s **https** public URL, e.g. `https://something.up.railway.app`.
4. On the **API** service, set:

   `HLTV_BRIDGE_URL=https://something.up.railway.app/`

(Trailing slash is fine; the API normalizes it.)

### A6 — Verify

- Open the **API** service’s **public URL** in the browser for the app UI: `https://.../`
- Open `https://.../api/health` and confirm `status: ok`

---

## Path B: Render (very similar, different names)

1. [render.com](https://render.com) → sign in with **GitHub**.
2. **New** → **Web Service** (or **Blueprint** if you add a `render.yaml` later) → connect this repo.
3. **Environment**: **Docker**; set **Dockerfile path** to the **root** `Dockerfile` (or leave default if root is correct for your layout).
4. **Instance type**: smallest is fine to start.
5. Add a **MySQL** instance: **New** → **MySQL** (or use a provider they document). Put `MYSQL_*` in the **web service** env from the MySQL **Internal** (or **External** if allowed) connection details. Render will often give you a **port** and **user**; match them in env vars.
6. Run `schema.sql` and `seed.sql` against that database (same as A4).
7. HLTV: add a second **Web Service** from `hltv-bridge/Dockerfile` (same as A5) and set `HLTV_BRIDGE_URL` on the API.

**Health check URL** in Render: you can set a path `/api/health` in the service settings if the UI offers it, so restarts are cleaner.

---

## "Someone else" using your repo (clone)

1. `git clone ...` then open a terminal in the project folder.
2. Copy `example.env` to `.env`, fill in keys and local MySQL or use `docker compose up` as above.
3. `dotnet` path: from repo root, `dotnet run --project CS2TacticalAssistant.Api` (and run `hltv-bridge` in another terminal for matches), or use `start-dev.ps1` on Windows.

**They** deploy the same `Dockerfile` to Railway/Render with the same env list.

---

## Troubleshooting (short)

| Symptom | What to check |
|--------|----------------|
| API starts but "missing MYSQL_*" | All five `MYSQL_` variables set in the **host** dashboard, then redeploy or restart. |
| DB connection / SSL errors | `MYSQL_SSL_MODE=Required` (or provider’s docs). |
| HLTV empty or errors in prod | `HLTV_BRIDGE_URL` must be the **second service’s URL**, not localhost. |
| 404 on `/api/...` | You might be on an old build; ensure latest image is deployed and you are not behind a path-prefix misconfiguration. |
| `docker compose` fails: no `.env` | Add `.env` with at least `OPENAI_API_KEY=...` for the API container. |

---

## Files that matter

| File | Role |
|------|------|
| `Dockerfile` | Builds the .NET API + `wwwroot` UI. |
| `hltv-bridge/Dockerfile` | Optional second service for HLTV. |
| `docker-compose.yml` | Local: MySQL + seed + API + HLTV in one go. |
| `example.env` | Template of **names**; copy to `.env` for local. |
| `database/schema.sql`, `database/seed.sql` | Run against production MySQL **once** per new database. |
| `DEPLOYMENT.md` | Shorter, Heroku/CLI-oriented notes. |

This should be enough to go from "never used Railway" to a working public URL, with a clear list of what is optional (HLTV second service) vs required (DB + `OPENAI_*` for chat + API env).
