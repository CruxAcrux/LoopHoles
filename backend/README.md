# Loopholes — Backend

Server-authoritative game backend for **Loopholes**, per `../loopholes_project_brief.md`.
ASP.NET Core 9 + EF Core/SQLite. This resolves open item §19 of the brief in
favour of **.NET over Python**.

## Boundary with the game client

This is a **standalone service**. It has no reference to the Godot project and
the Godot project has no reference to it — the only contract between them is
HTTP + JSON over `/api/v1`. The client keeps its own copy of the wire models.

That means this solution builds, tests, runs and deploys on its own, and the two
sides can move independently. Changing a DTO here is a wire-format change: it
has to stay backwards-compatible or be versioned under a new route prefix, the
same as for any third-party consumer.

## Running it

```bash
cd src/Loopholes.Api
dotnet run                 # http://localhost:5147, migrates loopholes.db on boot
```

```bash
dotnet test                # 104 tests
```

Swap SQLite for Postgres by changing one provider call in
`Loopholes.Infrastructure/ServiceCollectionExtensions.cs` — nothing above that
line knows which database it is talking to.

## Projects

| Project | Role |
|---|---|
| `Loopholes.Contracts` | Wire DTOs and enums — the shape of the JSON, server-side. |
| `Loopholes.Domain` | Entities plus pure economy maths. No EF, no ASP.NET, no I/O. |
| `Loopholes.Infrastructure` | `DbContext`, migrations, and the game services. |
| `Loopholes.Api` | Minimal-API endpoints, bearer auth, market ticker. |
| `Loopholes.Tests` | Unit tests over the maths, plus integration tests over the real HTTP app. |

## Why it's built this way

The brief's hard requirement is that the economy cannot be exploited the way
DeKrim's price-refresh bug was. Four structural decisions carry that:

**Job outcomes are sealed at start, not at collect.** `ActiveJob.RolledPayout` and
`RiskTriggered` are decided and stored when the job begins. Collecting only
reveals them. A player who dislikes a result has nothing to retry — the second
collect returns `job_not_running`.

**Prices only move in the background ticker.** `MarketTickerService` runs every
5 minutes server-side. No client request path can cause a price to re-roll.
Trades feed `NetVolumeSinceTick`, so prices move with aggregate player activity
(brief §6) rather than pure randomness, damped by mean reversion and hard-clamped
to 0.25×–4× base.

**Time is derived, never sent.** Energy is a snapshot plus a timestamp
(`EnergyMath.Regenerate`), resolved on read. Freezes, study timers, travel and
season rollovers work the same way. There is no clock value the client can lie
about, and no scheduler to keep in sync.

**All game randomness is behind `IRandomSource`,** which exists only on the
server. Tests substitute a dial so "always caught" and "never caught" are both
one line.

## Balance lives in one file

`Loopholes.Domain/GameData.cs` holds every tunable number, transcribed from
`loopholes_economy_model.xlsx`. Nothing else hardcodes an economy value.
`BalanceTests.cs` asserts the transcription good-by-good and job-by-job — change
the spreadsheet, and exactly the corresponding test fails.

## API

All routes under `/api/v1`. Auth is `Authorization: Bearer <token>` from
`/auth/register` (device-bound; only the SHA-256 hash of the token is stored).
Broken rules return a stable `{ "Code", "Message" }` so the client can branch on
`insufficient_energy` rather than parse prose.

| Method | Route | Notes |
|---|---|---|
| POST | `/auth/register` | Register a device or re-issue its token |
| GET | `/me` | Player state with all timers advanced to now |
| GET | `/jobs` | Catalogue with per-player unlock state + running jobs |
| POST | `/jobs/start` · `/jobs/collect` | Outcome sealed at start, revealed at collect |
| GET | `/market` | Live prices (public) |
| POST | `/market/buy` · `/market/sell` | Server-priced, 2% fee both ways |
| GET | `/inventory` | Stacked, with hold times and appreciation |
| GET | `/travel/options` · POST `/travel/start` | Trust-adjusted risk, rolled once per trip |
| POST | `/license/study` · `/license/exam` | Legit and cheat paths |
| POST | `/freeze/paperwork` · `/freeze/lawyer` | Combinable, per brief §11 |
| GET | `/leaderboard?mode=` | `Empire` \| `Richest` \| `MostTrusted` |
| GET | `/stats` | Lifetime totals + dated trust history |
| GET | `/wardrobe` · POST `/wardrobe/buy` · `/wardrobe/equip` | Favor only, never cash |

## Judgement calls worth reviewing

These weren't specified in the brief; they're marked so a balance pass can
overrule them:

- **Tier 2 unlocks after 5 Tier 1 hustles** (`GameData.Tier2UnlockJobsCompleted`).
  The brief says "require some trust/cash" and targets Day 1; a job count is the
  most predictable read of that.
- **The $500 licence fee is charged on the cheat path too.** The brief only says
  cheating skips *study*, so the fee still buys the licence.
- **Vintage Furniture can be sold but not bought.** Otherwise buy-and-hold is a
  risk-free +30% bounded only by storage. It's obtainable from Furniture Pickup.
- **Starting cash is $50** — exactly one Ghost Invoice of runway.
- **Failed travel rolls pick cargo-fine or delay at 50/50.** The brief lists both
  and doesn't weight them.
- **Empire Score is ratcheted within a season**, so a late sell-off can't be used
  to dodge a rival's rank.
- **Repeat licence cheating escalates Minor → Major freeze** on the second
  offence, matching the §11 trigger table.

## Not built (phase 2, per brief §18)

Companies and Tier 3/4 jobs, joint ventures and the betrayal mechanic, clans and
sabotage, the full rotating cosmetics shop. The trust system, including
`TrustEventKind.FairSplit` / `Betrayal` / `Restitution` and
`TrustMath.RestitutionOwed`, is already in place as the foundation those need.

Also not built here: push notifications (§18) — that needs the client's device
tokens, so it belongs with the Godot work.

## Note on living next to the Godot project

`backend/` sits inside the Godot project folder for convenience, but is not part
of it. Two guards keep them apart:

- `backend/.gdignore` stops the engine importing any of it as game resources.
- The client's `test_dekrim.csproj` carries `<Compile Remove="backend/**" />`,
  because Godot's generated project globs `**/*.cs` from the root and would
  otherwise try to compile the server into the game.

Do not add a `ProjectReference` in either direction. If the two ever need to
share a definition, the answer is a wire-format change, not a shared assembly.
