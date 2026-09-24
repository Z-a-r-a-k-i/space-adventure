# Asset brief — Operator pistol v1

Status: owner authorized the retained separate pistol for the third Medic on
2026-09-12. Exact live grip and handling remain in the station review gate.

| Field | Contract |
| --- | --- |
| Asset ID | `weapon.crew.operator_pistol.v1` |
| Compatible character | `character.crew.operator.v1` |
| Attack source | Separate one-handed firearm; content owns range, damage and timing |
| Approved reference | `art/reference-sheets/frontier-station-v1/poc-models/operator-pistol-turnaround-v1.png` |
| Source and reproduction | [Production record](../source/weapon.crew.operator_pistol.v1/production.md) |

Preserve the short chunky barrel, squared housing, open muzzle, comfortable
primary grip, protected trigger, charcoal mechanism, warm-gray plates, tiny
cyan accents and restrained wear. No stock, support grip, suppressor, scope,
loose magazine, character mesh or fused hand.

Reuse the retained Blender reconstruction of candidate 02. Its open bore and
rigid logical parts repair the raw Tripo candidate's shallow muzzle. Preserve
the raw candidate and original run record; do not create a third generation.
The existing Tripo task is `615ff81e-441e-4cea-b123-109cc93d65a3`.

Root coincides with `socket.grip.primary`; expose `socket.attack.muzzle.primary`
and no `socket.grip.support`. Author in meters with Blender +Z up/+Y forward;
export +Y up/-Z forward. Muzzle marker orientation must preserve that runtime
-Z outward convention. The right-thigh attachment points the holstered barrel
down; one-handed draw and aim must remain clear of the left hand and outfit.

Budget: 2,500–6,000 triangles, hard limit 9,000; ten meshes, four material slots,
one 2048 texture set maximum. Stable materials are
`mat.operator_pistol.mechanism.dark`, `mat.operator_pistol.armor.warm_gray`,
and `mat.operator_pistol.accent.cyan`. No skeleton, embedded animation,
collision or projectile rules.

Original bounded production authorization: 2026-07-24, project owner. Current
source reuse requires no provider upload, API key, purchase or regeneration.
Inspect the exact Operator assembly and muzzle at 7.5, 14.5 and 20 m under the
[art pipeline](../../docs/ART-PIPELINE.md). Final acceptance includes owner
visual and full-speed handling review.
