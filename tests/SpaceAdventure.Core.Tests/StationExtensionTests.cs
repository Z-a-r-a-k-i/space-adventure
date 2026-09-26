using SpaceAdventure.Core;
using Xunit;

namespace SpaceAdventure.Core.Tests;

public sealed partial class CombatSessionTests
{
    private static readonly int[] ExtensionCenters = [18, 32, 46, 62];
    private static readonly int[] ExtensionCounts = [3, 4, 4, 5];
    private static readonly EntityId MedicId = new("actor.companion.medic");
    private static readonly EntityId MedicInteractionId = new("interaction.medic");
    private static readonly AbilityId HealId = new("ability.crew.medic.heal");
    private static readonly AbilityId FieldId = new("ability.crew.medic.healing_field");

    internal static StationEncounterPlacement[] ExtensionEncounterPlacements() => TestDefinition.Combat.Encounters.Skip(2)
        .Select((encounter, index) =>
        {
            double center = ExtensionCenters[index];
            var hostiles = encounter.HostileIds.Select((id, enemyIndex) => new StationHostilePlacement(id,
                new WorldPosition(center + 1 + enemyIndex / 3 * 2, 0, 5.5 + enemyIndex % 3 * 2.5), new WorldPosition(-1, 0, 0))).ToArray();
            return new StationEncounterPlacement(encounter.Id, hostiles[0].Position, HostilePlacements: hostiles);
        }).ToArray();

    // The former authored restart line five metres in front of each extension room's hostiles.
    private static CrewStart ExtensionCrewStart(int index)
    {
        var x = ExtensionCenters[index] - 4.0;
        return new CrewStart(new WorldPosition(x, 0, 7), new WorldPosition(x, 0, 8), new WorldPosition(x, 0, 9));
    }

    private static GameSession CreateAtMedicEncounter(StationEncounterPlacement? servicePlacement = null, StationRouteDefinition? definition = null,
        ISpatialPathfinder? pathfinder = null, CrewStart? crew = null)
    {
        crew ??= ExtensionCrewStart(0);
        var session = CreateBeforeMedicRecruitment(servicePlacement, definition, pathfinder, crew);
        RecruitMedic(session);
        // Recruitment sets the service objective; the waiting team spots the crew on the next tick.
        Assert.Equal(1, session.AdvanceTicks(1));
        var route = Observe(session);
        Assert.Equal(TestDefinition.Combat.Encounters[2].Id, route.Encounter!.Id);
        Assert.Equal(EncounterPhase.Readying, route.Encounter.Phase);
        Assert.True(session.IsPaused);
        Assert.Equal(new[] { crew.Protagonist, crew.Protector, crew.Medic!.Value }, route.Party.Select(actor => actor.Position));
        return session;
    }

    /// <summary>
    /// Wins the party fight and stages the crew at <paramref name="crew"/>, with the Vanguard in dialogue with the
    /// Medic. The service fight cannot start before she joins, so nothing is spotted while the crew take position.
    /// </summary>
    private static GameSession CreateBeforeMedicRecruitment(StationEncounterPlacement? servicePlacement, StationRouteDefinition? definition,
        ISpatialPathfinder? pathfinder, CrewStart crew, IEnumerable<StationVisionBlocker>? blockers = null)
    {
        var placements = ExtensionEncounterPlacements();
        if (servicePlacement is not null) { placements[0] = servicePlacement; }
        var session = CreateAtPartyEncounter(definition: definition, extensionPlacements: placements, pathfinder: pathfinder,
            serviceCrew: crew, blockers: blockers);
        WinAuthoredEncounter(session);
        MoveAndArrive(session, ProtectorId, crew.Protector);
        CompleteInteraction(session, MedicInteractionId, "medic.recruit");
        Assert.Equal(crew.Protagonist, Observe(session).Protagonist.Position);
        return session;
    }

    private static void RecruitMedic(GameSession session) =>
        Assert.True(session.Execute(new ChooseDialogueResponseCommand(new CommandId("medic.join"), ProtagonistId,
            MedicInteractionId, new DialogueResponseId("response.recruit_medic"))).Accepted);

    private static void EnterExtensionEncounter(GameSession session, int index)
    {
        var placement = ExtensionEncounterPlacements()[index];
        // Walk toward the next room; its guards stop the crew wherever they first see one of them.
        Assert.True(session.Execute(new MovePartyCommand(new CommandId($"extension.enter.{index}"),
            [ProtagonistId, ProtectorId, MedicId], ExtensionCrewStart(index).Protector)).Accepted);
        session.Execute(new SetPauseCommand(new CommandId("extension.travel.resume"), false));
        AdvanceUntil(session, route => route.Encounter!.Id == placement.EncounterId, 1000);
        Assert.Equal(EncounterPhase.Readying, Observe(session).Encounter!.Phase);
        Assert.True(session.IsPaused);
    }

    private static void WinAuthoredEncounter(GameSession session)
    {
        if (Observe(session).Encounter!.Phase == EncounterPhase.Readying) { ResumeIntoActiveCombat(session); }
        session.Execute(new SetPauseCommand(new CommandId("extension.combat.resume"), false));
        for (var tick = 0; tick < 2000 && Observe(session).Encounter!.Phase == EncounterPhase.Active; tick++)
        {
            var route = Observe(session);
            foreach (var actor in route.Party.Where(member => !member.Combat!.IsDefeated))
            {
                var target = route.Hostiles!.Where(enemy => !enemy.Combat.IsDefeated)
                    .OrderBy(enemy => enemy.Combat.Health).ThenBy(enemy => enemy.Id.Value, StringComparer.Ordinal).FirstOrDefault();
                if (target is null) { continue; }
                if (actor.Combat!.RememberedAttackTargetId is null)
                { Assert.True(Attack(session, actor.Id, target.Id).Accepted); }
                if (actor.CurrentAction?.Kind == PrimaryActionKind.Ability || actor.PendingAction?.Kind == PrimaryActionKind.Ability) { continue; }
                bool Ready(AbilityId abilityId) => actor.Combat.Cooldowns.Single(cd => cd.AbilityId == abilityId).RemainingTicks == 0;
                if (actor.Id == ProtagonistId)
                {
                    if (Ready(BurstId) && actor.Position.DistanceTo(target.Position) <= CombatTuning.Burst.RangeMeters)
                    { session.Execute(new UseAbilityCommand(new CommandId($"auto.burst.{session.Tick}"), actor.Id, BurstId, new EntityAbilityTarget(target.Id))); }
                    else if (Ready(InterruptId) && actor.Position.DistanceTo(target.Position) <= CombatTuning.ProtagonistAbility.RangeMeters)
                    { session.Execute(new UseAbilityCommand(new CommandId($"auto.interrupt.{session.Tick}"), actor.Id, InterruptId, new PositionAbilityTarget(target.Position))); }
                }
                else if (actor.Id == ProtectorId)
                {
                    if (Ready(TauntId)) { session.Execute(new UseAbilityCommand(new CommandId($"auto.taunt.{session.Tick}"), actor.Id, TauntId, new SelfAbilityTarget())); }
                    else if (Ready(BarrierId)) { session.Execute(new UseAbilityCommand(new CommandId($"auto.barrier.{session.Tick}"), actor.Id, BarrierId,
                        new BarrierAbilityTarget(new WorldPosition(actor.Position.X + .8, 0, actor.Position.Z), new WorldPosition(1, 0, 0)))); }
                }
                else
                {
                    var injured = route.Party.Where(member => !member.Combat!.IsDefeated && member.Position.DistanceTo(actor.Position) <= CombatTuning.DirectHeal.RangeMeters)
                        .OrderBy(member => (double)member.Combat!.Health / member.Combat.MaximumHealth).First();
                    if (Ready(HealId) && injured.Combat!.MaximumHealth - injured.Combat.Health >= 20)
                    { session.Execute(new UseAbilityCommand(new CommandId($"auto.heal.{session.Tick}"), actor.Id, HealId, new EntityAbilityTarget(injured.Id))); }
                    else if (Ready(FieldId)) { session.Execute(new UseAbilityCommand(new CommandId($"auto.field.{session.Tick}"), actor.Id, FieldId,
                        new PositionAbilityTarget(route.Party.Single(member => member.Id == ProtectorId).Position))); }
                }
            }
            session.AdvanceTicks(1);
        }
        Assert.NotEqual(EncounterPhase.Defeat, Observe(session).Encounter!.Phase);
        AdvanceUntil(session, route => route.Encounter!.Phase == EncounterPhase.Victory, 60);
    }

    [Fact]
    public void MedicHasIndependentKitAndHealingValidatesAtomicallyThenClampsAtRelease()
    {
        var session = CreateAtMedicEncounter();
        Assert.Equal(3, Observe(session).Party.Count);
        var medic = Observe(session).Party.Single(actor => actor.Id == MedicId);
        Assert.Equal(90, medic.Combat!.Health);
        foreach (var (command, expected) in new[]
        {
            (new UseAbilityCommand(new CommandId("heal.enemy"), MedicId, HealId, new EntityAbilityTarget(Observe(session).Hostiles![0].Id)),
                CommandRejectionCode.InvalidAbilityTarget),
            (new UseAbilityCommand(new CommandId("heal.owner"), ProtagonistId, HealId, new EntityAbilityTarget(ProtagonistId)),
                CommandRejectionCode.UnknownAbility),
            (new UseAbilityCommand(new CommandId("field.range"), MedicId, FieldId, new PositionAbilityTarget(new WorldPosition(100, 0, 0))),
                CommandRejectionCode.AbilityTargetOutOfRange),
        }) { Assert.Equal(expected, session.Execute(command).RejectionCode); }
        Assert.Null(Observe(session).Party.Single(actor => actor.Id == MedicId).PendingAction);
        ResumeIntoActiveCombat(session);
        AdvanceUntil(session, route => route.Party.Single(actor => actor.Id == MedicId).Combat!.Health < 90, 180);
        var before = Observe(session).Party.Single(actor => actor.Id == MedicId).Combat!.Health;
        var eventStart = session.Observe().LatestEventSequence;
        Assert.True(session.Execute(new UseAbilityCommand(new CommandId("heal.self"), MedicId, HealId, new EntityAbilityTarget(MedicId))).Accepted);
        session.AdvanceTicks(CombatTuning.DirectHeal.WindupTicks);
        var healing = Assert.Single(session.EventsSince(eventStart).Select(item => item.Detail).OfType<HealingAppliedEventDetail>());
        Assert.Equal(Math.Min(45, 90 - before), healing.Amount);
        Assert.Equal(90, healing.RemainingHealth);
        Assert.Equal(210, Observe(session).Party.Single(actor => actor.Id == MedicId).Combat!.Cooldowns.Single(cd => cd.AbilityId == HealId).RemainingTicks);
    }

    [Fact]
    public void HealingFieldIsFixedPausedPeriodicAndRetainsAttackIntent()
    {
        var session = CreateAtMedicEncounter();
        ResumeIntoActiveCombat(session);
        AdvanceUntil(session, route => route.Party.Any(actor => actor.Combat!.Health < actor.Combat.MaximumHealth), 180);
        var medic = Observe(session).Party.Single(actor => actor.Id == MedicId);
        var center = medic.Position;
        var target = Observe(session).Hostiles![^1].Id;
        Assert.True(Attack(session, MedicId, target).Accepted);
        session.AdvanceTicks(8);
        Assert.True(session.Execute(new UseAbilityCommand(new CommandId("field.deploy"), MedicId, FieldId, new PositionAbilityTarget(center))).Accepted);
        session.AdvanceTicks(CombatTuning.HealingField.WindupTicks);
        var field = Assert.IsType<HealingFieldObservation>(Observe(session).Encounter!.HealingField);
        var health = Observe(session).Party.Select(actor => actor.Combat!.Health).ToArray();
        session.Execute(new SetPauseCommand(new CommandId("field.pause"), true));
        session.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal(field, Observe(session).Encounter!.HealingField);
        Assert.Equal(health, Observe(session).Party.Select(actor => actor.Combat!.Health).ToArray());
        session.Execute(new SetPauseCommand(new CommandId("field.resume"), false));
        session.AdvanceTicks(CombatTuning.HealingField.RecoveryTicks);
        Assert.Equal(target, Observe(session).Party.Single(actor => actor.Id == MedicId).Combat!.RememberedAttackTargetId);
        session.Execute(new MoveActorCommand(new CommandId("medic.move"), MedicId, new WorldPosition(center.X - 3, 0, center.Z)));
        session.AdvanceTicks(31);
        Assert.Equal(center, Observe(session).Encounter!.HealingField!.Position);
        Assert.Contains(session.EventsSince(0), item => item.Detail is HealingAppliedEventDetail healing && healing.AbilityId == FieldId);
    }

    [Fact]
    public void SpottingAnyRecruitedCrewStartsTheFightForAllAndFourVictoriesUnlockAirlockThenBoardingCompletesOnce()
    {
        var session = CreateAtMedicEncounter();
        for (var index = 0; index < 4; index++)
        {
            Assert.Equal(ExtensionCounts[index], Observe(session).Hostiles!.Count);
            WinAuthoredEncounter(session);
            var victory = Observe(session);
            Assert.Equal(index + 3, victory.CompletedEncounterIds!.Count);
            Assert.All(victory.Party, actor =>
            {
                Assert.Equal(actor.Combat!.MaximumHealth, actor.Combat.Health);
                Assert.Null(actor.Combat.DefeatedAtTick);
                Assert.All(actor.Combat.Cooldowns, cooldown => Assert.Equal(0, cooldown.RemainingTicks));
            });
            if (index == 3) { break; }
            // Required crew need only be recruited and alive: the duo walking into view starts the
            // next fight for all three, and the Medic fights from where she was left behind.
            var next = ExtensionEncounterPlacements()[index + 1];
            var medic = victory.Party.Single(actor => actor.Id == MedicId);
            Assert.True(session.Execute(new MovePartyCommand(new CommandId($"duo.only.{index}"), [ProtagonistId, ProtectorId],
                ExtensionCrewStart(index + 1).Protector)).Accepted);
            AdvanceUntil(session, route => route.Encounter!.Id == next.EncounterId, 1000);
            var started = Observe(session);
            Assert.Equal(EncounterPhase.Readying, started.Encounter!.Phase);
            Assert.True(session.IsPaused);
            Assert.Contains(started.Encounter.SpottedActorId, new EntityId?[] { ProtagonistId, ProtectorId });
            Assert.Equal(medic.Position, started.Party.Single(actor => actor.Id == MedicId).Position);
            Assert.Equal(3, started.Party.Count);
            Assert.All(started.Hostiles!, hostile => Assert.Equal(EncounterPhase.Readying, hostile.EncounterPhase));
        }
        Assert.True(FindInteraction(Observe(session), new EntityId("interaction.evacuation_airlock")).CanInteract);
        CompleteInteraction(session, new EntityId("interaction.evacuation_airlock"), "airlock.open");
        var boarding = new EntityId("interaction.escape_cutter.board");
        Assert.True(session.Execute(new InteractCommand(new CommandId("board.all"), ProtagonistId, boarding)).Accepted);
        AdvanceUntil(session, route => route.Phase == ScenarioPhase.Completed, 1000);
        Assert.Single(session.EventsSince(0), item => item.Type == GameplayEventType.ScenarioCompleted);
        Assert.False(session.Execute(new InteractCommand(new CommandId("board.again"), MedicId, boarding)).Accepted);
    }

    [Fact]
    public void ExtensionDefeatRetryPreservesRecruitsProgressAndClearsFieldProjectilesAndOrders()
    {
        var session = CreateAtMedicEncounter();
        var spotted = Observe(session).Party.Select(actor => (actor.Id, actor.Position, actor.Facing)).ToArray();
        ResumeIntoActiveCombat(session);
        var position = Observe(session).Party.Single(actor => actor.Id == MedicId).Position;
        Assert.True(session.Execute(new MoveActorCommand(new CommandId("retry.fall.back"), ProtagonistId, new WorldPosition(10, 0, 4))).Accepted);
        Assert.True(session.Execute(new UseAbilityCommand(new CommandId("retry.field"), MedicId, FieldId, new PositionAbilityTarget(position))).Accepted);
        AdvanceUntil(session, route => route.Encounter!.HealingField is not null, 60);
        AdvanceUntil(session, route => route.Encounter!.Phase == EncounterPhase.Defeat, 1800);
        Assert.NotEqual(spotted[0].Position, Observe(session).Protagonist.Position);
        var id = Observe(session).Encounter!.Id;
        Assert.True(session.Execute(new RestartEncounterCommand(new CommandId("extension.retry"), id)).Accepted);
        var retry = Observe(session);
        Assert.Equal(2, retry.Encounter!.Attempt);
        Assert.Equal(2, retry.CompletedEncounterIds!.Count);
        Assert.Equal(3, retry.Party.Count);
        Assert.Null(retry.Encounter.HealingField);
        Assert.Empty(retry.Encounter.Projectiles!);
        Assert.Equal(RoutePowerMode.ServiceRerouted, retry.RoutePowerMode);
        // The retry restores the start of this encounter only: every crew member is back where the
        // service team first saw it, every hostile at its placement, with no carried-over orders.
        Assert.Equal(spotted, retry.Party.Select(actor => (actor.Id, actor.Position, actor.Facing)));
        var placement = ExtensionEncounterPlacements()[0];
        Assert.Equal(placement.HostilePlacements!.Select(hostile => hostile.ActorId.Value).Order(),
            retry.Hostiles!.Select(hostile => hostile.Id.Value).Order());
        Assert.All(retry.Hostiles!, hostile =>
        {
            Assert.Equal(placement.HostilePlacements!.Single(item => item.ActorId == hostile.Id).Position, hostile.Position);
            Assert.Equal(hostile.Combat.MaximumHealth, hostile.Combat.Health);
            Assert.Null(hostile.CurrentAction);
            Assert.Null(hostile.Combat.TauntedBy);
        });
        Assert.All(retry.Party, actor =>
        {
            Assert.Equal(actor.Combat!.MaximumHealth, actor.Combat.Health);
            Assert.Null(actor.CurrentAction);
            Assert.Null(actor.PendingAction);
            Assert.Null(actor.Combat.RememberedAttackTargetId);
        });
    }

    [Fact]
    public void RouteValidationRejectsObjectivesAndPlacementsThatWouldStrandTheRoute()
    {
        // Encounters start only under the objective the route sets before them.
        Assert.Throws<InvalidDataException>(() => VisionDefinition(json =>
            json["combat"]!["encounters"]![1]!["objective"]!["id"] = "objective.renamed_party"));
        Assert.Throws<InvalidDataException>(() => VisionDefinition(json =>
            json["combat"]!["encounters"]![3]!["objective"]!["id"] = "objective.clear_service"));
        Assert.Throws<InvalidDataException>(() => VisionDefinition(json =>
            json["combat"]!["encounters"]![4]!["objective"]!["id"] = "objective.recruit_medic"));

        var full = CreateLayout();
        var extensions = full.Encounters.Skip(2).ToArray();
        StationRouteLayout With(IEnumerable<StationEncounterPlacement> encounters, StationEncounterPlacement? party = null,
            IEnumerable<StationVisionBlocker>? blockers = null) =>
            new(full.ProtagonistStart, full.Actors, full.Interactions, full.Encounter, party ?? full.PartyEncounter, encounters, blockers);
        void Rejected(StationRouteLayout layout) =>
            Assert.Throws<InvalidDataException>(() => GameSession.CreateStationRoute(TestDefinition, layout, new DirectPathfinder()));

        // The airlock needs every authored encounter, so each must be placed.
        Rejected(With(extensions.Skip(1)));
        // No crew placements are needed: retry restores the crew where a hostile first saw them.
        var perIdParty = full.PartyEncounter! with
        {
            HostilePlacements = [new(MainEnforcerId, new WorldPosition(-2, 0, 8), new WorldPosition(0, 0, -1)),
                new(SentryId, new WorldPosition(2.5, 0, 10.5), new WorldPosition(0, 0, -1))],
        };
        Assert.NotNull(GameSession.CreateStationRoute(TestDefinition, With(extensions, perIdParty), new DirectPathfinder()));
        // Every later encounter still needs a per-ID hostile placement.
        Rejected(With(extensions.Select((placement, index) => index == 0 ? placement with { HostilePlacements = null } : placement)));
        // A hostile inside a sight blocker could never be seen, targeted, or defeated.
        var spawn = extensions[0].HostilePlacements![0].Position;
        Rejected(With(extensions, blockers: [new StationVisionBlocker("test.spawn.wall",
            new WorldPosition(spawn.X - .5, 0, spawn.Z - .5), new WorldPosition(spawn.X + .5, 3, spawn.Z + .5))]));
    }

    [Fact]
    public void FinalFightRetryPreservesFiveWinsAndBoardingPathFailureMutatesNoCrewOrders()
    {
        var pathfinder = new BoardingTestPathfinder();
        var session = CreateAtMedicEncounter(pathfinder: pathfinder);
        for (var index = 0; index < 3; index++)
        {
            WinAuthoredEncounter(session);
            EnterExtensionEncounter(session, index + 1);
        }
        ResumeIntoActiveCombat(session);
        AdvanceUntil(session, route => route.Encounter!.Phase == EncounterPhase.Defeat, 1800);
        var finalId = Observe(session).Encounter!.Id;
        Assert.True(session.Execute(new RestartEncounterCommand(new CommandId("final.retry"), finalId)).Accepted);
        Assert.Equal(5, Observe(session).CompletedEncounterIds!.Count);
        Assert.Equal(2, Observe(session).Encounter!.Attempt);
        WinAuthoredEncounter(session);
        CompleteInteraction(session, new EntityId("interaction.evacuation_airlock"), "final.airlock");
        pathfinder.BlockMedic = true;
        var before = Observe(session).Party;
        var board = new EntityId("interaction.escape_cutter.board");
        var rejection = session.Execute(new InteractCommand(new CommandId("board.unreachable"), ProtagonistId, board));
        Assert.Equal(CommandRejectionCode.DestinationUnreachable, rejection.RejectionCode);
        Assert.Equal(before.Select(actor => (actor.Id, actor.Position, actor.CurrentAction, actor.PendingAction, actor.Combat!.Health)),
            Observe(session).Party.Select(actor => (actor.Id, actor.Position, actor.CurrentAction, actor.PendingAction, actor.Combat!.Health)));
        pathfinder.BlockMedic = false;
        Assert.True(session.Execute(new InteractCommand(new CommandId("board.retry"), ProtagonistId, board)).Accepted);
        AdvanceUntil(session, route => route.Phase == ScenarioPhase.Completed, 600);
        Assert.Single(session.EventsSince(0), item => item.Type == GameplayEventType.ScenarioCompleted);
    }

    private sealed class BoardingTestPathfinder : ISpatialPathfinder
    {
        public bool BlockMedic { get; set; }
        public SpatialPathResult FindPath(EntityId actorId, WorldPosition origin, WorldPosition destination)
        {
            _ = origin;
            return BlockMedic && actorId == MedicId ? SpatialPathResult.Unreachable : SpatialPathResult.Reachable([destination]);
        }
    }
}
