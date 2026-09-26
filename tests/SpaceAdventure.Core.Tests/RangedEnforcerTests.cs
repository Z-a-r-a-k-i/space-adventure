using System.Text.Json.Nodes;
using SpaceAdventure.Core;
using Xunit;

namespace SpaceAdventure.Core.Tests;

public sealed partial class CombatSessionTests
{
    private static StationEncounterPlacement RangedOnlyPlacement(double rangedX = 26) => QuietMedicPlacement() with
    {
        HostilePlacements = QuietMedicPlacement().HostilePlacements!.Select((hostile, index) => index == 2
            ? hostile with { Position = new WorldPosition(rangedX, 0, 8) } : hostile).ToArray(),
    };

    private static readonly CrewStart RangedOnlyCrew = new(new WorldPosition(14, 0, 12), new WorldPosition(14, 0, 8), new WorldPosition(14, 0, 13));

    [Fact]
    public void RangedEnforcerAdvancesToRangeStopsForWindupAndBarrierBlocksItsReleasedProjectile()
    {
        // The rifleman must notice the Protector 12 m away, beyond its 8 m rifle range, to show the advance.
        var definition = VisionDefinition(json => json["vision"]!["hostile_detection_meters"] = 12.5);
        var session = CreateAtMedicEncounter(RangedOnlyPlacement(), definition, crew: RangedOnlyCrew);
        ResumeIntoActiveCombat(session);
        var rangedId = Observe(session).Hostiles![2].Id;
        AdvanceUntil(session, route => route.Hostiles!.Single(enemy => enemy.Id == rangedId).CurrentAction?.Phase == PrimaryActionPhase.Windup, 180);
        var stopped = Observe(session).Hostiles!.Single(enemy => enemy.Id == rangedId);
        Assert.True(stopped.Position.X < 26);
        Assert.InRange(stopped.Position.DistanceTo(Observe(session).Party.Single(actor => actor.Id == ProtectorId).Position), 7.8, 8);
        session.AdvanceTicks(5);
        Assert.Equal(stopped.Position, Observe(session).Hostiles!.Single(enemy => enemy.Id == rangedId).Position);
        AdvanceUntil(session, route => route.Encounter!.Projectiles!.Count > 0, 60);
        var projectile = Assert.Single(Observe(session).Encounter!.Projectiles!);
        Assert.Equal(rangedId, projectile.SourceId);
        session.Execute(new SetPauseCommand(new CommandId("ranged.pause"), true));
        session.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(projectile, Assert.Single(Observe(session).Encounter!.Projectiles!));
        Assert.True(Barrier(session, position: new WorldPosition(16, 0, 8), facing: new WorldPosition(1, 0, 0)).Accepted);
        session.Execute(new SetPauseCommand(new CommandId("ranged.resume"), false));
        session.AdvanceTicks(25);
        Assert.Contains(session.EventsSince(0), item => item.Type == GameplayEventType.ProjectileBlocked
            && item.Detail is ProjectileEventDetail detail && detail.Id == projectile.Id);
        Assert.Equal(150, Observe(session).Party.Single(actor => actor.Id == ProtectorId).Combat!.Health);
    }

    [Fact]
    public void RangedProjectileKeepsDestinationAfterSourceDeathAndCanBeDodged()
    {
        var placement = RangedOnlyPlacement(21);
        var json = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "content", "station-route.json")))!;
        var rifle = json["combat"]!["attacks"]!.AsArray().Single(attack => attack!["id"]!.GetValue<string>() == "attack.enemy.ranged_enforcer.rifle")!;
        rifle["projectile_speed_meters_per_second"] = 4;
        var enemy = json["combat"]!["hostiles"]!.AsArray().Single(hostile => hostile!["id"]!.GetValue<string>() == "actor.enemy.ranged_enforcer.service.3")!;
        enemy["maximum_health"] = 18;
        var session = CreateAtMedicEncounter(placement, StationRouteContent.ParseJson(json.ToJsonString()),
            crew: new CrewStart(new WorldPosition(15, 0, 10), new WorldPosition(15, 0, 8), new WorldPosition(14, 0, 13)));
        ResumeIntoActiveCombat(session);
        AdvanceUntil(session, route => route.Encounter!.Projectiles!.Count > 0, 60);
        var projectile = Assert.Single(Observe(session).Encounter!.Projectiles!);
        Assert.True(Burst(session, projectile.SourceId).Accepted);
        Assert.True(session.Execute(new MoveActorCommand(new CommandId("ranged.dodge"), projectile.TargetId, new WorldPosition(15, 0, 5))).Accepted);
        session.AdvanceTicks(CombatTuning.Burst.WindupTicks);
        var defeated = Observe(session).Hostiles!.Single(hostile => hostile.Id == projectile.SourceId);
        Assert.True(defeated.Combat.IsDefeated);
        Assert.Equal(session.Tick, defeated.Combat.DefeatedAtTick);
        var deathTick = defeated.Combat.DefeatedAtTick;
        var flying = Assert.Single(Observe(session).Encounter!.Projectiles!);
        Assert.Equal(projectile.Destination, flying.Destination);
        session.AdvanceTicks(projectile.FlightTicks);
        Assert.Equal(150, Observe(session).Party.Single(actor => actor.Id == projectile.TargetId).Combat!.Health);
        Assert.Contains(session.EventsSince(0), item => item.Type == GameplayEventType.ProjectileImpacted
            && item.Detail is ProjectileEventDetail detail && detail.Id == projectile.Id);
        Assert.Equal(deathTick, Observe(session).Hostiles!.Single(hostile => hostile.Id == projectile.SourceId).Combat.DefeatedAtTick);
        session.Execute(new StopActorsCommand(new CommandId("ranged.wait.for.defeat"), [ProtagonistId, ProtectorId, MedicId]));
        AdvanceUntil(session, route => route.Encounter!.Phase == EncounterPhase.Defeat, 2400);
        Assert.True(session.Execute(new RestartEncounterCommand(new CommandId("ranged.death.retry"), Observe(session).Encounter!.Id)).Accepted);
        var retried = Observe(session).Hostiles!.Single(hostile => hostile.Id == projectile.SourceId);
        Assert.Null(retried.Combat.DefeatedAtTick);
        Assert.False(retried.Combat.IsDefeated);
    }

    [Fact]
    public void FieldPulsesUseCurrentMembershipRatherThanCastTimeTargets()
    {
        var session = CreateAtMedicEncounter(RangedOnlyPlacement(16),
            crew: new CrewStart(new WorldPosition(14, 0, 8), new WorldPosition(14, 0, 12), new WorldPosition(14, 0, 13)));
        ResumeIntoActiveCombat(session);
        AdvanceUntil(session, route => route.Protagonist.Combat!.Health < 100, 90);
        var center = new WorldPosition(14, 0, 13);
        Assert.True(session.Execute(new UseAbilityCommand(new CommandId("membership.field"), MedicId, FieldId,
            new PositionAbilityTarget(center))).Accepted);
        session.AdvanceTicks(CombatTuning.HealingField.WindupTicks + CombatTuning.HealingField.PulseIntervalTicks);
        Assert.DoesNotContain(session.EventsSince(0), item => item.Detail is HealingAppliedEventDetail healing
            && healing.TargetId == ProtagonistId && healing.AbilityId == FieldId);
        session.Execute(new MoveActorCommand(new CommandId("membership.enter"), ProtagonistId, center));
        AdvanceUntil(session, route => route.Protagonist.Position.DistanceTo(center) < .1, 90);
        session.AdvanceTicks(CombatTuning.HealingField.PulseIntervalTicks);
        Assert.Contains(session.EventsSince(0), item => item.Detail is HealingAppliedEventDetail healing
            && healing.TargetId == ProtagonistId && healing.AbilityId == FieldId);
        session.Execute(new MoveActorCommand(new CommandId("membership.leave"), ProtagonistId, new WorldPosition(14, 0, 8)));
        AdvanceUntil(session, route => route.Protagonist.Position.DistanceTo(center) > CombatTuning.HealingField.RadiusMeters, 60);
        var sequence = session.Observe().LatestEventSequence;
        session.AdvanceTicks(CombatTuning.HealingField.PulseIntervalTicks);
        Assert.DoesNotContain(session.EventsSince(sequence), item => item.Detail is HealingAppliedEventDetail healing
            && healing.TargetId == ProtagonistId && healing.AbilityId == FieldId);
    }
}
