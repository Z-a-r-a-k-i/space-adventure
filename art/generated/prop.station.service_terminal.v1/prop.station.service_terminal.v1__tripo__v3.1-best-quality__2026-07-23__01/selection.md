# Candidate selection

Status: candidate 01 selected for cleanup, then rejected at the bake-off
geometry hard gate; retained as a renderer/readability comparator

The run permits one initial candidate and one retry only for a named defect.
Candidate 01 was selected for bounded Blender cleanup. Its final experiment
decision is rejection because the best retained shell still violates the
geometry hard gate.

## Candidate 01

- Tripo task/model ID:
  `d3014f04-3de4-45ba-9502-90d6f80ea67b`
- Review state: selected for Blender cleanup; rejected after final topology
  validation; not production-integrated
- Credits: 55
- Retry used: no

Selection checks:

- recognizable freestanding pedestal, hood, and dominant screen;
- unambiguous front/back silhouette at tactical distance;
- no cables, keyboards, logos, text, hostile red sensor, or floating hologram;
- enough intact large forms for a sub-4,000-triangle cleanup;
- usable rear and underside rather than an image-card or hollow shell;
- violet remains the primary interaction accent after material cleanup;
- normalizable to 0.80 m x 1.30 m x 0.42 m without damaging proportions.

A retry is allowed only if candidate 01 fails one or more named checks above
and the defect is plausibly correctable by a second bounded generation.

## Provisional decision

Candidate 01 is the strongest and only generated candidate. It passes the
authored selection checks:

- the pedestal, protected hood, and dominant violet screen read immediately;
- the front and unlit rear have distinct silhouettes;
- front, three-quarter, rear, and high-angle review show a complete volumetric
  asset rather than an image card, hollow rear, or broken underside;
- the large-form armor breaks are suitable cleanup guides;
- no cables, keyboard, logo, legible glyph, red hostile sensor, green-dominant
  lighting, or floating hologram is present; and
- the proportions can be normalized to the brief without visible distortion.

The candidate's 1,916,879 raw triangles, 1,000,557 raw vertices, and current
4K generated texture are expected provider-source defects, not selection
failures. They require Blender retopology and a maximum 1024-pixel material
publication before use. Because the form is strong and those defects are
normal cleanup work, a second 55-credit generation would not be justified.

This raw-candidate selection was part of the bounded Phase 2 bake-off, not ADR
0016 production work. It advanced the candidate to cleanup only; it did not
grant production acceptance.

## Blender bake-off result

The best welded pass reaches 3,979 triangles, one mesh/material, one 1024²
texture set, and exact 0.80 × 1.30 × 0.42 m bounds. Its standardized renders
retain the pedestal, hood, cyan strips, and violet screen without visible
holes.

The editable provider shell retains 42 boundary edges and 64 non-manifold
edges. One generated material encodes all visual regions, so palette swapping
is less flexible than the preferred multi-material construction. The topology
defects fail the experiment hard gate even though tactical readability is
useful for comparison.

The first unwelded cleanup pass was rejected for fragmented surfaces. Its
named failure, exact hash, and topology metrics remain in `metadata.md`; the
duplicate transient binary and render folder are not carried in the cleaned
review branch. No second Tripo generation or additional credit was used.

## Material revision v2

Godot imported v1 mechanically, but direct station-lighting review found its
housing nearly black and its violet display unreadable beyond 7.5 m. A
separate v2 material review export addresses the presentation defect without
changing geometry.

V2 assigns 33 UV-confirmed screen faces to the exact
`mat.state.optional.violet` material with restrained emission and applies a
conservative shadow lift to a duplicated housing base-color map. It remains at
3,979 triangles, two materials, three 1024² maps, and the exact exported
envelope.

The v2 Blender review passed its material/readability checks. The retained v2
editable source and final v3 review export preserve that revision. Tactical
readability was reviewed directly in Godot at 7.5, 14.5, and 20 m.

## Orientation normalization v3

Godot validation exposed that v2's display was physically exported on `+Z`
despite its documentation naming `-Z` as front. That is a normalization
defect, not a reason to spend another 55-credit generation. The v2 editable
source remains immutable provenance and is superseded for review by a
non-overwriting v3 Blender export.

V3 rotates only the review mesh by π radians around exported `+Y`. It
preserves the retained shape, exact 0.80 × 1.30 × 0.42 m bounds, ground
contact, 3,979 triangles, two materials, emission, UVs, and all three embedded
texture payloads. Fresh import places the violet primitive at negative Z.

Direct Godot review at 7.5, 14.5, and 20 m found the violet interaction
accent readable, and fresh isolated-gallery import and health checks pass.
Candidate 01 remains rejected at the bake-off geometry hard gate and is
retained only as a renderer/orientation comparator. See
`derived/v3/publication.md`; any temporary review staging remains local and
ignored.
