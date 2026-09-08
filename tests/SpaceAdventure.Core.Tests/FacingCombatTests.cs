using SpaceAdventure.Core;
using Xunit;

namespace SpaceAdventure.Core.Tests;

public sealed partial class CombatSessionTests
{
    private static CommandAcknowledgement Face(GameSession session, WorldPosition facing, params EntityId[] actors) =>
        session.Execute(new FaceActorsCommand(new CommandId($"face.{session.Tick}"), actors, facing));

    [Fact]
    public void FacingQueuesForTheGroupAndTurnsInPlaceOnlyOnTicks()
    {
        var session = CreateAtPartyEncounter();
        ResumeIntoActiveCombat(session);
        session.Execute(new SetPauseCommand(new CommandId("face.pause"), true));
        var before = Observe(session).Party.ToArray();
        Assert.True(Face(session, new WorldPosition(1e308, 0, -1e308), ProtagonistId, ProtectorId).Accepted);
        Assert.Equal(0, session.AdvanceTicks(20));
        Assert.Equal(before.Select(actor => actor.Facing), Observe(session).Party.Select(actor => actor.Facing));
        Assert.All(Observe(session).Party, actor =>
        {
            Assert.False(actor.FacingHeld);
            Assert.Equal(PrimaryActionKind.Face, actor.PendingAction!.Kind);
            Assert.True(actor.PendingAction.Facing!.Value.IsFinite);
            Assert.False(actor.PendingAction.HasRemainingMovement);
        });
        session.StepWhilePaused(1);
        Assert.All(Observe(session).Party, actor => Assert.True(actor.FacingHeld));
        session.StepWhilePaused(9);
        Assert.Equal(before.Select(actor => actor.Position), Observe(session).Party.Select(actor => actor.Position));
        Assert.All(Observe(session).Party, actor =>
        {
            Assert.Null(actor.PendingAction); Assert.Null(actor.CurrentAction);
            Assert.Equal(Math.Sqrt(.5), actor.Facing.X, 8);
            Assert.Equal(-Math.Sqrt(.5), actor.Facing.Z, 8);
        });
    }

    [Fact]
    public void InvalidFacingGroupCannotReplaceAnyQueuedOrder()
    {
        var session = CreateAtPartyEncounter();
        Assert.True(Attack(session, ProtagonistId, SentryId).Accepted);
        var pending = Observe(session).Protagonist.PendingAction;
        foreach (var direction in new[] { default(WorldPosition), new WorldPosition(0, 1, 1),
            new WorldPosition(double.NaN, 0, 1), new WorldPosition(double.PositiveInfinity, 0, 1) })
        { Assert.Equal(CommandRejectionCode.InvalidFacing, Face(session, direction, ProtagonistId).RejectionCode); }
        var facing = new WorldPosition(1, 0, 0);
        Assert.Equal(CommandRejectionCode.EmptyPartySelection, Face(session, facing).RejectionCode);
        Assert.Equal(CommandRejectionCode.DuplicateActor, Face(session, facing, ProtagonistId, ProtagonistId).RejectionCode);
        Assert.Equal(CommandRejectionCode.UnknownActor, Face(session, facing, ProtagonistId, new EntityId("actor.missing")).RejectionCode);
        Assert.Equal(pending, Observe(session).Protagonist.PendingAction);
        Assert.All(Observe(session).Party, actor => Assert.False(actor.FacingHeld));
    }

    [Fact]
    public void FacingDoesNotStartAnAttackAndExplicitAttacksReleaseTheHeading()
    {
        var session = CreateAtPartyEncounter(CreatePartyPlacement() with
            { HostileSpawnPosition = new WorldPosition(.55, 0, 8) });
        Assert.True(Face(session, new WorldPosition(0, 0, -1), ProtagonistId, ProtectorId).Accepted);
        ResumeIntoActiveCombat(session); session.AdvanceTicks(30);
        Assert.All(Observe(session).Party, actor =>
        {
            Assert.Null(actor.CurrentAction);
            Assert.Equal(-1, actor.Facing.Z); Assert.Null(actor.Combat!.RememberedAttackTargetId);
        });
        var after = session.Observe().LatestEventSequence;
        Assert.True(Face(session, new WorldPosition(0, 0, 1), ProtectorId).Accepted);
        session.AdvanceTicks(20);
        Assert.DoesNotContain(session.EventsSince(after), item => item.Type == GameplayEventType.AttackReleased
            && item.Detail is AttackEventDetail attack && attack.SourceId == ProtectorId);
        Assert.True(Observe(session).Party[1].FacingHeld);
        Assert.Equal(1, Observe(session).Party[1].Facing.Z);
        Assert.True(Attack(session, ProtagonistId, MainEnforcerId).Accepted);
        Assert.False(Observe(session).Protagonist.FacingHeld);
        session.AdvanceTicks(10);
        Assert.True(Observe(session).Protagonist.Facing.Z > 0);
    }

    [Fact]
    public void TurningCancelsMovementAndKeepsReleasedAttackRecovery()
    {
        var session = CreateAtPartyEncounter();
        Assert.True(Attack(session, ProtectorId, MainEnforcerId).Accepted);
        ResumeIntoActiveCombat(session); session.AdvanceTicks(10);
        var deadline = Observe(session).Party[1].Combat!.OffensiveRecoveryUntilTick;
        Assert.True(deadline > session.Tick);
        Assert.True(session.Execute(new MoveActorCommand(new CommandId("face.move"), ProtectorId, new WorldPosition(4, 0, 4.5))).Accepted);
        session.AdvanceTicks(2);
        var position = Observe(session).Party[1].Position;
        Assert.True(Face(session, new WorldPosition(-1, 0, 0), ProtectorId).Accepted);
        session.AdvanceTicks(2);
        Assert.Equal(position, Observe(session).Party[1].Position);
        Assert.Equal(deadline, Observe(session).Party[1].Combat!.OffensiveRecoveryUntilTick);
        Assert.True(Attack(session, ProtectorId, MainEnforcerId).Accepted);
        var after = session.Observe().LatestEventSequence;
        session.AdvanceTicks((int)(deadline - session.Tick));
        Assert.DoesNotContain(session.EventsSince(after), item => item.Type == GameplayEventType.AttackReleased
            && item.Detail is AttackEventDetail attack && attack.SourceId == ProtectorId);
    }

    [Fact]
    public void BarrierDoesNotChangeFacingAndRepeatingTheTargetKeepsTheShot()
    {
        var session = CreateAtPartyEncounter(CreatePartyPlacement() with
            { HostileSpawnPosition = new WorldPosition(.55, 0, 8) });
        Assert.True(Attack(session, ProtectorId, MainEnforcerId).Accepted);
        ResumeIntoActiveCombat(session); session.AdvanceTicks(10);
        Assert.True(Barrier(session).Accepted);
        session.AdvanceTicks(18);
        AdvanceUntil(session, route => route.Party[1].CurrentAction is
            { Kind: PrimaryActionKind.Attack, Phase: PrimaryActionPhase.Windup }, 40);
        var actor = Observe(session).Party[1];
        Assert.Equal(PrimaryActionKind.Attack, actor.CurrentAction!.Kind);
        Assert.Equal(PrimaryActionPhase.Windup, actor.CurrentAction.Phase);
        session.Execute(new SetPauseCommand(new CommandId("face.same.pause"), true));
        Assert.True(Attack(session, ProtectorId, actor.CurrentAction.CombatTargetId!.Value).Accepted);
        Assert.False(Observe(session).Party[1].FacingHeld);
        Assert.Null(Observe(session).Party[1].PendingAction);
        session.Execute(new SetPauseCommand(new CommandId("face.same.resume"), false));
        var resumed = Observe(session).Party[1];
        Assert.False(resumed.FacingHeld); Assert.Null(resumed.PendingAction);
        Assert.Equal(actor.CurrentAction.InstanceId, resumed.CurrentAction!.InstanceId);
        Assert.Equal(actor.CurrentAction.PhaseTicksRemaining, resumed.CurrentAction.PhaseTicksRemaining);
    }

    [Fact]
    public void DeployedBarrierKeepsItsFacingAndPositionAfterOwnerTurnsAndMoves()
    {
        var session = CreateAtPartyEncounter();
        Assert.True(Barrier(session).Accepted);
        ResumeIntoActiveCombat(session); session.AdvanceTicks(10);
        var before = Observe(session).Encounter!.Barrier!;
        var cooldown = Observe(session).Party[1].Combat!.Cooldowns.Single(cd => cd.AbilityId == BarrierId).RemainingTicks;
        session.Execute(new SetPauseCommand(new CommandId("shield.face.pause"), true));
        Assert.True(Face(session, new WorldPosition(0, 0, -1), ProtectorId).Accepted);
        Assert.Equal(0, session.AdvanceTicks(15));
        Assert.Equal(before, Observe(session).Encounter!.Barrier);
        for (var tick = 0; tick < 8; tick++)
        {
            session.StepWhilePaused(1);
            var barrier = Observe(session).Encounter!.Barrier!;
            Assert.Equal(before.Facing, barrier.Facing);
            Assert.Equal(before.Position, barrier.Position);
            Assert.Equal(before.DeployedAtTick, barrier.DeployedAtTick);
        }
        Assert.Equal(-1, Observe(session).Party[1].Facing.Z);
        Assert.Equal(before.Facing, Observe(session).Encounter!.Barrier!.Facing);
        Assert.Equal(cooldown - 8, Observe(session).Party[1].Combat!.Cooldowns.Single(cd => cd.AbilityId == BarrierId).RemainingTicks);
        Assert.Single(session.EventsSince(0), item => item.Type == GameplayEventType.BarrierDeployed);
        Assert.True(session.Execute(new MoveActorCommand(new CommandId("shield.face.move"), ProtectorId, new WorldPosition(4, 0, 4.5))).Accepted);
        session.StepWhilePaused(8);
        Assert.False(Observe(session).Party[1].FacingHeld);
        Assert.Equal(before.Facing, Observe(session).Encounter!.Barrier!.Facing);
        Assert.Equal(before.Position, Observe(session).Encounter!.Barrier!.Position);
    }

    [Fact]
    public void WiderShieldProtectsAnAllyOutsideTheOwnersSilhouette()
    {
        var placement = CreatePartyPlacement() with
        {
            ProtagonistRestartPosition = new WorldPosition(-1.2, 0, 4.5),
            CompanionRestartPosition = new WorldPosition(0, 0, 5),
            HostileSpawnPosition = new WorldPosition(-4, 0, 9),
            AdditionalHostiles = [new StationActorPlacement(SentryId, new WorldPosition(0, 0, 10.5))],
        };
        var session = CreateAtPartyEncounter(placement);
        Assert.True(Barrier(session).Accepted); ResumeIntoActiveCombat(session);
        AdvanceUntil(session, _ => session.EventsSince(0).Any(item => item.Type == GameplayEventType.ProjectileBlocked), 100);
        var impact = (ProjectileEventDetail)session.EventsSince(0).First(item => item.Type == GameplayEventType.ProjectileBlocked).Detail!;
        Assert.Equal(ProtagonistId, impact.TargetId);
        Assert.True(Math.Abs(impact.ImpactPosition!.Value.X) > .7);
        Assert.DoesNotContain(session.EventsSince(0), item => item.Detail is DamageAppliedEventDetail damage && damage.SourceId == SentryId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TurningTheOwnerDoesNotChangeInterceptionOfAnAlreadyFlyingShot(bool turnIntoFire)
    {
        var session = CreateAtPartyEncounter(CreatePartyPlacement() with
        {
            CompanionRestartPosition = new WorldPosition(-.4, 0, 5),
            HostileSpawnPosition = new WorldPosition(-4, 0, 9),
        });
        Assert.True(Barrier(session, new WorldPosition(0, 0, turnIntoFire ? -1 : 1)).Accepted);
        ResumeIntoActiveCombat(session);
        AdvanceUntil(session, route => route.Encounter!.Projectiles!.Count > 0, 100);
        var projectile = Assert.Single(Observe(session).Encounter!.Projectiles!);
        Assert.True(Face(session, new WorldPosition(0, 0, turnIntoFire ? 1 : -1), ProtectorId).Accepted);
        AdvanceUntil(session, route => route.Encounter!.Projectiles!.All(shot => shot.Id != projectile.Id), 25);
        Assert.Equal(!turnIntoFire, session.EventsSince(0).Any(item => item.Type == GameplayEventType.ProjectileBlocked
            && item.Detail is ProjectileEventDetail blocked && blocked.Id == projectile.Id));
        Assert.Equal(turnIntoFire, session.EventsSince(0).Any(item => item.Detail is DamageAppliedEventDetail damage && damage.SourceId == SentryId));
    }
}
