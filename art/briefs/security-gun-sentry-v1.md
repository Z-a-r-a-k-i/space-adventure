# Asset brief — Security gun sentry v1

Status: Phase 3 production base approved by the project owner in the combined
Godot hostile gallery on 2026-08-07

## Identity

| Field | Value |
|---|---|
| Asset ID | `machine.security.gun_sentry.v1` |
| Role | Stationary ranged security hostile |
| Height | 2.15 m target, ±0.03 m |
| Footprint | Maximum 1.0 × 1.0 m |
| Attack source | `integrated`; short central barrel |
| Muzzle socket | `socket.attack.muzzle.primary`, local `-Z` outward and `+Y` up |
| Reference | `art/reference-sheets/frontier-station-v1/poc-models/gun-sentry-turnaround-v1.png` |

## Visual contract

Preserve the approved square bolt-down floor base, thick armored pedestal,
large rectangular gun housing, central short barrel and recoil sleeve, broad
bevels, dark-navy shell, worn warm-gray armor, black joint gaps, and narrow red
sensor. The sentry must remain taller than the 1.90 m Security Enforcer and
read as a stationary integrated firearm at the 20 m tactical-camera limit.

Do not add legs, a tripod, wheels, tracks, hover effects, articulated arms,
missiles, shields, exposed cables, skeletal deformation, logos, text, faction
markings, or transformable parts.

## Geometry and hierarchy

- Author deterministically in Blender; Tripo and skeletal rigging are not used.
- Maximum 8,000 runtime triangles, eight meshes, and exactly three materials.
- Ground-center origin, unit scale, no shear, `+Y` up and local `-Z` forward in
  Godot.
- Publish the exact hierarchy
  `Base → Aim_Pivot → Gun_Housing/Threat_Sensor/Recoil → Barrel/socket.attack.muzzle.primary`.
- `Aim_Pivot` permits ±60 degrees yaw and -15/+25 degrees pitch around the
  head center. `Recoil` travels at most 0.08 m along local `+Z`.
- Use `mat.security_sentry.shell.dark`,
  `mat.security_sentry.armor.warm_gray`, and
  `mat.security_sentry.threat.red`.
- Collision, targeting, telegraph, attack timing, recoil timing, projectiles,
  damage, hit response, and shutdown remain Godot-owned.

## Phase boundary and approval

Phase 3 publishes the neutral rigid assembly and validated pivot interfaces.
It contains no authored combat animation. Phase 4 drives ready, tracking,
wind-up, recoil, recovery, hit response, and shutdown from authoritative
combat observations.

The base must pass Blender bounds, topology, material, hierarchy, axis, pivot,
socket, and fresh-GLB-reimport gates plus exact Godot review at 7.5 m, 14.5 m,
and 20 m. Live station-route activation remains blocked until Phase 4.
