# Vanguard carbine — solo repair record

Asset: `weapon.crew.vanguard_carbine.v1`. The approved brief and reference
provenance remain `art/briefs/vanguard-carbine-v1.md` and its linked turnaround.
The dimensionally authored source and publication are
`vanguard-carbine-v1.blend` and the same-ID GLB under `game/Assets/Published/`.

The 2026-09-06 experiment under ADR 0027 preserves geometry, materials and the
0.82 × 0.13 × 0.27 m envelope. Inspection found that the published physical
muzzle originally pointed toward +Z while its declared frame used -Z.
`repair_solo_presentation.py` rotates the rigid mesh and marker positions by
180 degrees around authored up, retaining the grip origin and dimensions.
Blender +Y now exports as Godot -Z. The marker's local -Z points out through
the actual muzzle. `solo_axis_revision=1` makes the operation idempotent;
`build_vanguard_carbine_v1.py` applies it during fresh production as well.

Godot attaches the weapon with an orthonormal world transform, avoiding the
Vanguard armature's inherited 0.01906913 scale. The reviewed assembled length
is 0.82 m (±2% enforced at every checkpoint). Primary/support grip and actual
muzzle frames remain presentation-only; they never resolve range or damage.
No new provider source, material family, weapon mechanic or art dependency was
introduced. Contextual checks are recorded in `docs/SOLO-REVIEW-RESULTS.md`.
