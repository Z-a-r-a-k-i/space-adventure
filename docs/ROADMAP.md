# Roadmap

## Current state

**Phase 5 — The station escape slice is implemented; owner acceptance remains open.**
The owner approved the three-person crew and station escape expansion on
2026-09-12. The current revision is `station-route-v18`, with content and
automation schema 11.
The accepted solo/Protector baseline remains the opening; Operator joins as
Medic before four new connected fights and a short cutter departure.
[Product](PRODUCT.md) owns the route and kits. The [tactical layout](station-layout.md)
adds staggered approaches and distinct movement loops to all six fights.
The [shared crew vision rules](ARCHITECTURE.md#shared-crew-vision) add enemy
discovery during exploration without changing encounter entry conditions.

**Next gate: owner acceptance and the remaining station hardening checks.**
Agent build, rule, CLI, headless and graphical checks are recorded in
[prototype evidence](archive/prototype-history.md). Handling and listening
acceptance remain open. Agent verification does not satisfy the five
consecutive blocker-free owner-operated full-route runs. Do not infer
acceptance from the earlier solo/party reviews.
Shared vision now has its own rule, actual-layout, and graphical results in
the evidence record; the owner gate remains separate.

The prior party slice and station/Field Ops polish established the baseline.
Its completion evidence belongs in Git/PR history. Presentation rebuild
instructions live with the [Blender tools](../tools/blender/README.md);
[Testing](testing.md) owns verification and the local display workaround.

## Remaining milestones

| Phase | Work | Exit |
| --- | --- | --- |
| 5 — Complete and harden station POC | Recruit Medic, complete six fights, recover between victories, retry each encounter independently, and board/depart in the cutter. Finish three-person controls, production presentation, and station hardening. | Relevant automated and graphical checks pass; owner accepts visuals, handling, and sound and completes five consecutive blocker-free full-route runs. Another contributor can reproduce an accepted asset and author/verify a small encounter from a clean checkout; useful startup/frame-time/memory/tooling baselines are recorded. |
| 6 — Ship combat | One escape-cutter battle with Vanguard, Protector, and Medic under the approved [spec](future/ship-combat-poc.md). Prove deterministic greybox before ship interiors, enemy-ship art, or production combat UI. | Rule, CLI, Godot, graphical, and owner-operated checks pass; pause-based crew/power/target decisions are useful. |

The former standalone hardening phase is part of Phase 5's completion gate;
ship combat is the next gameplay milestone. Phases 0–4 established the C#
foundation, production station/character bases, solo combat, and party combat.

## Scope discipline

Each change should test the active milestone's playable outcome. Approved
asset production follows the [art pipeline](ART-PIPELINE.md). Operator/pistol,
the rifle-equipped Enforcer derivative, station extensions, and cutter exterior
are authorized for this route; unrelated exploratory concepts are not.
Animation and effects are finalized with authoritative combat timings.

Wall-utility integration, broader inventory/progression, procedural runs,
generated dialogue, ship interiors/combat systems, and generalized boarding
remain deferred. The cutter's authored boarding/departure closes the station
route. Record consequential scope changes in [DECISIONS.md](DECISIONS.md).
