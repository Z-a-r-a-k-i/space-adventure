# Security Enforcer production run

Status: Phase 3 production base approved in the combined Godot gallery on 2026-08-07

- Asset: `character.enemy.security_enforcer.v1`
- Run: `prod-tripo-v31bq-20260807-01`
- Provider: signed-in Tripo Studio, Sharing Only
- Selected task: `c48a7f4c-7a43-42c6-ac08-678b4c297821`
- Input: `input/front-tpose.png`
- Generation: direct single-image HD Model, v3.1 Best Quality, Ultra,
  Triangle 2M, 4K PBR, AI Complete off, Generate in Parts off, 8K off
- Source result: 1,949,121 faces and 1,001,563 vertices
- Retopology: Smart Low-Poly v2, Quad, target 10,000; result 13,438
  faces and 12,322 vertices
- Retopology inspection: 24,586 triangulated faces, one mesh, one material,
  grounded source bounds, and an intact strict T-pose in Blender 5.2
- Export: geometry-only Mixamo FBX preset with 4K material master plus an
  untouched retopologized GLB
- Mixamo: symmetry, Standard Skeleton (65), front-facing orientation, neutral
  FBX Binary with skin, 30 fps, and no keyframe reduction
- Baseline: untouched with-skin Standard Walk, In Place, Overdrive 50,
  Character Arm-Space 50; direct Godot test travelled 6.01 m with 0.0168 m
  planted-foot range and alternating 0.0527/0.0395 m foot lift
- Donor exception: both no-skin exports changed the accepted rest skeleton.
  Publication therefore uses matching with-skin donors and retains the
  rejected no-skin files only in the ignored diagnostic cache.

The input is the exact strict-front crop from the owner-approved Enforcer
turnaround; no new identity image was generated. Untouched Tripo and Mixamo
exports remain in the ignored run-local `raw/` cache and are enumerated by the
tracked manifest after acceptance.
