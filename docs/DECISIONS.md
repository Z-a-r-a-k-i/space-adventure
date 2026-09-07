# Decisions worth retaining

This is a compact record of consequential choices and their reasons. Current
rules live in the linked guides and code; routine changes belong in Git/PRs.
IDs remain stable for asset records. Superseded detail is recoverable from Git
history before the documentation consolidation (`7172c1e`).

| ADR | Decision and reason |
| --- | --- |
| 0001 | Godot 4.7.1 .NET, Forward+, Windows first: one supported desktop baseline. |
| 0002 | C# with a pure `net8.0` core: fast rule tests and shared CLI/game behavior. Supersedes the removed GDScript spike. |
| 0003 | An authored 8–12 minute station POC before procedural runs: establish an enjoyable, reproducible loop first. |
| 0004 | Typed commands, atomic validation, stable IDs, explicit 30 Hz ticks: consistent human and automated control. |
| 0005 | Authored dialogue on the POC critical path: no runtime model dependency. Optional local experiments use supported Codex CLI access, never scraped sessions or distributed developer credentials. |
| 0006 | Blender editable sources → validated GLB → Godot review: replaceable providers and reproducible publication. Implemented through the current art pipeline. |
| 0007 | Separate branches/worktrees for concurrent implementation, isolated Godot state, one owner for shared contracts/assets. The current single-agent exception is 0027. |
| 0008 | Rule, CLI, engine, and graphical verification answer different questions; none substitutes for a required higher layer. |
| 0009 | Validated text gameplay content joined to authored Godot spatial scenes: rules remain usable outside the engine. Revisit authoring ergonomics only when they become a demonstrated bottleneck. |
| 0010 | One current action and one replaceable pending order; explicit-target repeating attacks with player-directed abilities. Keeps tactical control bounded; refined by 0027. |
| 0011 | Future dialogue profiles select model, reasoning, and Fast mode independently; coherence precedes latency. Settings remain outside gameplay state. |
| 0012 | Automatic pause on combat entry, otherwise manual combat control. Amended by 0026 to pause on defeat for retry; no auto-pause for ordinary threats/cooldowns. |
| 0013 | Future free-text dialogue is the player's exact utterance; neither rewriting it nor treating it as authoritative world state is allowed. |
| 0014 | Handheld, integrated, and body attacks share core authority. Animation and physical presentation cannot resolve hits or damage. |
| 0015 | One fixed runtime outfit per human; editable undersuit/armor pieces support future whole-outfit variants without an equipment system. Complete topology work before rigging. |
| 0016 | Owner-approved humanoid pipeline (2026-08-02, amended Aug 3–4): T-pose → Tripo Quad-10k → validated Mixamo → Blender repair. Prevent repeat rig/rest-pose failures. Matching-with-skin donor exceptions require recorded evidence; routine provider steps need no repeated confirmation. |
| 0021 | Owner-approved ship experiment (2026-07-29) follows station POC hardening. The separated overhead composition is a direction reference, not authorization for final assets or a larger ship system. |
| 0022 | Enforcer melee plus rigid sentry ranged pressure replaces the ram drone (2026-08-02; bases approved Aug 7): two distinct threats without bespoke non-humanoid locomotion. |
| 0023 | Static station production advanced to Phase 3 (2026-08-02): dimensionally authored Blender modules over independent Godot spatial wrappers. |
| 0024 | Solo fight before Protector recruitment (2026-08-04); Vanguard is fixed, Operator deferred. Reviewed production presentation covers the active route before combat; fight animation is finalized with gameplay timing. |
| 0025 | Two authoritative service-door gates in the five-area route (2026-08-04): navigation availability and approach-triggered completion stay observable. The original locked solo exit was advanced by 0026. |
| 0026 | Bounded solo encounter (2026-08-08): Vanguard/Enforcer, suppression, one-charge aid, defeat pause, isolated retry, victory exit, recruitment. Damage uses numbers/effects without hit-reaction clips. Timing/handling recipe superseded by 0027. |
| 0027 | Repair the solo slice before expanding (owner-approved 2026-09-06): preserve attack intent through abilities/aid, prevent click/replacement exploits, and allow authored handling plus procedural aim/IK/recoil. Use bounded graphical input/motion checks and separate real-time performance measurement. The owner explicitly chose the existing checkout for gameplay and art. |

See [Product](PRODUCT.md) for scope, [Architecture](ARCHITECTURE.md) for rules,
[Art pipeline](ART-PIPELINE.md) for production, and [Roadmap](ROADMAP.md) for
the active gate. The ship and dialogue specs remain under `future/`.
