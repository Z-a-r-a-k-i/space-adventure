using SpaceAdventure.Core;
using Xunit;

namespace SpaceAdventure.Core.Tests;

// Station fights start when a living hostile of the next encounter sees a living recruited crew
// member within hostile detection range, with no trigger zones, teleports or pop-in.
public sealed partial class CombatSessionTests
{
    private static readonly EncounterId SoloEncounterId = new("encounter.station.solo_tutorial");

    private static StationRouteDefinition DetectionDefinition(double meters) =>
        VisionDefinition(json => json["vision"]!["hostile_detection_meters"] = meters);

    // Opens the entry door, which sets the solo fight's objective, where the Enforcer cannot yet see the Vanguard.
    private static GameSession OpenEntryDoor(StationRouteLayout layout, StationRouteDefinition? definition = null)
    {
        var session = NewVisionSession(layout, definition);
        CompleteInteraction(session, new EntityId("interaction.survivor"), "detect.survivor");
        Assert.True(session.Execute(new ChooseDialogueResponseCommand(new CommandId("detect.choice"), ProtagonistId,
            new EntityId("interaction.survivor"), new DialogueResponseId("response.reroute_service_power"))).Accepted);
        CompleteInteraction(session, new EntityId("interaction.service_door.entry"), "detect.entry");
        var route = Observe(session);
        Assert.Equal(TestDefinition.CombatThresholdObjective.Id, route.Objective.Id);
        Assert.Equal(EncounterPhase.Dormant, route.Encounter!.Phase);
        Assert.Equal(SoloDoorApproach, route.Protagonist.Position);
        return session;
    }

    // Steps a paused session one tick at a time; returns the last observation before a fight began and the first after.
    private static (StationRouteObservation Before, StationRouteObservation After) StepUntilEncounterStarts(GameSession session, int maximumTicks)
    {
        var before = Observe(session);
        var after = before;
        for (var tick = 0; tick < maximumTicks && after.Encounter!.Phase != EncounterPhase.Readying; tick++)
        {
            before = after;
            session.StepWhilePaused(1);
            after = Observe(session);
        }

        Assert.Equal(EncounterPhase.Readying, after.Encounter!.Phase);
        return (before, after);
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(-0.99, false)]
    public void HostilesNoticeCrewOnlyWithinTheirInclusiveDetectionRange(double stopZ, bool starts)
    {
        // An explicit range puts the first stop exactly on it and the second one centimetre outside.
        const double detection = 6;
        var hostile = new WorldPosition(-10, 0, -1 - detection);
        var session = OpenEntryDoor(VisionLayout(solo: CreateSoloPlacement(hostile)), DetectionDefinition(detection));

        // The crew already see the Enforcer from the doorway; it has not noticed them.
        Assert.True(Sees(session, EnforcerId));
        Assert.True(SoloDoorApproach.DistanceTo(hostile) > detection);
        session.AdvanceTicks(30);
        Assert.Equal(EncounterPhase.Dormant, Observe(session).Encounter!.Phase);
        Assert.DoesNotContain(session.EventsSince(0), item => item.Type == GameplayEventType.EncounterStarted);

        Pause(session, true);
        var stop = new WorldPosition(-10, 0, stopZ);
        Assert.True(session.Execute(new MoveActorCommand(new CommandId("detect.approach"), ProtagonistId, stop)).Accepted);
        if (!starts)
        {
            session.StepWhilePaused(120);
            var waiting = Observe(session);
            Assert.Equal(stop, waiting.Protagonist.Position);
            Assert.True(waiting.Protagonist.Position.DistanceTo(hostile) > detection);
            Assert.Equal(EncounterPhase.Dormant, waiting.Encounter!.Phase);
            return;
        }

        var (before, after) = StepUntilEncounterStarts(session, 120);
        Assert.Equal(EncounterPhase.Dormant, before.Encounter!.Phase);
        Assert.True(before.Protagonist.Position.DistanceTo(hostile) > detection);
        // It starts on the arrival tick, exactly at the detection range.
        Assert.Equal(stop, after.Protagonist.Position);
        Assert.Equal(detection, after.Protagonist.Position.DistanceTo(hostile));
        Assert.Equal(EnforcerId, after.Encounter!.SpotterId);
        Assert.Equal(ProtagonistId, after.Encounter.SpottedActorId);
    }

    [Fact]
    public void AWallHidesCrewInsideDetectionRangeUntilTheFirstTickWithAClearLine()
    {
        // Explicit range so the whole walk out from behind the wall stays inside it.
        const double detection = 8;
        var hostile = new WorldPosition(-10, 0, -2);
        var wall = new StationVisionBlocker("test.detection.wall", new WorldPosition(-10.5, 0, -.2), new WorldPosition(-9.5, 3, .2));
        var session = OpenEntryDoor(VisionLayout([wall], CreateSoloPlacement(hostile)), DetectionDefinition(detection));

        Assert.True(SoloDoorApproach.DistanceTo(hostile) <= detection);
        Assert.False(Sees(session, EnforcerId));
        session.AdvanceTicks(30);
        Assert.Equal(EncounterPhase.Dormant, Observe(session).Encounter!.Phase);

        Pause(session, true);
        Assert.True(session.Execute(new MoveActorCommand(new CommandId("detect.step.out"), ProtagonistId, new WorldPosition(-4, 0, 4.85))).Accepted);
        var (before, after) = StepUntilEncounterStarts(session, 120);
        // In range but still behind the wall on the previous tick; spotted on the first tick the line clears.
        Assert.True(before.Protagonist.Position.DistanceTo(hostile) <= detection);
        Assert.DoesNotContain(before.VisibleHostiles, enemy => enemy.Id == EnforcerId);
        Assert.Contains(after.VisibleHostiles, enemy => enemy.Id == EnforcerId);
        Assert.Equal(EnforcerId, after.Encounter!.SpotterId);
    }

    [Fact]
    public void AClosedDoorHidesTheNextRoomUntilItOpensAndTheFightStartsThatTick()
    {
        var detection = TestDefinition.Vision.HostileDetectionMeters;
        var serviceDoorId = new EntityId("interaction.service_door.service");
        var door = new StationVisionBlocker("test.service.door", new WorldPosition(11.9, 0, 0), new WorldPosition(12.1, 3, 16), serviceDoorId);
        // The crew wait just before the door, the service team just past it.
        var crew = new CrewStart(new WorldPosition(10.5, 0, 7), new WorldPosition(10.5, 0, 8), new WorldPosition(10.5, 0, 9));
        var service = ExtensionEncounterPlacements()[0];
        service = service with
        {
            HostilePlacements = service.HostilePlacements!.Select(hostile => hostile with { Position = new WorldPosition(14, 0, hostile.Position.Z) }).ToArray(),
        };
        var session = CreateBeforeMedicRecruitment(service, null, null, crew, [door]);
        RecruitMedic(session);
        session.AdvanceTicks(90);

        // The objective is set and all three are recruited and alive, each inside detection range of a guard.
        var waiting = Observe(session);
        Assert.Equal(TestDefinition.Combat.Encounters[2].Objective!.Id, waiting.Objective.Id);
        Assert.Equal(3, waiting.Party.Count);
        Assert.All(waiting.Party, actor => Assert.Contains(service.HostilePlacements!,
            hostile => hostile.Position.DistanceTo(actor.Position) <= detection));
        Assert.NotEqual(service.EncounterId, waiting.Encounter!.Id);
        Assert.DoesNotContain(waiting.VisibleHostiles, enemy => enemy.EncounterId == service.EncounterId);

        var sequence = session.Observe().LatestEventSequence;
        Assert.True(session.Execute(new InteractCommand(new CommandId("detect.open.service"), ProtagonistId, serviceDoorId)).Accepted);
        AdvanceUntil(session, route => route.Encounter!.Id == service.EncounterId, 120);
        var events = session.EventsSince(sequence);
        var opened = Assert.Single(events, item => item.Detail is InteractionCompletedEventDetail completed
            && completed.InteractionId == serviceDoorId);
        var started = Assert.Single(events, item => item.Type == GameplayEventType.EncounterStarted);
        Assert.Equal(opened.Tick, started.Tick);
        Assert.Equal(EncounterPhase.Readying, Observe(session).Encounter!.Phase);
    }

    [Fact]
    public void CrewFightFromWhereTheyAreSpottedWithoutFacingSnapAndRetryReturnsThemThere()
    {
        var detection = TestDefinition.Vision.HostileDetectionMeters;
        // Off to the side of the Vanguard's walk east from the doorway, and out of range of the doorway itself.
        var hostile = new WorldPosition(4, 0, 3);
        Assert.True(SoloDoorApproach.DistanceTo(hostile) > detection);
        var session = OpenEntryDoor(VisionLayout(solo: CreateSoloPlacement(hostile)));

        Pause(session, true);
        var walk = new CommandId("detect.walk.east");
        Assert.True(session.Execute(new MoveActorCommand(walk, ProtagonistId, new WorldPosition(10, 0, SoloDoorApproach.Z))).Accepted);
        var (before, after) = StepUntilEncounterStarts(session, 300);
        var walking = before.Protagonist;
        var spotted = after.Protagonist;
        Assert.Equal(walk, walking.CurrentAction!.CommandId);
        Assert.True(spotted.Position.DistanceTo(hostile) <= detection);

        // No teleport: the start tick was one ordinary step east along the same line.
        Assert.Equal(SoloDoorApproach.Z, spotted.Position.Z);
        Assert.Equal(walking.Position.X + TestDefinition.Protagonist.MovementSpeedMetersPerSecond / GameSession.TicksPerSecond,
            spotted.Position.X, 9);
        // No facing snap: still heading east rather than turned toward the Enforcer off to the side.
        Assert.Equal(new WorldPosition(1, 0, 0), walking.Facing);
        Assert.Equal(walking.Facing, spotted.Facing);
        // Orders and attack intent are cleared; the Enforcer has not moved.
        Assert.Null(spotted.CurrentAction);
        Assert.Null(spotted.PendingAction);
        Assert.Null(spotted.Combat!.RememberedAttackTargetId);
        Assert.Equal(hostile, Assert.Single(after.Hostiles!).Position);
        Assert.True(session.IsPaused);

        // Readying does not move the crew either.
        session.StepWhilePaused(after.Encounter!.TransitionTicksRemaining);
        Assert.Equal(EncounterPhase.Active, Observe(session).Encounter!.Phase);
        Assert.Equal(spotted.Position, Observe(session).Protagonist.Position);

        // Fall back during the fight and lose: retry returns the Vanguard to where it was spotted, facing as it was.
        Pause(session, false);
        Assert.True(session.Execute(new MoveActorCommand(new CommandId("detect.fall.back"), ProtagonistId, SoloDoorApproach)).Accepted);
        AdvanceUntil(session, route => route.Encounter!.Phase == EncounterPhase.Defeat, 1500);
        var defeated = Observe(session);
        Assert.NotEqual(spotted.Position, defeated.Protagonist.Position);
        Assert.True(session.Execute(new RestartEncounterCommand(new CommandId("detect.retry"), SoloEncounterId)).Accepted);
        var retried = Observe(session);
        Assert.Equal(2, retried.Encounter!.Attempt);
        Assert.Equal(EncounterPhase.Readying, retried.Encounter.Phase);
        Assert.Equal(spotted.Position, retried.Protagonist.Position);
        Assert.Equal(spotted.Facing, retried.Protagonist.Facing);
        Assert.Equal(hostile, Assert.Single(retried.Hostiles!).Position);
    }

    [Fact]
    public void TheSpotterAndTheCrewMemberItSawAreReportedAndItsWholeTeamWakesTogether()
    {
        var detection = TestDefinition.Vision.HostileDetectionMeters;
        // The Sentry sees only the Protector, not the Vanguard beside it; the Enforcer sees no one.
        var sentry = new WorldPosition(PartyCrewStart.Protector.X + detection - .5, 0, PartyCrewStart.Protector.Z);
        var enforcer = new WorldPosition(-2, 0, 20);
        Assert.True(PartyCrewStart.Protagonist.DistanceTo(sentry) > detection);
        Assert.True(PartyCrewStart.Protagonist.DistanceTo(enforcer) > detection && PartyCrewStart.Protector.DistanceTo(enforcer) > detection);
        var session = CreateAtPartyEncounter(CreatePartyPlacement() with
        {
            HostileSpawnPosition = enforcer,
            AdditionalHostiles = [new StationActorPlacement(SentryId, sentry)],
        });

        var route = Observe(session);
        Assert.Equal(SentryId, route.Encounter!.SpotterId);
        Assert.Equal(ProtectorId, route.Encounter.SpottedActorId);
        var starts = session.EventsSince(0).Where(item => item.Type == GameplayEventType.EncounterStarted)
            .Select(item => item.Detail).ToArray();
        Assert.Equal(new GameplayEventDetail?[]
        {
            new EncounterEventDetail(SoloEncounterId, 1, EnforcerId, ProtagonistId),
            new EncounterEventDetail(PartyEncounterId, 1, SentryId, ProtectorId),
        }, starts);

        // The Enforcer that saw no one is readied with its team and joins the fight with it.
        Assert.Equal(2, route.Hostiles!.Count);
        Assert.All(route.Hostiles, hostile =>
        {
            Assert.Equal(PartyEncounterId, hostile.EncounterId);
            Assert.Equal(EncounterPhase.Readying, hostile.EncounterPhase);
            Assert.Equal(1, hostile.EncounterAttempt);
        });
        ResumeIntoActiveCombat(session);
        Assert.All(Observe(session).Hostiles!, hostile => Assert.Equal(EncounterPhase.Active, hostile.EncounterPhase));
        session.AdvanceTicks(30);
        var closing = Observe(session).Hostiles!.Single(hostile => hostile.Id == MainEnforcerId);
        Assert.NotNull(closing.CurrentAction?.CombatTargetId);
        Assert.True(closing.Position.DistanceTo(PartyCrewStart.Protagonist) < enforcer.DistanceTo(PartyCrewStart.Protagonist));
    }

    [Fact]
    public void UnrecruitedCompanionsAreNeverSpotted()
    {
        var detection = TestDefinition.Vision.HostileDetectionMeters;
        // The Enforcer stands between the waiting Protector and Medic, out of range of the doorway.
        var hostile = new WorldPosition(5, 0, 6);
        var layout = VisionLayout(solo: CreateSoloPlacement(hostile));
        Assert.Equal(2, layout.Actors.Count);
        Assert.All(layout.Actors, waiting => Assert.True(waiting.Position.DistanceTo(hostile) <= detection));
        Assert.True(SoloDoorApproach.DistanceTo(hostile) > detection);
        var session = OpenEntryDoor(layout);

        session.AdvanceTicks(60);
        Assert.Single(Observe(session).Party);
        Assert.Equal(EncounterPhase.Dormant, Observe(session).Encounter!.Phase);
        Assert.DoesNotContain(session.EventsSince(0), item => item.Type == GameplayEventType.EncounterStarted);

        Assert.True(session.Execute(new MoveActorCommand(new CommandId("detect.approach"), ProtagonistId, new WorldPosition(0, 0, 4.85))).Accepted);
        AdvanceUntil(session, route => route.Encounter!.Phase == EncounterPhase.Readying, 300);
        Assert.Equal(ProtagonistId, Observe(session).Encounter!.SpottedActorId);
    }

    [Fact]
    public void AHostileInRangeWaitsForItsFightsObjective()
    {
        var detection = TestDefinition.Vision.HostileDetectionMeters;
        var session = NewVisionSession(VisionLayout());
        var survivor = new EntityId("interaction.survivor");
        CompleteInteraction(session, survivor, "detect.gate.survivor");
        var talking = Observe(session);
        Assert.True(talking.Protagonist.Position.DistanceTo(Assert.Single(talking.Hostiles!).Position) <= detection);
        Assert.True(Sees(session, EnforcerId));
        Assert.True(session.Execute(new ChooseDialogueResponseCommand(new CommandId("detect.gate.choice"), ProtagonistId,
            survivor, new DialogueResponseId("response.reroute_service_power"))).Accepted);
        session.AdvanceTicks(60);
        Assert.Equal(TestDefinition.EntryDoorObjective.Id, Observe(session).Objective.Id);
        Assert.Equal(EncounterPhase.Dormant, Observe(session).Encounter!.Phase);

        // Opening the door sets the fight's objective, and the Enforcer spots the Vanguard on that tick.
        var sequence = session.Observe().LatestEventSequence;
        var door = new EntityId("interaction.service_door.entry");
        CompleteInteraction(session, door, "detect.gate.door");
        var events = session.EventsSince(sequence);
        var opened = Assert.Single(events, item => item.Detail is InteractionCompletedEventDetail completed && completed.InteractionId == door);
        Assert.Equal(opened.Tick, Assert.Single(events, item => item.Type == GameplayEventType.EncounterStarted).Tick);
    }

    [Fact]
    public void LaterFightsWaitTheirTurnEvenWhenTheirHostilesSeeTheCrew()
    {
        var detection = TestDefinition.Vision.HostileDetectionMeters;
        // The party Enforcer and a service guard flank the entry door; the solo Enforcer waits out of range.
        var party = CreatePartyPlacement() with { HostileSpawnPosition = new WorldPosition(-11.5, 0, 5) };
        var extensions = ExtensionEncounterPlacements();
        extensions[0] = extensions[0] with
        {
            HostilePlacements = extensions[0].HostilePlacements!.Select((hostile, index) => index == 0
                ? hostile with { Position = new WorldPosition(-8.5, 0, 5) } : hostile).ToArray(),
        };
        var session = OpenEntryDoor(CreateLayout(party, extensions, solo: CreateSoloPlacement(new WorldPosition(-10, 0, -8))));

        session.AdvanceTicks(60);
        var waiting = Observe(session);
        Assert.Equal(EncounterPhase.Dormant, waiting.Encounter!.Phase);
        var later = waiting.VisibleHostiles.Where(enemy => enemy.EncounterId != SoloEncounterId
            && enemy.Position.DistanceTo(waiting.Protagonist.Position) <= detection).ToArray();
        Assert.Contains(later, enemy => enemy.Id == MainEnforcerId);
        Assert.Contains(later, enemy => enemy.EncounterId == extensions[0].EncounterId);
        Assert.DoesNotContain(session.EventsSince(0), item => item.Type == GameplayEventType.EncounterStarted);

        // Only the solo Enforcer, next in route order, starts a fight when it sees the Vanguard.
        Assert.True(session.Execute(new MoveActorCommand(new CommandId("detect.order.approach"), ProtagonistId, new WorldPosition(-10, 0, -2))).Accepted);
        AdvanceUntil(session, route => route.Encounter!.Phase == EncounterPhase.Readying, 300);
        var solo = Observe(session);
        Assert.Equal(SoloEncounterId, solo.Encounter!.Id);
        Assert.Equal(EnforcerId, solo.Encounter.SpotterId);
        Assert.All(solo.VisibleHostiles.Where(enemy => enemy.EncounterId != SoloEncounterId),
            enemy => Assert.Equal(EncounterPhase.Dormant, enemy.EncounterPhase));

        // After the win, the defeated Enforcer never restarts its fight, and the party fight still waits
        // for its objective even with the Vanguard standing beside its Enforcer.
        Assert.True(Attack(session, ProtagonistId, EnforcerId).Accepted);
        ResumeIntoActiveCombat(session);
        AdvanceUntil(session, route => route.Encounter!.Phase == EncounterPhase.Victory, 1200);
        var victorySequence = session.Observe().LatestEventSequence;
        MoveAndArrive(session, ProtagonistId, SoloDoorApproach);
        session.AdvanceTicks(60);
        var after = Observe(session);
        Assert.True(after.Protagonist.Position.DistanceTo(party.HostileSpawnPosition) <= detection);
        Assert.Equal(SoloEncounterId, after.Encounter!.Id);
        Assert.Equal(EncounterPhase.Victory, after.Encounter.Phase);
        Assert.True(Assert.Single(after.Hostiles!).Combat.IsDefeated);
        Assert.Equal(TestDefinition.SoloExitDoorObjective.Id, after.Objective.Id);
        Assert.DoesNotContain(session.EventsSince(victorySequence), item => item.Type == GameplayEventType.EncounterStarted);
    }

    [Fact]
    public void ContentRequiresHostileDetectionWithinCrewSight()
    {
        var sight = TestDefinition.Vision.RangeMeters;
        Assert.InRange(TestDefinition.Vision.HostileDetectionMeters, double.Epsilon, sight);
        Assert.Equal(sight, DetectionDefinition(sight).Vision.HostileDetectionMeters);
        Assert.Throws<InvalidDataException>(() => DetectionDefinition(0));
        Assert.Throws<InvalidDataException>(() => DetectionDefinition(-1));
        Assert.Throws<InvalidDataException>(() => DetectionDefinition(sight + .001));
        Assert.Throws<InvalidDataException>(() => VisionDefinition(json => json["vision"]!["range_meters"] = TestDefinition.Vision.HostileDetectionMeters - .5));
        Assert.Throws<InvalidDataException>(() => VisionDefinition(json => json["vision"]!.AsObject().Remove("hostile_detection_meters")));
    }
}
