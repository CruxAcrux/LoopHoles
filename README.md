# Loopholes

A non-violent mobile crime/hustle game — corporate-satire reskin of the DeKrim /
Torn City loop. Design doc: [`loopholes_project_brief.md`](loopholes_project_brief.md).
Balance source: `loopholes_economy_model(1).xlsx`.

Two independent .NET projects that talk **only over HTTP**:

```
test-dekrim/
├─ test_dekrim.csproj     Godot 4.7 C# client  (net8.0)
├─ scenes/  scripts/      the game
├─ tools/                 wire-contract checker
└─ backend/               ASP.NET Core 9 service — separate solution, separate build
```

Neither side references the other. `backend/` happens to live in this folder for
convenience, but it is excluded from the game build (`<Compile Remove="backend/**" />`)
and hidden from the engine (`backend/.gdignore`). See
[`backend/README.md`](backend/README.md) for the service itself.

## Running it

Two terminals:

```bash
# 1. the service
cd backend/src/Loopholes.Api && dotnet run        # http://localhost:5147

# 2. the game
godot --path .                                     # or open the project in the editor
```

First launch shows a connect screen with the server address prefilled. It is
editable and persisted to `user://session.cfg`, so pointing a build at a LAN
address or staging needs no rebuild.

```bash
dotnet test backend/Loopholes.sln                  # 104 tests
python3 tools/wire_contract_check.py               # needs the service running
```

## The split, and the one risk it creates

The client keeps its own copy of the wire models in `scripts/Net/ApiModels.cs`.
That is what "no shared code" costs: a property renamed on the server does not
break the build, it quietly deserializes to null.

`tools/wire_contract_check.py` closes that hole. It parses the client's declared
records, calls every endpoint on a running server, and walks the real JSON against
what the client expects — including through nested objects and lists. Run it after
any DTO change on either side. It currently passes on all eleven probed endpoints.

## Client architecture

| Path | Role |
|---|---|
| `scripts/Net/ApiModels.cs` | The wire contract, client side. Hand-written on purpose. |
| `scripts/Net/ApiJson.cs` | Source-generated serialization — reflection-based JSON fails under the AOT an iOS export needs. |
| `scripts/Net/LoopholesApi.cs` | Typed wrapper over every endpoint. The only thing that knows the server exists. |
| `scripts/Game/Session.cs` | Autoload: token persistence, last known state, clock skew. |
| `scripts/UI/*` | Screens, built in code. |

Transport is Godot's `HttpRequest` node rather than `System.Net.Http.HttpClient`,
because `await ToSignal(...)` resumes on the main thread — a caller can touch the
scene tree straight after awaiting, with no marshalling.

**The client evaluates no game rules.** It renders the server's `Unlocked` /
`LockedReason` rather than deciding what is affordable, shows the server's
trust-adjusted risk rather than recomputing the formula, and never predicts a
payout. The one thing it computes locally is countdown text between refreshes,
drawn against server time (`Session.ServerNow`) so a device-clock change does
nothing. Refusals come back as an `ApiException` with the server's stable code
(`insufficient_energy`, `under_investigation`, …) and surface in the toast strip.

## Status

**Working end to end** — verified against a live server: register, the four
screens (Jobs, Market, Travel, Profile), start/collect with sealed outcomes, market
trades with the fee both ways, trust-adjusted travel quotes, licence paths, freeze
paperwork/lawyer, seasonal boards, wardrobe.

**Not built yet on the client:** the guided §16 onboarding (the loop is playable,
but there is no scripted first-five-minutes sequence), push notifications, and any
art beyond the code-built UI. Phase-2 systems are not built on either side.
