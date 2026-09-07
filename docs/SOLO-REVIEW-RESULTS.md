# Solo-combat experiment results — 2026-09-06

The experiment is implemented in the owner's requested checkout,
`C:/Developpement/space-adventure`, on `codex/solo-combat-repair`. A fresh fetch
confirmed the base matches `origin/main` at `3ef703e`. The three other
worktrees were removed after checking their state;
122 original raw art files (about 2.6 GiB) were preserved in this checkout's
ignored cache. Branch history remains available.

## Result

The principal defects were concrete implementation problems: inherited rig
scale shrank the carbine, its physical muzzle axis disagreed with its marker,
attack replacement lost intent or altered cadence, and presentation was not
reliably synchronized with deterministic stepping. The repair addresses those
causes rather than adding more combat content.

The selected presentation uses restrained recoil. The stronger alternative was
recorded against the identical sequence and checked at release, recoil and
recovery. Its extra torso/barrel motion did not offer a clear readability gain
over the existing muzzle, impact, sound and threat cues. Both alternatives are
reproducible with `review -Recoil ...`; this is an agent judgment, not owner
acceptance of the final feel.

Motion and input checks caught additional defects during the experiment:
accumulating procedural spine offsets, a draw-to-armed support-hand reach
mismatch on retry, a frozen blend preventing a clear defeated pose, and open
door interaction volumes intercepting floor clicks. These were repaired and
their relevant checks rerun. The accepted late-fight pose remains stable, and
the retry check enforces the grip tolerance on the first armed frame.

## Checks run

| Check | Result |
|---|---|
| Canonical .NET build | Passed; existing NU1900 vulnerability-feed warnings remain |
| Pure-core tests | 38 passed, 0 failed |
| CLI `station-route` | Passed; 515 ticks, including automatic attack resume after aid |
| Godot headless victory and defeat/retry | Passed |
| Graphical input victory/recruitment | Passed at 1280x720 through contextual clicks, dialogue keys, ability/aid buttons and X |
| Graphical input defeat/retry | Passed; visible retry button is inside the viewport and starts attempt 2 |
| Real-time versus paused seek | Passed at the sampled armed state: same clip within one frame, muzzle position within 1 mm |
| Live review checkpoint | Passed; returns the real paused game to player control |
| Existing wall-cutaway lifecycle capture | Passed; viewport inspected |
| Camera/resolution coverage | 7.5, 14.5 and 20 m at 720p and 1080p; 1080p close view also recorded |
| Exact Blender GLB reimport | Passed; 1.82 m Vanguard, unchanged skin/locomotion metrics and repaired handling |
| Portable paths and whitespace | Passed |

Every recorded/captured checkpoint passed the 0.82 m ±2% weapon-length gate.
Fully armed checkpoints passed the 3 cm palm-proxy fit gate. Early, recoil and
late-firing checkpoints passed forward muzzle alignment. Retry retained no
effects from its previous attempt. Grip errors during the deliberately
one-handed portion of draw/holster are not treated as two-handed pose tests.

The two final Godot recordings each contain **775 frames at 60 fps** (12.917 s)
at 1920x1080. Frame sequences were inspected for draw/holster transfers,
repeated fire, recoil, contact and follow-through. Audio is present throughout
the expected cues; the decoded mono peak is about 0.214 full scale. Audio
timbre and overall combat feel still need the owner's listening/playtest.

Paused checkpoint sampling and the 60 Hz movie samples reach the same gameplay
ticks. Vanguard clip-time differences were zero; the largest muzzle-position
difference was 0.592 mm. The Enforcer's death sampling differed by one 60 Hz
frame at two checkpoints. This supports the intended review tolerance, not
bit-identical rendering across machines.

## Real-time performance

The final sample used the RTX 2070 SUPER with Godot 4.7.1 Forward+ at 1920x1080.
It measured 30.017 seconds of wall time and 900 simulation ticks after a
separately reported 2.005-second warmup. No MovieWriter or fixed render delta
was active during this sample.

| Measurement | Result |
|---|---:|
| Render intervals | 1,800 |
| Frame time p50 / p95 / p99 | 16.67 / 16.86 / 16.96 ms |
| Worst frame | 40.99 ms |
| Frames above 33.33 / 50 ms | 1 / 0 |
| Simulation advance CPU p95 | 0.015 ms |
| Presentation CPU p95 | 0.963 ms |
| Sampled commands / rejections | 9 / 0 |
| Command handler to first render, maximum | 16.56 ms |

One isolated hitch remains around the first shot (tick 149). Its cause was
not established by this sample; it should not be described as solved or as
persistent game lag. Startup-to-checkpoint timing was 0.683 seconds **inside
the game host**, excluding process/engine launch. OS input, compositor latency
and external tool round trips were not measured.

Movie encoding took about 36–37 seconds for the 12.917-second clips, mostly
encoding work. That slower tool workflow is separate from gameplay frame pacing.

## Local evidence and next gate

Ignored evidence lives under `artifacts/solo-review/`: the two OGV movies,
same-name MP4 previews, per-profile `review.json` files and PNGs,
`sampling-comparison.json`, and the performance profile's `performance.json`.
`draw-motion.png`, `holster-motion.png` and `recoil-comparison.png` retain the
named transition/comparison inspections. The earlier audit remains in
`AUDIT-2026-09-05.md`; reproduction commands are in `SOLO-REVIEW.md`.

The next gate is an owner-operated solo playthrough judging aim, weight,
interrupt readability and pause rhythm. After that, the roadmap's next useful
implementation is Protector's bounded kit and the sentry/main encounter.
Inventory, procedural content, ship systems and broader art production remain
deferred. No claim is made that the POC's five manual full-playthrough exit gate
has passed.

## Projectile follow-up — 2026-09-07

Carbine and suppression shots now launch bright cyan bolts from the actual
muzzle, with a short tapered trail. Travel lasts 0.14–0.24 seconds on the
presentation clock. The impact flash sits at the front of the target's torso
and appears on visual arrival; the suppression ground pulse uses the same
delay. Authoritative damage and interruption still resolve on release.

The 1080p Godot recording at the normal 14.5 m camera distance passed through
victory and recruitment (775 frames / 12.917 seconds). Recorded departure,
trail growth and arrival were inspected; that review caught and corrected an
impact flash initially hidden inside the torso. The graphical input profile
also passed actual-muzzle launch, twelve frozen render frames under tactical
pause, stepped travel, delayed impact and bolt cleanup. Build, Godot import,
the headless station-route scenario, portable paths and whitespace checks passed.

An initial performance run caught a 160.37 ms first-shot frame. Projectile
meshes/materials are now shared and instantiated invisibly during scene
startup, and the four synthesized audio cues are prepared before combat.
The follow-up 30-second 1080p sample recorded 1,799 render intervals: p95
16.83 ms, p99 16.92 ms, maximum 28.80 ms, and no intervals over 33.33 ms.
It observed zero mesh/draw pipeline compilations and two surface compilations
during the measured interval. These are measurements from this workstation,
not proof that every driver or cache state is free of first-use stalls.

The current local preview is `artifacts/solo-review/projectiles.mp4`;
`projectile-motion.png` contains the recorded arrival inspection. The normal
record profile and `performance-victory-1920x1080-14.5-restrained/performance.json`
contain the follow-up evidence; the performance table above records the
September 6 experiment before projectiles were added.

## PR 19 review follow-up

The X shortcut now respects the Stop button's availability, floating damage
numbers appear with projectile impact, and direct review arguments reject
non-finite camera distances before creating an output manifest.

The canonical build and both graphical input profiles passed. The checks cover
ignored X presses during dialogue, defeat and securing, valid Stop during
combat, and damage numbers hidden until arrival. Direct headless Godot runs
with `NaN`, `Infinity` and `-Infinity` each returned exit code 1 with the range
validation error and left all review manifests untouched.
The 1080p Godot recording passed through recruitment; its firing frames were
inspected to confirm the number appears with the impact flash.
