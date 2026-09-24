# Asset brief — Escape cutter v1

Status: exterior production authorized by the owner-approved station escape
plan, 2026-09-12; final Godot visual acceptance remains pending.

| Field | Value |
| --- | --- |
| Asset ID | `ship.escape_cutter.v1` |
| Role | Station destination, authored boarding and short departure |
| Approved design | [Exterior turnaround](../concepts/station-escape-ship-combat-v1/escape-cutter-exterior-turnaround-v1.png) |
| Source | [Blender scene](../source/ship.escape_cutter.v1/model.blend) |
| Production record | [Record](../source/ship.escape_cutter.v1/production.md) |

Preserve the compact chunky industrial cutter: dark navy armored shell,
segmented warm-gray frame, angular cockpit, short integrated paired engine
pods, landing feet, sparse cyan signals, amber caution lamps, and rear entry.
The hull is approximately 11 m long and 6 m wide. An open rear ramp extends
the grounded footprint. No wings, guns, exposed interior rooms, labels, or
additional gameplay systems are required.

Author original dimensional geometry/materials in Blender 5.2 using metres,
ground contact at Z=0, +Y bow and +Z up. Export GLB with Godot +Y up and -Z bow;
the asset origin is centered on the hull at ground level. Keep transforms unit
scale and geometry under 40,000 triangles, 30 meshes, and ten materials.

Publish rigid `pivot.ramp` and `pivot.door`, with `socket.boarding.entry`,
`socket.boarding.interior`, and `socket.engine.left/right`. The ramp is open
by default; its exact local closure angle and socket coordinates belong to the
generated manifest. Engine cores remain separate emissive meshes. No armature,
authored animation, generated collision, or authoritative gameplay is included.

Inspect the exact exported GLB after fresh Blender import and in Godot's launch
bay. Confirm silhouette, rear access, landing contact, ramp closure, engine
readability, and camera-distance readability. The exterior authorizes neither
ship interiors nor ship-combat mechanics.
