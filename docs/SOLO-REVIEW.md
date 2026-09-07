# Solo-combat repair and review

The September 2026 experiment repairs the existing solo slice under ADR 0027.
It does not implement Protector combat, the sentry encounter, or airlock
completion. Recruitment displays **Current prototype slice complete**.
Changes remain subject to the owner's hands-on judgment; automated input is
not a claim that the POC's manual-playthrough exit gate has passed.

## Play and reproduce

Run from the repository root. `dev.ps1` selects the pinned .NET SDK from the
documented local candidates for its process and restores the original
environment afterward. No editor plugin is needed for any command below.

```powershell
pwsh -NoProfile -File scripts/dev.ps1 build
pwsh -NoProfile -File scripts/dev.ps1 import
pwsh -NoProfile -File scripts/dev.ps1 run

# Open the real game at a paused, armed checkpoint; Space resumes.
pwsh -NoProfile -File scripts/dev.ps1 review -Mode live -Checkpoint armed -AutoQuitSeconds 120

# One frozen checkpoint, with state and metric diagnostics beside it.
pwsh -NoProfile -File scripts/dev.ps1 review -Mode capture -Checkpoint recoil -Distance 7.5 -Resolution 1280x720

# Full 1080p / 60 fps Godot MovieWriter sequence, including draw and holster.
pwsh -NoProfile -File scripts/dev.ps1 review -Mode record -Recoil restrained -Distance 7.5
pwsh -NoProfile -File scripts/dev.ps1 review -Mode record -Recoil strong -Distance 7.5

# Real Godot input routing: keyboard, contextual picking, buttons and navigation.
pwsh -NoProfile -File scripts/dev.ps1 review -Mode input -Sequence victory -Resolution 1280x720
pwsh -NoProfile -File scripts/dev.ps1 review -Mode input -Sequence defeat -Resolution 1280x720

# Separate 30-second wall-clock frame-pacing sample, without MovieWriter.
pwsh -NoProfile -File scripts/dev.ps1 review -Mode performance -Resolution 1920x1080
```

`-Sequence` accepts `victory` and `defeat`; `-Distance` accepts 7.5–20 m;
`-Resolution` accepts 1280x720 and 1920x1080. Recording always uses 1920x1080
and 60 fps. `-Recoil strong` is a review alternative; normal play defaults to
restrained recoil. Use `-Checkpoint all` for the full sequence, or a named
checkpoint: `ready`, `draw`, `armed`, `armed-walk`, `fire`, `recoil`,
`anticipation`, `contact`, `heal`, `interrupt`, `late-fire`, `holster`,
`victory`, `slice-complete`, `defeat`, or `retry`. Not every checkpoint belongs
to both sequences. `input` and `performance` use their complete profile with
the default `all` checkpoint.

Outputs stay ignored under `artifacts/solo-review/`. Profile folders include
mode, sequence, resolution, distance and recoil variant. `review.json` records
ticks, action instances/phases, sampled clips, grip proxies, weapon dimensions,
muzzle frame, projectile origin/position/destination/progress, lintel occlusion
and effect ages/delays. Failed geometric checks retain
their diagnostics and the named defect capture. Movies are
`solo-{sequence}-{recoil}-{distance}.ogv`. Optional MP4 previews made with an
ignored local FFmpeg tool are convenience copies, not game dependencies.

## What changed

- Basic attacks remember an explicitly selected target through ability and
  Field Aid. Move, Interact and Stop clear it. Repeated clicks cannot reset a
  running attack or bypass released-shot recovery. Paused replacement still
  uses one pending slot, with an observable waiting reason.
- Carbine timings remain 9/21 ticks. Suppression is 6/24, Enforcer 42/36, and
  draw/holster 54 ticks each. Costs and cooldowns apply only on release.
- The 0.82 m carbine keeps metric size under the scaled rig and uses its actual
  forward muzzle axis. Authored arm/handling poses, upper-body aim, built-in
  support-hand IK and event-driven recoil replace the old whole-body fire clip.
  The final draw stance matches the armed shoulder position through retry.
- Carbine shots and suppression fire use moving muzzle bolts with short trails.
  Impact flashes follow their visual arrival; tactical pause freezes travel.
  Shared projectile meshes/materials are instantiated invisibly during scene
  startup, and the four short audio cues are prepared before combat.
- Sampling restores the authored bone pose before applying aim and IK. This
  prevents offsets accumulating over repeated shots. Fixed-tick position
  interpolation and time-based turning follow actual travel direction.
- The Enforcer shows anticipation, contact and follow-through over its
  authoritative phases. Impacts, interruption and healing have concise visual
  and original synthesized audio cues. Ordinary hits do not select a full-body
  reaction clip.
- A compact anchored HUD exposes health, current/pending orders, cooldown
  seconds, charges, Stop, pause and retry. Combat entry frames the fight once;
  subsequent camera input stays in control. The door lintel fades when it
  obstructs the protagonist. Completed doors no longer intercept floor clicks.
  Unavailable NPCs remain opaque and completed interaction labels stay quiet.

## Verification boundaries

The pure-core suite checks command atomicity, duplicate-click cadence,
recovery preservation, queued revalidation, interrupt timing, healing resume,
pause replacement and retry. CLI and Godot headless scenarios prove rules and
engine/navigation integration.

The graphical `input` profile injects Godot `InputEvent` mouse/key events. It
checks their resulting human commands, exercises GUI buttons, and resumes
ordinary real-time gameplay briefly before returning to exact paused steps.
It is not OS-level mouse latency measurement or an owner-operated playthrough.

Capture/record profiles use ordinary typed commands and the same presentation
clock. Armed checkpoints enforce 0.82 m ±2% length, palm-to-grip proxy errors
at most 3 cm, and forward muzzle alignment on early and late firing samples.
Retry rejects stale effects. Motion recordings are necessary: early stills
alone did not expose accumulated torso offsets or the retry boundary defect.
Firing checkpoints also check muzzle launch and forward projectile travel;
the input profile checks the bolt across twelve paused render frames, stepped
travel, delayed impact, and cleanup on arrival.

MovieWriter uses fixed deltas and can encode slower than real time. The
`performance` profile instead measures `Stopwatch` intervals between actual
`FramePostDraw` signals for 30 seconds after a separately reported warmup. It
reports p50/p95/p99/max, slow-frame counts, simulation and presentation CPU
time, and command-handler-to-first-render latency. Engine startup, GPU time,
pipeline compilation counts, OS/compositor latency and external tool round
trips are distinct quantities. Pipeline mesh/surface/draw counter changes are
reported separately for the measured interval. See Godot's
[pipeline precompilation guidance](https://docs.godotengine.org/en/stable/tutorials/performance/pipeline_compilations.html)
for the startup preparation approach used by projectile effects.
The metrics describe this workstation, scene and run, not a universal FPS
guarantee. See `SOLO-REVIEW-RESULTS.md` for the checked experiment.
