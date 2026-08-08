using SpaceAdventure.Core;
using Xunit;

namespace SpaceAdventure.Core.Tests;

public sealed class CombatSessionTests
{
    private static readonly EntityId ProtagonistId = new("actor.protagonist");
    private static readonly EntityId EnforcerId = new("actor.enemy.security_enforcer.solo");
    private static readonly EntityId SoloExitDoorId = new("interaction.service_door.solo_exit");
    private static readonly EntityId ProtectorInteractionId = new("interaction.protector");
    private static readonly AbilityId SuppressiveFireId = new("ability.crew.vanguard.suppressive_fire");
    private static readonly ItemId FieldAidId = new("item.healing.field_aid.v1");

    [Fact]
    public void EncounterAutoPausesOnceAndPromotesThePendingAttackAfterReadying()
    {
        var session = CreateAtEncounter();
        var started = Observe(session);

        Assert.True(session.IsPaused);
        Assert.Equal(EncounterPhase.Readying, started.Encounter!.Phase);
        Assert.Equal(1, started.Encounter.Attempt);
        Assert.Equal(new ObjectiveId("objective.defeat_security_enforcer"), started.Objective.Id);

        var attack = session.Execute(new AssignBasicAttackTargetCommand(
            new CommandId("combat.attack.pending"),
            ProtagonistId,
            EnforcerId));
        Assert.True(attack.Accepted);
        Assert.Null(Observe(session).Protagonist.CurrentAction);
        Assert.Equal(PrimaryActionKind.Attack, Observe(session).Protagonist.PendingAction!.Kind);
        Assert.Equal(0, session.AdvanceTicks(10));

        Assert.True(session.Execute(new SetPauseCommand(
            new CommandId("combat.resume"),
            Paused: false)).Accepted);
        Assert.Equal(42, session.AdvanceTicks(42));
        Assert.Equal(EncounterPhase.Active, Observe(session).Encounter!.Phase);
        session.AdvanceTicks(1);
        Assert.Equal(PrimaryActionKind.Attack, Observe(session).Protagonist.CurrentAction!.Kind);
        Assert.Null(Observe(session).Protagonist.PendingAction);
    }

    [Fact]
    public void SuppressiveFireInterruptsOnlyWindupAndVictoryUnlocksTheExit()
    {
        var session = CreateAtEncounter();
        ResumeIntoActiveCombat(session);
        AdvanceUntil(
            session,
            observation => observation.Hostiles![0].CurrentAction?.Phase == PrimaryActionPhase.Windup,
            300);

        var hostilePosition = Observe(session).Hostiles![0].Position;
        Assert.True(session.Execute(new SetPauseCommand(
            new CommandId("combat.pause.for-suppression"),
            Paused: true)).Accepted);
        Assert.True(session.Execute(new UseAbilityCommand(
            new CommandId("combat.suppress"),
            ProtagonistId,
            SuppressiveFireId,
            new PositionAbilityTarget(hostilePosition))).Accepted);
        Assert.True(session.Execute(new SetPauseCommand(
            new CommandId("combat.resume.suppress"),
            Paused: false)).Accepted);
        session.AdvanceTicks(18);

        Assert.Contains(session.EventsSince(0), gameEvent =>
            gameEvent.Type == GameplayEventType.ActionInterrupted);
        Assert.Equal(110, Observe(session).Hostiles![0].Combat.Health);
        Assert.Equal(240, Assert.Single(Observe(session).Protagonist.Combat!.Cooldowns).RemainingTicks);

        Assert.True(session.Execute(new AssignBasicAttackTargetCommand(
            new CommandId("combat.attack.finish"),
            ProtagonistId,
            EnforcerId)).Accepted);
        AdvanceUntil(
            session,
            observation => observation.Protagonist.Combat!.Health <= 60,
            600);
        Assert.True(session.Execute(new SetPauseCommand(
            new CommandId("combat.pause.finish-heal"),
            Paused: true)).Accepted);
        Assert.True(session.Execute(new UseItemCommand(
            new CommandId("combat.finish-heal"),
            ProtagonistId,
            FieldAidId,
            ProtagonistId)).Accepted);
        Assert.True(session.Execute(new SetPauseCommand(
            new CommandId("combat.resume.finish-heal"),
            Paused: false)).Accepted);
        session.AdvanceTicks(30);
        Assert.True(session.Execute(new AssignBasicAttackTargetCommand(
            new CommandId("combat.attack.finish-after-heal"),
            ProtagonistId,
            EnforcerId)).Accepted);
        AdvanceUntil(
            session,
            observation => observation.Encounter!.Phase == EncounterPhase.Victory,
            1200);

        var victory = Observe(session);
        Assert.Equal(0, victory.Hostiles![0].Combat.Health);
        Assert.Equal(new ObjectiveId("objective.open_solo_exit_service_door"), victory.Objective.Id);
        Assert.True(FindInteraction(victory, SoloExitDoorId).CanInteract);
        Assert.False(FindInteraction(victory, ProtectorInteractionId).CanInteract);

        CompleteInteraction(session, SoloExitDoorId, "solo-exit");
        var beyondExit = Observe(session);
        Assert.Equal(new ObjectiveId("objective.recruit_protector"), beyondExit.Objective.Id);
        Assert.True(FindInteraction(beyondExit, ProtectorInteractionId).CanInteract);
        Assert.False(FindInteraction(
            beyondExit,
            new EntityId("interaction.evacuation_airlock")).CanInteract);

        CompleteInteraction(session, ProtectorInteractionId, "protector");
        Assert.True(session.Execute(new ChooseDialogueResponseCommand(
            new CommandId("protector.recruit"),
            ProtagonistId,
            ProtectorInteractionId,
            new DialogueResponseId("response.recruit_protector"))).Accepted);
        var recruited = Observe(session);
        Assert.Equal(new ObjectiveId("objective.reach_main_combat"), recruited.Objective.Id);
        Assert.False(FindInteraction(
            recruited,
            new EntityId("interaction.evacuation_airlock")).CanInteract);
    }

    [Fact]
    public void FieldAidConsumesOneChargeAndDefeatRetryRestoresOnlyCombatState()
    {
        var session = CreateAtEncounter();
        ResumeIntoActiveCombat(session);
        AdvanceUntil(session, observation => observation.Protagonist.Combat!.Health <= 60, 600);
        Assert.True(session.Execute(new SetPauseCommand(
            new CommandId("combat.pause.heal"),
            Paused: true)).Accepted);
        Assert.True(session.Execute(new UseItemCommand(
            new CommandId("combat.heal"),
            ProtagonistId,
            FieldAidId,
            ProtagonistId)).Accepted);
        Assert.True(session.Execute(new SetPauseCommand(
            new CommandId("combat.resume.heal"),
            Paused: false)).Accepted);
        session.AdvanceTicks(15);

        var healed = Observe(session).Protagonist.Combat!;
        Assert.True(healed.Health > 60);
        Assert.Equal(0, Assert.Single(healed.Items).Charges);
        Assert.Contains(session.EventsSince(0), gameEvent =>
            gameEvent.Type == GameplayEventType.HealingApplied);

        AdvanceUntil(
            session,
            observation => observation.Encounter!.Phase == EncounterPhase.Defeat,
            900);
        Assert.True(session.IsPaused);
        var routePower = Observe(session).RoutePowerMode;
        var entryState = FindInteraction(
            Observe(session),
            new EntityId("interaction.service_door.entry")).State;
        var tickBeforeRetry = session.Tick;
        Assert.True(session.Execute(new RestartEncounterCommand(
            new CommandId("combat.retry"),
            new EncounterId("encounter.station.solo_tutorial"))).Accepted);

        var retried = Observe(session);
        Assert.True(session.IsPaused);
        Assert.True(session.Tick >= tickBeforeRetry);
        Assert.Equal(EncounterPhase.Readying, retried.Encounter!.Phase);
        Assert.Equal(2, retried.Encounter.Attempt);
        Assert.Equal(100, retried.Protagonist.Combat!.Health);
        Assert.Equal(140, retried.Hostiles![0].Combat.Health);
        Assert.Equal(1, Assert.Single(retried.Protagonist.Combat.Items).Charges);
        Assert.Equal(routePower, retried.RoutePowerMode);
        Assert.Equal(entryState, FindInteraction(
            retried,
            new EntityId("interaction.service_door.entry")).State);
        Assert.False(FindInteraction(retried, SoloExitDoorId).CanInteract);
    }

    private static GameSession CreateAtEncounter()
    {
        var session = GameSession.CreateStationRoute(
            StationRouteContent.ParseJson(File.ReadAllText(
                Path.Combine(AppContext.BaseDirectory, "content", "station-route.json"))),
            CreateLayout(),
            new DirectPathfinder());
        Assert.True(session.Execute(new ChooseProtagonistKitCommand(
            new CommandId("kit.vanguard"),
            new ProtagonistKitId("kit.protagonist.vanguard"))).Accepted);
        CompleteInteraction(session, new EntityId("interaction.survivor"), "survivor");
        Assert.True(session.Execute(new ChooseDialogueResponseCommand(
            new CommandId("survivor.choice"),
            ProtagonistId,
            new EntityId("interaction.survivor"),
            new DialogueResponseId("response.reroute_service_power"))).Accepted);
        CompleteInteraction(session, new EntityId("interaction.service_door.entry"), "entry");
        Assert.True(session.Execute(new MoveActorCommand(
            new CommandId("move.to.combat"),
            ProtagonistId,
            new WorldPosition(-10, 0, 2.75))).Accepted);
        AdvanceUntil(session, observation => observation.Encounter!.Phase == EncounterPhase.Readying, 300);
        return session;
    }

    private static void ResumeIntoActiveCombat(GameSession session)
    {
        Assert.True(session.Execute(new SetPauseCommand(
            new CommandId("combat.resume.initial"),
            Paused: false)).Accepted);
        session.AdvanceTicks(42);
        Assert.Equal(EncounterPhase.Active, Observe(session).Encounter!.Phase);
    }

    private static void CompleteInteraction(GameSession session, EntityId id, string commandPrefix)
    {
        Assert.True(session.Execute(new InteractCommand(
            new CommandId($"{commandPrefix}.interact"),
            ProtagonistId,
            id)).Accepted);
        AdvanceUntil(
            session,
            observation => FindInteraction(observation, id).State is
                InteractionState.DialogueActive or InteractionState.Completed,
            300);
    }

    private static StationRouteLayout CreateLayout()
    {
        return new StationRouteLayout(
            new WorldPosition(-10, 0, 8.5),
            [new StationActorPlacement(
                new EntityId("actor.companion.protector"),
                new WorldPosition(-1.5, 0, 0))],
            [
                new(new EntityId("interaction.survivor"), new WorldPosition(-8.5, 0, 6.5), new WorldPosition(-9.3, 0, 6.5)),
                new(new EntityId("interaction.service_door.entry"), new WorldPosition(-10, 0, 4), new WorldPosition(-10, 0, 4.85)),
                new(SoloExitDoorId, new WorldPosition(-5, 0, 0), new WorldPosition(-5.85, 0, 0)),
                new(ProtectorInteractionId, new WorldPosition(-1.5, 0, 0), new WorldPosition(-2.35, 0, 0)),
                new(new EntityId("interaction.service_terminal"), new WorldPosition(-11.5, 0, 6.5), new WorldPosition(-10.65, 0, 6.5)),
                new(new EntityId("interaction.evacuation_airlock"), new WorldPosition(12, 0, 8), new WorldPosition(11.15, 0, 8)),
            ],
            new StationEncounterPlacement(
                new EncounterId("encounter.station.solo_tutorial"),
                new WorldPosition(-10, 0, 2.75),
                0.75,
                new WorldPosition(-10, 0, 2.5),
                new WorldPosition(-10, 0, -1)));
    }

    private static void AdvanceUntil(
        GameSession session,
        Func<StationRouteObservation, bool> condition,
        int maximumTicks)
    {
        for (var index = 0; index < maximumTicks; index++)
        {
            if (condition(Observe(session)))
            {
                return;
            }

            session.AdvanceTicks(1);
        }

        Assert.Fail($"Condition was not reached within {maximumTicks} ticks.");
    }

    private static StationRouteObservation Observe(GameSession session) =>
        Assert.IsType<StationRouteObservation>(session.Observe().StationRoute);

    private static InteractionObservation FindInteraction(
        StationRouteObservation observation,
        EntityId id) =>
        Assert.Single(observation.Interactions, interaction => interaction.Id == id);

    private sealed class DirectPathfinder : ISpatialPathfinder
    {
        public SpatialPathResult FindPath(EntityId actorId, WorldPosition origin, WorldPosition destination)
        {
            _ = actorId;
            _ = origin;
            return SpatialPathResult.Reachable([destination]);
        }
    }
}
