# Prototype evidence

Historical outcomes only. [Roadmap](../ROADMAP.md) owns current gate status;
[Testing](../testing.md) owns the executable procedure. Detailed superseded
reports and protocols are retained in Git at `7172c1e` and in
[PR 19](https://github.com/Z-a-r-a-k-i/space-adventure/pull/19).

| Date | Evidence | Outcome |
| --- | --- | --- |
| 2026-07-24 | Owner physical-input run, `94f4e5b`, route v1 | Walking-skeleton gate passed: movement, pause/replacement, camera/cutaway, survivor, optional inspection, airlock completion. |
| 2026-08-03 | Owner Godot review | Vanguard base/locomotion accepted. Asset sources and measurements remain in its production record. |
| 2026-08-04 | Agent route-v5 graphical checkpoint | Structure/door integration passed; owner door/camera feel was still pending. |
| 2026-08-07 | Owner contextual/gallery reviews | Survivor/Protector and Enforcer/sentry production bases accepted. |
| 2026-09-06–07 | Agent solo repair, `c64d0b5` plus review fixes `88867f2`, route v7 | Build, 38 core tests, CLI victory (515 ticks), Godot victory/defeat, graphical input, motion, and exact-GLB review passed. Follow-up checks covered unavailable Stop, impact-number alignment, and rejection of non-finite review distance. No complete owner-operated solo gate recorded. |

The repair addressed lost attack intent, cadence/recovery exploits, weapon
scale/axis, pose accumulation, draw/retry fit, and obscured clicks. Restrained
recoil was selected after comparing the same motion sequence. Original art
exports were preserved when the owner consolidated to one checkout.

The latest recorded performance baseline is the September 7, 30-second sample:
Godot 4.7.1 Forward+, RTX 2070 SUPER, 1920×1080, 1,799 render intervals;
p95 **16.83 ms**, p99 **16.92 ms**, maximum **28.80 ms**, none above 33.33 ms.
An earlier first-shot stall motivated resource/audio preparation at startup.
This workstation sample does not establish behavior for every driver/cache.

Ignored evidence is under `artifacts/solo-review/`: profile `review.json`,
OGV recordings, `projectiles.mp4`, and
`performance-victory-1920x1080-14.5-restrained/performance.json`. Copies may be
absent on a fresh checkout; reproduce them with the testing guide.
