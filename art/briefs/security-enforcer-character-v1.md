# Asset brief — Security Enforcer character v1

Status: accepted gameplay role and approved visual reference; production-ready
brief awaiting explicit owner acceptance before offline source production

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

Use the normal humanoid pipeline: unrigged T-pose, Quad-10k retopology,
human-approved Mixamo markers, neutral FBX with skin, Blender weight repair,
Mixamo clips without skin, Blender cleanup, and exact GLB review in Godot.

Required coverage is idle, locomotion, close-combat stance, readable wind-up,
one reinforced-forearm strike, contact pose, recovery, hit reaction, and down.
Prefer conservative stock Mixamo locomotion and punch clips. Gameplay owns
world movement, target selection, range, timing, interruption, contact, and
damage; combat clips remain in-place.

## Approval gates

1. Project-owner approval of the reference — completed 2026-08-02.
2. Static T-pose source and Quad-10k topology review.
3. Human Mixamo marker confirmation.
4. Blender deformation review at shoulders, elbows, wrists, hips, knees,
   ankles, and armor boundaries.
5. Full-speed strike and recovery with a stable contact socket.
6. Exact GLB review in Godot at 14.5 m and 20 m.

Reference approval does not by itself authorize Tripo submission or live
replacement. The project owner must separately accept this production-ready
brief and the normal production preflight must be complete.
