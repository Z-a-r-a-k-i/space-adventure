# Roadmap

## Current state

Phase 4 is active on `station-route-v7` (content schema 4). The playable route
reaches survivor choice, entry door, Vanguard versus one Security Enforcer,
victory-gated exit, and Protector recruitment. Recruitment shows **Current
prototype slice complete**; the scenario remains in progress. Protector
combat, the sentry encounter, and final airlock completion are unavailable.

The solo repair and agent rule/engine/input/motion checks are complete.
**Next gate: owner hands-on acceptance of the solo fight**, especially weapon
handling, interrupt readability, camera/input feel, and pause rhythm. Use the
[manual protocol](testing.md#manual-playtest) and fix named blockers before
expanding combat. Evidence is summarized in [history](archive/prototype-history.md).

## Remaining milestones

| Phase | Work | Exit |
| --- | --- | --- |
| 4 — Party combat | After solo acceptance, finalize Protector's shotgun/Guard Ally timing and presentation, then add the rigid sentry and main two-character encounter using existing command rules. | Active pause materially helps coordinate both characters; victory, defeat, retry, and graphical checks pass. |
| 5 — Complete station POC | Join both fights, recruitment, dialogue consequence, inspection, healing, opening final airlock, and completion summary into the authored 8–12 minute flow. Add comprehension polish only. | Five consecutive blocker-free manual runs and the relevant automated checks pass. |
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
