# Tripo run metadata

Status: candidate 01 passed the Blender and isolated Godot gates; raw export preserved; final owner visual review pending

## Run

- Asset ID: `prop.station.wall_utility.v1`
- Run ID:
  `prop.station.wall_utility.v1__tripo__v3.1-best-quality__2026-07-24__01`
- Lane: `bakeoff`
- Run-ID note: this task predates commit `71ea25e`, which introduced the
  explicit `__bakeoff__` lane token. The original directory and run ID remain
  unchanged as immutable provenance; new bake-off runs use the current format.
- UTC date: 2026-07-24
- Assigned owner: primary SpaceAdventure art-pipeline agent
- Review-staging owner: primary SpaceAdventure art-pipeline agent
- Dedicated worktree:
  `C:\Developpement\space-adventure-art-production`
- Owned tracked paths: this run directory,
  `art/source/prop.station.wall_utility.v1/`,
  `tools/art/prepare_wall_utility_bakeoff_inputs.ps1`, and
  `tools/blender/process_wall_utility_candidate.py`
- Local ignored review staging: `artifacts/reviews/` and
  `artifacts/godot-asset-gallery/space-adventure-art-production/`
- Godot MCP port: not recorded in the original preflight; legacy gap
- Candidate class: static wall-mounted prop
- Brief: `art/briefs/station-wall-utility-v1.md`
- Authorization: Phase 2 bounded generator bake-off
- Candidate cap: two; retry only for a named identity or construction defect

## Live Studio settings

- Product: signed-in Tripo Studio web application
- Plan displayed: Max
- Privacy: `Sharing Only`
- Model: `v3.1 - Best Quality`
- Mode: `HD Model` / multi-view image-to-3D
- Generate in Parts: off
- 8K Texture trial: off
- Displayed cost at submission: 55 credits
- Visible balance before submission: 24,760 credits
- Visible balance after submission: 24,705 credits
- API use: none
- Purchase or upgrade: none
- Final settings evidence:
  `evidence/settings/tripo-preflight-front-right-v3.1-55credits.png`

## Provider rights, privacy, and retention record

Official provider pages were reviewed on 2026-07-24:

- [Tripo pricing](https://www.tripo3d.ai/pricing) lists the Max plan with
  private models and commercial use.
- [Tripo Terms of Service](https://www.tripo3d.ai/terms), last updated
  2025-07-11, section 5.2.2 gives paid users broad rights over their inputs
  and outputs and states that the company will not use those inputs or outputs
  as training data.
- [Tripo Privacy Policy](https://www.tripo3d.ai/privacy), effective
  2025-02-05, is the recorded privacy-policy source.

The exact Studio privacy control visible for this run was `Sharing Only`.
Pricing advertises permanent model edit history for Max; this records only
that advertised history feature. Sections 4 and 10.5 of the recorded terms
disclaim an obligation to store outputs and permit deletion after service
termination. Tripo is therefore a best-effort recovery source, not the
archive.

The Studio multi-view surface exposes Front, Left, Right, and Back slots.
Only exact compatible elevations were submitted:

| Studio slot | Run input | Use |
|---|---|---|
| Front | `input/front.png` | Submitted |
| Left | none | Deliberately blank; no approved left elevation |
| Right | `input/right.png` | Submitted |
| Back | none | Deliberately blank; no approved rear elevation |

`input/top.png` and `input/front-right-3q.png` are retained as validation
evidence. They were not misrepresented as left or rear orthographic views.

The separate stacked-image control was rejected before generation because its
220-credit quote represented four independent models rather than one
multi-view reconstruction. No credits were spent through that mode.

## Approved inputs

Source sheet:
`art/reference-sheets/frontier-station-v1/poc-models/station-wall-utility-turnaround-v1.png`

Source ownership and approval are recorded in the adjacent provenance and
approval files. The sheet was created with the built-in Codex image-generation
tool and approved by the project owner. The built-in generator did not expose
its underlying model, seed, or job identifier.

| File | Bytes | SHA-256 |
|---|---:|---|
| `input/front.png` | 443455 | `F5B1ABE74CA12A27A3C31AD57448CFFDC114725984DF0A43D4830BC77E806497` |
| `input/right.png` | 370881 | `ACB628BB9D8E7438018A667BEBFB9A2F6F0D0EF0E40A9ED6125833621902A509` |
| `input/top.png` | 391850 | `41BA7369D647DD2D89DC2DE714199C6A90551F0FCBB1F99B78C05D2086068649` |
| `input/front-right-3q.png` | 468264 | `CB164BFE764A3984B6B8819D65009F6B295012DA918A4082E567DB0437347DD1` |
| `input/prompt.txt` | 1145 | `7F39E5C718CBAA71CDD7110AC761B4A586B4106166D2B1C60B34ADA6F706C0CB` |

Approved source sheet:

- Bytes: 1,782,611
- SHA-256:
  `393777D6575333D977CE10DCD9B2D06E18A5A5541272ABB0906F68688A7FB300`

The exact semantic prompt is preserved even though this multi-view Studio mode
did not expose a text-prompt field. No substitute prompt was entered.

## Operation and credits

- Candidate 01 task ID:
  `162d5614-e586-4a2b-a9b6-bbfe71d8caf9`
- Credits consumed: 55
- Studio-generated asset label: `futuristic machine panel 3d model`
- Studio topology display: 1,943,381 triangle faces and 1,017,000 vertices
- Generating-state evidence:
  `evidence/studio-review/candidate01-generating-task-162d5614-e586-4a2b-a9b6-bbfe71d8caf9.png`
- Provider-side segmentation, retopology, texturing, rigging, and animation:
  none
- Export format: GLB with the current 4K generated texture
- Export-settings evidence:
  `evidence/settings/candidate01-export-glb-current-4k.png`
- Raw export:
  `raw/prop.station.wall_utility.v1__raw__tripo-v3.1__candidate-01.glb`
- Raw export bytes: 58,219,864
- Raw export SHA-256:
  `7D1B87029212C9DA8757DABBA7643B7811808F124FFEC7E80C4A7E546F969059`
- Raw storage: ignored local cache on the dedicated art workstation
- Tracked raw-export record: `raw-export.manifest.json`
- Local size/hash verification: passed 2026-07-24
- Off-machine archive: deferred by owner decision; no locator recorded

The untouched download was produced twice while recovering from a browser
download-event timeout. Both downloaded files were 58,219,864 bytes with the
same SHA-256 above. One exact copy was placed in `raw/`; the other download was
not added to the repository. No additional credit or provider operation was
charged.

## Evidence hashes

| File | Bytes | SHA-256 |
|---|---:|---|
| `evidence/settings/tripo-preflight-front-right-v3.1-55credits.png` | 96057 | `40806E34C7F2EBB53656D88B7F926E4169442B48BA8294F0955281C42441BFB1` |
| `evidence/settings/candidate01-export-glb-current-4k.png` | 129858 | `37C0279746B73FE8EF072A3B262F3FFBA6CA933FBD6BD29C39E854CABD9C94D9` |
| `evidence/studio-review/candidate01-generating-task-162d5614-e586-4a2b-a9b6-bbfe71d8caf9.png` | 149500 | `F95E3E8175A6A9A0C32CCA79009C5528D0D861581221FE0EBE33466E8FCD6A0D` |
| `evidence/studio-review/candidate01-complete-front.png` | 133539 | `C3C3CA642B78B3D871AE09E3954330896496BE1CFB4C0B65405FF1AE610BE063` |
| `evidence/studio-review/candidate01-quarter.png` | 133417 | `11B7694EE695E5082F62A528D692CE4FF83F1EEA6E739FE8C40CC5B7969F7B51` |
| `evidence/studio-review/candidate01-side.png` | 125340 | `D336E32844D8DF492D3B643A4623327B3391A6E3F44C9A74DCC9BEFEF2E22F4E` |
| `evidence/studio-review/candidate01-back.png` | 123482 | `F83AEE0B868074889AB0A58DF37DAD38DEA908A995FA9C93A89FC986AA9A3C69` |

The complete nine-file screenshot inventory is also recorded in
`evidence/evidence-hashes.sha256`.

## Provisional selection

Candidate 01 is the only generated candidate and is provisionally selected for
the Blender hard gate. The front identity is exceptionally close to the
approved sheet: one dark shallow enclosure, dominant grille, two broad copper
runs, heavy clamps, and a single subordinate cyan status strip. It contains no
violet, green, hostile red, text, logo, loose wire, weapon cue, or structural
wall section.

The Studio rear inspection exposes a named construction defect: the generated
rear repeats utility detail and includes a broad blue baked-color artifact
instead of the required flat mounting plane. A second generation was not used
because the approved pack has no rear elevation and repeating the same
front/right inputs would not plausibly target that defect. Blender must remove
or reconstruct the rear within the 30-minute active cleanup cap. Failure to
reach a flat `Z = 0` mount without redesign rejects this candidate at the hard
gate.

## Gate status

- Blender normalization and the 30-minute active cleanup cap: passed.
- Exact-hash Blender validation: passed for
  `104B03AAF161192610D9F8F1089B092C1D1EE6140F1333C13FAE3235F1E6BAF2`.
- Exact-hash Godot import and isolated-gallery validation: passed for the
  same GLB at 7.5, 14.5, and 20 m.
- Keep the live station route unchanged.
- Final project-owner visual review: pending.

## Blender cleanup and validation

- Blender version: `5.2.0 LTS`
- Parameterized tool:
  `tools/blender/process_wall_utility_candidate.py`
- Editable source:
  `art/source/prop.station.wall_utility.v1/wall-utility-v1__tripo-candidate-01.blend`
- Editable source SHA-256:
  `F5E1C2AE41D65F8A0E193BB24A4D13F90778C694D68E8EA7B64C38AFC52B9326`
- Derived GLB:
  `derived/prop.station.wall_utility.v1__clean__tripo-v3.1__candidate-01.glb`
- Derived GLB bytes: 485,876
- Derived GLB SHA-256:
  `104B03AAF161192610D9F8F1089B092C1D1EE6140F1333C13FAE3235F1E6BAF2`
- Final exact-GLB metrics: 2,764 triangles, 1 mesh, 2 materials, one
  1024x1024 base/normal/RM texture set, 0 armatures, 0 actions, and no
  collision object.
- Bounds after fresh import: 1.20 x 0.80 x 0.22 m published, within the
  brief's ±2% tolerance.
- Pivot: bottom center of the rear mounting plane.
- Published coordinates: `+Y` up, visible front `-Z`, rear plane `Z = 0`.
- Signed published AABB: X `[-0.60,+0.60]`, Y `[0,+0.80]`,
  Z `[-0.22,0]`. The detailed face is visible from camera positions on `-Z`;
  the flat mounting rear is visible from `+Z`.
- Blender authoring coordinates: `+Z` up and `+Y` front, with geometry
  extending from rear `Y = 0` toward `+Y`.
- The 180-degree authoring Z rotation preserves the approved front layout:
  dominant vent on the left, broad utility runs on the right.
- Final deterministic Blender cleanup pass: 27.853 seconds.
- Cumulative automated Blender cleanup across recorded repair trials:
  272.018 seconds. Diagnosis, visual inspection, and scripting remained below
  the 30-minute active cleanup cap.

The generated duplicate rear was removed at a planar depth cut and replaced
with a closed, flat, untextured `mat.station.wall.dark` mounting face. The
broad blue baked artifact is absent from the derived GLB. The retained front
was spatially welded, reduced from 1,943,381 to 2,764 triangles, normalized,
and freshly imported for validation. glTF attribute seams duplicate vertices
on exact import; the recorded spatial-weld diagnostic resolves those standard
UV/normal seam duplicates to a closed manifold surface with zero boundary,
non-manifold, wire, loose, or zero-area geometry.

Reports:

- `derived/raw-inspection.json`
- `derived/blender-validation.json`
- `derived/blender-validation.md`
- `derived/blender-processing-history.md`
- `derived/artifact-hashes.sha256`

Ignored local standardized Blender review staging is under:
`artifacts/reviews/prop.station.wall_utility.v1/104B03AAF161192610D9F8F1089B092C1D1EE6140F1333C13FAE3235F1E6BAF2/blender/`.

## Godot import and tactical review

- Tracked derived review GLB:
  `derived/prop.station.wall_utility.v1__clean__tripo-v3.1__candidate-01.glb`
- Derived review GLB SHA-256:
  `104B03AAF161192610D9F8F1089B092C1D1EE6140F1333C13FAE3235F1E6BAF2`
- Ignored local staging copy and gallery:
  `artifacts/godot-asset-gallery/space-adventure-art-production/game/`
- Imported world AABB: position
  `[-0.6000000238,0.8999999762,-0.2200000435]`, size
  `[1.2000000477,0.8000000715,0.2200001031]`.
- The imported review mesh was visible. A separate, correctly sized
  greybox fallback remains hidden and can be enabled without changing the
  stable asset ID or the live station route.
- Scene validation: 0 errors, 0 warnings, 4 dependencies.
- Godot editor errors, debugger errors, and blocking dialogs: 0.
- Tactical-camera captures passed visual inspection at 7.5, 14.5, and 20 m.
  The grille, utility runs, enclosure mass, orientation, and silhouette remain
  readable at their intended review distances.

Ignored local standardized Godot review staging is under:
`artifacts/reviews/prop.station.wall_utility.v1/104B03AAF161192610D9F8F1089B092C1D1EE6140F1333C13FAE3235F1E6BAF2/godot/`.
