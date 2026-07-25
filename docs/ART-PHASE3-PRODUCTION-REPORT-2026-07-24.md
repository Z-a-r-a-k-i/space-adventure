# Phase 3 art-production report — 2026-07-24

Status: Vanguard and separate carbine remain available in the isolated asset
gallery, but their production-art implementation and live replacement are
postponed after a `revise` visual decision. Real idle, holstered locomotion,
dialogue-idle, and dialogue-listen donors pass Blender and Godot on the shared
skeleton. All currently authorized static Tripo crew/weapon runs are
reconciled and provisionally selected for later offline work.

Branch: `codex/phase3-vanguard-production-20260724`

Worktree: `C:\Developpement\space-adventure-art-production`

Baseline: `origin/main` at
`63ec38bfaea2018f62094c865cf65a8bfa65935f`

All selections in this report are provisional pending the project owner's
final visual review.

## Scope change

The Phase 2 human-playthrough exit gate passed on 2026-07-24, activating Phase
3. The owner then explicitly authorized production and provisional integration
of the approved roster assets on the dedicated art machine. This permits
Tripo candidate work through the signed-in Studio subscription, Blender-owned
source reconstruction, provisional GLB publication, and isolated Godot
asset-gallery integration with an immediate greybox fallback.

The authorization does not define gameplay attacks or abilities, approve
final visuals, replace actors in the live station route, or make provider
rigs/animations authoritative. No Tripo API, API key, purchase, upgrade, or
ability-specific content was used.

The owner subsequently narrowed the active checkpoint to Tripo-only work.
Existing Operator, pistol, Protector, and shotgun tasks were therefore audited
and preserved without starting Blender or Godot work. This is a scheduling
change only: the Phase 3 authorization remains active, while offline cleanup
and engine integration are explicitly deferred until the owner resumes them.

The owner then postponed the current Vanguard 3D implementation so the rest of
the Phase 3 functionality can be implemented and playtested first. The
approved 2D direction remains valid, but the generated character, carbine
assembly, rig, animations, final visual approval, and live replacement are not
accepted. The live game keeps its Vanguard greybox.

## Provider-operation provenance

| Asset | Provider task | Attempts | Displayed operation cost | Result |
|---|---|---:|---:|---|
| Vanguard c01 | `b18e331b-0699-453d-ad8e-a71ffa0e373c` | 1 | 0 | Rejected: duplicate rear face |
| Vanguard c02 | `c889d05a-90fe-4186-85eb-12d4eceafb35` | 1 | 55 | Provisionally selected |
| Vanguard humanoid donor rig | `c889d05a-90fe-4186-85eb-12d4eceafb35` | 1 | 20 | Four shared retarget proofs passed; nine diagnostic clips preserved |
| Vanguard carbine c01 | `01bb9aea-6b10-419d-bbeb-9648c9867a97` | 1 | 55 | Provisionally selected |
| Operator c01 | `dd18ffbe-b4bb-4035-9a82-d87da93d9d8a` | 1 | 55 | Provisionally selected |
| Operator pistol c01/c02 | `615ff81e-441e-4cea-b123-109cc93d65a3` | 2 | 55 | c01 rejected; zero-credit Free Retry c02 selected as offline reconstruction source |
| Protector c01 | `1fb3f3bb-1f0c-49cd-a9bd-87cf2a4abe75` | 1 | 55 | Provisionally selected |
| Protector shotgun c01 | `83d827a9-9149-4aea-8ef1-d52fb5c83a21` | 1 | 55 | Provisionally selected |
| Station Survivor c01 | `06cccd46-1120-4961-8b36-0fb5243e0bd1` | 1 | 60 | Rejected: duplicate rear face |
| Station Survivor c02 | `62e5884d-0f12-467c-b38b-77689ee7f984` | 1 | 45 | Provisionally selected |

Task IDs, operations, outcomes, and keep/reject decisions are the useful
provenance. Displayed per-operation costs above are retained as historical
observations only. Account balances and aggregate totals are intentionally not
reconciled and are not acceptance criteria. The existing selected sources
remain sufficient until a named defect justifies another targeted candidate.

## Station Survivor generation

Asset: `character.npc.station_survivor.v1`

Run:
`prod-tripo-v31bq-20260724-01`

The first multi-view candidate was rejected because its back-of-head geometry
contained a second complete face. Its untouched 46,151,260-byte GLB remains
in the ignored raw cache. The named construction/identity defect justified
one targeted retry using the approved front-right three-quarter reference as
a truthful single-image input.

Candidate 02 is provisionally selected. Front, three-quarter, and rear review
shows one correctly oriented face, a clean back of head, the approved older
civilian identity, side-shaved salt-and-pepper hair, navy maintenance
coverall, warm-gray work vest, restrained amber/cyan accents, complete hands
and feet, and no weapon, shield, or loose tool. Its untouched 55,332,160-byte
GLB is preserved in the ignored raw cache.

Final topology cleanup, material regions, shared-rig binding, animation
retargeting, GLB publication, and Godot integration are not claimed. They are
blocked behind the Vanguard-first shared-rig/retarget gate. The complete
inputs, prompts, settings, textual selection record, and schema-version-2 raw
manifest are versioned under the run.

## Tripo-only crew backlog audit

The signed-in Studio account already contained the remaining four static
Phase 3 tasks. Reopening the exact tasks, submitted-reference dialogs, and
current outputs confirmed that they are the intended approved-reference runs.
No duplicate generation was submitted and the audit spent zero credits.

### Operator and separate pistol

Operator task `dd18ffbe-b4bb-4035-9a82-d87da93d9d8a`, candidate 01, is
provisionally selected. It preserves the approved adult female identity, high
dark bun, asymmetric sensor, agile silhouette, unarmed A-pose, empty
right-side holster, complete rear surfaces, and restrained palette. Its
untouched 57,824,996-byte static GLB is present in the ignored run-local raw
cache. One candidate of the two-candidate ceiling was used; no named
Tripo-stage failure justifies a retry.

Pistol task `615ff81e-441e-4cea-b123-109cc93d65a3` used candidate 01 plus a
zero-credit Free Retry. Candidate 01 is rejected because its barrel terminates
in a solid cap. Candidate 02 is the stronger compact-pistol source, but its
bore is still shallow and capped; it is provisionally selected only for later
offline reconstruction, not accepted as generated. Both untouched exports
remain in the ignored raw cache at 59,054,116 and 56,269,344 bytes. The
two-candidate ceiling is exhausted.

### Protector and separate shotgun

Protector task `1fb3f3bb-1f0c-49cd-a9bd-87cf2a4abe75`, candidate 01, is
provisionally selected. It preserves the approved adult Black male identity,
close-cropped hair, beard, broad heavy-armored silhouette, unarmed and
shield-free A-pose, and complete rear surfaces. The explicit upper-back
shotgun rail is absent, but the usable rear armor makes this a bounded rigid
hardware repair for later offline work rather than a reason to risk identity
and anatomy with another paid candidate. Its untouched 57,751,640-byte GLB is
present in the ignored raw cache.

Shotgun task `83d827a9-9149-4aea-8ef1-d52fb5c83a21`, candidate 01, is
provisionally selected. It retains the approved short, broad, front-heavy
class, coherent stock and receiver, reachable pump/support region, complete
underside and opposite side, and a visibly open muzzle. It remains a separate
rigid asset with no fused character, hand, shield, shell, or magazine. Its
untouched 58,116,884-byte GLB is present in the ignored raw cache.

All four runs now contain the exact prompt, submitted settings, approved input
crops, Studio task IDs, live-review decisions, selection decisions, and
schema-version-2 raw manifests. Raw 3D files are
identified by task/run reference and byte size without content hashing. No
rigging, animation, remeshing, material reconstruction, socket authoring, GLB
publication, or Godot integration is claimed for these four assets.

## Vanguard

Asset: `character.crew.vanguard.v1`

Run:
`prod-tripo-v31bq-20260723-01`

The selected unarmed candidate retains the approved Vanguard identity,
palette, carry hardware, and separate-firearm boundary. Its untouched provider
export remains under the ignored run-local `raw/` cache and is referenced by
the committed schema-version-2 raw manifest. The rejected first candidate is
also preserved locally with its named duplicate-face failure.

Current editable source:
`art/source/character.crew.vanguard.v1/vanguard-character-v1.blend`
(13,171,789 bytes).

Current provisional GLB:
`game/Assets/Published/character.crew.vanguard.v1.glb`
(12,188,056 bytes).

Fresh Blender export/re-import measured:

- 1.817825 m grounded height;
- 28,000 triangles in two skinned mesh objects;
- two stable material regions;
- 59 published bones;
- four maximum skin influences and zero unweighted vertices;
- `socket.weapon.hand_primary` and
  `socket.weapon.holster_primary`; and
- all 16 shared presentation action names.

Twelve actions remain one-frame interface landmarks. The selected Tripo
candidate was auto-rigged with `v1.0 - Good for Humanoid` for 20 credits, then
the `idle`, `walk`, `run`, `shoot`, `hit_to_body_01`, `fall`, and `turn`
diagnostic presets were queued without an additional displayed charge.

The untouched 79,317,132-byte idle and 79,122,136-byte walk donor GLBs each
contain one skin, 41 donor joints, and one animation. The combined
79,801,332-byte diagnostic export preserves all seven presets, but Tripo names
them generically, so the individual exports are the authoritative clip
mappings. Blender maps 26 semantic joints onto `rig.crew.humanoid.v1`, strips
root translation and provider objects, and replaces only the requested action.
The resulting 12.3-second idle and 2.4-second holstered walk passed fresh GLB
re-import with one armature, 59 bones, 16 exact action names, and visually
coherent sampled poses. A separately exported 79,350,188-byte
`standing_relax` donor then passed the same path as the 17.6-second
`anim.humanoid.dialogue_idle` contract. The separately exported
79,139,372-byte `wait` donor also passed as the 6.0-second
`anim.humanoid.dialogue_listen` contract. This proves the shared retarget
workflow across both locomotion and unarmed dialogue presentation without
accepting Tripo's skeleton or weights as production authority.

## Separate Vanguard carbine

Asset: `weapon.crew.vanguard_carbine.v1`

Run:
`prod-tripo-v31bq-20260723-01`

The firearm remains a separate rigid asset. Blender reconstructed a closed
7,398-triangle mesh with an exact 0.82 × 0.13 × 0.27 m envelope, three
de-lit material regions, no armature or actions, and the required
`socket.grip.primary`, `socket.grip.support`, and
`socket.attack.muzzle.primary` frames.

Current editable source:
`art/source/weapon.crew.vanguard_carbine.v1/vanguard-carbine-v1.blend`
(272,005 bytes).

Current provisional GLB:
`game/Assets/Published/weapon.crew.vanguard_carbine.v1.glb`
(143,676 bytes).

## Complete-assembly decision

The exact published GLBs were assembled read-only in the provisional
`anim.humanoid.idle_armed` landmark:

- primary root/socket offset: 0 m;
- support-palm gap: 0.00454767 m;
- muzzle line: clear;
- rear-right holster overlap: 0 triangle pairs; and
- held hand/stock contact: 1,308 triangle-pair overlaps, retained for visual
  review.

The mechanical assembly review passes. The visual decision remains `revise`:
the generated glove topology reads visibly open below both vertical grips in
the close view. No firearm deformation, hidden compensating offset, gameplay
timing, or attack definition was invented to conceal that defect.

The reusable live-review conclusion is recorded in
`art/generated/character.crew.vanguard.v1/prod-tripo-v31bq-20260723-01/visual-review.md`.
Future agents reuse that conclusion instead of rebuilding or reopening a
screenshot set. Another direct review requires an asset change, a named new
question, or an explicit independent-review request.

## Godot gallery integration

`game/scenes/asset_gallery.tscn` imports the exact two provisional GLBs as
separate PackedScenes. Every production presentation has a sibling
`GreyboxFallback` node under the same stable asset slot. Reversing their
visibility restores brief-sized greybox geometry without changing an asset ID
or touching the live station route.

Godot 4.7.1 Mono imported the exact files successfully. Direct live gallery
review exercised the unarmed dialogue-listen contract at 7.5 m, 14.5 m, and
20 m.

Godot sanitizes dotted Blender action names to underscores; the gallery maps
the imported name back to the requested dotted presentation contract,
validates duration and seeks deterministically on the exact published GLB. The
6.0-second dialogue listen passed at a 3.0-second review point. Live review
showed the intended readability falloff. Vanguard's identity and palette
remain clear at 14.5 m; the 20 m view keeps the broad class silhouette but no
longer supports close material or hand-fit judgment.

## Verification

| Check | Result |
|---|---|
| Blender 5.2 Python compilation for ten retained pipeline scripts | Pass |
| Fresh Blender source export and exact GLB re-import | Pass |
| Tripo idle and walk donor structure plus Blender shared-rig retarget | Pass; each individual donor has one skin and one animation, with 41 donor joints mapped to 26 shared-rig joints |
| Tripo dialogue-idle donor retarget | Pass; 17.6-second `standing_relax` donor mapped to the shared dialogue-idle contract |
| Tripo dialogue-listen donor retarget | Pass; 6.0-second `wait` donor mapped to the shared dialogue-listen contract |
| Exact Vanguard/carbine mechanical assembly review | Pass, with visual `revise` finding |
| Direct isolated Godot import | Pass |
| Fresh-cache Godot reimport with embedded Basis Universal textures | Pass; no loose texture copies required |
| Direct live Godot gallery review at 7.5 m, 14.5 m, and 20 m | Pass |
| Direct real-Godot `--station-route-smoke` regression | Pass; completed at tick 97 |
| Pure C# build | Pass; 0 warnings, 0 errors |
| Pure C# tests | Pass; 14/14 |

The machine has .NET SDK 8.0.421 rather than the repository's pinned 8.0.319.
The C# checks used a temporary worktree-only `latestFeature` roll-forward and
`global.json` was restored immediately afterward. `pwsh` is not installed,
and Windows PowerShell 5 cannot use the wrapper's
`ProcessStartInfo.ArgumentList`; direct Godot commands were therefore used
with isolated user data for the engine checks.

## Preserved evidence and storage

- Exact approved inputs, prompts, submitted settings, task IDs, selection
  records, and schema-v2 raw manifests are versioned under each run.
- Untouched Tripo GLBs remain in each ignored run-local `raw/` directory.
- Large 3D binaries are identified through task/run references, byte size, and
  Git/LFS revision history; no new large-binary hashes were computed.
- Current machine-readable Blender reports are under each run's
  `derived/v2-production/` directory; the idle retarget proof
  is under the Vanguard run's `derived/v3-retarget-proof/`, and the current
  locomotion and dialogue proofs are under `derived/v4-locomotion-proof/`,
  `derived/v5-dialogue-idle-proof/`, and
  `derived/v6-dialogue-listen-proof/`.
- Superseded Vanguard/carbine Blender iterations, disposable visual review
  artifacts, and rejected experimental scripts remain local under ignored
  `artifacts/` paths rather than entering repository history.

## Remaining defects and gates

1. Revise and visually approve the Vanguard two-hand glove/grip closure.
2. Replace the one-frame draw, aim, recoil, recovery, and holster landmarks
   with reviewed presentation animation. Final attack timing still waits for
   gameplay definitions.
3. Resume offline work with Operator/pistol, then Protector/shotgun. The
   Tripo audit is complete; do not create a third pistol candidate. Shared
   skeleton binding still waits for the Vanguard hand-fit and deformation
   gates.
4. Process the provisionally selected station Survivor through Blender only
   after the earlier ordered Operator and Protector offline checkpoints.
5. Keep the live station route on its greybox actor until a separately reviewed
   gameplay integration change adopts the production presentation.

No final visual selection, gameplay attack binding, ability art, shield,
production machine, station structure, or airlock asset is claimed by this
report.
