# Candidate selection

Status: candidate 01 passed Blender and isolated Godot review; final owner visual review pending

The bounded run permits one initial candidate and one retry only for a named
defect that a second generation can plausibly correct. Candidate 01 is the
strongest and only generated candidate. No retry was used.

## Candidate 01

- Tripo task/model ID:
  `162d5614-e586-4a2b-a9b6-bbfe71d8caf9`
- Review state: selected to attempt the Blender hard gate
- Credits: 55
- Retry used: no
- Raw GLB SHA-256:
  `7D1B87029212C9DA8757DABBA7643B7811808F124FFEC7E80C4A7E546F969059`

Selection traits:

- preserves the approved enclosure, dominant grille, two copper-dark utility
  runs, broad clamps, compact junction box, navy/neutral palette, and tiny cyan
  status accent;
- reads as repeated noninteractive wall infrastructure rather than a terminal,
  door control, weapon, or complete machine;
- keeps the main masses chunky enough for tactical readability and aggressive
  reconstruction under the 3,000-triangle hard limit;
- has a complete volumetric side profile rather than a paper-thin image card;
- includes no violet, green, hostile red, label, logo, dangling hose, antenna,
  structural wall, or floating part; and
- is normalizable to the 1.20 x 0.80 x 0.22 m envelope without changing the
  approved front proportions.

Named defect:

- the generated rear is not a flat mounting plane; it repeats utility detail
  and contains a broad blue baked-color artifact.

This defect requires rear reconstruction in Blender. The candidate advances
only because the approved front/right form is strong and rear flattening can
be tested directly against the 30-minute cleanup cap. If it needs a redesign,
remains non-manifold or paper-thin, exceeds 3,000 triangles, or cannot keep all
geometry at or in front of `Z = 0`, it fails the bake-off hard gate.

A second Tripo candidate was not justified. There is no approved rear
elevation to add, so resubmitting the same two views would not target the
named rear defect and would spend another 55 credits without a controlled
change.

## Blender decision

Candidate 01 passed the Blender hard gate without a second Tripo generation.
The generated rear was removed and closed with a flat dark mounting face; the
blue rear artifact is absent. The exact derived GLB:

- is 485,876 bytes with SHA-256
  `104B03AAF161192610D9F8F1089B092C1D1EE6140F1333C13FAE3235F1E6BAF2`;
- contains 2,764 triangles, 1 mesh, 2 materials, and one 1024x1024
  base/normal/RM texture set;
- has exact exported bounds 1.20 x 0.80 x 0.22 m;
- uses `+Y` up and `-Z` front with all geometry at or in front of the rear
  `Z = 0` mounting plane;
- has signed exported AABB Z `[-0.22,0]`, with the detailed face visible
  from `-Z` and the flat mounting rear visible from `+Z`;
- preserves the approved front handedness after the orientation correction:
  vent left and broad utility runs right;
- has a bottom-center rear pivot;
- has no rig, animation, or collision; and
- resolves to a closed manifold surface after the diagnostic weld of standard
  glTF UV/normal attribute-seam duplicates.

The vent, enclosure, broad copper runs, clamps, and single cyan status strip
remain visually readable in the standardized Blender views and in the exact
derived review GLB at 7.5, 14.5, and 20 m in the isolated local Godot gallery.
Godot reported no scene-validation, editor, debugger, or dialog errors. The
correctly sized greybox fallback remains present and hidden in local staging,
and the live station route is unchanged.

This selection remains provisional pending the project owner's final visual
review. It is not a final bake-off provider decision because the Meshy result
or documented provider failure, Blender-only comparison, and weighted
scorecard are not complete for this asset row.
