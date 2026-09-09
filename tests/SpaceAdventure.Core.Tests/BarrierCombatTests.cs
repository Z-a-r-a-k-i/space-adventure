using SpaceAdventure.Core;
using Xunit;

namespace SpaceAdventure.Core.Tests;

public sealed partial class CombatSessionTests
{
    [Fact]
    public void InvalidGroundPlacementPreservesOrdersAndCooldown()
    {
        var session = CreateAtPartyEncounter();
        Assert.True(Attack(session, ProtectorId, MainEnforcerId).Accepted);
        var pending = Observe(session).Party[1].PendingAction;
        foreach (var position in new[] { new WorldPosition(double.NaN, 0, 5), new WorldPosition(double.PositiveInfinity, 0, 5),
            new WorldPosition(.55, 2, 5), new WorldPosition(.55, 0, 15) })
        {
            Assert.False(Barrier(session, position: position).Accepted);
            Assert.Equal(pending, Observe(session).Party[1].PendingAction);
            Assert.Equal(0, Observe(session).Party[1].Combat!.Cooldowns.Single(cd => cd.AbilityId == BarrierId).RemainingTicks);
        }
        Assert.Null(Observe(session).Encounter!.Barrier);
    }

    [Fact]
    public void BarrierRechecksTheWholeFootprintAtRelease()
    {
        var blocked = false;
        var session = CreateAtPartyEncounter(pathfinder: new BarrierPlacementPathfinder(point => blocked && point.X > 1.5));
        ResumeIntoActiveCombat(session);
        Assert.True(Barrier(session).Accepted);
        session.AdvanceTicks(3);
        blocked = true;
        session.AdvanceTicks(3);
        Assert.Null(Observe(session).Encounter!.Barrier);
        Assert.Equal(0, Observe(session).Party[1].Combat!.Cooldowns.Single(cd => cd.AbilityId == BarrierId).RemainingTicks);
        Assert.Equal(CommandRejectionCode.InvalidAbilityTarget, Barrier(session).RejectionCode);
    }

    [Fact]
    public void BarrierFacingIsIndependentOfAutomaticOwnerHeading()
    {
        var session = CreateAtPartyEncounter();
        ResumeIntoActiveCombat(session);
        session.AdvanceTicks(12);
        var owner = Observe(session).Party[1];
        var ground = new WorldPosition(.55, 0, 6);
        Assert.True(Barrier(session, new WorldPosition(1, 0, 0), position: ground).Accepted);
        session.AdvanceTicks(6);
        Assert.Equal(owner.Position, Observe(session).Party[1].Position);
        Assert.Equal(new WorldPosition(1, 0, 0), Observe(session).Encounter!.Barrier!.Facing);
        Assert.NotEqual(owner.Facing, Observe(session).Encounter!.Barrier!.Facing);
        Assert.Equal(ground.X, Observe(session).Encounter!.Barrier!.Position.X);
        Assert.Equal(ground.Z, Observe(session).Encounter!.Barrier!.Position.Z);
    }

    [Fact]
    public void BarrierNormalizesFacingWithoutOverflowAndOutlivesItsOwner()
    {
        var placement = CreatePartyPlacement() with { HostileSpawnPosition = new WorldPosition(.55, 0, 5),
            ProtagonistRestartPosition = new WorldPosition(-4, 0, 4.5), SentryForward = new WorldPosition(0, 0, 1) };
        var session = CreateAtPartyEncounter(placement);
        ResumeIntoActiveCombat(session);
        AdvanceUntil(session, state => state.Party[1].Combat!.Health == 15, 1000);
        Assert.True(Barrier(session, new WorldPosition(1e308, 0, 1e308)).Accepted);
        session.AdvanceTicks(6);
        Assert.True(Observe(session).Encounter!.Barrier!.Facing.IsFinite);
        Assert.True(Taunt(session).Accepted); session.AdvanceTicks(6);
        AdvanceUntil(session, state => state.Party[1].Combat!.IsDefeated, 100);
        Assert.NotNull(Observe(session).Encounter!.Barrier);
        Assert.DoesNotContain(session.EventsSince(0), item => item.Type == GameplayEventType.BarrierEnded);
        Assert.All(Observe(session).Hostiles!, enemy => Assert.Null(enemy.Combat.TauntedBy));
    }

    [Fact]
    public void DefeatRetainsEachDeathTickAndRetryClearsItAndProjectiles()
    {
        var session = CreateAtPartyEncounter();
        Assert.True(Barrier(session).Accepted);
        ResumeIntoActiveCombat(session);
        AdvanceUntil(session, route => route.Encounter!.Phase == EncounterPhase.Defeat, 2400);
        var deathTicks = Observe(session).Party.Select(actor => actor.Combat!.DefeatedAtTick).ToArray();
        Assert.All(deathTicks, tick => Assert.NotNull(tick));
        Assert.NotEqual(deathTicks[0], deathTicks[1]);
        Assert.Equal(session.Tick, deathTicks.Max());
        var pausedTick = session.Tick;
        Assert.Equal(0, session.AdvanceTicks(100));
        Assert.Equal(pausedTick, session.Tick);
        Assert.True(session.Execute(new RestartEncounterCommand(new CommandId("barrier.defeat.retry"), PartyEncounterId)).Accepted);
        Assert.All(Observe(session).Party, actor => Assert.Null(actor.Combat!.DefeatedAtTick));
        Assert.Empty(Observe(session).Encounter!.Projectiles!);
        Assert.Null(Observe(session).Encounter!.Barrier);
    }

    private sealed class BarrierPlacementPathfinder(Func<WorldPosition, bool> blocked) : ISpatialPathfinder
    {
        public SpatialPathResult FindPath(EntityId actorId, WorldPosition origin, WorldPosition destination) =>
            blocked(destination) ? SpatialPathResult.Unreachable : SpatialPathResult.Reachable([destination]);
    }
}
