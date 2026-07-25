# Architecture decision log

This log replaces the Godot 4.6 and GDScript decisions made by the superseded foundation spike.

## ADR 0001 — Godot 4.7.1 .NET desktop foundation

Status: accepted.

Use the official Godot `4.7.1.stable.mono` build, Forward+ rendering, and a Windows desktop target for the POC. Use the engine's supported default 3D physics configuration until a concrete requirement justifies changing it. Mobile, web, console, and multiplayer constraints do not guide the initial architecture.

The automation scripts resolve the console executable from `SPACE_ADVENTURE_GODOT`, an explicit argument, or a small documented candidate list. The verified local installation is under `C:\Program Files\Godot_v4.7.1-stable_mono_win64`.

## ADR 0002 — C# with a pure gameplay core

Status: accepted; supersedes the former GDScript decision.

Use C# for game-owned code. `SpaceAdventure.Core` targets `net8.0` and does not reference Godot. The Godot .NET project, simulation CLI, and tests reference the core. This gives gameplay rules compile-time refactoring, fast tests, a direct CLI path, and a clear boundary for parallel agents.

Godot addons may contain GDScript, but project gameplay does not grow parallel C# and GDScript implementations. Enable nullable reference types and built-in analyzers. Add third-party frameworks only for current evidence-backed needs.

## ADR 0003 — Authored demo before roguelite structure

Status: accepted.

The first playable POC is an 8–12 minute authored journey from a start to a destination. The working setting is a disabled frontier transfer station. Procedural levels, run generation, and metaprogression are later experiments.

This establishes a stable case for testing exploration, recruitment, dialogue, inventory use, active-pause combat, and completion before generating content at scale.

## ADR 0004 — Typed commands and an explicit simulation clock

Status: accepted.

Human input, automation, scenarios, and future replays dispatch the same typed C# commands. JSON DTOs exist only at external boundaries. `GameSession` owns a fixed 30 Hz gameplay clock. Tactical pause stops gameplay ticks without stopping UI, input, camera, observation, or command entry. Presentation never mutates authoritative state.

Every command is fully validated before mutation and produces a typed acknowledgement or stable rejection. Each party member has at most one pending primary action in the POC; a new accepted order replaces it.

## ADR 0005 — No runtime LLM dependency in the POC

Status: accepted.

The POC critical path uses authored dialogue. An optional local development provider may automate schema-constrained requests through the official Codex CLI authenticated with the developer's ChatGPT subscription. Manual inbox and recorded providers remain available. We do not scrape ChatGPT, automate its website, use unofficial session endpoints, or distribute the developer's credentials. All generated proposals remain outside authoritative state validation.

## ADR 0006 — Controlled GLB publication pipeline

Status: accepted in direction; implementation deferred to the art milestone.

Use Blender 5.2 LTS as the current editable-source and automation baseline, GLB as the published runtime model format, and standardized Blender plus Godot renders for review. Record provenance and tool versions, validate mechanically, and require structured visual review. Prominent assets also require human approval.

The official Blender MCP is a development control surface, not the pipeline's source of truth. Pipeline operations must ultimately be reproducible by versioned Blender scripts or explicit profiles.

## ADR 0007 — Worktrees for parallel implementation

Status: accepted.

Concurrent agents use separate branches and Git worktrees with explicit ownership. Worktrunk is the preferred convenience layer on Windows, but ordinary Git worktrees remain valid. Generated Godot state, user-data roots, running editors, logs, and Godot MCP ports are isolated per worktree.

Shared scenes, `project.godot`, command schemas, and binary assets have one integration owner. Task assignment never implies authorization to commit or push.

## ADR 0008 — Separate rule, engine, and visual verification

Status: accepted.

Use four complementary layers:

1. .NET unit tests for pure rules.
2. A simulation CLI for deterministic command scenarios.
3. Godot headless tests for engine integration.
4. Real graphical playtests and screenshots for interaction and visual quality.

Passing a lower layer does not replace a relevant higher-layer check. The optional Godot MCP accelerates graphical control and inspection but is not a test dependency.

## ADR 0009 — Text gameplay definitions and Godot spatial scenes

Status: provisional for the POC.

Store small gameplay definitions in versioned, validated text data that loads into core C# records. Use Godot scenes for level layout, navigation, collision, spawn markers, and presentation references. Connect them with stable IDs.

This keeps rule content usable by the CLI and friendly to diffs and agents while retaining Godot's strengths for spatial editing. Revisit after one full encounter is authored; if duplicated wiring or poor editor ergonomics dominate, add a dedicated authoring adapter rather than moving authority into scene nodes.

## ADR 0010 — Minimal POC action semantics

Status: accepted.

Each character owns a current action and at most one pending primary action. New accepted primary orders replace pending ones. The POC does not implement arbitrary queues, programmable AI, behavior scripting, or a timeline editor.

Basic attacks repeat against an explicitly assigned target while active abilities require direct orders. Playtesting may revise this if it fails to create the intended Aarklash-like control rhythm.

## ADR 0011 — Quality-first, selectable dialogue profiles

Status: accepted for the dialogue experiment.

The development dialogue panel selects model, reasoning effort, and Fast mode independently. The initial baseline is `gpt-5.6-sol`, medium reasoning, and Fast mode off. Profiles can change without a rebuild, apply only to new requests, and are recorded with every result. Coherence, factual consistency, relevance, and voice rank ahead of latency; performance and credit use remain measured constraints.

Product-specific settings live in the local provider and experiment configuration, never authoritative gameplay state or save data. The UI exposes only values actually available through the signed-in Codex installation and rejects unsupported combinations instead of silently substituting them.

## ADR 0012 — Combat pauses automatically only when combat starts

Status: accepted for the POC.

Entering combat automatically pauses the simulation once so the player can assess the situation and issue orders. After that, pause is manual. The POC does not auto-pause for low health, ability readiness, target loss, incapacitation, or telegraphed attacks.

## ADR 0013 — Free-text input is literal protagonist speech

Status: accepted for the dialogue experiment.

When the player types dialogue, the submitted text is the protagonist's exact spoken utterance. The game does not rewrite it into another line or silently change its meaning. Authored response suggestions provide a convenient canonical voice without limiting free-form input.

Typed text remains untrusted content. It cannot establish world facts, grant authority, or directly cause gameplay effects; NPCs may reject or challenge unsupported claims, and only validated deterministic transitions can change authoritative state.

## ADR 0014 — Attack rules are independent from weapon presentation

Status: accepted for combat and asset planning; offline source work follows
ADR 0016 and gameplay implementation remains in Phases 3 and 4.

Every production combatant declares at least one credible attack source before
its model, rig, or animation is approved. The presentation source is
`handheld`, `integrated`, or `body`; these are art and Godot profiles, not
different gameplay architectures.

The pure core owns stable attack identity, target validity, range, fixed-tick
timing, movement, resolution, damage, interruption, and repetition. Godot
consumes observations and events to attach weapons and play rigs, animations,
telegraphs, effects, and audio. Animation callbacks, mesh collisions, sockets,
and asset node paths never mutate authoritative gameplay.

Humanoid handheld weapons are separate presentation assets even when their POC
loadout is fixed. Integrated machine weapons may remain permanent parts of the
machine, and a body attacker needs no weapon when its striking surface and
motion are clear. These choices do not add weapon switching, ammunition,
reload, equipment slots, or generalized inventory. Detailed production gates
are defined in `ATTACK-PRESENTATION.md`; the approved POC presentation profiles
and phase-scoped target inventory are recorded in `POC-ASSET-ROSTER.md`.

## ADR 0015 — Fixed POC outfits retain modular editable sources

Status: accepted for the POC art pipeline; offline source work follows ADR
0016 and runtime integration remains in Phase 3.

Each human is presented and reviewed in one fixed complete outfit. The POC
does not equip separate chest, leg, boot, glove, or other armor items and does
not add armor compatibility rules or generalized equipment UI.

The Blender source retains a close-fitting technical undersuit and major armor,
footwear, glove, and accessory pieces as named objects on the normalized shared
humanoid skeleton when practical. The published GLB may keep multiple skinned
meshes, but Godot treats the approved combination as one outfit. Hidden
undersuit or body polygons may be omitted beneath rigid armor to prevent
clipping; a complete unclothed body is not a requirement. Handheld weapons
remain separate under ADR 0014.

Provider part generation, segmentation, part completion, retopology,
low-poly conversion, remeshing, and fitting occur before final rigging because
mesh reconstruction invalidates skeletons and weights. Blender owns the final
topology, fit, rig, weights, attachments, and export. Whole-outfit variants are
the preferred future extension. Individual equipment slots require a separate
product and architecture decision.

## ADR 0016 — Subscription credits prioritize complete asset quality

Status: accepted for offline POC art production.

Offline source production is authorized independently from gameplay-phase
activation when an approved roster asset has an approved visual reference, an
accepted production-ready art brief, clear ownership, and resolved licensing
and privacy prerequisites. This lane may generate and refine candidates,
complete parts and topology, texture, test provider rigs and animation donors,
finish the editable Blender source, publish review GLBs to staging, and inspect
them in the isolated Godot asset gallery. It does not authorize replacement of
live greybox content, gameplay wiring, ability-specific work, final
attack-timing synchronization, or other integration owned by a later roadmap
phase.

Offline work uses a dedicated art branch/worktree with explicit asset-ID and
path ownership, worktree-local Godot state, and a distinct MCP port. Staged
GLBs and the isolated review project remain under ignored `artifacts/` paths.
The art worker does not edit `game/project.godot`, live gameplay scenes,
shared content schemas, or imported-asset registries without a separately
assigned integration-owner task.

“Accepted” is a recorded approval, not an agent inference. The brief must name
its status as `accepted for offline source production`, approval date,
approver, authorized asset ID and operations, any blocked gameplay fields,
production owner, and dedicated worktree. The project owner or an art owner
explicitly delegated in a versioned task or decision may approve it. Authoring
or being assigned a brief does not grant approval authority, and a production
agent may not self-approve unless that delegation is explicit.

The Tripo Studio subscription is a prepaid production resource, not a scarce
per-request API budget. There is no general hard attempt cap and the agent does
not stop merely because the first viable candidate exists. It should select a
promising source early, then spend credits productively on the operations and
targeted alternatives that improve the finished asset: useful segmentation,
part completion, topology branches, texture repair, rig diagnostics, and
animation donors. Multiple candidates are created only when they answer a
named quality problem; candidate count is not a goal.

Every provider operation records its purpose, settings, task or version ID,
result, and keep/reject decision. A displayed operation cost may be captured
opportunistically when it is already visible, but cost is not an acceptance
criterion. Repeating an unchanged failed operation or preserving unreviewed
variants is not productive use. Work stops when the asset passes its applicable
reviews, the provider cannot improve the named defect, or Blender is the better
next tool—not simply to conserve unused monthly credits.

Amendment accepted 2026-07-24: production work does not track remaining account
balance or reconcile historical credit totals. Existing historical observations
may remain as provenance, but contradictory totals are removed or marked
unknown rather than investigated. The bounded provider bake-off retains its
separate fixed attempt caps and cost scorecard.

Credits consumed never waive the asset brief, attack-presentation contract,
provenance, Blender ownership, Godot review, or human approval requirements.
The bounded provider bake-off retains its scientific attempt and cleanup caps;
any later quality-oriented work is recorded as production work and excluded
from the bake-off scorecard. No production-lane work may begin for any of the
three bake-off asset IDs until the whole experiment's scorecards, baselines,
and result are finalized and frozen.

## ADR 0017 — Tripo raw exports use an ignored workstation cache

Status: accepted by the project owner on 2026-07-24.

Untouched Tripo exports are retained at
`art/generated/<asset-id>/<run-id>/raw/` on the dedicated art workstation, but
that directory is ignored by Git. Raw Tripo payloads are production inputs,
not game-runtime dependencies, and do not belong in the repository or its LFS
history by default. This decision does not set storage policy for another
provider.

Each run commits a `raw-export.manifest.json` containing the provider,
top-level repository run ID, and provider task ID plus one entry for every
cached payload: expected cache-relative path, original filename, format and
export settings, byte size,
`local_presence_checked_utc`, and `local_presence_status` (`present` or
`missing`). The manifest also records plan/privacy/licensing state and may
record a displayed operation cost when it was captured opportunistically. It
does not require an account balance or reconciled historical total. Prompts,
approved inputs, settings, selection decisions, processing records, editable
Blender sources, and publishable outputs remain versioned under the normal
pipeline rules.

Raw-dependent Blender tooling must refuse a missing cache file and flag an
unexpected byte size for manual review. It must not read an entire raw payload
solely to calculate or verify a content hash. Normal clones, CI, game builds,
and runtime publication do not fetch from Tripo and do not require the raw
cache. The existing art workstation is the authoritative local cache while art
production remains on that machine.

Tripo task IDs and Studio history are best-effort recovery aids, not a durable
archive. Tripo's terms disclaim an obligation to store outputs and allow
storage limits or deletion. Off-machine archival is deferred; when the owner
adds private online storage, the manifest records a non-secret archive locator
and verified restore date. Until then, the accepted risk is that simultaneous
loss of the workstation cache and provider copy makes the untouched raw export
unrecoverable, while retained Blender sources and game assets remain usable.

Do not delete a locally cached raw export merely because its manifest is
committed. Eviction requires either a verified off-machine copy or a separate
owner decision. Never commit provider session URLs, cookies, tokens, account
identifiers, or temporary download links.

## ADR 0018 — large 3D binaries use reference-based provenance

Status: accepted by the project owner on 2026-07-24.

The art pipeline no longer requires SHA-256 or another separately computed
content hash for raw provider exports, donor exports, `.blend` sources, GLB,
FBX, OBJ, provider archives, or other large 3D binary files. Repeatedly reading
those files consumed workstation time and CPU without improving the current
single-workstation recovery model.

Ignored provider payloads are referenced by stable asset ID, run ID, provider
task or job ID, original filename, expected cache-relative path, export format
and settings, and byte size. Presence and size are availability checks, not
content identity or integrity proof: same-size corruption or substitution can
pass silently. A missing file remains a blocker, and an unexpected size
requires manual confirmation against the provider task or trusted local copy.
When integrity-sensitive recovery is needed or corruption is suspected,
restore the exact task export or another trusted local/private-archive copy,
then perform the normal structural import and validation checks. Do not hash
the large file merely to compare it.

The repository `run_id` is the canonical `asset_revision` for every
provider-backed review. The provider `task_id` is recorded separately for
generation and recovery. Review directories and findings use the `run_id`, so
they join directly to the top-level `run_id` in `raw-export.manifest.json`;
tracked derivatives additionally record their repository path and introducing
Git commit.

Tracked binary sources and published outputs rely on normal Git/LFS revision
identity and repository history. The art pipeline does not add a second
file-content hash. Their production metadata records the repository-relative
path and introducing Git commit so a derived artifact is identifiable
independently of its provider task. Small prompt, settings, manifest, script,
reference-image, and review-evidence files may continue to use hashes where
they are cheap and already part of a deterministic contract.

New or updated `raw-export.manifest.json` files use schema version 2 and omit
binary `sha256` fields. Existing schema-version-1 hashes and hash-keyed review
directories remain valid historical records, but agents must not recompute
those hashes during inventory, migration, hydration, or routine validation.

## ADR 0019 — asset review screenshots are local and disposable

Status: accepted by the project owner on 2026-07-25.

Generated concept art, approved reference sheets, and exact image inputs used
to create an asset are durable production sources and remain versioned.
Screenshots of Tripo Studio, Blender, Godot, build output, gallery views,
turntables, contact sheets, and sampled animation frames are not production
sources and are not committed as routine evidence.

Asset appearance and animation are reviewed directly in the owning tool.
Temporary frozen captures are permitted only for a named diagnostic need and
belong under the ignored `artifacts/` tree. Agents open the minimum necessary
capture once and do not create a repository gallery or image-hash ledger.

Durable review evidence is compact text or JSON: provider task/version and
settings, structural metrics, named defects, keep/reject/revise decisions,
approval identity, and the tool/version used. A `visual-review.md` may cache a
live review conclusion but must not enumerate screenshots. This keeps the
repository focused on sources and outputs, reduces LFS and clone noise, and
avoids repeated high-resolution image ingestion while preserving auditable
decisions.
