using System.Text.Json;
using System.Text.Json.Nodes;
using SpaceAdventure.Core;
using Xunit;

namespace SpaceAdventure.Core.Tests;

public sealed partial class CombatSessionTests
{
    private static StationRouteDefinition VisionDefinition(Action<JsonNode>? configure = null)
    {
        var json = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "content", "station-route.json")))!;
        configure?.Invoke(json);
        return StationRouteContent.ParseJson(json.ToJsonString());
    }

    private static StationRouteLayout VisionLayout(IEnumerable<StationVisionBlocker>? blockers = null,
        StationEncounterPlacement? solo = null, StationEncounterPlacement? party = null, WorldPosition? start = null)
    {
        var layout = CreateLayout(party);
        return new StationRouteLayout(start ?? layout.ProtagonistStart, layout.Actors, layout.Interactions,
            solo ?? layout.Encounter, layout.PartyEncounter, layout.Encounters.Skip(2), blockers);
    }

    private static GameSession NewVisionSession(StationRouteLayout layout, StationRouteDefinition? definition = null, ISpatialPathfinder? pathfinder = null)
    {
        var session = GameSession.CreateStationRoute(definition ?? TestDefinition, layout, pathfinder ?? new DirectPathfinder());
        Assert.True(session.Execute(new ChooseProtagonistKitCommand(new CommandId("vision.kit"), TestDefinition.ProtagonistKits[0].Id)).Accepted);
        return session;
    }

    private static GameSession VisionAtSolo(StationRouteLayout layout, StationRouteDefinition? definition = null, ISpatialPathfinder? pathfinder = null)
    {
        var session = NewVisionSession(layout, definition, pathfinder);
        CompleteInteraction(session, new EntityId("interaction.survivor"), "vision.survivor");
        Assert.True(session.Execute(new ChooseDialogueResponseCommand(new CommandId("vision.choice"), ProtagonistId,
            new EntityId("interaction.survivor"), new DialogueResponseId("response.reroute_service_power"))).Accepted);
        CompleteInteraction(session, new EntityId("interaction.service_door.entry"), "vision.entry");
        Assert.True(session.Execute(new MoveActorCommand(new CommandId("vision.enter"), ProtagonistId, layout.Encounter!.TriggerCenter)).Accepted);
        AdvanceUntil(session, route => route.Encounter!.Phase == EncounterPhase.Readying, 300);
        return session;
    }

    private static GameSession VisionAtParty(StationRouteLayout layout, StationRouteDefinition? definition = null, ISpatialPathfinder? pathfinder = null)
    {
        var session = VisionAtSolo(layout, definition, pathfinder);
        Assert.True(Attack(session, ProtagonistId, EnforcerId).Accepted);
        ResumeIntoActiveCombat(session);
        AdvanceUntil(session, route => route.Encounter!.Phase == EncounterPhase.Victory, 1200);
        CompleteInteraction(session, SoloExitDoorId, "vision.exit");
        CompleteInteraction(session, ProtectorInteractionId, "vision.protector");
        Assert.True(session.Execute(new ChooseDialogueResponseCommand(new CommandId("vision.join"), ProtagonistId,
            ProtectorInteractionId, new DialogueResponseId("response.recruit_protector"))).Accepted);
        Assert.True(session.Execute(new MovePartyCommand(new CommandId("vision.party"), [ProtagonistId, ProtectorId], layout.PartyEncounter!.TriggerCenter)).Accepted);
        AdvanceUntil(session, route => route.Encounter!.Id == PartyEncounterId, 900);
        return session;
    }

    private static string VisionState(GameSession session) => JsonSerializer.Serialize(Observe(session));
    private static bool Sees(GameSession session, EntityId id) => Observe(session).VisibleHostiles.Any(enemy => enemy.Id == id);
    private static StationVisionBlocker VisionWall(double minX = -.5, double maxX = .5) =>
        new("test.wall", new WorldPosition(minX, 0, 5), new WorldPosition(maxX, 3, 6));

    [Fact]
    public void SharedVisionRevealsDormantWorldEnemiesWithoutActivatingOrMakingThemTargetable()
    {
        var session = NewVisionSession(VisionLayout());
        var route = Observe(session);
        var upcoming = Assert.Single(route.VisibleHostiles, enemy => enemy.Id == MainEnforcerId);
        Assert.Equal(PartyEncounterId, upcoming.EncounterId);
        Assert.Equal(EncounterPhase.Dormant, upcoming.EncounterPhase);
        Assert.Equal(0, upcoming.EncounterAttempt);
        Assert.Equal(new WorldPosition(0, 0, -1), upcoming.Facing);
        Assert.Single(route.Hostiles!);
        Assert.Equal(EncounterPhase.Dormant, route.Encounter!.Phase);
        Assert.Equal(CommandRejectionCode.CombatInactive, Attack(session, ProtagonistId, upcoming.Id).RejectionCode);
        session.AdvanceTicks(20);
        Assert.Equal(EncounterPhase.Dormant, Observe(session).Encounter!.Phase);
        Assert.DoesNotContain(session.EventsSince(0), item => item.Type == GameplayEventType.EncounterStarted);
    }

    [Theory]
    [InlineData(18, true)]
    [InlineData(18.001, false)]
    [InlineData(-18, true)]
    public void SharedVisionHasAnInclusiveRangeAndNoFacingCone(double x, bool expected)
    {
        var solo = CreateLayout().Encounter! with { HostileSpawnPosition = new WorldPosition(x, 0, 0) };
        var session = NewVisionSession(VisionLayout(solo: solo, start: default(WorldPosition)));
        Assert.Equal(expected, Sees(session, EnforcerId));
    }

    [Fact]
    public void SharedVisionIgnoresUnrecruitedCompanions()
    {
        var solo = CreateLayout().Encounter! with { HostileSpawnPosition = new WorldPosition(18, 0, 8) };
        var session = NewVisionSession(VisionLayout(solo: solo));
        Assert.Single(Observe(session).Party);
        Assert.False(Sees(session, EnforcerId)); // Waiting Medic is only 9 m from this enemy.
    }

    [Theory]
    [InlineData(1.3, true)]
    [InlineData(1.4, false)]
    [InlineData(3, false)]
    public void SharedVisionUsesCopiedFullWallBoundsAtTheAuthoredEyeHeight(double wallTop, bool expected)
    {
        var blockers = new List<StationVisionBlocker> { new("wall", new WorldPosition(1, 0, -1), new WorldPosition(2, wallTop, 1)) };
        var solo = CreateLayout().Encounter! with { HostileSpawnPosition = new WorldPosition(3, 0, 0) };
        var layout = VisionLayout(blockers, solo, start: default(WorldPosition));
        blockers.Clear(); // Changes to the adapter's collection cannot change authoritative geometry.
        var session = NewVisionSession(layout);
        Assert.Equal(expected, Sees(session, EnforcerId));
        Assert.Single(layout.VisionBlockers);
    }

    [Fact]
    public void SharedVisionDoorBlocksWhileAvailableAndRevealsOnlyWhenCompleted()
    {
        var door = new EntityId("interaction.service_door.entry");
        var blocker = new StationVisionBlocker("entry", new WorldPosition(-11, 0, 3.8), new WorldPosition(-9, 3, 4.2), door);
        var session = NewVisionSession(VisionLayout([blocker]));
        Assert.False(Sees(session, EnforcerId));
        CompleteInteraction(session, new EntityId("interaction.survivor"), "vision.door.survivor");
        Assert.True(session.Execute(new ChooseDialogueResponseCommand(new CommandId("vision.door.choice"), ProtagonistId,
            new EntityId("interaction.survivor"), new DialogueResponseId("response.reroute_service_power"))).Accepted);
        Assert.Equal(InteractionState.Available, FindInteraction(Observe(session), door).State);
        Assert.False(Sees(session, EnforcerId));
        CompleteInteraction(session, door, "vision.door.open");
        Assert.True(Sees(session, EnforcerId));
        Assert.Equal(EncounterPhase.Dormant, Observe(session).Encounter!.Phase);
    }

    [Fact]
    public void SharedVisionHidesAndReacquiresWithoutAdvancingDuringPause()
    {
        var solo = CreateLayout().Encounter! with { HostileSpawnPosition = new WorldPosition(18, 0, 0) };
        var session = NewVisionSession(VisionLayout(solo: solo, start: default(WorldPosition)));
        Assert.True(session.Execute(new SetPauseCommand(new CommandId("vision.pause"), true)).Accepted);
        Assert.True(session.Execute(new MoveActorCommand(new CommandId("vision.leave"), ProtagonistId, new WorldPosition(-1, 0, 0))).Accepted);
        var paused = VisionState(session);
        Assert.Equal(0, session.Advance(TimeSpan.FromSeconds(10)));
        Assert.Equal(paused, VisionState(session));
        Assert.True(Sees(session, EnforcerId));
        session.StepWhilePaused(15);
        Assert.False(Sees(session, EnforcerId));
        Assert.True(session.Execute(new MoveActorCommand(new CommandId("vision.return"), ProtagonistId, default)).Accepted);
        session.StepWhilePaused(15);
        Assert.True(Sees(session, EnforcerId));
    }

    [Fact]
    public void SharedVisionUsesLivingCrewUnionAndClearsTargetsWhenTheOnlyViewerFalls()
    {
        var party = CreatePartyPlacement() with { ProtagonistRestartPosition = new WorldPosition(-30, 0, 5),
            CompanionRestartPosition = new WorldPosition(0, 0, 5), HostileSpawnPosition = new WorldPosition(0, 0, 5.5),
            AdditionalHostiles = [new(SentryId, new WorldPosition(100, 0, 5))] };
        var definition = VisionDefinition(json => json["combat"]!["companion_maximum_health"] = 1);
        var session = VisionAtParty(VisionLayout(party: party), definition);
        Assert.True(Sees(session, MainEnforcerId));
        Assert.True(Attack(session, ProtagonistId, MainEnforcerId).Accepted);
        ResumeIntoActiveCombat(session);
        AdvanceUntil(session, route => route.Party[1].Combat!.IsDefeated, 200);
        Assert.False(Sees(session, MainEnforcerId));
        Assert.Null(Observe(session).Protagonist.Combat!.RememberedAttackTargetId);
        Assert.Null(Observe(session).Protagonist.CurrentAction);
        Assert.Null(Observe(session).Protagonist.PendingAction);
        var before = VisionState(session);
        Assert.Equal(CommandRejectionCode.CombatTargetNotVisible, Attack(session, ProtagonistId, MainEnforcerId).RejectionCode);
        Assert.Equal(CommandRejectionCode.CombatTargetNotVisible, Burst(session, MainEnforcerId).RejectionCode);
        Assert.Equal(before, VisionState(session));
    }

    [Fact]
    public void SharedVisionFromTeammateDoesNotPermitFiringThroughAWall()
    {
        var party = CreatePartyPlacement() with { ProtagonistRestartPosition = new WorldPosition(0, 0, 4),
            CompanionRestartPosition = new WorldPosition(3, 0, 4), HostileSpawnPosition = new WorldPosition(0, 0, 8) };
        var session = VisionAtParty(VisionLayout([VisionWall()], party: party), pathfinder: new VisionDetourPathfinder());
        ResumeIntoActiveCombat(session);
        Assert.True(Sees(session, MainEnforcerId));
        var before = VisionState(session);
        var preview = session.CheckBurstTarget(ProtagonistId, MainEnforcerId);
        Assert.Equal(CommandRejectionCode.AbilityTargetObstructed, preview);
        Assert.Equal(before, VisionState(session));
        Assert.Equal(preview, Burst(session, MainEnforcerId).RejectionCode);
        Assert.Equal(before, VisionState(session));
        Assert.True(Attack(session, ProtagonistId, MainEnforcerId).Accepted);
        var sequence = session.Observe().LatestEventSequence;
        session.AdvanceTicks(2);
        Assert.Equal(PrimaryActionPhase.Moving, Observe(session).Protagonist.CurrentAction!.Phase);
        Assert.True(Observe(session).Protagonist.Position.X > 0);
        Assert.DoesNotContain(session.EventsSince(sequence), item => item.Detail is AttackEventDetail attack && attack.SourceId == ProtagonistId);
    }

    [Fact]
    public void SharedVisionLossKeepsUnrelatedSupportActionAndClearsRememberedOffense()
    {
        var party = CreatePartyPlacement() with { ProtagonistRestartPosition = new WorldPosition(2, 0, 4),
            CompanionRestartPosition = new WorldPosition(0, 0, 4), HostileSpawnPosition = new WorldPosition(0, 0, 8) };
        var session = VisionAtParty(VisionLayout([VisionWall()], party: party));
        ResumeIntoActiveCombat(session);
        Assert.True(Attack(session, ProtectorId, MainEnforcerId).Accepted);
        Assert.True(Barrier(session, position: new WorldPosition(0, 0, 4)).Accepted);
        Assert.True(session.Execute(new MoveActorCommand(new CommandId("vision.last.viewer.leaves"), ProtagonistId, new WorldPosition(0, 0, 4))).Accepted);
        AdvanceUntil(session, _ => !Sees(session, MainEnforcerId), 30);
        var protector = Observe(session).Party[1];
        Assert.Null(protector.Combat!.RememberedAttackTargetId);
        Assert.Equal(BarrierId, protector.CurrentAction!.AbilityId);
        Assert.True(protector.Combat.Cooldowns.Single(cd => cd.AbilityId == BarrierId).RemainingTicks > 0);
    }

    [Fact]
    public void SharedVisionBurstRevalidatesPersonalSightAfterFirstShotWithoutRefundingCooldownOrRecovery()
    {
        var party = CreatePartyPlacement() with { ProtagonistRestartPosition = new WorldPosition(1, 0, 4),
            CompanionRestartPosition = new WorldPosition(-3, 0, 8), HostileSpawnPosition = new WorldPosition(0, 0, 8),
            AdditionalHostiles = [new(SentryId, new WorldPosition(100, 0, 8))] };
        var definition = VisionDefinition(json => json["combat"]!["hostiles"]!.AsArray()
            .Single(enemy => enemy!["id"]!.GetValue<string>() == MainEnforcerId.Value)!["movement_speed_meters_per_second"] = 3);
        var session = VisionAtParty(VisionLayout([VisionWall(-.2, .2)], party: party), definition);
        ResumeIntoActiveCombat(session);
        var sequence = session.Observe().LatestEventSequence;
        Assert.True(Burst(session, MainEnforcerId).Accepted);
        session.AdvanceTicks(CombatTuning.Burst.WindupTicks + CombatTuning.Burst.ShotIntervalTicks);
        var releases = session.EventsSince(sequence).Where(item => item.Detail is AbilityReleasedEventDetail release && release.AbilityId == BurstId).ToArray();
        var release = Assert.Single(releases);
        Assert.Equal(MainEnforcerId, ((AbilityReleasedEventDetail)release.Detail!).TargetId);
        Assert.Contains(session.EventsSince(sequence), item => item.Detail is PrimaryActionFailedEventDetail failure
            && failure.Reason == CommandRejectionCode.AbilityTargetObstructed);
        var actor = Observe(session).Protagonist;
        Assert.Equal(release.Tick + CombatTuning.Burst.RecoveryTicks, actor.Combat!.OffensiveRecoveryUntilTick);
        Assert.True(actor.Combat.Cooldowns.Single(cd => cd.AbilityId == BurstId).RemainingTicks > 0);
        Assert.Equal(PrimaryActionPhase.Recovery, actor.CurrentAction!.Phase);
    }

    [Fact]
    public void SharedVisionValidatesContentAndBlockerContracts()
    {
        Assert.Throws<InvalidDataException>(() => VisionDefinition(json => json["vision"]!["range_meters"] = 0));
        Assert.Throws<InvalidDataException>(() => VisionDefinition(json => json["vision"]!["eye_height_meters"] = 6));
        Assert.Throws<ArgumentException>(() => new StationVisionBlocker("bad", default, default));
        var wall = VisionWall();
        Assert.Throws<ArgumentException>(() => VisionLayout([wall, wall]));
        Assert.Throws<InvalidDataException>(() => NewVisionSession(VisionLayout([
            new StationVisionBlocker("bad.door", wall.Minimum, wall.Maximum, ProtectorInteractionId)])));
    }

    [Fact]
    public void SharedVisionAreaSkillsAffectOnlyVisibleActiveEnemies()
    {
        var party = CreatePartyPlacement() with { ProtagonistRestartPosition = new WorldPosition(0, 0, 4),
            CompanionRestartPosition = new WorldPosition(0, 0, 4), HostileSpawnPosition = new WorldPosition(0, 0, 8),
            AdditionalHostiles = [new(SentryId, new WorldPosition(4, 0, 8))] };
        var layout = VisionLayout([VisionWall()], party: party);
        var extensions = layout.Encounters.Skip(2).Select((encounter, index) => index == 0
            ? encounter with { HostilePlacements = encounter.HostilePlacements!.Select(hostile => hostile with { Position = new WorldPosition(2, 0, 4) }).ToArray() }
            : encounter).ToArray();
        layout = new StationRouteLayout(layout.ProtagonistStart, layout.Actors, layout.Interactions,
            layout.Encounter, layout.PartyEncounter, extensions, layout.VisionBlockers);
        var session = VisionAtParty(layout);
        ResumeIntoActiveCombat(session);
        Assert.False(Sees(session, MainEnforcerId));
        var dormant = Observe(session).VisibleHostiles.Where(enemy => enemy.EncounterId == extensions[0].EncounterId).ToArray();
        Assert.Equal(3, dormant.Length);
        Assert.True(session.Execute(new UseAbilityCommand(new CommandId("vision.blind.interrupt"), ProtagonistId,
            InterruptId, new PositionAbilityTarget(new WorldPosition(0, 0, 8)))).Accepted);
        Assert.True(Taunt(session).Accepted);
        session.AdvanceTicks(Math.Max(CombatTuning.ProtagonistAbility.WindupTicks, CombatTuning.Taunt.WindupTicks));
        var hidden = Observe(session).Hostiles!.Single(enemy => enemy.Id == MainEnforcerId);
        Assert.Equal(hidden.Combat.MaximumHealth, hidden.Combat.Health);
        Assert.Null(hidden.Combat.TauntedBy);
        Assert.Equal(ProtectorId, Observe(session).Hostiles!.Single(enemy => enemy.Id == SentryId).Combat.TauntedBy);
        foreach (var enemy in dormant)
        {
            var after = Observe(session).VisibleHostiles.Single(candidate => candidate.Id == enemy.Id);
            Assert.Equal(enemy.Combat.Health, after.Combat.Health);
            Assert.Null(after.Combat.TauntedBy);
            Assert.Equal(EncounterPhase.Dormant, after.EncounterPhase);
        }
    }

    [Fact]
    public void SharedVisionSentryWaitsBehindWallAndRechecksSightAtRelease()
    {
        var party = CreatePartyPlacement() with { ProtagonistRestartPosition = new WorldPosition(0, 0, 4),
            CompanionRestartPosition = new WorldPosition(0, 0, 4), HostileSpawnPosition = new WorldPosition(100, 0, 8),
            AdditionalHostiles = [new(SentryId, new WorldPosition(0, 0, 8))] };
        var session = VisionAtParty(VisionLayout([VisionWall()], party: party));
        ResumeIntoActiveCombat(session);
        var sequence = session.Observe().LatestEventSequence;
        session.AdvanceTicks(90);
        Assert.DoesNotContain(session.EventsSince(sequence), item => item.Detail is AttackEventDetail attack && attack.SourceId == SentryId);
        Assert.True(session.Execute(new MoveActorCommand(new CommandId("vision.expose"), ProtagonistId, new WorldPosition(2, 0, 4))).Accepted);
        AdvanceUntil(session, route => route.Hostiles!.Single(enemy => enemy.Id == SentryId).CurrentAction?.Phase == PrimaryActionPhase.Windup, 40);
        Assert.True(session.Execute(new MoveActorCommand(new CommandId("vision.hide.before.release"), ProtagonistId, new WorldPosition(0, 0, 4))).Accepted);
        session.AdvanceTicks(CombatTuning.GetAttack(TestDefinition.Combat.GetHostile(SentryId).BasicAttackId).WindupTicks);
        Assert.Contains(session.EventsSince(sequence), item => item.Type == GameplayEventType.AttackReleased
            && item.Detail is AttackEventDetail { Hit: false } attack && attack.SourceId == SentryId);
        Assert.DoesNotContain(session.EventsSince(sequence), item => item.Detail is ProjectileEventDetail projectile && projectile.SourceId == SentryId);
    }

    [Fact]
    public void SharedVisionLossDoesNotEraseAlreadyLaunchedHostileProjectiles()
    {
        var party = CreatePartyPlacement() with { ProtagonistRestartPosition = new WorldPosition(1.01, 0, 4),
            CompanionRestartPosition = new WorldPosition(0, 0, 4), HostileSpawnPosition = new WorldPosition(100, 0, 8),
            AdditionalHostiles = [new(SentryId, new WorldPosition(0, 0, 8))] };
        var session = VisionAtParty(VisionLayout([VisionWall()], party: party));
        ResumeIntoActiveCombat(session);
        AdvanceUntil(session, route => route.Encounter!.Projectiles!.Count > 0, 80);
        var projectile = Assert.Single(Observe(session).Encounter!.Projectiles!);
        Assert.True(session.Execute(new MoveActorCommand(new CommandId("vision.hide.after.release"), ProtagonistId, new WorldPosition(0, 0, 4))).Accepted);
        session.AdvanceTicks(1);
        Assert.False(Sees(session, SentryId));
        Assert.Contains(Observe(session).Encounter!.Projectiles!, item => item.Id == projectile.Id);
        session.AdvanceTicks(projectile.FlightTicks);
        Assert.Contains(session.EventsSince(0), item => item.Type == GameplayEventType.ProjectileImpacted
            && item.Detail is ProjectileEventDetail detail && detail.Id == projectile.Id);
    }

    [Fact]
    public void SharedVisionLossDropsPendingOffenseDuringSpentRecovery()
    {
        var party = CreatePartyPlacement() with { ProtagonistRestartPosition = new WorldPosition(1, 0, 4),
            CompanionRestartPosition = new WorldPosition(-1, 0, 8), HostileSpawnPosition = new WorldPosition(0, 0, 8),
            AdditionalHostiles = [new(SentryId, new WorldPosition(3, 0, 8))] };
        var definition = VisionDefinition(json =>
        {
            json["combat"]!["companion_maximum_health"] = 1;
            var attacks = json["combat"]!["attacks"]!.AsArray();
            var quick = attacks.Single(attack => attack!["id"]!.GetValue<string>() == TestDefinition.Combat.GetHostile(MainEnforcerId).BasicAttackId.Value)!.DeepClone();
            quick["id"] = "attack.enemy.vision.quick"; quick["windup_ticks"] = 12; quick["damage"] = 1;
            attacks.Add(quick);
            json["combat"]!["hostiles"]!.AsArray().Single(enemy => enemy!["id"]!.GetValue<string>() == MainEnforcerId.Value)!["basic_attack_id"] = "attack.enemy.vision.quick";
        });
        var session = VisionAtParty(VisionLayout([VisionWall()], party: party), definition);
        ResumeIntoActiveCombat(session);
        Assert.True(Attack(session, ProtagonistId, SentryId).Accepted);
        session.AdvanceTicks(CarbineTuning.WindupTicks);
        var deadline = Observe(session).Protagonist.Combat!.OffensiveRecoveryUntilTick;
        Assert.True(deadline > session.Tick);
        Assert.True(Attack(session, ProtagonistId, MainEnforcerId).Accepted);
        Assert.Equal(MainEnforcerId, Observe(session).Protagonist.PendingAction!.CombatTargetId);
        AdvanceUntil(session, route => route.Party[1].Combat!.IsDefeated, 20);
        var actor = Observe(session).Protagonist;
        Assert.Null(actor.PendingAction);
        Assert.Null(actor.Combat!.RememberedAttackTargetId);
        Assert.Equal(deadline, actor.Combat.OffensiveRecoveryUntilTick);
        Assert.Equal(PrimaryActionPhase.Recovery, actor.CurrentAction!.Phase);
        Assert.Equal(SentryId, actor.CurrentAction.CombatTargetId);
    }

    [Fact]
    public void SharedVisionRifleApproachesAroundWallEvenWhileWithinWeaponRange()
    {
        var party = CreatePartyPlacement() with { ProtagonistRestartPosition = new WorldPosition(0, 0, 4),
            CompanionRestartPosition = new WorldPosition(0, 0, 4), HostileSpawnPosition = new WorldPosition(0, 0, 8),
            AdditionalHostiles = [new(SentryId, new WorldPosition(100, 0, 8))] };
        var definition = VisionDefinition(json =>
        {
            var enemy = json["combat"]!["hostiles"]!.AsArray().Single(item => item!["id"]!.GetValue<string>() == MainEnforcerId.Value)!;
            enemy["behavior"] = "ranged"; enemy["basic_attack_id"] = "attack.enemy.ranged_enforcer.rifle";
        });
        var session = VisionAtParty(VisionLayout([VisionWall()], party: party), definition, new VisionDetourPathfinder());
        ResumeIntoActiveCombat(session);
        var sequence = session.Observe().LatestEventSequence;
        session.AdvanceTicks(2);
        var enemy = Observe(session).Hostiles!.Single(hostile => hostile.Id == MainEnforcerId);
        Assert.True(enemy.Position.X > 0);
        Assert.Equal(PrimaryActionPhase.Moving, enemy.CurrentAction!.Phase);
        Assert.DoesNotContain(session.EventsSince(sequence), item => item.Detail is AttackEventDetail attack && attack.SourceId == MainEnforcerId);
    }

    [Fact]
    public void SharedVisionMeleeApproachesAroundWallInsteadOfStrikingThroughIt()
    {
        // Within the 1.5 m body-strike reach, but the wall separates the two.
        var party = CreatePartyPlacement() with { ProtagonistRestartPosition = new WorldPosition(0, 0, 4.7),
            CompanionRestartPosition = new WorldPosition(0, 0, 4.7), HostileSpawnPosition = new WorldPosition(0, 0, 6.1),
            AdditionalHostiles = [new(SentryId, new WorldPosition(100, 0, 8))] };
        var session = VisionAtParty(VisionLayout([VisionWall()], party: party), pathfinder: new VisionDetourPathfinder());
        ResumeIntoActiveCombat(session);
        var sequence = session.Observe().LatestEventSequence;
        session.AdvanceTicks(2);
        var enemy = Observe(session).Hostiles!.Single(hostile => hostile.Id == MainEnforcerId);
        Assert.True(enemy.Position.X > 0);
        Assert.Equal(PrimaryActionPhase.Moving, enemy.CurrentAction!.Phase);
        Assert.DoesNotContain(session.EventsSince(0), item => item.Detail is AttackEventDetail attack && attack.SourceId == MainEnforcerId);
        Assert.DoesNotContain(session.EventsSince(sequence), item => item.Detail is DamageAppliedEventDetail damage && damage.SourceId == MainEnforcerId);
    }

    [Fact]
    public void SharedVisionReportsAnAttackDroppedWhenTheOnlyViewerLeavesMidTick()
    {
        // Vanguard (party order 0) is the only viewer. When its move breaks sight, the
        // Protector's attack is dropped later in that same tick and must still report why.
        var party = CreatePartyPlacement() with { ProtagonistRestartPosition = new WorldPosition(2, 0, 4),
            CompanionRestartPosition = new WorldPosition(0, 0, 4), HostileSpawnPosition = new WorldPosition(0, 0, 8),
            AdditionalHostiles = [new(SentryId, new WorldPosition(100, 0, 8))] };
        var definition = VisionDefinition(json => json["combat"]!["hostiles"]!.AsArray()
            .Single(enemy => enemy!["id"]!.GetValue<string>() == MainEnforcerId.Value)!["movement_speed_meters_per_second"] = .01);
        var session = VisionAtParty(VisionLayout([VisionWall()], party: party), definition, new ProtectorBehindWallPathfinder());
        ResumeIntoActiveCombat(session);
        Assert.True(Sees(session, MainEnforcerId));
        Assert.True(Attack(session, ProtectorId, MainEnforcerId).Accepted);
        var sequence = session.Observe().LatestEventSequence;
        Assert.True(session.Execute(new MoveActorCommand(new CommandId("vision.only.viewer.leaves"), ProtagonistId, new WorldPosition(0, 0, 4))).Accepted);
        AdvanceUntil(session, _ => !Sees(session, MainEnforcerId), 60);
        Assert.Contains(session.EventsSince(sequence), item => item.Detail is PrimaryActionFailedEventDetail failure
            && failure.ActorId == ProtectorId && failure.Reason == CommandRejectionCode.CombatTargetNotVisible);
        Assert.Null(Observe(session).Party.Single(actor => actor.Id == ProtectorId).CurrentAction);
    }

    // Keeps the Protector's approach straight behind the wall, so it never gains sight itself.
    private sealed class ProtectorBehindWallPathfinder : ISpatialPathfinder
    {
        public SpatialPathResult FindPath(EntityId actorId, WorldPosition origin, WorldPosition destination) =>
            actorId == ProtectorId && destination.Z > 7.5 && Math.Abs(destination.X) < .1
                ? SpatialPathResult.Reachable([new WorldPosition(0, 0, -6), destination])
                : SpatialPathResult.Reachable([destination]);
    }

    private sealed class VisionDetourPathfinder : ISpatialPathfinder
    {
        public SpatialPathResult FindPath(EntityId actorId, WorldPosition origin, WorldPosition destination) =>
            actorId == ProtagonistId && origin.X >= -.5 && origin.X <= .5 && origin.Z >= 3 && origin.Z < 5 && destination.Z > 6
                ? SpatialPathResult.Reachable([new WorldPosition(1, 0, 4), new WorldPosition(1, 0, 7), destination])
                : actorId == MainEnforcerId && origin.X >= -.5 && origin.X <= .5 && origin.Z > 6 && destination.Z < 5
                    ? SpatialPathResult.Reachable([new WorldPosition(1, 0, 8), new WorldPosition(1, 0, 4), destination])
                : SpatialPathResult.Reachable([destination]);
    }
}
