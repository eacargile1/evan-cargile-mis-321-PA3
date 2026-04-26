# CS2 Tactical Assistant

Full-stack **Counter-Strike 2** tactical dashboard: chat coach powered by an **LLM**, **RAG** over a MySQL knowledge base, **OpenAI function calling** into C# tools, and **upcoming pro matches** from [HLTV](https://www.hltv.org/) via the [gigobyte/HLTV](https://github.com/gigobyte/HLTV) npm package (Node bridge; **not** an official HLTV API or API key).

## What this repo contains

| Area | Location |
|------|----------|
| ASP.NET Core Web API + static UI | `CS2TacticalAssistant.Api/` |
| MySQL schema | `database/schema.sql` |
| Seed data (knowledge, lineups) | `database/seed.sql` |
| HLTV bridge (Node + `hltv` npm) | `hltv-bridge/` |
| Env template | `example.env` |
| Container deploy | `Dockerfile`, `hltv-bridge/Dockerfile`, `docker-compose.yml`, `DEPLOYMENT.md`, **[`DEPLOY_FIRST_STEPS.md`](./DEPLOY_FIRST_STEPS.md)** |

### File structure (high level)

```
321FinalPA/
  CS2TacticalAssistant.sln
  Dockerfile
  DEPLOYMENT.md
  example.env
  README.md
  database/
    schema.sql
    seed.sql
  hltv-bridge/
    server.mjs          # GET /matches → HLTV.getMatches()
  CS2TacticalAssistant.Api/
    Program.cs                 # DI, static files, PORT for Heroku
    Controllers/               # REST endpoints
    Services/
      DatabaseService.cs       # MYSQL_* → connection string
      RagService.cs            # RAG retrieval (FULLTEXT + LIKE)
      LlmService.cs            # LLM + tool loop + RAG prompt
      FunctionCallingService.cs# Tool dispatch
      StrategyService.cs
      EconomyService.cs
      HltvBridgeMatchService.cs  # HTTP → hltv-bridge (gigobyte/hltv)
      LineupService.cs
    Models/
    wwwroot/
      index.html
      css/app.css
      js/app.js
```

## Features (grading checklist)

- [x] **LLM chat** — `POST /api/chat` → `LlmService` → OpenAI `ChatClient` (`OPENAI_API_KEY`).
- [x] **RAG** — `knowledge_chunks` table; `RagService` + injection into system prompt (see comments in `LlmService`).
- [x] **Function calling** — tools: `generate_round_strategy`, `explain_economy_decision`, `search_lineups`, `get_today_matches` (schemas + loop in `LlmService`; execution in `FunctionCallingService`).
- [x] **MySQL** — JawsDB/Heroku-style `MYSQL_*` env vars; all domain tables + seed data.
- [x] **REST API** — chat, knowledge search, strats, matches, economy, lineups (see below).
- [x] **Frontend** — dark CS-inspired UI, suggested prompts, sources panel, matches, lineups, economy helper, save strat.
- [x] **HLTV matches** — `hltv-bridge` (Node) calls `hltv`’s `getMatches` (1× request to hltv.org per refresh; no official API key — respect rate limits / Cloudflare, see gigobyte README).
- [x] **Deployment notes** — `DEPLOYMENT.md`, `Dockerfile`, `PORT` binding.

## API endpoints

| Method | Path | Purpose |
|--------|------|---------|
| POST | `/api/chat` | Chat + RAG + tools |
| GET | `/api/knowledge/search?q=` | Direct RAG search |
| POST | `/api/strats/save` | Save JSON strat for a user |
| GET | `/api/strats?userId=1` | List saved strats |
| GET | `/api/matches/today?tournament=` | Upcoming/live matches (via `hltv-bridge` → HLTV) |
| POST | `/api/economy/explain` | Heuristic buy guidance |
| GET | `/api/lineups/search?map=&site=&grenade_type=&side=` | Lineup library |

## HLTV bridge (upcoming match list)

The “Upcoming (HLTV)” panel does **not** use MySQL. A small **Node** process wraps [gigobyte/HLTV](https://github.com/gigobyte/HLTV) (unofficial; **no HLTV API key**; it loads public hltv.org pages — respect rate limits and expect occasional `ECONNRESET` / Cloudflare issues).

**Do this once (install):**

1. Open a **terminal** at the **repository root** (same folder as `CS2TacticalAssistant.sln`).
2. `cd hltv-bridge`
3. `npm install`

**Every time you develop (run the bridge before or while the .NET app runs):**

1. In that same `hltv-bridge` folder, run: `npm start`  
   - You should see: `hltv-bridge listening on http://127.0.0.1:3847`  
   - The API calls `http://127.0.0.1:3847/matches` (or whatever you set in env).
2. In the **repo root** `.env` (copy from `example.env`), set:
   - `HLTV_BRIDGE_URL=http://127.0.0.1:3847`  
   (If you change the port, set `HLTV_BRIDGE_PORT` in the environment when you start the bridge, and set `HLTV_BRIDGE_URL` to the same base URL. The C# app adds the trailing slash internally.)
3. (Re)start **`dotnet run`** for `CS2TacticalAssistant.Api` so it loads `.env`.
4. In the browser, open the app, then **↻** on the HLTV card if the list was empty.

**Check that it works:**

- Browser: [http://127.0.0.1:3847/matches](http://127.0.0.1:3847/matches) should return a JSON array (or an error object if hltv.org is blocking).
- PowerShell: `Invoke-RestMethod http://127.0.0.1:3847/matches`

**If the UI shows an error or empty list:** bridge not running, wrong `HLTV_BRIDGE_URL`, hltv.org throttling — wait ~30s and hit ↻, or set `HLTV_RETRIES=5` in the environment when you start the bridge. See `hltv-bridge/README.md` and `hltv-bridge/server.mjs` for `HLTV_RETRIES` / `HLTV_BRIDGE_PORT` / `HLTV_BRIDGE_HOST`.

## Local setup (TA walkthrough)

**Prerequisites:** [.NET 9 SDK](https://dotnet.microsoft.com/download), [Node.js](https://nodejs.org/) 18+ (for the HLTV bridge), MySQL 8+ (local or remote), OpenAI API key.

1. **Clone** the repository.

2. **Create database** (example name `cs2_tactical`).

3. **Run SQL:**
   ```bash
   mysql -u YOUR_USER -p cs2_tactical < database/schema.sql
   mysql -u YOUR_USER -p cs2_tactical < database/seed.sql
   ```

4. **Environment variables** — copy `example.env` to **`.env`** in the **repo root** and set (do not commit `.env`):
   - `OPENAI_API_KEY`
   - `MYSQL_HOST`, `MYSQL_PORT`, `MYSQL_USER`, `MYSQL_PASSWORD`, `MYSQL_DATABASE`
   - `HLTV_BRIDGE_URL` — must match the HLTV bridge; default `http://127.0.0.1:3847` (see **[HLTV bridge](#hltv-bridge-upcoming-match-list)** above)

   **Windows (PowerShell, session only):**
   ```powershell
   $env:OPENAI_API_KEY="sk-..."
   $env:MYSQL_HOST="127.0.0.1"
   $env:MYSQL_PORT="3306"
   $env:MYSQL_USER="root"
   $env:MYSQL_PASSWORD="..."
   $env:MYSQL_DATABASE="cs2_tactical"
   $env:HLTV_BRIDGE_URL="http://127.0.0.1:3847"
   ```

5. **Start the HLTV bridge** (separate terminal; required for the match list): [steps above](#hltv-bridge-upcoming-match-list) (`hltv-bridge` → `npm install` once → `npm start`).

6. **Run API (serves UI from wwwroot):**
   ```bash
   cd CS2TacticalAssistant.Api
   dotnet run --launch-profile http
   ```

7. **Open** `http://localhost:5168/` (see `Properties/launchSettings.json` for the exact port).

8. **Test** the **AI Coach** quick prompts; confirm **Sources (RAG)** updates; **Upcoming (HLTV)** loads (bridge must be running); **Lineup search** returns Mirage A smokes; **Economy helper** works; **Save strat** persists to `saved_strats`.

### Demo prompts (seeded)

- “Generate a T-side pistol round strat for Ancient.”
- “Should we force buy after losing pistol?”
- “Give me a Mirage A smoke lineup.”
- “What should our CT setup be on Inferno B?”
- “Who is playing in pro league today?”
- “Explain when to save instead of retake.”
- “Create a full-buy execute for B site Mirage.”

## Where to point during a demo

| Topic | File(s) |
|-------|---------|
| LLM call / tool loop | `Services/LlmService.cs` |
| RAG retrieval | `Services/RagService.cs` + prompt block in `LlmService` |
| Function calling dispatch | `Services/FunctionCallingService.cs` |
| MySQL connection | `Services/DatabaseService.cs` |
| Hosted PORT | `Program.cs` (Heroku) |
| HLTV schedule (Node bridge) | `hltv-bridge/server.mjs` + `Services/HltvBridgeMatchService.cs` |

## Screenshots

_Add screenshots of the chat UI, sources panel, and matches card here for your write-up._

## Project explanation (short)

Users interact with a coach-style assistant. Each question triggers **RAG** search against `knowledge_chunks`; retrieved text is embedded in the **system prompt**. The model may call **tools** that run **server-side logic** (strategy generator, economy helper, lineup DB query, HLTV match list). **Chat logs** optionally persist to `chat_logs` for auditing. Upcoming matches are read from HLTV public pages through the `hltv` npm package (see `hltv-bridge/`), not from MySQL.

## Live hosting

See **`DEPLOYMENT.md`** and the step-by-step **`DEPLOY_FIRST_STEPS.md`** (Railway/Render, no assumed prior experience). `docker compose up` from the repo root runs **MySQL + HLTV bridge + API** for local use. Heroku does not ship a first-class .NET buildpack in all stacks; using the **Dockerfile** on a container-capable host is the most predictable path.

## License / course use

Built for coursework / portfolio; Counter-Strike is a trademark of Valve Corporation — this project is an independent fan/education project.
