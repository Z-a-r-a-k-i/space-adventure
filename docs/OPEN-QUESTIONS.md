# Open questions

There are no unresolved questions blocking the documented POC or bootstrap. New questions discovered during implementation or playtesting belong here before they silently become architectural assumptions.

## Already decided

- C# rather than GDScript for game-owned code.
- Godot 4.7.1 Mono and Windows desktop first.
- Single-player only.
- Authored demo before roguelite or procedural structure.
- The disabled frontier transfer station is the POC setting.
- Player starts alone and recruits the second character.
- Basic attacks repeat after an explicit target order; active abilities remain explicit.
- Combat automatically pauses when it starts; all later pausing is manual in the POC.
- Free camera yaw with constrained pitch and zoom, plus orientation reset.
- Vanguard and Operator protagonist kits with a Protector-style fixed companion.
- Vanguard, Operator, Protector, and the station survivor are four distinct
  human presentations on one normalized humanoid skeleton; a playthrough still
  controls only the selected protagonist plus Protector.
- Vanguard uses a separate two-handed carbine, Operator a separate compact
  pistol, and Protector a separate two-handed shotgun.
- Each POC human has one fixed complete outfit. Editable Blender sources retain
  a fitted undersuit and major armor pieces for future whole-outfit variants,
  but no individual armor slots or equipment system are implied.
- Party firearms are holstered during exploration and visibly drawn for
  combat.
- The hostile presentation pair is a compact body-ram security drone and a
  taller integrated-gun security sentry.
- The evacuation airlock visibly opens before completion presentation.
- Exact active abilities remain intentionally deferred to Phase 3; their
  props, clips, icons, and effects are not invented during asset planning. The
  concept-art shield is not assigned to an ability.
- Exactly two controllable characters in the POC; early UI and architecture must not rule out four later.
- Compact automatic group formation with no formation editor.
- Free-text input is the protagonist's exact spoken utterance; the game does not rewrite it into different dialogue.
- Player and model text remain untrusted: neither can establish facts, grant authority, or directly apply gameplay effects.
- Each character has a current action plus at most one replaceable pending primary action; there is no arbitrary queue.
- Eligible sapient NPCs expose conversation, but dialogue does not guarantee persuasion or remain available through every combat state.
- The first ship-combat experiment is the separately gated Phase 7
  escape-cutter slice in `SHIP-COMBAT-POC.md`; it does not expand the current
  station POC.
- Phase 7 begins with exactly two player crew, one cutter, one deterministic
  hostile ship, weapons, engines, shields, a fixed reactor budget, and one
  fixed battle.
- Its combat presentation uses separate strict-overhead player-left and
  enemy-right views, both ships pointing upward, with a central divider and no
  movement or trajectory lines.
- Hostile-ship and generalized vehicle boarding remain later combat-system
  features and are not part of the initial loop or Phase 7. The authored
  transition into the escape cutter is not a boarding system.
- Authored dialogue remains the POC critical path.
- An optional Codex CLI provider may use the developer's ChatGPT sign-in for private automated experiments; it remains replaceable and outside authoritative state.
- Dialogue profiles select model, reasoning effort, and Fast mode independently, with Sol/medium/Fast-off as the initial quality baseline.
- Low-poly presentation and a controlled, replaceable art pipeline.
- Every production combatant declares a handheld, integrated, or body-based
  attack source before model and animation approval; all three share the same
  authoritative gameplay boundary.
- Both structured automation and real graphical playtests are required.

## Phase 7 questions deferred until greybox

These questions do not block the current station POC or the first
deterministic ship-combat implementation:

- the final authored room topology and integer tuning;
- whether the ship encounter eventually follows the station completion in the
  same executable flow or remains a separately launched scenario;
- whether a later slice should simulate enemy crew;
- whether a later slice should add oxygen, fire, breaches, missiles, drones,
  or boarding; and
- how much of the escape cutter persists into a larger campaign or run
  structure.
