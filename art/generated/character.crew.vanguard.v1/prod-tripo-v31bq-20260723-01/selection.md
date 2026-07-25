# Candidate selection

Status: candidate 02 provisionally selected; final owner visual review pending

The run permits one initial candidate and one retry only for a named defect.
Any selection is provisional pending the owner's final visual review.

## Candidate 01

- Tripo task/model ID: `b18e331b-0699-453d-ad8e-a71ffa0e373c`
- Review state: rejected; preserved as immutable evidence
- Credits: `0`
- Raw GLB:
  `raw/character.crew.vanguard.v1__raw-base__tripo-v3.1__candidate-01.glb`
- Raw cache record: `raw-export.manifest.json` (46,718,744 bytes)

Selection checks:

- same approved adult male identity in all views;
- sturdy Vanguard proportions distinct from Protector;
- complete unarmed body in a neutral A-pose with no firearm geometry;
- continuous deforming undersuit/body through shoulders, elbows, hips, and
  knees, with useful rigid armor boundaries;
- complete rear armor and empty carry hardware;
- readable hands, feet, face, and all major joints without fusion;
- restrained navy, warm-gray, and cyan palette;
- plausible fit to `rig.crew.humanoid.v1` without hierarchy redesign; and
- normalizable to 1.82 m without damaging proportions.

A retry is allowed only if candidate 01 fails a named check above and the
failure is plausibly correctable by one bounded generation. Do not generate
Vanguard animations until the selected static body, separate carbine assembly,
shared rig, and Godot retarget proof are established.

Candidate 01 passes the unarmed silhouette, rear-armor, hand, foot, joint,
carry-hardware, proportion, and broad part-boundary checks. It definitively
fails the identity/rig-fit check: isolated raw-GLB renders confirm two complete
faces on opposite sides of the head. This is not a viewer artifact, head-bone
rotation, or animation-layer problem.

The defect is plausibly caused by three-view head ambiguity and is therefore a
valid reason for the single allowed retry. Candidate 02 must add the approved
front-right three-quarter reference through a truthful auxiliary-image mode;
it must not be mislabeled as a strict Right orthographic elevation. Candidate
01 remains immutable evidence while the raw-GLB audit confirms the diagnosis.

## Candidate 02

- Input: approved `input/front-right-3q.png`
- Mode: HD Model single-image, v3.1 Best Quality, Ultra
- Parts / 8K: both disabled
- Displayed cost: 55 credits
- Tripo task/model ID: `c889d05a-90fe-4186-85eb-12d4eceafb35`
- Review state: accepted for Blender cleanup and shared-rig proof
- Charged credits: 55
- Raw GLB:
  `raw/character.crew.vanguard.v1__raw__tripo-v3.1__candidate-02.glb`
- Raw cache record: `raw-export.manifest.json` (58,610,072 bytes)

The single-image retry is deliberate. Studio's other unlabeled multiple-image
control is a 220-credit batch of four independent models, not a truthful
auxiliary-view reconstruction, so it was not submitted. The approved
three-quarter input is the one view that unambiguously relates face direction
to torso direction without being mislabeled as a strict Right elevation.

Candidate 02 is the provisional selection. It has one correctly oriented face
and a clean back of head, preserves the Vanguard identity and navy/warm-gray/
cyan palette, remains unarmed, has complete hands/feet/carry hardware, and
keeps a continuous neutral body suitable for the shared humanoid rig. It is
less densely armored than candidate 01 but does not require a head rebuild,
which is the decisive production advantage. No further candidate is allowed
or needed.

## Donor-rig disposition

Candidate 02 is also the sole Vanguard donor-rig source. Tripo Auto Rig used
`v1.0 - Good for Humanoid` on the same visible task/model ID for 20 credits.
This is not a third static candidate and does not change the provisional
visual selection.

The untouched idle, combined diagnostic, separately identified walk, dialogue
idle, and dialogue-listen exports are preserved in the ignored raw cache and
recorded in `raw-export.manifest.json`. Blender's 26-joint semantic mapping
onto `rig.crew.humanoid.v1` passed fresh export/re-import and exact Godot
gallery playback for idle, holstered locomotion, dialogue idle, and dialogue
listen. The provider skeleton, skin weights, and clips remain diagnostic donor
inputs only. This accepts the Vanguard-first retarget workflow provisionally;
the visible glove/grip revision and remaining weapon-handling presentation
still block final profile acceptance.
