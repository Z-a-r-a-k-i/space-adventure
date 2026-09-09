# Roadmap

## Current state

Phase 4 is active on `station-route-v15` (content schema 9). The owner accepted
the solo fight and requested faster weapon draw. The local preview now joins
recruitment to Vanguard/Protector versus an Enforcer and stationary sentry,
with independent orders, two skills per crew member,
party retry, and a compact tactical HUD. [Product](PRODUCT.md) owns the kits.
Arena victory ends this slice; final airlock completion remains unavailable.

**Next gate: owner review of the completed station polish pass.** The party
combat slice merged in PR 20 after the owner's 2026-09-08 preview acceptance;
the environment, Field Ops, and faster handling baseline merged in PR 21.
The follow-up adds zone identity, threat/contact cues, contextual skill previews,
staged guidance, and session sound controls. Agent verification remains separate
from owner handling and listening acceptance.
The fitted Protector and shotgun are included
under `game/Assets/Published/`; running the game needs no local art staging.
Rebuild instructions live with the [party presentation tool](../tools/blender/README.md).
The workstation's display workaround is in [Testing](testing.md); the measured
frame-pacing result is in [history](archive/prototype-history.md).

## Remaining milestones

| Phase | Work | Exit |
| --- | --- | --- |
| 4 — Party combat | Review the environment, Field Ops HUD, overhead health, and faster pacing follow-up. | Active pause helps coordinate both characters; victory, defeat, retry, and graphical checks pass; owner accepts the polished slice. |
| 5 — Complete station POC | Join both fights, recruitment, dialogue consequence, inspection, opening final airlock, and completion summary into the authored 8–12 minute flow. Add comprehension polish only. | Five consecutive blocker-free manual runs and the relevant automated checks pass. |
| 6 — Hardening | Reproduce an accepted asset and a small authored encounter from a clean checkout; record useful startup/frame-time/memory/tooling baselines. | Another contributor can author and verify them using the documented workflow. |
| 7 — Ship experiment | One separately gated escape-cutter battle under the approved [spec](future/ship-combat-poc.md). Prove deterministic greybox before final art. | Rule, CLI, Godot, graphical, and owner-operated checks pass; pause-based crew/power/target decisions are useful. |

Phases 0–3 established the C# foundation, walking skeleton, and production
station/character bases. Their completion evidence belongs in history, not
the current work list.

## Scope discipline

Each change should test the active milestone's playable outcome. Approved
offline assets may be prepared under the [art pipeline](ART-PIPELINE.md), but
that does not activate a later gameplay phase or authorize live replacement.
Animation and effects are finalized alongside authoritative combat timings.

Operator, wall-utility integration, broader inventory/progression, procedural
runs, generated dialogue, deeper ship systems, and boarding remain deferred.
The owner may reprioritize explicitly; record consequential changes in
[DECISIONS.md](DECISIONS.md).
