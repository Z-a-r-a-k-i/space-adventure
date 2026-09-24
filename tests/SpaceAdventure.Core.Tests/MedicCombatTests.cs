using System.Text.Json.Nodes;
using SpaceAdventure.Core;
using Xunit;

namespace SpaceAdventure.Core.Tests;

public sealed partial class CombatSessionTests
{
    private static StationEncounterPlacement QuietMedicPlacement() => ExtensionEncounterPlacements()[0] with
    {
        HostilePlacements = ExtensionEncounterPlacements()[0].HostilePlacements!.Select((hostile, index) =>
            hostile with { Position = new WorldPosition(100 + index, 0, 8) }).ToArray(),
    };

    private static CommandAcknowledgement Heal(GameSession session, EntityId target) => session.Execute(
        new UseAbilityCommand(new CommandId($"medic.heal.{session.Tick}"), MedicId, HealId, new EntityAbilityTarget(target)));

    [Fact]
    public void HealRevalidatesTargetRangeAtReleaseWithoutSpendingCooldownOrReplacingAttackIntent()
    {
        var placement = QuietMedicPlacement() with
        {
            CrewRestartPositions = [new(ProtagonistId, new WorldPosition(21.9, 0, 8)),
                new(ProtectorId, new WorldPosition(14, 0, 7)), new(MedicId, new WorldPosition(14, 0, 8))],
            // Keep the assigned enemy visible while isolating healing from incoming damage.
            HostilePlacements = QuietMedicPlacement().HostilePlacements!.Select((hostile, index) => index == 0
                ? hostile with { Position = new WorldPosition(29, 0, 8) } : hostile).ToArray(),
        };
        var session = CreateAtMedicEncounter(placement);
        ResumeIntoActiveCombat(session);
        var enemyId = Observe(session).Hostiles![0].Id;
        Assert.True(Attack(session, MedicId, enemyId).Accepted);
        Assert.True(Heal(session, ProtagonistId).Accepted);
        Assert.True(session.Execute(new MoveActorCommand(new CommandId("ally.leaves.range"), ProtagonistId, new WorldPosition(25, 0, 8))).Accepted);
        session.AdvanceTicks(CombatTuning.DirectHeal.WindupTicks);
        var medic = Observe(session).Party.Single(actor => actor.Id == MedicId);
        Assert.Equal(0, medic.Combat!.Cooldowns.Single(cd => cd.AbilityId == HealId).RemainingTicks);
        Assert.Equal(enemyId, medic.Combat.RememberedAttackTargetId);
        Assert.Contains(session.EventsSince(0), item => item.Detail is PrimaryActionFailedEventDetail failure
            && failure.ActorId == MedicId && failure.Reason == CommandRejectionCode.AbilityTargetOutOfRange);
    }

    [Fact]
    public void FullHealthHealIsValidButNeverOverhealsAndPauseReplacesPendingHeal()
    {
        var session = CreateAtMedicEncounter(QuietMedicPlacement());
        Assert.True(Heal(session, ProtagonistId).Accepted);
        Assert.True(Heal(session, MedicId).Accepted);
        Assert.Equal(MedicId, Observe(session).Party.Single(actor => actor.Id == MedicId).PendingAction!.CombatTargetId);
        session.Advance(TimeSpan.FromSeconds(5));
        Assert.All(Observe(session).Party, actor => Assert.Equal(actor.Combat!.MaximumHealth, actor.Combat.Health));
        ResumeIntoActiveCombat(session);
        session.AdvanceTicks(CombatTuning.DirectHeal.WindupTicks);
        var medic = Observe(session).Party.Single(actor => actor.Id == MedicId);
        Assert.Equal(90, medic.Combat!.Health);
        Assert.Equal(CombatTuning.DirectHeal.CooldownTicks, medic.Combat.Cooldowns.Single(cd => cd.AbilityId == HealId).RemainingTicks);
        Assert.DoesNotContain(session.EventsSince(0), item => item.Type == GameplayEventType.HealingApplied);
    }

    [Fact]
    public void DeadAllyHealIsRejectedAndAQueuedHealCannotReviveATargetDefeatedBeforeRelease()
    {
        var placement = QuietMedicPlacement() with
        {
            CrewRestartPositions = [new(ProtagonistId, new WorldPosition(14, 0, 8)),
                new(ProtectorId, new WorldPosition(14, 0, 12)), new(MedicId, new WorldPosition(14, 0, 13))],
            HostilePlacements = QuietMedicPlacement().HostilePlacements!.Select((hostile, index) => index == 2
                ? hostile with { Position = new WorldPosition(16, 0, 8) } : hostile).ToArray(),
        };
        var json = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "content", "station-route.json")))!;
        json["combat"]!["encounters"]![2]!["protagonist_maximum_health"] = 1;
        var session = CreateAtMedicEncounter(placement, StationRouteContent.ParseJson(json.ToJsonString()));
        ResumeIntoActiveCombat(session);
        AdvanceUntil(session, route => route.Encounter!.Projectiles!.Count > 0, 60);
        Assert.True(Heal(session, ProtagonistId).Accepted);
        session.AdvanceTicks(CombatTuning.DirectHeal.WindupTicks);
        Assert.True(Observe(session).Protagonist.Combat!.IsDefeated);
        Assert.Equal(0, Observe(session).Party.Single(actor => actor.Id == MedicId).Combat!.Cooldowns.Single(cd => cd.AbilityId == HealId).RemainingTicks);
        var rejected = Heal(session, ProtagonistId);
        Assert.Equal(CommandRejectionCode.CombatantDefeated, rejected.RejectionCode);
        Assert.Equal(0, Observe(session).Protagonist.Combat!.Health);
    }

    [Fact]
    public void FieldOutlivesDefeatedCasterAndExpiresOnItsOwnFixedDeadline()
    {
        var placement = QuietMedicPlacement() with
        {
            CrewRestartPositions = [new(ProtagonistId, new WorldPosition(14, 0, 12)),
                new(ProtectorId, new WorldPosition(14, 0, 13)), new(MedicId, new WorldPosition(14, 0, 8))],
            HostilePlacements = QuietMedicPlacement().HostilePlacements!.Select((hostile, index) => index == 2
                ? hostile with { Position = new WorldPosition(16, 0, 8) } : hostile).ToArray(),
        };
        var json = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "content", "station-route.json")))!;
        json["combat"]!["medic_maximum_health"] = 1;
        var session = CreateAtMedicEncounter(placement, StationRouteContent.ParseJson(json.ToJsonString()));
        ResumeIntoActiveCombat(session);
        // Centre the field between Medic (14, 8) and Vanguard (14, 12) so both stand inside it.
        Assert.True(session.Execute(new UseAbilityCommand(new CommandId("field.after.death"), MedicId, FieldId,
            new PositionAbilityTarget(new WorldPosition(14, 0, 10)))).Accepted);
        session.AdvanceTicks(CombatTuning.HealingField.WindupTicks);
        var deployed = Observe(session).Encounter!.HealingField!;
        AdvanceUntil(session, route => route.Party.Single(actor => actor.Id == MedicId).Combat!.IsDefeated, 90);
        Assert.NotNull(Observe(session).Encounter!.HealingField);
        session.AdvanceTicks((int)(deployed.DeployedAtTick + CombatTuning.HealingField.DurationTicks - session.Tick - 1));
        Assert.Equal(1, Observe(session).Encounter!.HealingField!.RemainingTicks);
        Assert.Contains(session.EventsSince(0), item => item.Detail is HealingAppliedEventDetail healing
            && healing.SourceId == MedicId && healing.AbilityId == FieldId);
        session.AdvanceTicks(1);
        Assert.Null(Observe(session).Encounter!.HealingField);
        // Every later pulse covered the fallen Medic; none of them revived her.
        var fallen = Observe(session).Party.Single(actor => actor.Id == MedicId);
        Assert.True(fallen.Position.DistanceTo(deployed.Position) <= deployed.RadiusMeters);
        Assert.Equal(0, fallen.Combat!.Health);
    }
}
