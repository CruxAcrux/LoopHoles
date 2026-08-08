# Loopholes — Project Brief

A non-violent mobile crime/hustle game, inspired by the old Swedish browser game
DeKrim and similar 2000s text-based crime RPGs (Torn City, Mob Wars, Mafia Wars).
Same greed-and-risk core loop as those games, reframed as white-collar / corporate
satire instead of violence: shell companies, ghost invoices, market manipulation,
audits — not guns or gore.

Tentative name: **Loopholes**

---

## 1. Platform & tech stack

- **Client**: Godot 4.x, using **C# scripting** (developer has a .NET background).
- **Target platforms**: Android and iOS.
  - Android: buildable on Windows/Mac/Linux, needs Android SDK + JDK 17.
  - iOS: requires a Mac to build/sign/submit (no Windows/Linux path), plus an
    Apple Developer account (~$99/yr).
- **Backend**: required and **must be server-authoritative**. The economy
  (market prices, job payouts, trust score, travel risk rolls) cannot live only
  on-device or it will get exploited the same way DeGKrim's price-refresh bug was.
  Developer is comfortable with either **.NET (ASP.NET Core)** or
  **Python (FastAPI/Flask)** for this — pick whichever is faster to iterate in;
  it's a separate codebase from the Godot client.
- **Developer**: solo.
- Known rough edges to watch for: no official ad-mediation SDK for Godot yet;
  in-app purchase tooling on Godot mobile has historically needed community
  plugins rather than official support.

---

## 2. Core theme & design pillars

- Non-violent — consequences are always financial/social/legal (fines, frozen
  access, reputation damage), never combat or physical harm.
- Tone: dry corporate-crime satire (shell companies, "consulting fees",
  ghost invoices, audits) rather than mafia/gangster tropes.
- Design goal stated by developer: the game should be **"not easy or hard, but
  require thinking"** — achieved through resource allocation (energy), timing
  (market movements), and real cost/benefit risk decisions, not through
  raw difficulty or punishment.
- Cosmetics/status must never affect gameplay power — kept strictly separate
  from anything stat-related, to preserve fairness (important since the game's
  whole theme is about trust and fairness of "the hustle").

---

## 3. Core loop

1. Spend energy on jobs (timed; energy regenerates over time).
2. Come back after the timer to collect payout.
3. Reinvest in the market, jobs, or (later) companies/ventures.
4. Build or spend trust score through partnerships.
5. Occasionally travel to meetings/events, with a small risk of a setback.
6. Progress through job tiers, trust tiers, and the seasonal leaderboard.

### Energy system
- Max energy: 10
- Regen: 1 point per 6 minutes (full refill in 60 minutes)

---

## 4. Currency

Two currencies, deliberately separated to avoid pay-to-win:

- **Cash (soft currency)** — earned only through play (jobs, market trades,
  later companies/ventures). Spent on nearly everything: market buys, bribes/
  fines, upkeep, restitution. This is the only currency that affects power.
- **Favor (premium currency)** — earned slowly through legit play or bought
  with real money. Spent **only** on cosmetics and time-skips (rushing a job
  timer). Never affects stats, market odds, or trust.

---

## 5. Jobs

### Tier 1 — Hustles (fast, low-risk, teach the loop)

| Job | Duration | Energy | Cost | Payout | Notes |
|---|---|---|---|---|---|
| Flip a Crate | 0.5 min | 1 | $0 | $5–8 | First-ever action, instant tap |
| Courier Run | 3 min | 1 | $0 | $10–15 | First real timer |
| Sidewalk Sale | 15 min | 2 | $0 | $25–35 | Sells a held market good, +bonus |
| Cook the Books | 10 min | 2 | $0 | $30–40 | Soft preview of audit risk (no penalty yet) |
| Ghost Invoice | 30 min | 2 | $50 | $90–120 | First spend-to-earn job |
| Overnight Storage Flip | 8 hr | 3 | $0 | $150–200 | Start before bed, collect in morning |
| Sneaker Line Save | 5 min | 1 | $0 | $15–20 | Timed around the Knockoff Sneakers drop event |
| Follower Farm Shift | 8 min | 1 | $0 | $18–25 | Foreshadows Bulk Influencer Followers good |
| Pawn the Goods | 20 min | 2 | $0 | $35–45 | Clears a random inventory item |
| Exam Key Delivery | 25 min | 2 | $20 | $55–70 | Foreshadows Leaked Exam Keys seasonality |
| Furniture Pickup | 45 min | 2 | $0 | 1x Vintage Furniture + $10 | Teaches delayed gratification (item appreciates if held) |
| Late Shift Cleanup | 1.5 hr | 3 | $0 | $110–140 | Second mid-length walk-away job |

### Tier 2 — Operations (require some trust/cash, multi-step)

| Job | Duration | Energy | Cost | Payout | Risk |
|---|---|---|---|---|---|
| Import Run | 2 hr | 4 | $200 | $350–450 | — |
| Warehouse Clearance | 4 hr | 5 | $400 | $700–900 | 8% chance: lose 20% of payout |
| Pop-up Storefront | 6 hr | 5 | $300 | $600–750 | — (pays out across duration) |
| Streaming Code Wholesale | 3 hr | 4 | $250 | $450–550 | — |
| Chip Smuggling Route | 2.5 hr | 4 | $300 | $400–700 | Payout varies with live market state |
| Crackdown Dodge | 3 hr | 4 | $250 | $500–650 | 10% chance: lose 30% of goods |
| Vintage Flip Circuit | 5 hr | 3 | $50 | $100 + $50/day held (cap 5 days = $350) | — |
| Paperwork Laundering | 4 hr | 3 | $200 | $150 + 25% off future Paperwork cost | — |

### Cut from MVP (phase 2)
- Tier 3 (Contracts, requires owning a company) and Tier 4 (Ventures, requires
  trust + partners) job tiers.
- Company ownership itself (with upkeep costs — pay or lose the company).

---

## 6. Market

8 goods at launch, each with a distinct, learnable price "personality":

| Good | Base Price | Volatility | Behavior |
|---|---|---|---|
| Bootleg Streaming Codes | $10 | Low (±5%/hr) | Steady — teaches basic buy-low/sell-high |
| Grey-Market Chips | $50 | High (±20%/hr) | Rumor-driven, rewards frequent check-ins |
| Knockoff Sneakers | $80 | Scheduled spike | +150% for 2hr on drop, then crashes -60% |
| Contraband Energy Drinks | $15 | Random event | +100% for 4hr during a crackdown event |
| Leaked Exam Keys | $40 | Seasonal | +80% for 3 days, ~2x/month |
| Bulk Influencer Followers | $5 | Fast bubble | +50%/-40% cycling roughly every 45 min |
| "Vintage" Furniture | $60 | Slow appreciating | +3%/day held, capped at +30% (day 10) |
| Shell Company Paperwork | $25 | Low (±2%) | Utility good for upkeep, not really traded |

Design rules:
- Prices move from **aggregate player activity**, not pure randomness (heavy
  buying pushes price up, heavy selling pushes it down).
- Scripted spike/crackdown events fire periodically, announced via notification.
- A small trading fee on every trade acts as a cash sink.

---

## 7. Trust score

- Range 0–100. Starting value: **50** (neutral).
- Fair split on a joint venture: **+5**
- Betrayal (embezzling a shared vault): **−30**
- Idle decay: **+1/day** pulling the score back toward 50 if inactive.
- Clean streak recovery (while in the Untrustworthy tier): **+8 per completed
  transparent deal** — faster than the normal +5 rate, an active way back up.
- Restitution: pay back **150%** of what was taken to instantly clear the flag.

### Tiers
| Tier | Range | Effect |
|---|---|---|
| Untrustworthy | 0–29 | Public red tag; blocked from ventures; higher travel risk |
| Unproven | 30–69 | No badge; normal access |
| Trusted | 70–89 | Green badge; lower travel risk; invited to bigger ventures |
| Elite | 90–100 | Gold badge; cosmetic perks; first access to limited deals |

Trust gates **access and risk modifiers only** — it never directly buffs stats,
to avoid feeling pay-to-win.

Design note: trust is **not** touched by the driver's license cheating system
(see below) — that stays a local/contained consequence, since it doesn't
involve another player.

---

## 8. Joint ventures / merger mechanic (phase 2, not MVP)

- Two+ players pool resources into a shared vault.
- At payout: either **split fairly** (both profit, trust +5 each) or one
  player **embezzles** (bigger solo payout, but −30 trust and a public
  "untrustworthy" tag once below 30).
- Later, established players can merge into full "conglomerates" sharing
  market bonuses — dissolving one requires unanimous buy-in, so a rogue member
  can hold the group hostage or drain shared resources before being voted out.

---

## 9. Travel & risk

Players travel to meetings/events (ventures, exclusive markets) by choosing a
transport method:

| Transport | Duration | Base Risk % |
|---|---|---|
| Car | 2 hr | 8% |
| Jet | 20 min | 15% |
| Freight | 6 hr | 3% |

- **Trust-adjusted risk formula**: `risk × (1 − (trust − 50) / 100)`
  — higher trust lowers risk, lower trust raises it.
- Consequence on a failed roll: 20% cargo fine, or a +50% time delay.
- Risk is rolled **once per trip**, not continuously.
- Higher trust = lower risk is the core design link between the trust system
  and this one — confirmed direction: **risk should be percentage-based, and
  the more honest/trustworthy the player is, the easier (lower-risk) it
  becomes.**

---

## 10. Driver's license

Speeds up travel; entirely optional, never a hard gate.

- **Legit path**: $500 cost + 3 hr "study" timer (passive, like an Overnight
  job, no payout) + exam with a 95% pass rate.
- **Cheat path**: skip study, take the exam immediately; 35% chance of being
  caught.
- **If caught**: $300 fine + 48 hr cooldown before retrying (legit or cheat).
- **Benefit once obtained**: −25% travel time when using Car.
- Does **not** affect the interpersonal trust score (kept as a local,
  self-contained consequence).
- Optional future depth: a small ongoing chance of a random audit that
  invalidates a cheated license, forcing a retake (echoes the trust system's
  "your past can catch up with you" idea, scoped to just this system).

---

## 11. Audit Freeze ("Under Investigation")

The top-tier consequence for major failures — replaces a literal "prison"
mechanic, themed to fit the corporate-satire tone.

| Tier | Trigger | Duration | Trust Penalty | Locked |
|---|---|---|---|---|
| Minor Freeze | Failed Tier 2 risk roll (Warehouse Clearance, Crackdown Dodge); first cheating offense | 4 hr | −10 | Jobs + travel locked; market trading still allowed |
| Major Freeze | Second+ cheating offense; catastrophic operation failure; failed sabotage (phase 2) | 24 hr | −25 | Full lockout: jobs, market, travel, ventures, license exam |

Still available during a freeze: viewing portfolio/inventory/trust/wardrobe,
visiting clanmates (once clans exist), Vintage Furniture still passively
appreciates, and one "keep busy" action (filing an appeal).

### Ways to reduce or clear it
| Tier | Method | Cost | Effect |
|---|---|---|---|
| Minor | File paperwork (self-service) | Free | −20 min per use, usable every 30 min |
| Minor | Hire a lawyer | $400 | Clears instantly |
| Major | File paperwork (self-service) | Free | −45 min per use, usable every 1 hr |
| Major | Hire a lawyer | $2,500 | Clears instantly |
| Major | Hire a lawyer (partial) | $1,200 | Cuts remaining time by 50% |

Methods can be combined (pay partial, then keep filing paperwork on the rest).

---

## 12. Inventory

- **Stacked quantities**, not item-by-item (e.g. "12x Grey-Market Chips").
- **Capped storage** — creates real hold-vs-sell decisions; a storage upgrade
  is a natural, non-power-creepy Cash/Favor sink.
- **Per-stack metadata where needed** — e.g. Vintage Furniture must track how
  long it's been held, since its value depends on patience.
- Kept separate from the Wardrobe (cosmetics) system — inventory is
  consumable/tradeable stock, wardrobe is permanent equipped items.
- Later: owning a warehouse-type company should expand inventory cap
  (phase 2, ties into the "empire score" idea).

---

## 13. Cosmetics (MVP scope — decided)

Ship a **small starter wardrobe**, not the full shop, at launch:
- 1 free starter outfit piece, equipped by default (wardrobe isn't empty on day one)
- 2–3 purchasable items priced in Favor (e.g. a car skin, shades, one HQ decor piece)
- Full rotating shop, seasonal items, and leaderboard-reward cosmetics are
  fast-follow content, not MVP.
- Cosmetics are strictly cosmetic — zero effect on gameplay stats, ever.

---

## 14. Leaderboard

- **Season-based**, resets every **4 weeks**. Only the leaderboard/season
  score resets — all actual account progress (cash, trust, inventory,
  cosmetics, license) is permanent and never wiped.
- Primary ranking: **Empire Score** = net worth (cash + inventory + company
  value later) with a trust-tier modifier (small bonus for Trusted/Elite,
  small penalty for Untrustworthy) — rewards playing well *and* honestly, not
  just raw volume.
- Secondary boards: **Richest** (pure cash) and **Most Trusted** (pure trust
  score) — alternate paths to the top.
- Trust badge displays directly next to each leaderboard rank, so the board
  has to be *read*, not just skimmed.
- Rewards are cosmetic only: Top 100 gets a season-exclusive cosmetic, Top 10
  gets a rarer item + a title for next season, #1 gets something unique.
- Ties broken by whoever reached that score first.

---

## 15. Stats / history screen

- **Lifetime stats** (permanent, never reset): total cash earned, jobs
  completed, best empire score / best season rank ever reached, times caught
  (total Audit Freezes).
- **Trust history log**: a scrollable, dated feed of trust-affecting events
  (fair splits, betrayals, restitution payments, clean-streak progress) with
  their score deltas — the "receipts" behind the current trust badge.
- **Status row**: current trust tier/badge, license status (owned / not yet /
  currently suspended with time remaining).

---

## 16. Onboarding (first 5 minutes)

1. **0–1 min**: drop the player straight into a Tier 1 hustle, one tap, instant
   payout. No menus yet.
2. **1–2 min**: second job introduces a short timer, first taste of the
   come-back loop.
3. **2–4 min**: one guided market trade — buy cheap, watch it move, sell for
   profit — teaches the core economic tension in one rep.
4. **4–5 min**: show the profile screen (cash, trust at neutral, a locked
   cosmetic goal), then release into the open loop with a notification promise.

No stat-sheet dumps or forced tutorial popups — trust score, later companies,
and ventures should surface naturally as the player grows into needing them.

---

## 17. Progression curve (target pacing)

- **Day 1**: several Tier 1 hustles done, first Tier 2 operation unlocked, first
  taste of the market. Trust still neutral.
- **Week 1**: first company unlocked (phase 2), first joint venture offered
  (phase 2, likely low-stakes/NPC-guided to teach the trust mechanic safely),
  first cosmetic earned or bought.
- **Month 1**: small real portfolio, meaningful trust score in either
  direction, likely one real betrayal experienced (given or received), bumping
  into higher-tier content.
- Pacing goal: never let more than a day or two pass without unlocking
  *something* new, even small.

---

## 18. Roadmap

### MVP (launch)
- Player account + server-side save
- Jobs: Tier 1 + Tier 2 only
- Market: 8 goods as specified
- Trust score (numeric + 4 tiers/badges) — foundation stat, ventures not live yet
- Cash currency + sinks; Favor currency for the starter wardrobe
- Inventory (capped, stacked, hold-time tracking)
- Driver's license (legit + cheat paths)
- Audit Freeze (minor + major, paperwork + lawyer)
- Leaderboard (seasonal, 3 sort modes)
- Stats/history screen
- Small starter wardrobe (4–6 cosmetic items)
- Onboarding flow as specified
- Push notifications (job complete, market spike, freeze ending)

### Phase 2 (post-launch)
- Companies (Tier 3/4 jobs, upkeep costs, empire score integration)
- Joint ventures / merger + betrayal mechanic
- Full cosmetics shop (rotating, seasonal, leaderboard rewards)
- Clans with distinct player roles (Scout/Recon, Saboteur, Fixer/Diplomat, Financier)
- Tactical sabotage system (rare, hard to pull off, high reward — requires
  intel-gathering setup, real chance of failure/exposure, meaningful payout)
- Clan vs. clan dynamics

---

## 19. Open items / not yet decided

- Exact backend framework choice (.NET vs Python) — developer preference, not
  yet locked.
- Final game name — "Loopholes" is the current favorite among several options
  considered (others included Off The Books, Grey Ledger, The Fixer, Front Co.,
  Petty Empire, Smokescreen).
- Full numeric balance pass — current numbers are a first draft; intended to be
  sanity-checked in a spreadsheet economy model before implementation
  (a working version of that model already exists as a companion Excel file:
  `loopholes_economy_model.xlsx`, with sheets for Global Settings, Tier 1/2
  Jobs, Market Goods, Trust System, Travel Risk, Driver's License, and Audit
  Freeze).
