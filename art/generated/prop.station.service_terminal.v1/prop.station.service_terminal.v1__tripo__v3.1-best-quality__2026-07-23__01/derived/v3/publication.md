# Blender review export — service terminal candidate 01 v3

Status: renderer/orientation validation passed, but the candidate is rejected
at the bake-off geometry hard gate.

## Reason for revision

Godot validation of v2 exposed an axis defect: the violet display was visible
from exported `+Z`, while the project contract requires `+Y` up and `-Z`
front. V3 is an orientation-only normalization revision. It does not change
the approved shape, topology, UVs, materials, or texture payloads.

The editable source is Blender Z-up. The build applies π radians around
Blender `+Z`, which maps to π around exported `+Y`, then applies that transform
to the mesh data. The object origin and ground contact remain unchanged.

## Reproducible build

The executed revision of
`tools/blender/publish_service_terminal_tripo_candidate01_v3.py`
(`37CA0F455E9CE5C871E41A124C6097FA9FFA3050D07DF0C7AE0B5865F73B380B`)
opened the exact v2 source hash
`BCC9F1F39133AF3696856EAE1DEB21EF087B9F358B86DFB46B47E6D62926364A`,
verified the then-current raw/v1/v2 inputs, performed the single orientation
operation, saved a separate v3 source, exported a separate v3 GLB, parsed its
GLB structure, and fresh-imported it into an empty Blender database. The
current script is review-branch-safe and validates only retained raw/v2 inputs
before rebuilding the same final outputs.

Machine-readable results are in `orientation-fix-report.json`
(`9180273EFFEA53AEE324388ECC86659D91FC84C557C162CDF3DDA4005441A9E2`).

## Outputs

| Output | Bytes | SHA-256 |
|---|---:|---|
| `art/source/prop.station.service_terminal.v1/service-terminal-v1__tripo-candidate-01-v3.blend` | 3,402,472 | `94B2A770AC1E393FEFE77DA2262CA83651933519F738C7F0987383183C60F79D` |
| `derived/v3/prop.station.service_terminal.v1__clean__tripo-v3.1__candidate-01-v3.glb` | 536,644 | `1EA04834A19627B94AC004574C5EF1F9A62961FF80B60BFD1C579BFF061FB121` |

No copy is published into the game project. The exact derived GLB is the
tracked bake-off output; review copies remain in ignored local staging.

## Verified invariants

- one mesh and two material primitives;
- 3,979 triangles;
- exact 0.80 × 1.30 × 0.42 m exported envelope;
- `+Y` up, `-Z` front, ground contact at `Y = 0`;
- violet primitive exported at Z `-0.162501..-0.135006` m;
- exact materials `mat.station.generated.candidate01.v2` and
  `mat.state.optional.violet`;
- glTF violet emissive strength unchanged at `1.2960000171661363`;
- all three embedded JPEG payload hashes byte-identical to v2;
- 42 boundary edges, 64 non-manifold edges, and no topology change;
- no cameras, lights, armatures, skins, or animations.

Raw and then-current v1/v2 source/derived/published hashes were checked before
and after the executed build. Superseded duplicate outputs are documented by
hash but not retained in this cleaned branch. No Tripo operation or credit was
used.

## Review evidence

Ignored local exact-hash review staging:

`artifacts/reviews/prop.station.service_terminal.v1/1EA04834A19627B94AC004574C5EF1F9A62961FF80B60BFD1C579BFF061FB121/`

- `render-manifest.json`
  (`53811B1C6AF29D0BC8852417FFAF3B1429F3AA7779C5A389C672477627FA5F4B`)
- `contact-sheet.png`
  (`C5B3006B8C3FE0A6ACC387EBBA5660A74ACB34EECF158DA94670DA7DDEA493AA`)

The review renderer is explicitly configured for the v3 fresh-import `+Y`
review front, which corresponds to exported `-Z`. Godot import and tactical
camera evidence remain the authoritative orientation check.

## Godot validation

Godot 4.7.1 Mono fresh-imported the v3 GLB as `PackedScene`
`uid://k43o7ifeaeq5`. The exact asset was reviewed only in the dedicated local
terminal gallery, which retained a hidden exact-size greybox fallback. The
gallery and imported copy now remain under ignored local staging. No Godot
corrective rotation, live-route replacement, or gameplay-contract change was
introduced.

Dedicated `-Z`-front captures at 7.5, 14.5, and 20 m show the violet display.
Fresh import, 14 pure .NET tests, the unchanged station-route smoke test, and
the dedicated gallery health run passed. The local dedicated captures are
display-readability comparison evidence; they do not override the topology
hard-gate rejection.

Exact settings, image hashes, validation output, fallback state, and remaining
defects are recorded in:

`artifacts/reviews/prop.station.service_terminal.v1/1EA04834A19627B94AC004574C5EF1F9A62961FF80B60BFD1C579BFF061FB121/godot/validation.md`
