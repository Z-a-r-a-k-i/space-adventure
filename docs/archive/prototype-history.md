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
| 2026-09-07 | Owner solo playtest after PR 19 | Fight accepted; weapon draw felt too slow. Party combat and a focused UI update approved. |
| 2026-09-07 | Agent local party preview, `codex/party-combat-ui`, route v8 | Build, 50 core tests, party CLI victory/defeat, five engine smokes, party input at 720p/1080p, and solo input regressions passed. Assembled Godot review covered grip/muzzle fit, 7.5–20 m views, down/retry, and a 26-second motion recording. Owner shotgun/party acceptance remains open. |
| 2026-09-08 | Agent playtest repairs, `codex/party-combat-ui`, route v9 | Build, 59 core tests, three CLI scenarios, four engine profiles, solo/party graphical input and death playback passed. Native placement/Hold Fire inspected. Clean 1080p run: 1,800 frames/30 s, p95 17.39 ms, p99 18.75 ms, max 29.86 ms; resetting the retained desktop capture session removed observed frame spikes. Owner acceptance remains open. |
| 2026-09-08 | Agent two-skill kits, larger forearm shield and facing, route v10 | 77 core tests, three CLI scenarios, four engine profiles, solo/party input and 720p/1080p HUD passed. Native shield deployment/steering inspected. Performance: 1,800 intervals/30 s, p95 17.35 ms, p99 18.09 ms, max 33.81 ms (one over 33 ms). Owner kit/visual acceptance remains open. |
| 2026-09-08 | Agent manual targeting and aid removal, route v11 | Build, 73 core tests, three CLI scenarios, four engine profiles, solo/party graphical input and 720p/1080p HUD passed. Both fights won without healing; native HUD inspected. Owner acceptance remains open. |
| 2026-09-08 | Agent stationary Barrier and crew fall repair, route v12 | Build, 76 core tests, three CLI scenarios, four engine profiles, solo/party input and 720p/1080p HUD passed. Exact GLBs passed floor-contact and resting-pose checks; recorded falls and native ground placement/deployment inspected. Movement/turning preserve the deployed shield. Owner acceptance remains open. |
| 2026-09-08 | Agent group controls and owner preview acceptance, route v13 | Build, 76 core tests, three CLI scenarios, four engine profiles, and solo/party input at 720p/1080p passed. Native drag selection, group Tab ability focus, and edge panning inspected. Accepted party assets included; import, humanoid gallery, and party victory/defeat input passed again after packaging. |

The solo reference is the September 7, 30-second sample:
Godot 4.7.1 Forward+, RTX 2070 SUPER, 1920×1080, 1,799 render intervals;
p95 **16.83 ms**, p99 **16.92 ms**, maximum **28.80 ms**, none above 33.33 ms.
An earlier first-shot stall motivated resource/audio preparation at startup.
This workstation sample does not establish behavior for every driver/cache.

On September 7–8, the owner's dummy-display/RustDesk setup reproduced
12–13 FPS even in a one-cube project with no game assets or scripts. The party
baseline had 380 intervals in 30 seconds, p95 **80.27 ms**, despite about
1.67 ms GPU time. With the local display settings in [Testing](../testing.md),
the same Forward+/Vulkan party scene at 1080p produced 1,800 intervals in
30 seconds: p95 **17.28 ms**, p99 **18.48 ms**, maximum **29.62 ms**, none above
33.33 ms. Desktop capture and injected Space input showed visible live combat.
NVIDIA's native presentation alternative measured 60 FPS but showed a blank
desktop capture and was rejected. This isolates a presentation-path interaction;
the exact driver/display cause and RustDesk client latency remain unmeasured.
Probe files/logs: `artifacts/fps-probe/`.

Ignored evidence is under `artifacts/solo-review/` and `artifacts/party-review/`:
profile `review.json`, recordings, `party-preview.mp4`, and `performance.json`.
Copies may be absent on a fresh checkout; reproduce them with the testing guide.
