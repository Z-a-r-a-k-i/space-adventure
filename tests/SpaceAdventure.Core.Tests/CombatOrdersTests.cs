using SpaceAdventure.Core;
using Xunit;

namespace SpaceAdventure.Core.Tests;

public sealed partial class CombatSessionTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(30)]
    [InlineData(60)]
    public void ClickingAtDifferentFrequenciesKeepsTheSameFireCadence(int interval)
    {
        var session = ActiveSession();
        var start = session.Tick;
        for (var tick = 0; tick < 240; tick++)
        {
            if (tick % interval == 0 && !Observe(session).Hostiles![0].Combat.IsDefeated) { Attack(session, $"repeat.{tick}"); }
            session.AdvanceTicks(1);
        }

        var cycle = CarbineTuning.WindupTicks + CarbineTuning.RecoveryTicks;
        Assert.Equal(Enumerable.Range(0, 8).Select(index => start + CarbineTuning.WindupTicks + index * cycle), ShotTicks(session));
        var hostile = Observe(session).Hostiles![0].Combat;
        Assert.Equal(Math.Max(0, hostile.MaximumHealth - 80), hostile.Health);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MovementAndStopCancelWindupButCannotEraseReleasedRecovery(bool stop)
    {
        var session = ActiveSession();
        Attack(session);
        session.AdvanceTicks(5);
        CancelAttack(session, stop);
        session.AdvanceTicks(10);
        Assert.Empty(ShotTicks(session));
        Assert.Null(Observe(session).Protagonist.Combat!.RememberedAttackTargetId);

        Attack(session, "fire.before.cancel");
        session.AdvanceTicks(CarbineTuning.WindupTicks);
        var releasedTick = Assert.Single(ShotTicks(session));
        CancelAttack(session, stop);
        var cancelled = Observe(session).Protagonist;
        Assert.Equal(releasedTick + CarbineTuning.RecoveryTicks, cancelled.Combat!.OffensiveRecoveryUntilTick);
        var previousPosition = cancelled.Position;
        session.AdvanceTicks(1);
        if (!stop) { Assert.NotEqual(previousPosition, Observe(session).Protagonist.Position); }

        Attack(session, "fire.after.cancel");
        Assert.Equal(ActionWaitingReason.OffensiveRecovery, Observe(session).Protagonist.PendingAction!.WaitingReason);
        session.AdvanceTicks(CarbineTuning.WindupTicks + CarbineTuning.RecoveryTicks - 2);
        Assert.Single(ShotTicks(session));
        session.AdvanceTicks(1);
        Assert.Equal(new[] { releasedTick, releasedTick + CarbineTuning.WindupTicks + CarbineTuning.RecoveryTicks }, ShotTicks(session));
    }

    [Fact]
    public void PausedNewestAttackReplacesQueuedMovementWithoutRestartingCurrentWindup()
    {
        var session = ActiveSession();
        Attack(session);
        session.AdvanceTicks(4);
        var original = Observe(session).Protagonist.CurrentAction!;
        Pause(session, true);
        CancelAttack(session, stop: false);
        Attack(session, "paused.replace.move");
        Assert.Equal(PrimaryActionKind.Attack, Observe(session).Protagonist.PendingAction!.Kind);
        session.StepWhilePaused(1);
        var actor = Observe(session).Protagonist;
        Assert.Null(actor.PendingAction);
        Assert.Equal(original.InstanceId, actor.CurrentAction!.InstanceId);
        Assert.Equal(original.PhaseStartedTick, actor.CurrentAction.PhaseStartedTick);
        Assert.Equal(CarbineTuning.WindupTicks - 5, actor.CurrentAction.PhaseTicksRemaining);
        session.StepWhilePaused(actor.CurrentAction.PhaseTicksRemaining);
        Assert.Single(ShotTicks(session));
    }

    [Fact]
    public void SuppressionWaitsForRecoveryThenResumesTheExplicitAttack()
    {
        var session = ActiveSession();
        Attack(session);
        session.AdvanceTicks(CarbineTuning.WindupTicks);
        var first = Assert.Single(ShotTicks(session));
        Suppress(session);
        Assert.Equal(PrimaryActionKind.Ability, Observe(session).Protagonist.PendingAction!.Kind);
        Assert.Equal(0, Observe(session).Protagonist.Combat!.Cooldowns.Single(value => value.AbilityId == InterruptId).RemainingTicks);
        session.AdvanceTicks(CarbineTuning.RecoveryTicks + CombatTuning.ProtagonistAbility.WindupTicks - 1);
        Assert.DoesNotContain(session.EventsSince(0), item => item.Type == GameplayEventType.AbilityReleased);
        session.AdvanceTicks(1);
        Assert.Equal(240, Observe(session).Protagonist.Combat!.Cooldowns.Single(value => value.AbilityId == InterruptId).RemainingTicks);
        Assert.Equal(EnforcerId, Observe(session).Protagonist.Combat!.RememberedAttackTargetId);
        session.AdvanceTicks(CombatTuning.ProtagonistAbility.RecoveryTicks + CarbineTuning.WindupTicks);
        Assert.Equal(new[] { first, first + CarbineTuning.RecoveryTicks + CombatTuning.ProtagonistAbility.WindupTicks
            + CombatTuning.ProtagonistAbility.RecoveryTicks + CarbineTuning.WindupTicks }, ShotTicks(session));
    }

    [Fact]
    public void TauntCanStartDuringOffensiveRecoveryAndResumesOnlyAnExplicitTarget()
    {
        var session = CreateAtPartyEncounter();
        Assert.True(Attack(session, ProtectorId, MainEnforcerId).Accepted);
        ResumeIntoActiveCombat(session); session.AdvanceTicks(ShotgunTuning.WindupTicks);
        var recovery = Observe(session).Party[1].Combat!.OffensiveRecoveryUntilTick;
        Assert.True(Taunt(session).Accepted);
        Assert.Equal(PrimaryActionKind.Ability, Observe(session).Party[1].CurrentAction!.Kind);
        Assert.Equal(recovery, Observe(session).Party[1].Combat!.OffensiveRecoveryUntilTick);
        session.AdvanceTicks(ShotgunTuning.RecoveryTicks + ShotgunTuning.WindupTicks);
        Assert.Equal(2, session.EventsSince(0).Count(item => item.Type == GameplayEventType.AttackReleased
            && item.Detail is AttackEventDetail attack && attack.SourceId == ProtectorId));

        var withoutTarget = CreateAtPartyEncounter();
        Assert.True(Taunt(withoutTarget).Accepted);
        ResumeIntoActiveCombat(withoutTarget); withoutTarget.AdvanceTicks(60);
        Assert.Null(Observe(withoutTarget).Party[1].CurrentAction);
        Assert.DoesNotContain(withoutTarget.EventsSince(0), item => item.Type == GameplayEventType.AttackReleased
            && item.Detail is AttackEventDetail attack && attack.SourceId == ProtectorId);
    }

    [Fact]
    public void CancellingAbilityBeforeReleaseSpendsNothing()
    {
        var session = ActiveSession();
        Suppress(session);
        session.AdvanceTicks(CombatTuning.ProtagonistAbility.WindupTicks - 1);
        CancelAttack(session, stop: true);
        session.AdvanceTicks(30);
        Assert.Equal(0, Observe(session).Protagonist.Combat!.Cooldowns.Single(value => value.AbilityId == InterruptId).RemainingTicks);
        Assert.DoesNotContain(session.EventsSince(0), item => item.Type == GameplayEventType.AbilityReleased);
    }

    [Fact]
    public void AQueuedAbilityIsRevalidatedBeforeItReplacesMovement()
    {
        var session = ActiveSession();
        Attack(session);
        session.AdvanceTicks(CarbineTuning.WindupTicks);
        var origin = Observe(session).Protagonist.Position;
        Assert.True(session.Execute(new MoveActorCommand(new CommandId("back-away"), ProtagonistId,
            new WorldPosition(origin.X, origin.Y, origin.Z + 5))).Accepted);
        Pause(session, true);
        Assert.True(session.Execute(new UseAbilityCommand(new CommandId("edge-of-range"), ProtagonistId,
            InterruptId, new PositionAbilityTarget(new WorldPosition(origin.X, origin.Y, origin.Z - 9)))).Accepted);
        Pause(session, false);
        session.AdvanceTicks(CarbineTuning.RecoveryTicks);
        var actor = Observe(session).Protagonist;
        Assert.Null(actor.PendingAction);
        Assert.Equal(PrimaryActionKind.Move, actor.CurrentAction!.Kind);
        Assert.Equal(0, actor.Combat!.Cooldowns.Single(value => value.AbilityId == InterruptId).RemainingTicks);
        Assert.Contains(session.EventsSince(0), item => item.Type == GameplayEventType.PrimaryActionFailed
            && item.RejectionCode == CommandRejectionCode.AbilityTargetOutOfRange);
    }

    [Fact]
    public void SuppressionWithinHalfASecondInterruptsEvenWithAFullCarbineRecovery()
    {
        var session = ActiveSession();
        AdvanceUntil(session, state => state.Hostiles![0].CurrentAction?.Phase == PrimaryActionPhase.Windup, 200);
        var windupTick = session.Tick;
        session.AdvanceTicks(GameSession.TicksPerSecond / 2 - CarbineTuning.WindupTicks);
        Attack(session);
        session.AdvanceTicks(CarbineTuning.WindupTicks);
        Suppress(session);
        Assert.Equal(windupTick + 15, session.Tick);
        Assert.Equal(CarbineTuning.RecoveryTicks, Observe(session).Protagonist.Combat!.OffensiveRecoveryUntilTick - session.Tick);
        session.AdvanceTicks(CarbineTuning.RecoveryTicks + CombatTuning.ProtagonistAbility.WindupTicks);
        Assert.Equal(100, Observe(session).Protagonist.Combat!.Health);
        Assert.Contains(session.EventsSince(0), item => item.Type == GameplayEventType.ActionInterrupted
            && item.Tick == windupTick + GameSession.TicksPerSecond / 2 + CarbineTuning.RecoveryTicks + CombatTuning.ProtagonistAbility.WindupTicks);
    }

    [Fact]
    public void StopValidatesTheWholeSelectionAndPausedStopWinsOverAttack()
    {
        var session = ActiveSession();
        Attack(session);
        var before = Observe(session).Protagonist.CurrentAction;
        var rejected = session.Execute(new StopActorsCommand(new CommandId("invalid-selection"),
            [ProtagonistId, new EntityId("actor.missing")]));
        Assert.False(rejected.Accepted);
        Assert.Equal(before, Observe(session).Protagonist.CurrentAction);
        Pause(session, true);
        CancelAttack(session, stop: true);
        Assert.Equal(PrimaryActionKind.Stop, Observe(session).Protagonist.PendingAction!.Kind);
        session.StepWhilePaused(40);
        Assert.Null(Observe(session).Protagonist.CurrentAction);
        Assert.Null(Observe(session).Protagonist.Combat!.RememberedAttackTargetId);
        Assert.Empty(ShotTicks(session));
    }

    [Fact]
    public void CyclesAndRetryHaveDistinctPresentationIdentity()
    {
        var session = ActiveSession();
        Attack(session);
        var first = Observe(session).Protagonist.CurrentAction!;
        session.AdvanceTicks(CarbineTuning.WindupTicks + CarbineTuning.RecoveryTicks);
        var second = Observe(session).Protagonist.CurrentAction!;
        Assert.NotEqual(first.InstanceId, second.InstanceId);
        Assert.Equal(first.PhaseStartedTick + CarbineTuning.WindupTicks + CarbineTuning.RecoveryTicks, second.PhaseStartedTick);
        CancelAttack(session, stop: true);
        AdvanceUntil(session, state => state.Encounter!.Phase == EncounterPhase.Defeat, 900);
        Assert.True(session.Execute(new RestartEncounterCommand(new CommandId("retry.identity"),
            new EncounterId("encounter.station.solo_tutorial"))).Accepted);
        Assert.Null(Observe(session).Protagonist.Combat!.RememberedAttackTargetId);
        Assert.Equal(0, Observe(session).Protagonist.Combat!.OffensiveRecoveryUntilTick);
        ResumeIntoActiveCombat(session);
        Attack(session, "retry.attack");
        Assert.True(Observe(session).Protagonist.CurrentAction!.InstanceId > second.InstanceId);
    }

    private static GameSession ActiveSession()
    {
        var session = CreateAtEncounter();
        ResumeIntoActiveCombat(session);
        return session;
    }

    private static long[] ShotTicks(GameSession session) => session.EventsSince(0)
        .Where(item => item.Type == GameplayEventType.AttackReleased
            && item.Detail is AttackEventDetail { SourceId: var source } && source == ProtagonistId)
        .Select(item => item.Tick).ToArray();

    private static void Attack(GameSession session, string id = "orders.attack") =>
        Assert.True(session.Execute(new AssignBasicAttackTargetCommand(new CommandId(id), ProtagonistId, EnforcerId)).Accepted);

    private static void Suppress(GameSession session) =>
        Assert.True(session.Execute(new UseAbilityCommand(new CommandId("orders.suppress"), ProtagonistId,
            InterruptId, new PositionAbilityTarget(Observe(session).Hostiles![0].Position))).Accepted);

    private static void Pause(GameSession session, bool value) =>
        Assert.True(session.Execute(new SetPauseCommand(new CommandId("orders.pause"), value)).Accepted);

    private static void CancelAttack(GameSession session, bool stop)
    {
        var position = Observe(session).Protagonist.Position;
        IGameCommand command = stop
            ? new StopActorsCommand(new CommandId("orders.stop"), [ProtagonistId])
            : new MoveActorCommand(new CommandId("orders.move"), ProtagonistId,
                new WorldPosition(position.X + 1, position.Y, position.Z));
        Assert.True(session.Execute(command).Accepted);
    }
}
