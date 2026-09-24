# Ranged Enforcer production

Owner-authorized derivative on 2026-09-12. The
[brief](../../briefs/ranged-enforcer-character-v1.md) permits a separate rifle
while preserving the accepted security-frame identity.

Input: committed `art/source/character.enemy.security_enforcer.v1/security-enforcer-v1.blend`,
whose [production record](../character.enemy.security_enforcer.v1/production.md)
owns the original Tripo/Mixamo provider identity, rights and accepted skin.
The derivative preserves its complete 40-bone Mixamo skeleton, skin, materials
and actual idle/walk/down motion. No source replacement, provider upload,
regeneration, purchase or new external dependency occurred.

`ranged-enforcer-v1.blend` is the new editable source;
`game/Assets/Published/character.enemy.ranged_enforcer.v1.glb` is the publication.
It contains 24,580 triangles, three meshes/materials, 40 bones and four normalized
influences maximum. The original melee publication is unchanged.

The rifle is the existing separate `weapon.crew.vanguard_carbine.v1`.
Fitted armed idle/walk, draw, holster and attack poses expose
`socket.weapon.hand_primary` and `socket.weapon.holster_primary`, plus actual
palm offsets for both hands. The original melee contact marker/action are
excluded. The cloned base/down clips receive evaluated floor-contact correction;
core movement, attack timing, damage and projectile travel remain unchanged by art.

Rebuild using Blender 5.2:

```text
blender --background --factory-startup --python-exit-code 1 --python tools/blender/build_station_crew_expansion.py -- --asset ranged
blender --background --factory-startup --python-exit-code 1 --python tools/blender/verify_station_crew_expansion.py
```

Use `--publish-existing` to export the committed derivative source directly.
The builder uses the accepted Vanguard hand orientation only as an aim-frame
reference, never as a replacement skeleton. The verifier fresh-imports the
exact GLB and checks action/socket coverage, normalized skin, locomotion range,
loop endpoint, foot lift and bounds. Blender assembled idle, rifle aim and walk
were directly inspected. Godot muzzle, support-grip, ranged telegraph,
projectile interception, pause and retry remain in the integrated review.
