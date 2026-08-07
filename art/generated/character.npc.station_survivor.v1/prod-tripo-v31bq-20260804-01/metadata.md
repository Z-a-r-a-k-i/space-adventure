# Station survivor production run

Status: published, integrated, and visually approved in Godot on 2026-08-07

- Asset: `character.npc.station_survivor.v1`
- Run: `prod-tripo-v31bq-20260804-01`
- Provider: signed-in Tripo Studio, Sharing Only
- Selected task: `fa370b18-b604-417e-8fa2-c6349712708f`
- Input: `input/front-tpose.png`
- Generation: direct single-image HD Model, v3.1 Best Quality, Ultra,
  Triangle 2M, 4K PBR, AI Complete off, Generate in Parts off
- Selected source result: 1,958,419 triangles and 1,008,937 vertices
- Retopology: Smart Low-Poly v2, Quad, target 10,000
- Retopology result: 13,924 faces and 13,023 vertices in Tripo;
  25,758 triangulated faces in the downloaded GLB
- Export: Mixamo FBX preset with 4K material master

Mixamo used symmetry and Standard Skeleton (65). The accepted neutral rig and
the `Unarmed Idle`, `Talking - General Conversation`, and direct Standard Walk
baseline were downloaded with skin. Matching no-skin idle and talking exports
were retained only as diagnostics: their generic rest skeleton did not match
the accepted character, so the profile records the permitted matching-skin
exception.

Blender publishes one 2048 PBR texture set, five semantic materials, five
skinned mesh parts, 25,756 triangles, 64 bones, and at most four normalized
influences. Required actions are `anim.humanoid.dialogue_idle`,
`anim.humanoid.dialogue_speak`, and the phase-offset
`anim.humanoid.dialogue_listen`. The exact GLB passes fresh reimport and Godot
gallery validation.
