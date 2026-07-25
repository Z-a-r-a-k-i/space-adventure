# Tripo run metadata

Status: candidate 02 provisionally selected; Blender cleanup pending

## Run

- Asset ID: `character.npc.station_survivor.v1`
- Run ID:
  `prod-tripo-v31bq-20260724-01`
- Lane: `production`
- UTC date: 2026-07-24
- Candidate class: humanoid / noncombatant fixed-outfit source
- Production brief: `art/briefs/station-survivor-v1.md`
- Authorization: project-owner-approved offline source production under ADR
  0016; gameplay integration and shared-rig reuse remain separately gated
- Rig profile: `rig.crew.humanoid.v1`; final binding waits for Vanguard-first
  validation
- Candidate limit: one initial candidate and at most one named-defect retry
- Credits consumed by this run: `105`

## Approved source and input preparation

Approved source sheet:
`art/reference-sheets/frontier-station-v1/poc-models/station-survivor-turnaround-v1.png`
(`3B54A69C21A563FD9685737D44AC5E55330E8CA1A0E06186655EE96F780AD169`).
Its neighboring provenance records generation by the built-in Codex image
tool and owner approval on 2026-07-23.

The inputs are lossless pixel crops from the approved sheet. They were not
resampled, repainted, relit, or semantically changed. The divider and excess
empty margin were excluded and all bodies remain complete.

| File | Crop geometry | Dimensions | Bytes | SHA-256 |
|---|---|---:|---:|---|
| `input/front.png` | `500x590+65+10` | 500 × 590 | 337580 | `D6097E66469769A0D876C7808D84297F48000713661D7D9CD4DE3DA3BB8E6F69` |
| `input/left.png` | `500x590+689+10` | 500 × 590 | 302674 | `8FC8CC242323DB91DACF6A389724493A2F2F3889A399C475ED571D8B77F8B9F3` |
| `input/back.png` | `500x590+65+644` | 500 × 590 | 321560 | `D407E8B87265548412B976C21AA685D38F722617C326BD960745F0CDC7CD00C6` |
| `input/front-right-3q.png` | `500x590+689+644` | 500 × 590 | 325291 | `A18E05D0CBC3AC171052F5E2A8D6DED7BAF7F56282E5E8A1F6A3B2C889572429` |
| `input/prompt.txt` | exact brief prompt | n/a | 1146 | `70A31CB559E411002EB8BB8C9E22622C4844A41C417CDD8CE8ABDA3815DEA22D` |

The exact input crops above are the durable generation sources. Direct review
passed for full-body framing, neutral background, unarmed presentation,
consistent identity and scale, and front/left/back compatibility. The
front-right three-quarter view is retained for candidate validation rather
than mislabeled as a strict Right elevation.

## Submitted Studio operation

- Provider surface: signed-in Tripo Studio web application in Chrome.
- Plan displayed: Max.
- Privacy displayed: `Sharing Only`.
- Visible balance immediately before submission: 24,705 credits.
- Workflow: Build & Refine / HD Model / multi-view image-to-3D.
- Model: `v3.1 – Best Quality`.
- Ultra Mesh Quality: enabled.
- Source topology: Triangle, 2,000,000 target.
- Texture generation: disabled.
- Generate in Parts: enabled.
- Segmentation detail: `Balanced`.
- 8K Texture: disabled.
- Submitted view slots: Front, Left, and Back.
- Right slot: intentionally empty because the approved auxiliary view is a
  three-quarter perspective, not a strict right elevation.
- Displayed and charged cost: 60 credits.
- Visible balance after submission: 24,645 credits.
- Candidate 01 provider task ID:
  `06cccd46-1120-4961-8b36-0fb5243e0bd1`.
- The task routed to the Segmentation workspace with Balanced selected and was
  visibly queued/generating.
- No account, payment, purchase, upgrade, privacy, or API prompt appeared.

The submitted settings, task ID, and operation result are recorded above.
Disposable Studio screenshots are not versioned.

## Candidate 01 review and rejection

Candidate 01 completed with 1,916,956 faces and 963,932 vertices. Balanced
preview exposed 15 provisional parts. Front and three-quarter geometry broadly
preserved the approved outfit, but the rear view contains a complete second
face on the back of the head, aligned against the rear-facing torso. This is a
definitive identity and construction failure, not a viewer artifact.

- Decision: reject; do not segment, retopologize, texture, or rig.
- Untouched export:
  `raw/character.npc.station_survivor.v1__raw__tripo-v3.1__candidate-01.glb`.
- Raw bytes: 46,151,260.
- Cache identity and presence: `raw-export.manifest.json`.
- The duplicate rear face was confirmed directly in Studio's live geometry
  and rear views.

The duplicate rear face is the named defect that permits the run's one
targeted retry.

## Candidate 02 targeted retry and selection

Candidate 02 used only the approved `input/front-right-3q.png` as a truthful
single-image input. It was not mislabeled as a strict right elevation. This
isolated the front/back head ambiguity while preserving the approved identity.

- Provider task ID: `62e5884d-0f12-467c-b38b-77689ee7f984`.
- Workflow: Build & Refine / HD Model / single-image image-to-3D.
- Model: `v3.1 – Best Quality`.
- Ultra Mesh Quality: enabled.
- Source topology: Triangle, 2,000,000 target.
- Texture: 2K PBR enabled.
- AI Complete: disabled.
- Generate in Parts: disabled.
- 8K Texture: disabled.
- Privacy: `Sharing Only`.
- Displayed and charged cost: 45 credits.
- Visible balance: 24,645 before, 24,600 after.
- Studio topology: 1,921,817 faces and 988,279 vertices.
- Untouched export:
  `raw/character.npc.station_survivor.v1__raw__tripo-v3.1__candidate-02.glb`.
- Raw bytes: 55,332,160.
- Cache identity and presence: `raw-export.manifest.json`.
- Decision: provisionally selected pending project-owner visual review.

Front, three-quarter, and rear inspection show one correctly oriented face,
clean back-of-head construction, the approved side-shaved salt-and-pepper hair,
older warm-brown-skinned identity, navy coverall, warm-gray work vest and broad
panels, belt and closed pouches, broad boots, restrained amber/cyan accents,
complete hands and feet, and no weapon, shield, tool, or extra body. The
silhouette remains civilian and clearly lighter than the party combatants.
The geometry is provider-dense and not production topology; it still requires
segmentation/reconstruction, retopology, material normalization, and fit to the
shared Blender rig.

Candidate 02 was reviewed directly in Studio's live front, three-quarter, and
rear views. The settings, task ID, and selection decision above are the
durable record.

## Concurrent-workspace observation

While candidate 02 was visibly generating, a second clean signed-in Studio
workspace opened normally, exposed the new-model controls, and displayed no
concurrency, queue-limit, account, payment, privacy, or upgrade warning.
No input was submitted there, so this proves concurrent workspace
availability, not the provider's maximum simultaneous-job count. No unrelated
asset was started because the documented production order and Vanguard-first
shared-rig gate still apply.

## Licensing and privacy

- The approved reference sheet is project-owned generated visual direction;
  source roles and generator disclosure are recorded in its neighboring
  provenance file.
- The run uses the signed-in subscription workflow and `Sharing Only` privacy.
- Commercial-use status follows the signed-in Max subscription terms recorded
  by the project art pipeline. No API key, account identifier, private Studio
  URL, cookie, token, billing data, or temporary download link is recorded.
- Tripo history is best-effort recovery only. Both untouched static exports are
  present in the ignored run-local `raw/` cache and recorded by provider task,
  filename, path, byte size, and presence in `raw-export.manifest.json`.

## Next operation

Hydrate candidate 02 from the ignored cache by presence and byte-size check,
then perform Blender mesh reconstruction, segmentation, retopology, scale and
axis normalization, part and material naming, and tactical-camera review.
Final binding and retargeting to `rig.crew.humanoid.v1` remains blocked until
the Vanguard-first shared-skeleton and animation-retarget gate passes. Do not
accept a provider skeleton or publish into the live game as a workaround.
