# Gameplay combat screenshot directions v1

Status: final preview set approved by the project owner on 2026-07-31;
exploratory visual direction only, not runtime screenshots, gameplay authority,
implementation evidence, production UI, or phase activation

Generated: 2026-07-31

Generator: built-in Codex image-generation tool

Provider model, seed, quality setting, and job identifiers: not exposed by the
built-in tool

## Purpose

This pack shows the same two-character active-pause combat language in three
locations:

- the authored frontier station against its ram drone and gun sentry;
- a later jungle-planet encounter against non-sapient fauna; and
- a later city-planet encounter against non-sapient security machines.

All three final frames use one matching lower-left party HUD, the same pause
placement, cyan party selection, and red-orange hostile telegraph language.
They are generated gameplay mock screenshots, not captures from the current
Godot build.

Only the owner-approved final frames are retained. Earlier previews with
disconnected movement arrows or mismatched HUD layouts are intentionally not
committed.

## Outputs

| File | Direction | Dimensions | SHA-256 |
|---|---|---:|---|
| `station-combat-gameplay-v1.png` | Two crew in tactical pause against one ram drone and one gun sentry | 1672 × 941 | `c64a79f724d038b40d020a66ae3236371aeb110c8b840205941b9ac8ba74c2d9` |
| `jungle-combat-gameplay-v1.png` | Two crew facing one non-sapient crown predator beside a reclaimed research refuge | 1672 × 941 | `42075889eadd74fdbbecbe57a2318d2287034b130c751d6fe2464a0c5ac1a4d6` |
| `city-combat-gameplay-v1.png` | Two crew facing an agile drone and patrol automaton on a rain-dark transit platform | 1672 × 941 | `1f9ee9cfba93ba77fdbc53b5610c5c83a43fe3e4f25c62858cb518ce0cae984d` |

Exact final HUD-edit prompts are versioned under `prompts/`.

## Shared HUD decision

The retained screenshots use:

- two adjacent dark-metal party cards anchored in the lower-left;
- a cyan illuminated frame for the selected Vanguard;
- a matching but quieter Protector card;
- one portrait, health bar, weapon pane, and three icon-only command slots per
  character;
- one cyan pause emblem at the top center;
- cyan selection rings under party members; and
- source-specific red-orange hostile telegraphs.

A movement route appears only when a real movement command is pending. These
three paused combat frames intentionally show no cyan movement path or
arrowhead. The command-slot icons remain placeholders because active-ability
identity is still deferred.

## Existing input registry

| Input | Role | SHA-256 |
|---|---|---|
| `../frontier-station-v1/tactical-pause-combat.png` | Station camera, encounter, material, and initial combat-language anchor | `e891f3d4c738aa6074d71a5b889b42dc2ffb96b585551e77d6dd4b31b99badfc` |
| `../frontier-station-v1/poc-crew-lineup.png` | Vanguard and Protector proportions, equipment, and material anchor | `2f2363d10defd95c4ee8bfe4a89decb168698f491415632b05cba24ec9544dd5` |
| `../../reference-sheets/frontier-station-v1/poc-models/security-ram-drone-turnaround-v1.png` | Ram-drone design anchor | `f90dbc7c7ec58509077da1af1e50e1ef22789e4c10faf6bdfde1bdab2a2663cf` |
| `../../reference-sheets/frontier-station-v1/poc-models/security-gun-sentry-turnaround-v1.png` | Gun-sentry and city-machine construction anchor | `89872e6b84578fd213f9dc10b17d8905c88ca6621592e55f5c575f7a96f8d101` |
| `../planet-vibes-v1/jungle-planet-vibe-v1.png` | Jungle atmosphere, route, refuge, and palette anchor | `5645190f72ecbf3d00e8c321c2d249d685352c100364544328e69a0cc1cbbc7f` |
| `../alien-species-v1/alien-fauna-species-v1.png` | Non-sapient jungle-fauna anatomy and silhouette anchor | `9d8bdbe14a735aa89d5ab33bb4f4f2436ea2340752a15cee69cbd557b8bda53e` |
| `../planet-vibes-v1/city-planet-vibe-v1.png` | City atmosphere, vertical depth, transit, and palette anchor | `255d854deae4104dac299ef5cca601b07c7022580f64f7d93719f03aba96d0a8` |
| `../../reference-sheets/frontier-station-v1/poc-ui/party-hud-component-direction-v1.png` | Authoritative card construction, selection, health, weapon, and command-slot visual reference | `968ad235b4e61131bce544925283433bdf48423cee003e2885982e4458f7f94b` |
| `../../reference-sheets/frontier-station-v1/poc-ui/character-portrait-direction-v1.png` | Authoritative portrait framing reference | `5b7d00b9d48a0e4aecf72f5380b5a05e16b823fedc72351767c1b77a130284ae` |

## Transient edit inputs

The final precise edits used preview inputs that are deliberately not
committed under the owner's final-only retention direction:

| Input role | SHA-256 |
|---|---|
| Arrow-free station preview before HUD standardization | `2a1f3a1d46f67794e0f565b5a842736e05e15fc5418940ef4485997ea024877e` |
| Jungle preview before HUD standardization | `4747cab1cddd159ac3f405024df0baca924971d8b42b1a3fbf38b8b5bae4f626` |
| Arrow-free city preview before HUD standardization | `1aa09590f0bb962fad86514eaeb662e90301851806d9443437b263be990d90ca` |

The approved station output above was also the placement and scale master for
the final jungle and city HUD edits.

## Review boundary

The owner approved the final visual direction after confirming that:

- all three frames share the same party-card layout and pause placement;
- disconnected cyan movement paths and arrowheads are absent;
- the station, jungle, and city remain visually distinct;
- exactly two party members remain readable; and
- hostile intent is communicated through source-specific red-orange
  telegraphs.

Approval authorizes only these gameplay-screenshot visual directions. It does
not define abilities, icon meanings, health values, weapon balance, attack
timing, target geometry, jungle or city encounters, production UI, or runtime
implementation. The station POC and its existing gameplay authority remain
unchanged.
