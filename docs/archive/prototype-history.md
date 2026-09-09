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
| 2026-09-09 | Agent environment/UI polish, working tree on route v13 | Build/import, 78 core tests, three CLI scenarios, four engine profiles, solo/party victory/defeat input, party HUD at 720p/1080p, field-manual input isolation, downed-crew bounds, and wall cutaway passed. Native arrivals/dialogue/manual and party camera/combat inspected; 7.5–20 m views checked. 1080p/RTX 2070 SUPER: 1,800 intervals/30 s, p95 17.39 ms, p99 18.43 ms, max 31.59 ms, none over 33.33 ms. Environment publication fresh-imported within budget; owner visual acceptance pending. |
| 2026-09-09 | Agent Field Ops UI, working tree on route v13; owner selected direction C | Build/import, 78 core tests, three CLI scenarios, four engine profiles, and eight solo/party victory/defeat input runs at 720p/1080p passed. Native shared-order planning, manual transitions and live combat inspected; dialogue, defeat/retry and order captures checked. Initial solo 720p camera-pan assertion failed once, then passed unchanged; cause unconfirmed. 1080p/RTX 2070 SUPER: 1,800 intervals/30 s, p95 17.49 ms, p99 18.49 ms, max 32.11 ms, none over 33.33 ms. Owner review of the implementation remains open. |
| 2026-09-09 | Agent overhead health, working tree on route v13 | Enemy HUD panel removed; living crew/enemy health and hostile timing moved into the world. Build, 78 core tests, four engine profiles, party victory/defeat input at 720p/1080p and solo victory/defeat at 720p passed. Native paused zoom, countdowns, live combat and victory inspected. Fixed a crowded-label case that hid enemy health; bars now take precedence over status text. |
| 2026-09-09 | Agent manual-facing removal, working tree on route v13 | Removed Face Direction controls, arrows, heading lock, and command; retained automatic turning and independent Barrier orientation. Build, 74 core tests, three CLI scenarios, four engine profiles, and eight solo/party victory/defeat input runs at 720p/1080p passed. Native HUD, inactive T shortcut, and updated field manual inspected. Adapter schema advanced to 9. |
| 2026-09-09 | Agent station surround, working tree on route v13 | Added recessed foundations, ventilation, coolant tanks, pipes, and distant fade around the playable station. Exact GLB fresh import, build, four engine profiles, wall cutaway, and solo/party victory input at 1080p passed; inspected 7.5/14.5/20 m, both pitch limits, alternate yaw, and native zoom. 1080p/RTX 2070 SUPER: 1,800 intervals/30 s, p95 17.36 ms, p99 18.43 ms, max 31.94 ms; 1,648 frames unfocused, so this is a rendering sample rather than a focused-play acceptance result. Owner visual review pending. |
| 2026-09-09 | Agent animation pacing, working tree on route v14 | Faster travel/playback and synchronized attack/effect phases; Enforcer Interrupt reaction window retained. Build, 74 core tests, three CLI scenarios, six engine profiles, four solo/party victory/defeat input profiles at 1080p, and party motion recording passed. Native draw/combat, movement, pause, falls, and defeat inspected; recording frames reviewed. Owner feel review remains open. |
| 2026-09-09 | Agent station presentation pass, route v15 | Build/import, 74 core tests, three CLI scenarios, four final engine profiles, eight solo/party victory/defeat input profiles at 720p/1080p, wall cutaway, and both final motion recordings passed. Native combat/dialogue/manual/camera/retry and 7.5–20 m camera-limit captures inspected; fixed armed-walk grip reach, paused hostile retry pose, Taunt recoil, and recording audio cleanup. Focused 1080p/RTX 2070 SUPER samples each covered 1,800 intervals/30 s: solo p95 17.50 ms, p99 18.67 ms, max 33.49 ms; party p95 17.53 ms, p99 18.79 ms, max 30.05 ms. No intervals over 50 ms; one solo interval over 33.33 ms. Existing caches, two-second warmup, local VSync override, and released desktop capture session; surface compilation deltas solo 1 / party 2. Audio timing/pause and PCM checks passed, but this agent session could not hear audio: actual listening and owner handling/visual acceptance remain open. |

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
