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
- Vanguard is the only protagonist kit. Protector is the fixed recruit after a
  short solo tutorial fight; Operator is deferred.
- Vanguard, Protector, and the station survivor are the active human
  presentations on one normalized humanoid skeleton. A playthrough controls
  Vanguard alone until recruitment, then Vanguard plus Protector.
- Vanguard uses a separate two-handed carbine and Protector a separate
  two-handed shotgun. Operator and its separate compact pistol are deferred.
- Each POC human has one fixed complete outfit. Editable Blender sources retain
  a fitted undersuit and major armor pieces for future whole-outfit variants,
  but no individual armor slots or equipment system are implied.
- Party firearms are holstered during exploration and visibly drawn for
  combat.
- The hostile presentation pair is a mobile humanoid Security Enforcer with a
  reinforced-forearm body attack and a taller integrated-gun security sentry.
- The evacuation airlock visibly opens before completion presentation.
- The first-room boundary is an ordinary service door. The evacuation-airlock
  assembly is reserved for the final destination.
- The production station route uses five authored areas. The survivor choice
  unlocks the entry service door and its navigation link. Approaching it on an
  accepted path completes the authoritative door interaction and opens its
  leaves automatically. The far service door, Protector, and final airlock
  remain unavailable until the solo-combat victory is implemented.
- Vanguard Suppressive Fire is a position-targeted ability and Protector Guard
  Ally is an ally-targeted ability. Their final combat timing, clips, icons,
  and effects are defined in Phase 4. Operator Disruptor Shot is deferred with
  Operator; the concept-art shield remains unassigned.
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
- The static station structure, service terminal, ordinary service door, and
  evacuation airlock use dimensionally authored Blender sources without
  changing authored Godot gameplay authority. The wall utility remains gated.
- Every production combatant declares a handheld, integrated, or body-based
  attack source before model and animation approval; all three share the same
  authoritative gameplay boundary.
- Both structured automation and real graphical playtests are required.
- Visible station environment, NPCs, and combatants across the active route use
  reviewed production presentation before Phase 4 gameplay work starts.
  Hidden collision, navigation, interaction, and lighting primitives remain
  valid.
- Fight animation is selected and finalized alongside Phase 4 authoritative
  combat timing, not as a detached pre-combat animation batch.

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
