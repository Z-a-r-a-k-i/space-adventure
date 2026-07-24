# Security-drone body bake-off preflight

Status: blocked before run creation; zero credits spent

Asset ID: `machine.security_drone.body.v1`

Date: 2026-07-24

## Decision

Do not create a Tripo run or upload an image for this asset yet. The bounded
body-only experiment has no approved provider-neutral input pack, and no
existing repository-provenanced Tripo run can be continued under this asset
ID.

## Evidence

- `art/briefs/security-drone-body-v1.md` scopes the test to a disposable
  machine body. Locomotion, complete generated legs/tracks/wheels, rigging,
  animation, weapons, and attack readiness are excluded.
- `art/experiments/3d-generator-bakeoff-2026-07.md` requires an isolated
  provider-neutral subject pack and forbids sending the whole tactical-combat
  concept as the image-to-3D input.
- `docs/TRIPO-PRODUCTION-HANDOFF.md` requires an approved reference filename
  and hash before upload.
- The latest approved machine turnaround is
  `machine.security.ram_drone.v1`. Its provenance explicitly identifies it as
  the complete Phase 4 production machine and not the disposable body-only
  bake-off subject. Substituting it would change the asset identity and phase.
- The older utility-walker smoke-test image is
  `test.utility_walker.disposable.v1`, not this asset. Its manifest records
  `production_asset: false`, `provider_upload_authorized: false`,
  `generation_submitted: false`, and `credits_spent: 0`. It depicts a complete
  four-legged walker and violates the body-only brief.
- No `art/generated/machine.security_drone.body.v1/` run, tracked or untracked
  task record, or raw output existed before this note.

Relevant verified hashes:

- Tactical-combat whole-scene concept:
  `E891F3D4C738AA6074D71A5B889B42DC2FFB96B585551E77D6DD4B31B99BADFC`
- Utility-walker smoke-test input:
  `45EA7A55997D2B25D236CF809EA11B923BAC904F937B1A5F3A7B2A2E2EC7318A`
- Production ram-drone turnaround:
  `F90DBC7C7EC58509077DA1AF1E50E1EF22789E4C10FAF6BDFDE1BDAB2A2663CF`

## Unblock requirement

Create and approve a dedicated body-only provider pack that matches
`art/briefs/security-drone-body-v1.md`, with exact file hashes and an explicit
upload authorization. It must remain visually and semantically distinct from
`machine.security.ram_drone.v1`.

Until that prerequisite exists:

- no Tripo credit should be spent;
- the ram-drone sheet must not be substituted;
- the utility-walker task must not be relabelled; and
- this row remains an honest preflight blocker rather than a generated failure.
