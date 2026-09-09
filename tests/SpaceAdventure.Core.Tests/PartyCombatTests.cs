using SpaceAdventure.Core;
using Xunit;

namespace SpaceAdventure.Core.Tests;

public sealed partial class CombatSessionTests
{
    private static readonly EntityId ProtectorId = new("actor.companion.protector");
    private static readonly EntityId MainEnforcerId = new("actor.enemy.security_enforcer.main");
    private static readonly EntityId SentryId = new("actor.enemy.gun_sentry.main");
    private static readonly EncounterId PartyEncounterId = new("encounter.station.party");
    private static readonly AbilityId BarrierId = new("ability.crew.protector.barrier");

    [Fact]
    public void RecruitmentInitializesTheKitAndPartyEntryResetsBothMembers()
    {
        var session = CreateAtPartyEncounter();
        var route = Observe(session);
        Assert.True(session.IsPaused);
        Assert.Equal(CombatTuning.PartyEncounter.ReadyingTicks, route.Encounter!.TransitionTicksRemaining);
        Assert.Equal(2, route.Hostiles!.Count);
        Assert.Equal(150, route.Party.Single(actor => actor.Id == ProtectorId).Combat!.Health);
        Assert.Equal(100, route.Protagonist.Combat!.Health);
        Assert.False(FindInteraction(route, new EntityId("interaction.evacuation_airlock")).CanInteract);
    }

    [Fact]
    public void PartyOrdersAndPendingReplacementsAreIndependent()
    {
        var session = CreateAtPartyEncounter();
        Assert.True(Attack(session, ProtagonistId, SentryId).Accepted);
        Assert.True(Attack(session, ProtectorId, MainEnforcerId).Accepted);
        Assert.True(session.Execute(new StopActorsCommand(new CommandId("party.stop.one"), [ProtectorId])).Accepted);
        Assert.Equal(SentryId, Observe(session).Protagonist.PendingAction!.CombatTargetId);
        Assert.Equal(PrimaryActionKind.Stop, Observe(session).Party[1].PendingAction!.Kind);
        ResumeIntoActiveCombat(session);
        session.AdvanceTicks(1);
        Assert.Null(Observe(session).Party[1].CurrentAction);
        Assert.NotNull(Observe(session).Protagonist.CurrentAction);
    }

    [Fact]
    public void BarrierInterceptsAnAlreadyFlyingShotWithoutErasingRecovery()
    {
        var session = CreateAtPartyEncounter(CreatePartyPlacement() with { CompanionRestartPosition = new WorldPosition(-.4, 0, 5) });
        Assert.True(Attack(session, ProtectorId, MainEnforcerId).Accepted);
        ResumeIntoActiveCombat(session);
        AdvanceUntil(session, route => route.Encounter!.Projectiles!.Count > 0, 120);
        var projectile = Assert.Single(Observe(session).Encounter!.Projectiles!);
        Assert.DoesNotContain(session.EventsSince(0), item => item.Detail is DamageAppliedEventDetail damage && damage.SourceId == SentryId);
        var recoveryUntil = Observe(session).Party[1].Combat!.OffensiveRecoveryUntilTick;
        Assert.True(Barrier(session).Accepted);
        session.AdvanceTicks(6);
        Assert.NotNull(Observe(session).Encounter!.Barrier);
        Assert.Equal(recoveryUntil, Observe(session).Party[1].Combat!.OffensiveRecoveryUntilTick);
        AdvanceUntil(session, _ => session.EventsSince(0).Any(item => item.Detail is ProjectileEventDetail p && p.Id == projectile.Id && p.Blocked), 30);
        Assert.DoesNotContain(Observe(session).Encounter!.Projectiles!, item => item.Id == projectile.Id);
        Assert.DoesNotContain(session.EventsSince(0), item => item.Detail is DamageAppliedEventDetail damage && damage.SourceId == SentryId);
        Assert.Equal(MainEnforcerId, Observe(session).Party[1].Combat!.RememberedAttackTargetId);
    }

    [Fact]
    public void BarrierAndProjectilesFreezeOnPauseAndBarrierStaysAfterMovement()
    {
        var session = CreateAtPartyEncounter();
        Assert.True(Barrier(session).Accepted);
        ResumeIntoActiveCombat(session);
        AdvanceUntil(session, route => route.Encounter!.Projectiles!.Count > 0, 120);
        var barrier = Assert.IsType<BarrierObservation>(Observe(session).Encounter!.Barrier);
        var projectile = Assert.Single(Observe(session).Encounter!.Projectiles!);
        session.Execute(new SetPauseCommand(new CommandId("barrier.pause"), true));
        Assert.Equal(0, session.AdvanceTicks(300));
        Assert.Equal(barrier, Observe(session).Encounter!.Barrier);
        Assert.Equal(projectile, Assert.Single(Observe(session).Encounter!.Projectiles!));
        Assert.True(session.Execute(new MoveActorCommand(new CommandId("barrier.move"), ProtectorId, new WorldPosition(5, 0, 4.5))).Accepted);
        session.Execute(new SetPauseCommand(new CommandId("barrier.resume"), false));
        session.AdvanceTicks(100);
        Assert.Equal(barrier.Position, Observe(session).Encounter!.Barrier!.Position);
        Assert.Equal(barrier.Facing, Observe(session).Encounter!.Barrier!.Facing);
    }

    [Fact]
    public void InvalidBarrierTargetsDoNotReplaceOrdersOrSpendCooldown()
    {
        var session = CreateAtPartyEncounter();
        Assert.True(Attack(session, ProtectorId, MainEnforcerId).Accepted);
        var pending = Observe(session).Party[1].PendingAction;
        foreach (var target in new AbilityTarget[]
        {
            new PositionAbilityTarget(new WorldPosition(0, 0, 6.4)),
            new BarrierAbilityTarget(new WorldPosition(.55, 0, 5.3), default),
            new BarrierAbilityTarget(new WorldPosition(.55, 0, 5.3), new WorldPosition(double.NaN, 0, 1)),
            new BarrierAbilityTarget(new WorldPosition(.55, 0, 5.3), new WorldPosition(double.PositiveInfinity, 0, 1)),
            new BarrierAbilityTarget(new WorldPosition(.55, 0, 5.3), new WorldPosition(0, 1, 0)),
        })
        {
            Assert.False(session.Execute(new UseAbilityCommand(new CommandId("barrier.invalid"), ProtectorId, BarrierId, target)).Accepted);
            Assert.Equal(pending, Observe(session).Party[1].PendingAction);
        }
        Assert.Equal(0, Observe(session).Party[1].Combat!.Cooldowns.Single(value => value.AbilityId == BarrierId).RemainingTicks);
        Assert.False(session.Execute(new UseAbilityCommand(new CommandId("barrier.wrong.owner"), ProtagonistId, BarrierId,
            new BarrierAbilityTarget(new WorldPosition(.55, 0, 5.3), new WorldPosition(0, 0, 1)))).Accepted);
    }

    [Theory]
    [InlineData(0, -1)]
    [InlineData(-3, 1)]
    public void ShotsPassTheWrongSideOrOutsideTheBarrierWidth(double x, double facingZ)
    {
        var session = CreateAtPartyEncounter(CreatePartyPlacement() with { CompanionRestartPosition = new WorldPosition(-.4, 0, 5) });
        Assert.True(Barrier(session, new WorldPosition(x, 0, facingZ)).Accepted);
        ResumeIntoActiveCombat(session);
        AdvanceUntil(session, _ => session.EventsSince(0).Any(item => item.Detail is DamageAppliedEventDetail d && d.SourceId == SentryId), 150);
        Assert.DoesNotContain(session.EventsSince(0), item => item.Type == GameplayEventType.ProjectileBlocked);
    }

    [Fact]
    public void SuppressionDamagesAndInterruptsEveryHostileInsideItsRadius()
    {
        var placement = CreatePartyPlacement() with
        {
            HostileSpawnPosition = new WorldPosition(0, 0, 5.5),
            AdditionalHostiles = [new StationActorPlacement(SentryId, new WorldPosition(0.8, 0, 5.5))],
        };
        var session = CreateAtPartyEncounter(placement);
        ResumeIntoActiveCombat(session);
        session.AdvanceTicks(1);
        Assert.All(Observe(session).Hostiles!, hostile => Assert.Equal(PrimaryActionPhase.Windup, hostile.CurrentAction!.Phase));
        Assert.True(session.Execute(new UseAbilityCommand(new CommandId("party.suppress.both"), ProtagonistId,
            InterruptId, new PositionAbilityTarget(new WorldPosition(0.4, 0, 5.5)))).Accepted);
        session.AdvanceTicks(6);
        Assert.All(Observe(session).Hostiles!, hostile =>
        {
            Assert.Equal(hostile.Combat.MaximumHealth - 5, hostile.Combat.Health);
            Assert.True(hostile.CurrentAction!.Interrupted);
        });
    }

    [Fact]
    public void OneDownedMemberDoesNotEndTheFightAndRetryPreservesRouteProgress()
    {
        var session = CreateAtPartyEncounter();
        ResumeIntoActiveCombat(session);
        AdvanceUntil(session, route => route.Party.Any(actor => actor.Combat!.IsDefeated), 900);
        Assert.Equal(EncounterPhase.Active, Observe(session).Encounter!.Phase);
        AdvanceUntil(session, route => route.Encounter!.Phase == EncounterPhase.Defeat, 1200);
        Assert.True(session.IsPaused);
        Assert.True(session.Execute(new RestartEncounterCommand(new CommandId("party.retry"), PartyEncounterId)).Accepted);
        var route = Observe(session);
        Assert.Equal(2, route.Encounter!.Attempt);
        Assert.Equal(EncounterPhase.Readying, route.Encounter.Phase);
        Assert.Equal(RoutePowerMode.ServiceRerouted, route.RoutePowerMode);
        Assert.Equal(InteractionState.Completed, FindInteraction(route, SoloExitDoorId).State);
        Assert.Equal(2, route.Party.Count);
        Assert.All(route.Party, actor => Assert.Equal(actor.Combat!.MaximumHealth, actor.Combat.Health));
        Assert.All(route.Hostiles!, hostile => Assert.Equal(hostile.Combat.MaximumHealth, hostile.Combat.Health));
        Assert.Null(route.Encounter.Barrier);
    }

    [Fact]
    public void PartyCanWinWithIndependentTargetsAndItsTwoSkillKits()
    {
        var session = CreateAtPartyEncounter();
        Assert.True(Attack(session, ProtagonistId, SentryId).Accepted);
        Assert.True(Attack(session, ProtectorId, MainEnforcerId).Accepted);
        Assert.True(Burst(session).Accepted);
        Assert.True(Barrier(session).Accepted);
        ResumeIntoActiveCombat(session);
        session.AdvanceTicks(6);
        Assert.True(Taunt(session).Accepted);
        for (var tick = 0; tick < 1500 && Observe(session).Encounter!.Phase == EncounterPhase.Active; tick++)
        {
            var route = Observe(session);
            foreach (var actor in route.Party.Where(actor => !actor.Combat!.IsDefeated))
            {
                if (actor.Combat!.RememberedAttackTargetId is null && actor.PendingAction is null)
                {
                    var target = route.Hostiles!.Where(hostile => !hostile.Combat.IsDefeated)
                        .OrderBy(hostile => hostile.Position.DistanceTo(actor.Position)).FirstOrDefault();
                    if (target is not null) { Assert.True(Attack(session, actor.Id, target.Id).Accepted); }
                }
            }
            session.AdvanceTicks(1);
        }
        Assert.Equal(EncounterPhase.Securing, Observe(session).Encounter!.Phase);
        session.AdvanceTicks(CombatTuning.PartyEncounter.SecuringTicks);
        var victory = Observe(session);
        Assert.Equal(EncounterPhase.Victory, victory.Encounter!.Phase);
        Assert.Equal(ObjectiveStatus.Completed, victory.Objective.Status);
        Assert.Equal(ScenarioPhase.InProgress, victory.Phase);
        Assert.All(victory.Hostiles!, hostile => Assert.True(hostile.Combat.IsDefeated));
        Assert.False(FindInteraction(victory, new EntityId("interaction.evacuation_airlock")).CanInteract);
    }

    [Fact]
    public void BarrierExpiresAtItsDeadlineWithoutChaining()
    {
        var session = CreateAtPartyEncounter();
        Assert.True(Barrier(session).Accepted);
        ResumeIntoActiveCombat(session);
        session.AdvanceTicks(CombatTuning.Barrier.WindupTicks);
        var duration = Observe(session).Encounter!.Barrier!.TotalTicks;
        session.AdvanceTicks(duration - 1);
        Assert.Equal(1, Observe(session).Encounter!.Barrier!.RemainingTicks);
        session.AdvanceTicks(1);
        Assert.Null(Observe(session).Encounter!.Barrier);
        Assert.Contains(session.EventsSince(0), item => item.Detail is BarrierEventDetail { EndReason: BarrierEndReason.Expired });
    }

    [Fact]
    public void IdleCrewWaitForAnExplicitTargetAndStopDoesNotRetarget()
    {
        var session = CreateAtPartyEncounter();
        var positions = Observe(session).Party.Select(actor => actor.Position).ToArray();
        ResumeIntoActiveCombat(session);
        var after = session.Observe().LatestEventSequence;
        session.AdvanceTicks(90);
        Assert.Equal(positions, Observe(session).Party.Select(actor => actor.Position));
        Assert.All(Observe(session).Party, actor =>
        {
            Assert.Null(actor.CurrentAction); Assert.Null(actor.Combat!.RememberedAttackTargetId);
        });
        Assert.DoesNotContain(session.EventsSince(after), item => item.Detail is AttackEventDetail attack
            && (attack.SourceId == ProtagonistId || attack.SourceId == ProtectorId));
        Assert.True(Attack(session, ProtectorId, MainEnforcerId).Accepted);
        session.AdvanceTicks(10);
        Assert.Contains(session.EventsSince(after), item => item.Type == GameplayEventType.AttackReleased
            && item.Detail is AttackEventDetail attack && attack.SourceId == ProtectorId);
        Assert.True(session.Execute(new StopActorsCommand(new CommandId("manual.stop"), [ProtectorId])).Accepted);
        after = session.Observe().LatestEventSequence;
        session.AdvanceTicks(60);
        Assert.DoesNotContain(session.EventsSince(after), item => item.Detail is AttackEventDetail attack
            && (attack.SourceId == ProtagonistId || attack.SourceId == ProtectorId));
    }

    [Fact]
    public void SentryStaysStationaryAndDoesNotFireOutsideItsAuthoredArc()
    {
        var placement = CreatePartyPlacement() with { SentryForward = new WorldPosition(0, 0, 1) };
        var session = CreateAtPartyEncounter(placement);
        ResumeIntoActiveCombat(session);
        session.AdvanceTicks(100);
        var sentry = Observe(session).Hostiles!.Single(hostile => hostile.Id == SentryId);
        Assert.Equal(placement.AdditionalHostiles![0].Position, sentry.Position);
        Assert.DoesNotContain(session.EventsSince(0), item => item.Detail is AttackEventDetail attack && attack.SourceId == SentryId);
    }

    [Fact]
    public void SentryPlacementRejectsAVerticalComponentInItsFacing()
    {
        var placement = CreatePartyPlacement() with { SentryForward = new WorldPosition(0, 1, -1) };
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateLayout(placement));
    }

    [Fact]
    public void SentryKeepsItsWindupTargetWhenRelativeDistancesChange()
    {
        var session = CreateAtPartyEncounter();
        ResumeIntoActiveCombat(session);
        session.AdvanceTicks(1);
        var initial = Observe(session);
        var initialSentry = initial.Hostiles!.Single(hostile => hostile.Id == SentryId);
        Assert.True(initial.Protagonist.Position.DistanceTo(initialSentry.Position)
            > initial.Party[1].Position.DistanceTo(initialSentry.Position));
        Assert.Equal(ProtagonistId, Observe(session).Hostiles!.Single(hostile => hostile.Id == SentryId).CurrentAction!.CombatTargetId);
        Assert.True(session.Execute(new MoveActorCommand(new CommandId("sentry.target.moves"), ProtagonistId,
            new WorldPosition(2.5, 0, 8.8))).Accepted);
        session.AdvanceTicks(35);
        Assert.Equal(ProtagonistId, Observe(session).Hostiles!.Single(hostile => hostile.Id == SentryId).CurrentAction!.CombatTargetId);
        session.AdvanceTicks(1);
        Assert.Contains(session.EventsSince(0), item => item.Type == GameplayEventType.AttackReleased
            && item.Detail is AttackEventDetail attack && attack.SourceId == SentryId && attack.TargetId == ProtagonistId && attack.Hit);
    }

    private static CommandAcknowledgement Attack(GameSession session, EntityId actor, EntityId target) =>
        session.Execute(new AssignBasicAttackTargetCommand(new CommandId($"party.attack.{actor}.{session.Tick}"), actor, target));

    private static CommandAcknowledgement Barrier(GameSession session, WorldPosition? facing = null, WorldPosition? position = null) =>
        session.Execute(new UseAbilityCommand(new CommandId($"party.barrier.{session.Tick}"), ProtectorId, BarrierId,
            new BarrierAbilityTarget(position ?? new WorldPosition(Observe(session).Party[1].Position.X, Observe(session).Party[1].Position.Y, Observe(session).Party[1].Position.Z + .8), facing ?? new WorldPosition(0, 0, 1))));

    private static GameSession CreateAtPartyEncounter(StationEncounterPlacement? placement = null, ISpatialPathfinder? pathfinder = null)
    {
        var session = CreateAtEncounter(placement, pathfinder);
        Assert.True(Attack(session, ProtagonistId, EnforcerId).Accepted);
        ResumeIntoActiveCombat(session);
        AdvanceUntil(session, route => route.Encounter!.Phase == EncounterPhase.Victory, 1200);
        CompleteInteraction(session, SoloExitDoorId, "party.exit");
        CompleteInteraction(session, ProtectorInteractionId, "party.recruit");
        Assert.True(session.Execute(new ChooseDialogueResponseCommand(new CommandId("party.join"), ProtagonistId,
            ProtectorInteractionId, new DialogueResponseId("response.recruit_protector"))).Accepted);
        Assert.True(session.Execute(new MovePartyCommand(new CommandId("party.enter"), [ProtagonistId, ProtectorId],
            new WorldPosition(0, 0, 5))).Accepted);
        AdvanceUntil(session, route => route.Encounter!.Id == PartyEncounterId, 900);
        return session;
    }

    private static StationEncounterPlacement CreatePartyPlacement() => new(PartyEncounterId,
        new WorldPosition(0, 0, 5), 2, new WorldPosition(-0.55, 0, 4.5), new WorldPosition(-2, 0, 8),
        new WorldPosition(0.55, 0, 4.5), [new StationActorPlacement(SentryId, new WorldPosition(2.5, 0, 10.5))],
        new WorldPosition(0, 0, -1));
}
