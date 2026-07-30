# Station escape and ship-combat concept pack v1

Status: preferred ship-combat composition approved as a Phase 7 visual anchor;
the remaining images are exploratory, and none of the pack is an approved
production roster, art brief, 3D source, or runtime asset

Generated: 2026-07-29

Preferred ship-combat composition:
`ship-combat-separated-clean-direction-v4.png`. The diagonal v1, undivided
side-by-side v2, and targeting-line v3 compositions are retained as superseded
exploration.

Generator: built-in Codex image-generation tool

Provider model, seed, and job identifiers: not exposed by the built-in tool

## Purpose

This pack explores the approved bounded continuation from the disabled
frontier station into a small escape cutter and a later
real-time-with-pause ship-combat prototype. It preserves the project's
elevated 3D tactical camera, chunky
retro-industrial construction, dark shell, purposeful state colors, and
two-character party read.

The ship-combat image is FTL-inspired only at the level of readable
compartments, crew positioning, subsystem pressure, targeting, and tactical
pause. It intentionally does not copy FTL's pixel art, interface, icons, ship
silhouettes, or room layouts.

These images do not supersede `docs/POC.md`, `docs/ROADMAP.md`,
`docs/POC-ASSET-ROSTER.md`, or `docs/OPEN-QUESTIONS.md`. The bounded gameplay
scope and sequencing decision are now recorded in
`docs/SHIP-COMBAT-POC.md` and ADR 0021. Ship combat and humanoid enemies remain
outside the approved station POC.

## Outputs

| File | Purpose | Dimensions | SHA-256 |
|---|---|---:|---|
| `station-escape-launch-bay-key-art-v1.png` | Station-to-ship transition and launch-bay composition | 1672 × 941 | `89c3f57870ad7556bb8e08653cd7889fad9c2c54207497bfc985b52d6dacccf5` |
| `station-dock-service-prop-family-v1.png` | Six-piece dock-service and maintenance prop family | 1672 × 941 | `2c3a2211d5da6cc5861c6dba18bc8af911179c9ac94b33fb38d5eaa0384ba4e4` |
| `station-boarder-humanoid-turnaround-v1.png` | Sealed humanoid boarder and separate compact carbine exploration | 1254 × 1254 | `49ece34cccb1fed6a064e245130709422aa6c67a6107b47c0320e6800837169b` |
| `escape-cutter-exterior-turnaround-v1.png` | Six-view exterior candidate for the compact escape cutter | 1672 × 941 | `8febb15be1f41bb6200a60e864b0ff10f419494e2cd248f7bb2e879d6dcdb760` |
| `escape-cutter-interior-cutaway-v1.png` | Top-down and isometric single-deck room-direction exploration | 1672 × 941 | `fbb139a9b6c0dd4ad8c15e082d239a82720df6f5c3728ddbb4ea2cb50edad4cb` |
| `ship-combat-presentation-direction-v1.png` | Superseded diagonal 3D tactical ship-combat composition | 1672 × 941 | `131c0cdd37e2aec5cf721b27d15d3b8f275664c634a4017b3501d131c9c2f3dc` |
| `ship-combat-side-by-side-direction-v2.png` | Superseded undivided FTL-like high-level arrangement | 1672 × 941 | `c6ef3d667106d8e68316cdb520781374c94174843bd549a00867c62e39b5d27e` |
| `ship-combat-separated-viewports-direction-v3.png` | Superseded separated-viewports composition with targeting lines | 1672 × 941 | `b3516a8cee6b854abb97aff14df138b75703e495b4ddb52cbb86a7c3cf4480e7` |
| `ship-combat-separated-clean-direction-v4.png` | Preferred strict top-down separated-viewports composition without targeting lines | 1672 × 941 | `0641addddbaa285e59d733a7ae5b9d245e298283df11581afbe458dc865997bc` |

`provenance-inputs/ship-combat-presentation-direction-v1-initial.png` is the
durable edit input for the final combat image. Its dimensions are 1672 × 941
and its SHA-256 is
`668a4cdf6937c3945f146a3088c5c975fcc1c8eac674ffd6603ce09079cb4c5b`.
It is not a separate deliverable: the constrained edit removed an unintended
third crew figure so the final image respects the current two-character party.

## Existing input registry

| Input | Role | SHA-256 |
|---|---|---|
| `../frontier-station-v1/station-route-key-art.png` | Environment, camera, lighting, and material anchor | `a540d65afb6144030c1177478a0f2b4653146f76ef4eb9a4daf65fbc7e7294b1` |
| `../frontier-station-v1/tactical-pause-combat.png` | Encounter readability and hostile/player state-color anchor | `e891f3d4c738aa6074d71a5b889b42dc2ffb96b585551e77d6dd4b31b99badfc` |
| `../frontier-station-v1/poc-crew-lineup.png` | Crew proportions, materials, and render-quality anchor | `2f2363d10defd95c4ee8bfe4a89decb168698f491415632b05cba24ec9544dd5` |
| `../../reference-sheets/frontier-station-v1/poc-models/evacuation-airlock-reference-v1.png` | Airlock form and destination-state anchor | `27dca5bb57d4087188feb3423efad4b5288623c8301a06d64f4f92754baabd3b` |
| `../../reference-sheets/frontier-station-v1/poc-models/station-structure-kit-reference-v1.png` | Modular hard-surface family, bevel, and studio-sheet anchor | `f518b123fc6b2f67dda71cb846cad76aa334617c72bfec5e52f4fd817e4e3738` |
| `../../reference-sheets/frontier-station-v1/poc-models/station-survivor-turnaround-v1.png` | Four-view humanoid sheet layout and construction anchor | `3b54a69c21a563fd9685737d44ac5e55330e8ca1a0e06186655ee96f780ad169` |

Generated outputs used as later inputs are identified by their output hashes in
the table above. Exact generation and correction prompts are versioned under
`prompts/`.

## Recorded review outcome

On 2026-07-29 the project owner approved
`ship-combat-separated-clean-direction-v4.png` as the preferred combat
composition:

- both ships use strict overhead views and point upward;
- the player ship is on the left and the hostile ship is on the right;
- a central divider makes clear that they are not physically side by side; and
- no cyan, red, or other movement or trajectory lines connect the ships.

The launch bay, dock props, humanoid boarder, cutter exterior, cutter interior,
and hostile silhouette remain exploratory visual anchors. Their presence does
not approve a production brief or implementation.

## Review boundary

Review:

- whether the launch bay feels like a natural continuation of the current
  station;
- whether the dock-service family is worth promoting into individual briefs;
- whether the sealed boarder is the desired humanoid enemy direction;
- whether the escape cutter's exterior and cutaway feel like the same ship;
- whether one traversable 3D deck is preferable to a distinct 2D ship layer;
  and
- whether the preferred strict top-down combat screen and its central tactical
  divider communicate distance, pause, crew placement, subsystem pressure,
  and damage without becoming UI-heavy.

Approval of an image would authorize only a visual anchor. It would not define
sapience, negotiation rules, attacks, abilities, ship systems, room topology,
damage simulation, boarding, progression, encounter balance, or implementation
order. Those decisions require updates to the authoritative project documents
before gameplay work begins.
