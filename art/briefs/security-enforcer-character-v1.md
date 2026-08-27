# Asset brief — Security Enforcer character v1

Status: Phase 3 production base approved on 2026-08-07; Phase 4 strike, hit,
and down actions are integrated in the solo-tutorial candidate pending
graphical approval.

## Identity

| Field | Value |
|---|---|
| Asset ID | `character.enemy.security_enforcer.v1` |
| Role | Non-sapient mobile close-range station hostile |
| Height | 1.90 m target, ±2% |
| Outfit | One fixed complete armored synthetic body |
| Attack source | `body`; reinforced right forearm and blunt knuckle |
| Contact socket | `socket.attack.contact.primary`, local `-Z` outward and `+Y` up |
| Reference | `art/reference-sheets/frontier-station-v1/poc-models/security-enforcer-turnaround-v1.png` |
| Rig profile | `art/rigs/crew-humanoid-v1.md` |

## Visual contract

Preserve ordinary human proportions, the precise T-pose, compact sealed head,
narrow red visor, continuous dark flexible undersuit, warm-gray fixed armor,
reinforced forearms, blunt hands, and broad tactical silhouette. The model must
read as an autonomous security frame rather than a person wearing a removable
helmet.

Do not add a human face, hair, weapon, shield, claws, digitigrade legs, extra
limbs, exposed pistons at deforming joints, floating plates, cables, backpack,
transforming parts, logos, text, or faction markings. Hostile red remains
subordinate to silhouette and attack motion.

## Mesh and runtime limits

- unrigged T-pose source followed by Tripo Smart Low-Poly v2 Quad retopology
  with a 10,000 target;
- continuous deforming surface through shoulders, elbows, wrists, hips, knees,
  and ankles beneath separately named rigid armor where useful;
- maximum 30,000 runtime triangles, six materials, one 2048 texture set,
  64 published bones, and four normalized skin influences per vertex;
- origin at ground center, unit scale, no shear, grounded boot soles, `+Y` up,
  and local `-Z` forward after publication; and
- collision, attack range, contact volume, telegraph, and damage remain in
  Godot rather than the presentation GLB.

## Rig and animation sequence

Use the normal humanoid pipeline: direct single-image unrigged 4K T-pose,
Quad-10k retopology, agent-validated Mixamo markers with symmetry and
Standard Skeleton (65), neutral FBX with skin, Blender weight repair, Blender
cleanup, and exact GLB review in Godot. Use matching-with-skin combat donors
for this asset because its no-skin rest representation fails the accepted-rig
compatibility gate.

The publication includes holstered idle, in-place locomotion, a walk alias,
the reinforced-right-hand contact socket, melee strike, and down. Damage
feedback uses numbers and impact effects; hit-reaction animation is prohibited.
Gameplay owns world movement, target selection, range, timing, interruption,
contact, and damage; combat clips remain presentation-only.

## Approval gates

1. Project-owner approval of the reference — completed 2026-08-02.
2. Project-owner acceptance of this production brief — completed 2026-08-07.
3. Static T-pose source and Quad-10k topology review.
4. Agent validation of Mixamo marker placement and the Auto-Rigger preview.
5. Blender deformation review at shoulders, elbows, wrists, hips, knees,
   ankles, and armor boundaries.
6. Exact Phase 3 base GLB review in Godot at 7.5 m, 14.5 m, and 20 m.
7. Full-speed strike, hit, down, and recovery review remains the final Phase 4
   graphical gate.

The solo-tutorial candidate now instantiates this exact publication against
the authoritative Phase 4 encounter state.
