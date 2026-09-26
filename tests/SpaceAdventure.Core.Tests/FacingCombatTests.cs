using SpaceAdventure.Core;
using Xunit;

namespace SpaceAdventure.Core.Tests;

public sealed partial class CombatSessionTests
{
    [Fact]
    public void AutomaticHeadingFollowsMovementOnlyWhenSimulationAdvances()
    {
        var session = CreateAtPartyEncounter();
        ResumeIntoActiveCombat(session);
        session.Execute(new SetPauseCommand(new CommandId("heading.pause"), true));
        var before = Observe(session).Protagonist;
        var destination = new WorldPosition(before.Position.X + 3, before.Position.Y, before.Position.Z);
        Assert.True(session.Execute(new MoveActorCommand(new CommandId("heading.move"), ProtagonistId, destination)).Accepted);
        Assert.Equal(0, session.AdvanceTicks(20));
        Assert.Equal(before.Facing, Observe(session).Protagonist.Facing);
        Assert.Equal(before.Position, Observe(session).Protagonist.Position);
        session.StepWhilePaused(1);
        var first = Observe(session).Protagonist.Facing;
        Assert.True(first.IsFinite);
        Assert.Equal(0, first.Y);
        Assert.NotEqual(before.Facing, first);
        Assert.True(first.DistanceTo(before.Facing) < .42);
        session.StepWhilePaused(7);
        Assert.True(Observe(session).Protagonist.Facing.X > .999);
        Assert.True(Observe(session).Protagonist.Position.X > before.Position.X);
    }

    [Fact]
    public void RepeatingAssignedTargetAfterBarrierPreservesTheShot()
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
        session.Execute(new SetPauseCommand(new CommandId("barrier.same.pause"), true));
        Assert.True(Attack(session, ProtectorId, actor.CurrentAction.CombatTargetId!.Value).Accepted);
        Assert.Null(Observe(session).Party[1].PendingAction);
        session.Execute(new SetPauseCommand(new CommandId("barrier.same.resume"), false));
        var resumed = Observe(session).Party[1];
        Assert.Null(resumed.PendingAction);
        Assert.Equal(actor.CurrentAction.InstanceId, resumed.CurrentAction!.InstanceId);
        Assert.Equal(actor.CurrentAction.PhaseTicksRemaining, resumed.CurrentAction.PhaseTicksRemaining);
    }

    [Fact]
    public void WiderShieldProtectsAnAllyOutsideTheOwnersSilhouette()
    {
        var placement = CreatePartyPlacement() with
        {
            HostileSpawnPosition = new WorldPosition(-4, 0, 9),
            AdditionalHostiles = [new StationActorPlacement(SentryId, new WorldPosition(0, 0, 10.5))],
        };
        var session = CreateAtPartyEncounter(placement,
            crew: new CrewStart(new WorldPosition(-1.2, 0, 4.5), new WorldPosition(0, 0, 5)));
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
    public void MovingTheOwnerDoesNotChangeInterceptionOfAnAlreadyFlyingShot(bool barrierFacesAway)
    {
        var session = CreateAtPartyEncounter(CreatePartyPlacement() with { HostileSpawnPosition = new WorldPosition(-4, 0, 9) },
            crew: PartyCrewStart with { Protector = new WorldPosition(-.4, 0, 5) });
        Assert.True(Barrier(session, new WorldPosition(0, 0, barrierFacesAway ? -1 : 1)).Accepted);
        ResumeIntoActiveCombat(session);
        AdvanceUntil(session, route => route.Encounter!.Projectiles!.Count > 0, 100);
        var projectile = Assert.Single(Observe(session).Encounter!.Projectiles!);
        Assert.True(session.Execute(new MoveActorCommand(new CommandId("barrier.move.in.flight"), ProtectorId, new WorldPosition(-2, 0, 5))).Accepted);
        AdvanceUntil(session, route => route.Encounter!.Projectiles!.All(shot => shot.Id != projectile.Id), 25);
        Assert.Equal(!barrierFacesAway, session.EventsSince(0).Any(item => item.Type == GameplayEventType.ProjectileBlocked
            && item.Detail is ProjectileEventDetail blocked && blocked.Id == projectile.Id));
        Assert.Equal(barrierFacesAway, session.EventsSince(0).Any(item => item.Detail is DamageAppliedEventDetail damage && damage.SourceId == SentryId));
    }
}
