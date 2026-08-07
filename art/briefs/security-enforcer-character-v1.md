# Asset brief — Security Enforcer character v1

Status: Phase 3 production base approved by the project owner in the combined
Godot hostile gallery on 2026-08-07

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
Standard Skeleton (65), neutral FBX with skin, Blender weight repair, Mixamo
clips without skin, Blender cleanup, and exact GLB review in Godot.

Phase 3 publishes only holstered idle, in-place locomotion, a walk alias, and
the reinforced-right-hand contact socket. Phase 4 adds close-combat stance,
readable wind-up, one reinforced-forearm strike, contact pose, recovery, hit
reaction, and down after gameplay owns their authoritative timings. Prefer
conservative stock Mixamo locomotion and punch clips. Gameplay owns world
movement, target selection, range, timing, interruption, contact, and damage;
combat clips remain in-place.

## Approval gates

1. Project-owner approval of the reference — completed 2026-08-02.
2. Project-owner acceptance of this production brief — completed 2026-08-07.
3. Static T-pose source and Quad-10k topology review.
4. Agent validation of Mixamo marker placement and the Auto-Rigger preview.
5. Blender deformation review at shoulders, elbows, wrists, hips, knees,
   ankles, and armor boundaries.
6. Exact Phase 3 base GLB review in Godot at 7.5 m, 14.5 m, and 20 m.
7. Full-speed strike and recovery review remains a Phase 4 gate.

Live station-route activation remains blocked until Phase 4 provides the
authoritative hostile and combat state.
