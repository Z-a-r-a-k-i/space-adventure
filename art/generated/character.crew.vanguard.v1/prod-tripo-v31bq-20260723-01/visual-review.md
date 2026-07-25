# Vanguard production visual-review cache

- Asset ID: `character.crew.vanguard.v1`
- Asset revision: `prod-tripo-v31bq-20260723-01`
- Related weapon: `weapon.crew.vanguard_carbine.v1`
- Related weapon revision: `prod-tripo-v31bq-20260723-01`
- Review date: 2026-07-24
- Decision: `revise`; production-art implementation postponed

## Reusable conclusion

The approved 2D Vanguard and carbine directions remain valid. The current 3D
Vanguard is not approved for live replacement, and the complete
character-plus-carbine assembly remains revise-required. The visible glove and
finger geometry stays open or intersects around the vertical grips. Keep the
live greybox while the rest of Phase 3 is implemented and playtested.

Do not repeat the review merely to restate PR status, prepare a handoff, or
confirm the postponement. The prior screenshot set was removed from the
repository under ADR 0019 because the editable Blender source and exact Godot
asset are the authoritative review surfaces.

## Recorded live-review findings

- Overall Vanguard appearance is not approved for live replacement.
- Tactical readability does not override the revise decision.
- The standalone carbine source remains useful, but assembly approval is
  separate.
- Open or intersecting glove and finger geometry around the two-handed grip
  blocks assembly approval.

## Reinspection conditions

Repeat direct Blender/Godot review only when at least one condition is true:

- the asset revision changed;
- repaired hand or glove geometry needs a new grip decision;
- a new candidate is being compared;
- a named question is not answered above;
- an explicitly assigned independent reviewer needs a fresh pass; or
- the project owner requests another visual review.

For the repair pass, inspect the repaired assembly directly in Blender and
play it in the Godot gallery. If a frozen crop is required for a named defect,
write only that crop under ignored `artifacts/` and do not add it here.
