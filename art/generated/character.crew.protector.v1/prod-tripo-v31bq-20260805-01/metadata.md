# Protector production run

Status: published, integrated, and visually approved in Godot on 2026-08-07

- Asset: `character.crew.protector.v1`
- Run: `prod-tripo-v31bq-20260805-01`
- Provider: signed-in Tripo Studio, Sharing Only
- Selected task: `c9deee40-b461-45e1-840d-c0ca66cac4c3`
- Input: `input/front-tpose.png`
- Generation: direct single-image HD Model, v3.1 Best Quality, Ultra,
  Triangle 2M, 4K PBR, AI Complete off, Generate in Parts off, 8K off
- Source result: 1,928,840 faces and 991,660 vertices
- Retopology: Smart Low-Poly v2, Quad, target 10,000
- Retopology result: 12,011 faces and 10,990 vertices
- Export: Mixamo FBX preset with 4K material master

Mixamo auto-rigging succeeded after the joint markers were corrected, with
symmetry enabled. Although the UI offered Standard Skeleton (65), this
character's downloaded FBX contained 33 bones; Blender removes only
`HeadTop_End` and publishes the valid 32-bone hierarchy. The no-skin Standard
Walk and Unarmed Idle exports did not preserve the accepted rest pose, so the
profile records the permitted matching with-skin donor exception.

Blender publishes one 2048 PBR texture set, two materials, two mesh parts,
21,962 triangles, 32 bones, and four normalized influences maximum. The in-place
walk has 0.05587 m vertical hip motion and 0.15620/0.14666 m left/right foot
lift. Both required weapon sockets are present; exact shotgun fit remains
pending the separate weapon gate.
