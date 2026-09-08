using SpaceAdventure.Core;
using Xunit;

namespace SpaceAdventure.Core.Tests;

public sealed partial class CombatSessionTests
{
    private static readonly AbilityId BurstId = new("ability.crew.vanguard.burst");
    private static readonly AbilityId TauntId = new("ability.crew.protector.taunt");

    private static CommandAcknowledgement Burst(GameSession session, EntityId? target = null) => session.Execute(
        new UseAbilityCommand(new CommandId($"burst.{session.Tick}"), ProtagonistId, BurstId, new EntityAbilityTarget(target ?? SentryId)));
    private static CommandAcknowledgement Taunt(GameSession session) => session.Execute(
        new UseAbilityCommand(new CommandId($"taunt.{session.Tick}"), ProtectorId, TauntId, new SelfAbilityTarget()));

    [Fact]
    public void BurstReleasesThreeShotsAtFixedIntervalsAndPausesBetweenThem()
    {
        var session = CreateAtPartyEncounter(); ResumeIntoActiveCombat(session);
        Assert.True(Burst(session).Accepted);
        session.AdvanceTicks(6);
        var first = session.EventsSince(0).Single(e => e.Detail is AbilityReleasedEventDetail a && a.AbilityId == BurstId);
        var target = Observe(session).Hostiles!.Single(enemy => enemy.Id == SentryId);
        Assert.Equal(target.Combat.MaximumHealth - 18, target.Combat.Health);
        Assert.Equal(300, Observe(session).Protagonist.Combat!.Cooldowns.Single(cd => cd.AbilityId == BurstId).RemainingTicks);
        session.Execute(new SetPauseCommand(new CommandId("burst.pause"), true));
        Assert.Equal(0, session.AdvanceTicks(120));
        Assert.Equal(first.Tick, session.Tick);
        session.Execute(new SetPauseCommand(new CommandId("burst.resume"), false)); session.AdvanceTicks(10);
        var shots = session.EventsSince(0).Where(e => e.Detail is AbilityReleasedEventDetail a && a.AbilityId == BurstId).ToArray();
        Assert.Equal(new[] { first.Tick, first.Tick + 5, first.Tick + 10 }, shots.Select(e => e.Tick));
        Assert.Equal(target.Combat.MaximumHealth - 54, Observe(session).Hostiles!.Single(enemy => enemy.Id == SentryId).Combat.Health);
        Assert.Equal(0, Observe(session).Protagonist.Combat!.Cooldowns.Single(cd => cd.AbilityId == SuppressiveFireId).RemainingTicks);
        Assert.Equal(290, Observe(session).Protagonist.Combat!.Cooldowns.Single(cd => cd.AbilityId == BurstId).RemainingTicks);
    }

    [Fact]
    public void MovementCancelsTheRestOfBurstButKeepsReleasedRecoveryAndCost()
    {
        var session = CreateAtPartyEncounter(); ResumeIntoActiveCombat(session);
        Assert.True(Burst(session).Accepted); session.AdvanceTicks(6);
        var deadline = Observe(session).Protagonist.Combat!.OffensiveRecoveryUntilTick;
        Assert.True(session.Execute(new MoveActorCommand(new CommandId("burst.cancel"), ProtagonistId, Observe(session).Protagonist.Position)).Accepted);
        Assert.True(Attack(session, ProtagonistId, SentryId).Accepted);
        Assert.Equal(ActionWaitingReason.OffensiveRecovery, Observe(session).Protagonist.PendingAction!.WaitingReason);
        session.AdvanceTicks(10);
        Assert.Single(session.EventsSince(0), e => e.Detail is AbilityReleasedEventDetail a && a.AbilityId == BurstId);
        Assert.Equal(deadline, Observe(session).Protagonist.Combat!.OffensiveRecoveryUntilTick);
        Assert.False(Burst(session).Accepted);
    }

    [Fact]
    public void InvalidSecondaryTargetsPreserveQueuedOrdersAndBothCooldowns()
    {
        var session = CreateAtPartyEncounter(); Assert.True(Attack(session, ProtagonistId, SentryId).Accepted);
        var pending = Observe(session).Protagonist.PendingAction;
        foreach (var target in new AbilityTarget[] { new SelfAbilityTarget(), new EntityAbilityTarget(ProtectorId), new PositionAbilityTarget(default) })
        { Assert.False(session.Execute(new UseAbilityCommand(new CommandId("burst.invalid"), ProtagonistId, BurstId, target)).Accepted); }
        Assert.False(session.Execute(new UseAbilityCommand(new CommandId("taunt.owner"), ProtagonistId, TauntId, new SelfAbilityTarget())).Accepted);
        Assert.Equal(pending, Observe(session).Protagonist.PendingAction);
        Assert.All(Observe(session).Protagonist.Combat!.Cooldowns, cd => Assert.Equal(0, cd.RemainingTicks));
    }

    [Fact]
    public void ReplacingAnUnreleasedBurstDoesNotSpendItsCooldown()
    {
        var session = CreateAtPartyEncounter(); ResumeIntoActiveCombat(session);
        Assert.True(Burst(session).Accepted); session.AdvanceTicks(3);
        Assert.True(session.Execute(new MoveActorCommand(new CommandId("burst.cancel.windup"), ProtagonistId, Observe(session).Protagonist.Position)).Accepted);
        session.AdvanceTicks(10);
        Assert.DoesNotContain(session.EventsSince(0), e => e.Detail is AbilityReleasedEventDetail a && a.AbilityId == BurstId);
        Assert.Equal(0, Observe(session).Protagonist.Combat!.Cooldowns.Single(cd => cd.AbilityId == BurstId).RemainingTicks);
    }

    [Fact]
    public void TauntRedirectsUnreleasedAttacksAndExpiresOnSimulationTime()
    {
        var session = CreateAtPartyEncounter(); ResumeIntoActiveCombat(session); session.AdvanceTicks(1);
        Assert.Equal(ProtagonistId, Observe(session).Hostiles!.Single(h => h.Id == SentryId).CurrentAction!.CombatTargetId);
        Assert.True(Taunt(session).Accepted); session.AdvanceTicks(6);
        Assert.All(Observe(session).Hostiles!, h => { Assert.Equal(ProtectorId, h.Combat.TauntedBy); Assert.Equal(120, h.Combat.TauntRemainingTicks); });
        Assert.Equal(ProtectorId, Observe(session).Hostiles!.Single(h => h.Id == SentryId).CurrentAction!.CombatTargetId);
        session.Execute(new SetPauseCommand(new CommandId("taunt.pause"), true)); Assert.Equal(0, session.AdvanceTicks(100));
        Assert.All(Observe(session).Hostiles!, h => Assert.Equal(120, h.Combat.TauntRemainingTicks));
        session.Execute(new SetPauseCommand(new CommandId("taunt.resume"), false)); session.AdvanceTicks(120);
        Assert.All(Observe(session).Hostiles!, h => Assert.Null(h.Combat.TauntedBy));
    }

    [Fact]
    public void TauntDoesNotRetargetAProjectileAlreadyInFlight()
    {
        var session = CreateAtPartyEncounter(); ResumeIntoActiveCombat(session);
        AdvanceUntil(session, state => state.Encounter!.Projectiles!.Count > 0, 90);
        var flying = Assert.Single(Observe(session).Encounter!.Projectiles!);
        Assert.True(Taunt(session).Accepted); session.AdvanceTicks(6);
        var after = Observe(session).Encounter!.Projectiles!.Single(p => p.Id == flying.Id);
        Assert.Equal(flying.TargetId, after.TargetId); Assert.Equal(flying.Destination, after.Destination);
    }

    [Fact]
    public void TauntCannotMakeAStationarySentryFireOutsideItsArc()
    {
        var session = CreateAtPartyEncounter(CreatePartyPlacement() with { SentryForward = new WorldPosition(0, 0, 1) });
        Assert.True(Taunt(session).Accepted); ResumeIntoActiveCombat(session); session.AdvanceTicks(100);
        Assert.DoesNotContain(session.EventsSince(0), e => e.Detail is AttackEventDetail a && a.SourceId == SentryId);
    }

    [Fact]
    public void TauntAndForearmBarrierCanBeCombinedToBlockSentryFire()
    {
        var session = CreateAtPartyEncounter(); Assert.True(Barrier(session).Accepted); ResumeIntoActiveCombat(session); session.AdvanceTicks(6);
        Assert.True(Taunt(session).Accepted); session.AdvanceTicks(6);
        var before = Observe(session).Party[1].Combat!.Health;
        AdvanceUntil(session, _ => session.EventsSince(0).Any(e => e.Type == GameplayEventType.ProjectileBlocked), 90);
        Assert.DoesNotContain(session.EventsSince(0), e => e.Detail is DamageAppliedEventDetail d && d.SourceId == SentryId);
        Assert.True(Observe(session).Party[1].Combat!.Health <= before); // Melee remains dangerous through the shield.
    }

    [Fact]
    public void TauntOnlyAffectsEnemiesInsideItsRadius()
    {
        var session = CreateAtPartyEncounter(CreatePartyPlacement() with
        { AdditionalHostiles = [new StationActorPlacement(SentryId, new WorldPosition(2.5, 0, 18))] });
        Assert.True(Taunt(session).Accepted); ResumeIntoActiveCombat(session); session.AdvanceTicks(6);
        Assert.Equal(ProtectorId, Observe(session).Hostiles!.Single(enemy => enemy.Id == MainEnforcerId).Combat.TauntedBy);
        Assert.Null(Observe(session).Hostiles!.Single(enemy => enemy.Id == SentryId).Combat.TauntedBy);
    }

    [Fact]
    public void MovingTheOwnerDoesNotSweepTheBarrierAcrossAProjectile()
    {
        // Walking into the shot must not drag the deployed plane into its path.
        var session = CreateAtPartyEncounter(CreatePartyPlacement() with
        { CompanionRestartPosition = new WorldPosition(-.4, 0, 4.8833333333) });
        Assert.True(Barrier(session).Accepted); ResumeIntoActiveCombat(session);
        AdvanceUntil(session, state => state.Encounter!.Projectiles!.Count > 0, 90);
        var shot = Assert.Single(Observe(session).Encounter!.Projectiles!);
        Assert.True(session.Execute(new MoveActorCommand(new CommandId("barrier.sweep"), ProtectorId, new WorldPosition(-.4, 0, 8))).Accepted);
        session.AdvanceTicks(10);
        Assert.DoesNotContain(session.EventsSince(0), item => item.Detail is ProjectileEventDetail p && p.Id == shot.Id && p.Blocked);
        Assert.Contains(Observe(session).Encounter!.Projectiles!, projectile => projectile.Id == shot.Id);
        Assert.DoesNotContain(session.EventsSince(0), item => item.Detail is DamageAppliedEventDetail d && d.SourceId == SentryId);
    }
}
