# Tripo run metadata

Status: candidate 01 rejected at the bake-off geometry hard gate; v3 retained
as a Blender/Godot renderer and orientation comparator

## Run

- Asset ID: `prop.station.service_terminal.v1`
- Run ID:
  `prop.station.service_terminal.v1__tripo__v3.1-best-quality__2026-07-23__01`
- Lane: `bakeoff`
- Run-ID note: this task predates commit `71ea25e`, which introduced the
  explicit `__bakeoff__` lane token. The original directory and run ID remain
  unchanged as immutable provenance; new bake-off runs use the current format.
- UTC date: 2026-07-23
- Assigned owner: primary SpaceAdventure art-pipeline agent
- Review-staging owner: primary SpaceAdventure art-pipeline agent
- Dedicated worktree:
  `C:\Developpement\space-adventure-art-production`
- Owned tracked paths: this run directory,
  `art/source/prop.station.service_terminal.v1/`, and the matching
  `tools/blender/` scripts
- Local ignored review staging: `artifacts/reviews/` and
  `artifacts/godot-asset-gallery/space-adventure-art-production/`
- Godot MCP port: not recorded in the original preflight; legacy gap
- Candidate class: static prop
- Production brief: `art/briefs/station-service-terminal-v1.md`
- Authorization: Phase 2 bounded generator bake-off

This pre-existing run was continued on 2026-07-24 from the original art-run
baseline at `1f3d941800a05398221026da53108668c22d12d6`. No regeneration or
additional credit was used. Superseded Blender outputs are summarized below
rather than carried as duplicate binaries. The untouched provider export
remains in the ignored run-local workstation cache. Its manifest, submitted
inputs, Studio evidence, retained editable sources, and final derived
comparator GLB remain tracked. Standardized Blender/Godot renders stay in
ignored local review staging under the latest policy.

## Live Studio preflight

- Product: signed-in Tripo Studio web application
- Plan displayed: Max
- Visible balance before submission: 25,145 credits
- Privacy setting: Sharing Only
- Commercial-use status: Tripo's current official pricing page lists Max as
  including private models and commercial use
- Commercial-use source:
  `https://www.tripo3d.ai/pricing`, retrieved 2026-07-23
- Terms source:
  `https://www.tripo3d.ai/terms`, retrieved 2026-07-24; section 5.2.2
  gives paid users broad rights in their inputs and outputs and states that
  Tripo will not use paid-user inputs or outputs to train, validate, test, or
  improve its AI technology
- Privacy source:
  `https://www.tripo3d.ai/privacy`, retrieved 2026-07-24
- Retention note: the Max pricing page advertises permanent model edit
  history, but sections 4 and 10.5 of the recorded terms disclaim an
  obligation to store outputs and permit deletion after service termination.
  Tripo is a best-effort recovery source, not the archive.
- Live model: `v3.1 - Best Quality`
- Mode: HD Model / multi-view image-to-3D
- Geometry & Texture: Ultra enabled by the live preset
- Generate in Parts: disabled for the terminal
- 8K Texture trial: disabled
- Displayed generation cost before input upload: 55 credits
- Displayed generation cost after final input upload: 55 credits
- API use: none
- Purchase or upgrade: none
- Final submitted settings are recorded in text and the neighboring settings
  record; disposable Studio screenshots are not versioned.

The refreshed Studio page initially displayed the 8K trial switch as enabled.
It was explicitly disabled before submission; the final recorded settings show
both Generate in Parts and 8K Texture off.

## Inputs

| File | Bytes | SHA-256 |
|---|---:|---|
| `input/front.png` | 503748 | `E53B327BFD81B565DD5BD641846015C7B8560147FB1B3713DF23A5F0DFAF583B` |
| `input/right.png` | 456343 | `AA4B07C410AE4D70166A32F9FC46394064A835F52F1DA0857F90B55015720FE2` |
| `input/rear.png` | 460716 | `DDE48FB6041459FF02059D1175AD9E38630167CE3EF37A0F506168DED72AA16D` |
| `input/front-right-3q.png` | 519184 | `46DCF1D915ACBD37A06914864FCED2C001195F568E610C564992ACC6D64123E5` |
| `input/prompt.txt` | 1191 | `46B4AACBAFF6C1C9D564829CEE38086EF327FC049315C424CC5D7126CEA11B23` |

The prompt hash is for the exact CRLF working-copy bytes placed in this run
directory, resolving the older provider-pack manifest's LF/CRLF mismatch.

Source sheet:
`art/reference-sheets/frontier-station-v1/station-service-terminal-turnaround-v1.png`
(`B433BAE19A05A506257692B0E9E5C13235295CB3306ACFB56A2166EB15C85503`).
It was generated with the Codex built-in image-generation tool, approved by
the project owner, and authorized for this bounded Phase 2 provider upload.
The built-in generator's exact underlying model, seed, and job ID were not
exposed; see the neighboring reference provenance file.

Multi-view slot mapping:

| Studio slot | Run input | Use |
|---|---|---|
| Front | `input/front.png` | Submitted |
| Left | none | Deliberately blank; no true left elevation was approved |
| Right | `input/right.png` | Submitted |
| Back | `input/rear.png` | Submitted |

`input/front-right-3q.png` is retained as selection and cleanup validation
evidence. It was not misrepresented as a left elevation.

## Operations, outputs, and credits

- Candidate 01 generation submitted in Tripo Studio.
- Studio task/model identifier:
  `d3014f04-3de4-45ba-9502-90d6f80ea67b`
- Submitted operation: HD Model, multi-view image-to-3D, v3.1 Best Quality,
  Ultra preset, Generate in Parts off, 8K Texture off.
- Displayed cost at submission: 55 credits.
- Visible balance before submission: 25,145 credits.
- Visible balance after submission: 25,090 credits.
- Credits consumed by this run so far: `55`.
- Studio-generated asset label: `arcade machine 3d model`.
- Raw Studio topology: 1,916,879 triangles, 1,916,879 faces, and
  1,000,557 vertices.
- Raw export format: GLB with the current 4K generated texture, retained as
  immutable provider evidence rather than a publishable game asset.
- Raw export:
  `raw/prop.station.service_terminal.v1__raw__tripo-v3.1__candidate-01.glb`
- Raw export bytes: 57,150,780.
- Raw export SHA-256:
  `09249AE0F5D5201B684839C3ED81680F645161C99917117027F803BC28DB4CA0`.
- Raw storage: ignored local cache on the dedicated art workstation.
- Tracked raw-export record: `raw-export.manifest.json`.
- Local size/hash verification: passed 2026-07-24.
- Off-machine archive: deferred by owner decision; no locator recorded.
- Candidate 01 was reviewed directly in Studio's live front, quarter, rear,
  and alternate-angle views.

No retry, segmentation, provider retopology, provider texture, rig, or
animation operation was submitted. The raw export succeeded through the
Studio web workflow. Append every later Blender operation, version branch,
derived review export, hash, and defect below as it occurs.

## Known preflight limitations

- The raw provider topology and 4K texture exceed the game brief by design and
  are not eligible for direct Godot publication.
- The Studio privacy setting is `Sharing Only`, not `Public`.
- The candidate remained untrusted through cleanup and was ultimately rejected
  at the geometry hard gate.

## Blender processing history

Blender 5.2.0 LTS processed the exact raw hash without modifying it. The best
welded pass normalized the terminal, reduced it to 3,979 triangles,
retained one UV set, resized the three-map texture set to 1024 pixels, and
produced v1 hash
`AC4F65D79CB2B9A5CD79914CF5D11CA85D8985A4E3FD37BD05E7C96BB29183C0`.

Two transient attempts were rejected or superseded before the final review
export:

- unwelded collapse hash
  `5B185D1B54C595C901E7C02583F02B7CB17C0069536CAF784FE936A503522FFC`
  fragmented the screen and housing and retained 3,970 boundary, 4,501
  non-manifold, and 531 wire edges;
- centroid-only material selection hash
  `6F2C795648A07B8C615B423160B0A62AD9442DEADF4AD9517F2DE6B3953BB1D2`
  left dark wedges on the display edge.

The named failures, exact hashes, metrics, and processing inputs remain in
this record and `derived/v3/orientation-fix-report.json`. Their duplicate
GLBs, sources, render folders, recorder frames, and audio were intentionally
omitted from the review branch. The versioned Blender scripts reproduce the
processing sequence from the retained raw export.

The retained v2 editable source is:

`art/source/prop.station.service_terminal.v1/service-terminal-v1__tripo-candidate-01-v2.blend`

- Bytes: 3,403,474
- SHA-256:
  `BCC9F1F39133AF3696856EAE1DEB21EF087B9F358B86DFB46B47E6D62926364A`
- Superseded v2 GLB hash:
  `2303A47D74CD11B1047D44A32730448B02C6EF8C0252396DC28782748A40AF4B`
- Materials: `mat.station.generated.candidate01.v2` and
  `mat.state.optional.violet`
- Geometry: 3,979 triangles and exact 0.80 x 1.30 x 0.42 m bounds
- Additional provider credits: 0

The editable reduced shell retains 42 boundary edges and 64 non-manifold
edges. Those defects remain the reason this renderer-valid result fails the
bake-off geometry hard gate.

## Orientation normalization v3 and Godot validation

Godot review established that v2's documented front axis was reversed: its
violet display was physically present on exported `+Z`. A separate v3 Blender
review export corrects the comparator itself rather than adding a Godot
presentation or gameplay rotation.

- V3 editable source:
  `art/source/prop.station.service_terminal.v1/service-terminal-v1__tripo-candidate-01-v3.blend`
- Source bytes: 3,402,472
- Source SHA-256:
  `94B2A770AC1E393FEFE77DA2262CA83651933519F738C7F0987383183C60F79D`
- V3 derived GLB:
  `derived/v3/prop.station.service_terminal.v1__clean__tripo-v3.1__candidate-01-v3.glb`
- Derived bytes: 536,644
- Derived SHA-256:
  `1EA04834A19627B94AC004574C5EF1F9A62961FF80B60BFD1C579BFF061FB121`
- Geometry: unchanged at 3,979 triangles and exact
  0.80 × 1.30 × 0.42 m bounds.
- Export axes: `+Y` up, corrected `-Z` front, ground at `Y = 0`.
- Materials, emission, UVs, and all embedded JPEG payload hashes: unchanged
  from v2.
- Additional provider credits: 0.

The ignored local Godot review staging retains the exact camera settings,
verification results, and remaining defects:

`artifacts/reviews/prop.station.service_terminal.v1/1EA04834A19627B94AC004574C5EF1F9A62961FF80B60BFD1C579BFF061FB121/godot/validation.md`

Raw and then-current v1/v2 inputs were hash-checked before and after the v3
build. The raw provider export remains in the ignored local cache; its
manifest, retained editable sources, and final derived comparator GLB are
tracked. Review renders and gallery copies remain ignored local staging.
