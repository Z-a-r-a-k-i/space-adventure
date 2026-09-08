using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpaceAdventure.Core;

public enum StationInteractionKind
{
    Npc,
    Environment,
    Destination,
}

public enum StationInteractionEffect
{
    BeginSurvivorDialogue,
    BeginRecruitmentDialogue,
    RecordObservation,
    OpenEntryServiceDoor,
    OpenSoloExitServiceDoor,
    CompleteScenario,
}

public enum StationDialogueResponseEffect
{
    RerouteServicePower,
    PreserveShelterPower,
    RecruitProtector,
}

public sealed record StationActorDefinition(
    EntityId Id,
    string DisplayName,
    double MovementSpeedMetersPerSecond,
    PartyMemberLoadoutDefinition? Loadout);

public sealed record PartyMemberLoadoutDefinition(
    string WeaponName,
    AttackId BasicAttackId,
    AbilityId ActiveAbilityId,
    string ActiveAbilityName,
    AbilityTargetKind ActiveAbilityTargetKind,
    AbilityId SecondaryAbilityId,
    string SecondaryAbilityName,
    AbilityTargetKind SecondaryAbilityTargetKind);

public sealed record ProtagonistKitDefinition(
    ProtagonistKitId Id,
    string DisplayName,
    string Role,
    string WeaponName,
    AttackId BasicAttackId,
    AbilityId ActiveAbilityId,
    string ActiveAbilityName,
    AbilityTargetKind ActiveAbilityTargetKind,
    AbilityId SecondaryAbilityId,
    string SecondaryAbilityName,
    AbilityTargetKind SecondaryAbilityTargetKind);

public sealed record StationObjectiveDefinition(ObjectiveId Id, string Text);

public sealed record StationDialogueResponseDefinition(
    DialogueResponseId Id,
    string Text,
    StationDialogueResponseEffect Effect);

public sealed record StationDialogueDefinition(
    string Speaker,
    string Line,
    IReadOnlyList<StationDialogueResponseDefinition> Responses);

public sealed record StationInteractionDefinition(
    EntityId Id,
    StationInteractionKind Kind,
    string Prompt,
    double UseRadiusMeters,
    StationInteractionEffect Effect,
    string? ResultText,
    string? PreservedResultText,
    StationDialogueDefinition? Dialogue);

public sealed class StationRouteDefinition
{
    internal StationRouteDefinition(
        int schemaVersion,
        string contentRevision,
        ScenarioId scenarioId,
        StationActorDefinition protagonist,
        StationActorDefinition companion,
        IEnumerable<ProtagonistKitDefinition> protagonistKits,
        StationObjectiveDefinition briefingObjective,
        StationObjectiveDefinition entryDoorObjective,
        StationObjectiveDefinition combatThresholdObjective,
        StationObjectiveDefinition combatObjective,
        StationObjectiveDefinition soloExitDoorObjective,
        StationObjectiveDefinition recruitmentObjective,
        StationObjectiveDefinition mainCombatObjective,
        StationObjectiveDefinition destinationObjective,
        IEnumerable<StationInteractionDefinition> interactions,
        StationCombatDefinition combat)
    {
        SchemaVersion = schemaVersion;
        ContentRevision = contentRevision;
        ScenarioId = scenarioId;
        Protagonist = protagonist;
        Companion = companion;
        ProtagonistKits = new ReadOnlyCollection<ProtagonistKitDefinition>(
            protagonistKits.ToArray());
        BriefingObjective = briefingObjective;
        EntryDoorObjective = entryDoorObjective;
        CombatThresholdObjective = combatThresholdObjective;
        CombatObjective = combatObjective;
        SoloExitDoorObjective = soloExitDoorObjective;
        RecruitmentObjective = recruitmentObjective;
        MainCombatObjective = mainCombatObjective;
        DestinationObjective = destinationObjective;
        Interactions = new ReadOnlyCollection<StationInteractionDefinition>(
            interactions.ToArray());
        Combat = combat;
    }

    public int SchemaVersion { get; }

    public string ContentRevision { get; }

    public ScenarioId ScenarioId { get; }

    public StationActorDefinition Protagonist { get; }

    public StationActorDefinition Companion { get; }

    public IReadOnlyList<ProtagonistKitDefinition> ProtagonistKits { get; }

    public StationObjectiveDefinition BriefingObjective { get; }

    public StationObjectiveDefinition EntryDoorObjective { get; }

    public StationObjectiveDefinition CombatThresholdObjective { get; }

    public StationObjectiveDefinition CombatObjective { get; }

    public StationObjectiveDefinition SoloExitDoorObjective { get; }

    public StationObjectiveDefinition RecruitmentObjective { get; }

    public StationObjectiveDefinition MainCombatObjective { get; }

    public StationObjectiveDefinition DestinationObjective { get; }

    public IReadOnlyList<StationInteractionDefinition> Interactions { get; }

    public StationCombatDefinition Combat { get; }
}

public static class StationRouteContent
{
    public const int SupportedSchemaVersion = 9;

    private const int MaximumIdLength = 128;
    private const int MaximumTextLength = 4096;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static StationRouteDefinition ParseJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        StationRouteDto dto;
        try
        {
            dto = JsonSerializer.Deserialize<StationRouteDto>(json, JsonOptions)
                ?? throw new InvalidDataException("Station route content cannot be null.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Station route content is not valid schema-v{SupportedSchemaVersion} JSON.", exception);
        }

        if (dto.SchemaVersion != SupportedSchemaVersion)
        {
            throw new InvalidDataException(
                $"Unsupported station route schema version '{dto.SchemaVersion}'.");
        }

        var contentRevision = RequireText(dto.ContentRevision, "content_revision", MaximumIdLength);
        var scenarioId = new ScenarioId(RequireText(dto.ScenarioId, "scenario_id", MaximumIdLength));
        var protagonist = ParseActor(dto.Protagonist, "protagonist", requiresLoadout: false);
        var companion = ParseActor(dto.Companion, "companion", requiresLoadout: true);
        if (protagonist.Id == companion.Id)
        {
            throw new InvalidDataException("Protagonist and companion IDs must be distinct.");
        }

        var kits = ParseKits(dto.ProtagonistKits);
        ValidatePartyLoadoutIdentifiers(kits, companion.Loadout!);
        var briefingObjective = ParseObjective(dto.BriefingObjective, "briefing_objective");
        var entryDoorObjective = ParseObjective(dto.EntryDoorObjective, "entry_door_objective");
        var combatThresholdObjective = ParseObjective(
            dto.CombatThresholdObjective,
            "combat_threshold_objective");
        var combatObjective = ParseObjective(dto.CombatObjective, "combat_objective");
        var soloExitDoorObjective = ParseObjective(
            dto.SoloExitDoorObjective,
            "solo_exit_door_objective");
        var recruitmentObjective = ParseObjective(dto.RecruitmentObjective, "recruitment_objective");
        var mainCombatObjective = ParseObjective(dto.MainCombatObjective, "main_combat_objective");
        var destinationObjective = ParseObjective(dto.DestinationObjective, "destination_objective");
        if (new[]
            {
                briefingObjective.Id,
                entryDoorObjective.Id,
                combatThresholdObjective.Id,
                combatObjective.Id,
                soloExitDoorObjective.Id,
                recruitmentObjective.Id,
                mainCombatObjective.Id,
                destinationObjective.Id,
            }
            .Distinct()
            .Count() != 8)
        {
            throw new InvalidDataException("Station route objective IDs must be distinct.");
        }

        if (dto.Interactions is null || dto.Interactions.Count == 0)
        {
            throw new InvalidDataException("Station route content must define interactions.");
        }

        var interactions = dto.Interactions.Select(ParseInteraction).ToArray();
        var combat = ParseCombat(dto.Combat, kits, protagonist.Id, companion.Id, companion.Loadout!);
        ValidateInteractionSet(protagonist.Id, companion.Id, combat.Hostiles.Select(hostile => hostile.Id), interactions);

        return new StationRouteDefinition(
            dto.SchemaVersion,
            contentRevision,
            scenarioId,
            protagonist,
            companion,
            kits,
            briefingObjective,
            entryDoorObjective,
            combatThresholdObjective,
            combatObjective,
            soloExitDoorObjective,
            recruitmentObjective,
            mainCombatObjective,
            destinationObjective,
            interactions,
            combat);
    }

    private static ProtagonistKitDefinition[] ParseKits(List<KitDto?>? kitDtos)
    {
        if (kitDtos is null
            || kitDtos.Count != 1
            || kitDtos.Any(kit => kit is null))
        {
            throw new InvalidDataException(
                "Station route content requires exactly one protagonist kit.");
        }

        var kits = kitDtos.Select(kit =>
        {
            var value = kit!;
            return new ProtagonistKitDefinition(
                new ProtagonistKitId(RequireText(value.Id, "protagonist_kits[].id", MaximumIdLength)),
                RequireText(value.DisplayName, "protagonist_kits[].display_name", MaximumTextLength),
                RequireText(value.Role, "protagonist_kits[].role", MaximumTextLength),
                RequireText(value.WeaponName, "protagonist_kits[].weapon_name", MaximumTextLength),
                new AttackId(RequireText(value.BasicAttackId, "protagonist_kits[].basic_attack_id", MaximumIdLength)),
                new AbilityId(RequireText(value.ActiveAbilityId, "protagonist_kits[].active_ability_id", MaximumIdLength)),
                RequireText(value.ActiveAbilityName, "protagonist_kits[].active_ability_name", MaximumTextLength),
                ParseAbilityTargetKind(value.ActiveAbilityTargetKind),
                new AbilityId(RequireText(value.SecondaryAbilityId, "secondary_ability_id", MaximumIdLength)),
                RequireText(value.SecondaryAbilityName, "secondary_ability_name", MaximumTextLength),
                ParseAbilityTargetKind(value.SecondaryAbilityTargetKind));
        }).ToArray();

        if (kits.Select(kit => kit.Id).Distinct().Count() != kits.Length
            || kits.Select(kit => kit.BasicAttackId).Distinct().Count() != kits.Length
            || kits.Select(kit => kit.ActiveAbilityId).Distinct().Count() != kits.Length)
        {
            throw new InvalidDataException("Protagonist kit, attack, and ability IDs must be unique.");
        }

        return kits;
    }

    private static void ValidatePartyLoadoutIdentifiers(
        IReadOnlyCollection<ProtagonistKitDefinition> kits,
        PartyMemberLoadoutDefinition companionLoadout)
    {
        if (kits.Any(kit => kit.BasicAttackId == companionLoadout.BasicAttackId)
            || kits.Any(kit => kit.ActiveAbilityId == companionLoadout.ActiveAbilityId))
        {
            throw new InvalidDataException(
                "Attack and ability IDs must be unique across protagonist kits and the companion loadout.");
        }
    }

    private static AbilityTargetKind ParseAbilityTargetKind(string? value)
    {
        return value switch
        {
            "position" => AbilityTargetKind.Position,
            "entity" => AbilityTargetKind.Entity,
            "barrier" => AbilityTargetKind.Barrier,
            "self" => AbilityTargetKind.Self,
            _ => throw new InvalidDataException($"Unknown active ability target kind '{value}'."),
        };
    }

    private static StationActorDefinition ParseActor(
        ActorDto? actor,
        string field,
        bool requiresLoadout)
    {
        if (actor is null)
        {
            throw new InvalidDataException($"Station route content requires {field}.");
        }

        var speed = actor.MovementSpeedMetersPerSecond;
        if (!double.IsFinite(speed) || speed <= 0 || speed > 20)
        {
            throw new InvalidDataException(
                $"{field}.movement_speed_meters_per_second must be greater than zero and at most 20.");
        }

        PartyMemberLoadoutDefinition? loadout = null;
        if (actor.Loadout is not null)
        {
            loadout = new PartyMemberLoadoutDefinition(
                RequireText(actor.Loadout.WeaponName, $"{field}.loadout.weapon_name", MaximumTextLength),
                new AttackId(RequireText(
                    actor.Loadout.BasicAttackId,
                    $"{field}.loadout.basic_attack_id",
                    MaximumIdLength)),
                new AbilityId(RequireText(
                    actor.Loadout.ActiveAbilityId,
                    $"{field}.loadout.active_ability_id",
                    MaximumIdLength)),
                RequireText(
                    actor.Loadout.ActiveAbilityName,
                    $"{field}.loadout.active_ability_name",
                    MaximumTextLength),
                ParseAbilityTargetKind(actor.Loadout.ActiveAbilityTargetKind),
                new AbilityId(RequireText(actor.Loadout.SecondaryAbilityId, "secondary_ability_id", MaximumIdLength)),
                RequireText(actor.Loadout.SecondaryAbilityName, "secondary_ability_name", MaximumTextLength),
                ParseAbilityTargetKind(actor.Loadout.SecondaryAbilityTargetKind));
        }

        if (requiresLoadout != (loadout is not null))
        {
            throw new InvalidDataException(
                requiresLoadout
                    ? $"{field} requires a tactical loadout."
                    : $"{field} loadout must be selected from protagonist_kits instead.");
        }

        return new StationActorDefinition(
            new EntityId(RequireText(actor.Id, $"{field}.id", MaximumIdLength)),
            RequireText(actor.DisplayName, $"{field}.display_name", MaximumTextLength),
            speed,
            loadout);
    }

    private static StationObjectiveDefinition ParseObjective(ObjectiveDto? objective, string field)
    {
        if (objective is null)
        {
            throw new InvalidDataException($"Station route content requires {field}.");
        }

        return new StationObjectiveDefinition(
            new ObjectiveId(RequireText(objective.Id, $"{field}.id", MaximumIdLength)),
            RequireText(objective.Text, $"{field}.text", MaximumTextLength));
    }

    private static StationInteractionDefinition ParseInteraction(InteractionDto? interaction)
    {
        if (interaction is null)
        {
            throw new InvalidDataException("Station route interactions cannot contain null entries.");
        }

        var id = new EntityId(RequireText(interaction.Id, "interactions[].id", MaximumIdLength));
        var kind = ParseInteractionKind(interaction.Kind, id);
        var effect = ParseInteractionEffect(interaction.Effect, id);
        var radius = interaction.UseRadiusMeters;
        if (!double.IsFinite(radius) || radius <= 0 || radius > 10)
        {
            throw new InvalidDataException(
                $"Interaction '{id}' use_radius_meters must be greater than zero and at most 10.");
        }

        StationDialogueDefinition? dialogue = null;
        if (interaction.Dialogue is not null)
        {
            if (interaction.Dialogue.Responses is null
                || interaction.Dialogue.Responses.Count == 0
                || interaction.Dialogue.Responses.Any(response => response is null))
            {
                throw new InvalidDataException($"Interaction '{id}' dialogue requires responses.");
            }

            var responses = interaction.Dialogue.Responses.Select((response, responseIndex) =>
            {
                var value = response!;
                return new StationDialogueResponseDefinition(
                    new DialogueResponseId(RequireText(
                        value.Id,
                        $"interactions[{id}].dialogue.responses[{responseIndex}].id",
                        MaximumIdLength)),
                    RequireText(
                        value.Text,
                        $"interactions[{id}].dialogue.responses[{responseIndex}].text",
                        MaximumTextLength),
                    ParseDialogueResponseEffect(value.Effect, id));
            }).ToArray();

            if (responses.Select(response => response.Id).Distinct().Count() != responses.Length)
            {
                throw new InvalidDataException($"Interaction '{id}' dialogue response IDs must be unique.");
            }

            dialogue = new StationDialogueDefinition(
                RequireText(interaction.Dialogue.Speaker, $"interactions[{id}].dialogue.speaker", MaximumTextLength),
                RequireText(interaction.Dialogue.Line, $"interactions[{id}].dialogue.line", MaximumTextLength),
                responses);
        }

        var resultText = OptionalText(interaction.ResultText, $"interactions[{id}].result_text");
        var preservedResultText = OptionalText(
            interaction.PreservedResultText,
            $"interactions[{id}].preserved_result_text");
        ValidateInteractionShape(id, kind, effect, dialogue, resultText, preservedResultText);

        return new StationInteractionDefinition(
            id,
            kind,
            RequireText(interaction.Prompt, $"interactions[{id}].prompt", MaximumTextLength),
            radius,
            effect,
            resultText,
            preservedResultText,
            dialogue);
    }

    private static StationInteractionKind ParseInteractionKind(string? value, EntityId id)
    {
        return value switch
        {
            "npc" => StationInteractionKind.Npc,
            "environment" => StationInteractionKind.Environment,
            "destination" => StationInteractionKind.Destination,
            _ => throw new InvalidDataException($"Interaction '{id}' has unknown kind '{value}'."),
        };
    }

    private static StationInteractionEffect ParseInteractionEffect(string? value, EntityId id)
    {
        return value switch
        {
            "begin_survivor_dialogue" => StationInteractionEffect.BeginSurvivorDialogue,
            "begin_recruitment_dialogue" => StationInteractionEffect.BeginRecruitmentDialogue,
            "record_observation" => StationInteractionEffect.RecordObservation,
            "open_entry_service_door" => StationInteractionEffect.OpenEntryServiceDoor,
            "open_solo_exit_service_door" => StationInteractionEffect.OpenSoloExitServiceDoor,
            "complete_scenario" => StationInteractionEffect.CompleteScenario,
            _ => throw new InvalidDataException($"Interaction '{id}' has unknown effect '{value}'."),
        };
    }

    private static StationDialogueResponseEffect ParseDialogueResponseEffect(
        string? value,
        EntityId interactionId)
    {
        return value switch
        {
            "reroute_service_power" => StationDialogueResponseEffect.RerouteServicePower,
            "preserve_shelter_power" => StationDialogueResponseEffect.PreserveShelterPower,
            "recruit_protector" => StationDialogueResponseEffect.RecruitProtector,
            _ => throw new InvalidDataException(
                $"Interaction '{interactionId}' has unknown response effect '{value}'."),
        };
    }

    private static void ValidateInteractionShape(
        EntityId id,
        StationInteractionKind kind,
        StationInteractionEffect effect,
        StationDialogueDefinition? dialogue,
        string? resultText,
        string? preservedResultText)
    {
        if (effect is StationInteractionEffect.BeginSurvivorDialogue
            or StationInteractionEffect.BeginRecruitmentDialogue)
        {
            if (kind != StationInteractionKind.Npc
                || dialogue is null
                || resultText is not null
                || preservedResultText is not null)
            {
                throw new InvalidDataException(
                    $"Dialogue interaction '{id}' must be an NPC with dialogue and no result text.");
            }

            var responseEffects = dialogue.Responses.Select(response => response.Effect).ToArray();
            if (effect == StationInteractionEffect.BeginSurvivorDialogue
                && (!responseEffects.Order().SequenceEqual(new[]
                    {
                        StationDialogueResponseEffect.RerouteServicePower,
                        StationDialogueResponseEffect.PreserveShelterPower,
                    }.Order())))
            {
                throw new InvalidDataException(
                    $"Survivor interaction '{id}' requires exactly the two route-power responses.");
            }

            if (effect == StationInteractionEffect.BeginRecruitmentDialogue
                && (responseEffects.Length != 1
                    || responseEffects[0] != StationDialogueResponseEffect.RecruitProtector))
            {
                throw new InvalidDataException(
                    $"Recruitment interaction '{id}' requires exactly one recruit response.");
            }

            return;
        }

        if (dialogue is not null || string.IsNullOrWhiteSpace(resultText))
        {
            throw new InvalidDataException(
                $"Non-dialogue interaction '{id}' requires result_text and cannot define dialogue.");
        }

        if (effect == StationInteractionEffect.RecordObservation)
        {
            if (kind != StationInteractionKind.Environment
                || string.IsNullOrWhiteSpace(preservedResultText))
            {
                throw new InvalidDataException(
                    $"Observation interaction '{id}' requires both route-power result texts.");
            }

            return;
        }

        if (effect is StationInteractionEffect.OpenEntryServiceDoor
            or StationInteractionEffect.OpenSoloExitServiceDoor)
        {
            if (kind != StationInteractionKind.Environment
                || preservedResultText is not null)
            {
                throw new InvalidDataException(
                    $"Service-door interaction '{id}' must be an environment interaction with one result text.");
            }

            return;
        }

        if (effect != StationInteractionEffect.CompleteScenario
            || kind != StationInteractionKind.Destination
            || preservedResultText is not null)
        {
            throw new InvalidDataException($"Completion interaction '{id}' has an invalid shape.");
        }
    }

    private static StationCombatDefinition ParseCombat(
        CombatDto? combat,
        IReadOnlyList<ProtagonistKitDefinition> kits,
        EntityId protagonistId,
        EntityId companionId,
        PartyMemberLoadoutDefinition companionLoadout)
    {
        if (combat is null)
        {
            throw new InvalidDataException("Station route content requires combat.");
        }

        if (combat.Attacks is null
            || combat.Attacks.Count != 4
            || combat.Attacks.Any(attack => attack is null))
        {
            throw new InvalidDataException("Station combat requires exactly four attacks.");
        }

        var attacks = combat.Attacks.Select((attack, index) =>
        {
            var value = attack!;
            return new AttackDefinition(
                new AttackId(RequireText(value.Id, $"combat.attacks[{index}].id", MaximumIdLength)),
                RequirePositiveBounded(value.RangeMeters, $"combat.attacks[{index}].range_meters", 50),
                RequirePositiveBounded(value.Damage, $"combat.attacks[{index}].damage", 10000),
                RequirePositiveBounded(value.WindupTicks, $"combat.attacks[{index}].windup_ticks", 3000),
                RequirePositiveBounded(value.RecoveryTicks, $"combat.attacks[{index}].recovery_ticks", 3000),
                value.ProjectileSpeedMetersPerSecond == 0 ? 0
                    : RequirePositiveBounded(value.ProjectileSpeedMetersPerSecond, $"combat.attacks[{index}].projectile_speed_meters_per_second", 100));
        }).ToArray();
        if (attacks.Select(attack => attack.Id).Distinct().Count() != attacks.Length)
        {
            throw new InvalidDataException("Combat attack IDs must be unique.");
        }

        var abilityDto = combat.ProtagonistAbility
            ?? throw new InvalidDataException("Combat requires protagonist_ability.");
        var ability = new AbilityDefinition(
            new AbilityId(RequireText(abilityDto.Id, "combat.protagonist_ability.id", MaximumIdLength)),
            ParseAbilityTargetKind(abilityDto.TargetKind),
            RequirePositiveBounded(abilityDto.RangeMeters, "combat.protagonist_ability.range_meters", 50),
            RequirePositiveBounded(abilityDto.RadiusMeters, "combat.protagonist_ability.radius_meters", 20),
            RequirePositiveBounded(abilityDto.Damage, "combat.protagonist_ability.damage", 10000),
            RequirePositiveBounded(abilityDto.WindupTicks, "combat.protagonist_ability.windup_ticks", 3000),
            RequirePositiveBounded(abilityDto.RecoveryTicks, "combat.protagonist_ability.recovery_ticks", 3000),
            RequirePositiveBounded(abilityDto.CooldownTicks, "combat.protagonist_ability.cooldown_ticks", 30000),
            abilityDto.InterruptsWindup);

        if (combat.Hostiles is not { Count: 3 } || combat.Hostiles.Any(hostile => hostile is null)
            || combat.Encounters is not { Count: 2 } || combat.Encounters.Any(encounter => encounter is null))
        {
            throw new InvalidDataException("Station combat requires three hostiles across two encounters.");
        }
        var hostiles = combat.Hostiles.Select(value =>
        {
            var hostile = value!;
            var behavior = hostile.Behavior switch
            {
                "melee" => HostileBehavior.Melee,
                "sentry" => HostileBehavior.Sentry,
                _ => throw new InvalidDataException("Unknown hostile behavior."),
            };
            var speed = hostile.MovementSpeedMetersPerSecond;
            if (behavior == HostileBehavior.Sentry && speed != 0)
            {
                throw new InvalidDataException("Sentries must remain stationary.");
            }
            if (behavior == HostileBehavior.Melee) { RequirePositiveBounded(speed, "hostile speed", 20); }
            return new HostileDefinition(
                new EntityId(RequireText(hostile.Id, "hostile.id", MaximumIdLength)),
                RequireText(hostile.DisplayName, "hostile.display_name", MaximumTextLength), speed,
                RequirePositiveBounded(hostile.MaximumHealth, "hostile.maximum_health", 10000),
                new AttackId(RequireText(hostile.BasicAttackId, "hostile.basic_attack_id", MaximumIdLength)), behavior);
        }).ToArray();
        var encounters = combat.Encounters.Select(value =>
        {
            var encounter = value!;
            if (encounter.HostileIds is null || encounter.HostileIds.Count != (encounter.RequiresCompanion ? 2 : 1))
            {
                throw new InvalidDataException("Encounter has the wrong hostile count.");
            }
            return new EncounterDefinition(
                new EncounterId(RequireText(encounter.Id, "encounter.id", MaximumIdLength)),
                Array.AsReadOnly(encounter.HostileIds.Select(id => new EntityId(RequireText(id, "encounter.hostile_ids", MaximumIdLength))).ToArray()),
                RequirePositiveBounded(encounter.ProtagonistMaximumHealth, "encounter.protagonist_maximum_health", 10000),
                RequirePositiveBounded(encounter.ReadyingTicks, "encounter.readying_ticks", 3000),
                RequirePositiveBounded(encounter.SecuringTicks, "encounter.securing_ticks", 3000),
                encounter.RequiresCompanion);
        }).ToArray();
        var barrierDto = combat.Barrier ?? throw new InvalidDataException("Combat requires barrier.");
        var barrier = new BarrierDefinition(
            new AbilityId(RequireText(barrierDto.Id, "barrier.id", MaximumIdLength)),
            RequirePositiveBounded(barrierDto.RangeMeters, "barrier.range_meters", 10),
            RequirePositiveBounded(barrierDto.WindupTicks, "barrier.windup_ticks", 3000),
            RequirePositiveBounded(barrierDto.RecoveryTicks, "barrier.recovery_ticks", 3000),
            RequirePositiveBounded(barrierDto.DurationTicks, "barrier.duration_ticks", 3000),
            RequirePositiveBounded(barrierDto.CooldownTicks, "barrier.cooldown_ticks", 30000),
            RequirePositiveBounded(barrierDto.WidthMeters, "barrier.width_meters", 10),
            RequirePositiveBounded(barrierDto.HeightMeters, "barrier.height_meters", 5),
            RequirePositiveBounded(barrierDto.CenterHeightMeters, "barrier.center_height_meters", 3));
        var burstDto = combat.Burst ?? throw new InvalidDataException("Combat requires burst.");
        var burst = new BurstDefinition(
            new AbilityId(RequireText(burstDto.Id, "burst.id", MaximumIdLength)),
            RequirePositiveBounded(burstDto.RangeMeters, "burst.range_meters", 50),
            RequirePositiveBounded(burstDto.DamagePerShot, "burst.damage_per_shot", 10000),
            RequirePositiveBounded(burstDto.ShotCount, "burst.shot_count", 6),
            RequirePositiveBounded(burstDto.ShotIntervalTicks, "burst.shot_interval_ticks", 60),
            RequirePositiveBounded(burstDto.WindupTicks, "burst.windup_ticks", 3000),
            RequirePositiveBounded(burstDto.RecoveryTicks, "burst.recovery_ticks", 3000),
            RequirePositiveBounded(burstDto.CooldownTicks, "burst.cooldown_ticks", 30000));
        var tauntDto = combat.Taunt ?? throw new InvalidDataException("Combat requires taunt.");
        var taunt = new TauntDefinition(
            new AbilityId(RequireText(tauntDto.Id, "taunt.id", MaximumIdLength)),
            RequirePositiveBounded(tauntDto.RadiusMeters, "taunt.radius_meters", 20),
            RequirePositiveBounded(tauntDto.DurationTicks, "taunt.duration_ticks", 3000),
            RequirePositiveBounded(tauntDto.WindupTicks, "taunt.windup_ticks", 3000),
            RequirePositiveBounded(tauntDto.RecoveryTicks, "taunt.recovery_ticks", 3000),
            RequirePositiveBounded(tauntDto.CooldownTicks, "taunt.cooldown_ticks", 30000));
        var hostileIds = hostiles.Select(hostile => hostile.Id).ToHashSet();
        var encounterHostiles = encounters.SelectMany(encounter => encounter.HostileIds).ToArray();
        var attackIds = attacks.Select(attack => attack.Id).ToHashSet();
        if (hostileIds.Count != 3 || hostileIds.Contains(protagonistId) || hostileIds.Contains(companionId)
            || encounters.Select(encounter => encounter.Id).Distinct().Count() != 2
            || encounters.Count(encounter => encounter.RequiresCompanion) != 1
            || encounterHostiles.Distinct().Count() != 3 || !hostileIds.SetEquals(encounterHostiles)
            || hostiles.Any(hostile => !attackIds.Contains(hostile.BasicAttackId))
            || !attackIds.Contains(kits.Single().BasicAttackId) || !attackIds.Contains(companionLoadout.BasicAttackId)
            || kits.Single().ActiveAbilityId != ability.Id || ability.TargetKind != AbilityTargetKind.Position
            || new[] { barrier.Id, ability.Id, burst.Id, taunt.Id }.Distinct().Count() != 4
            || kits.Single().ActiveAbilityTargetKind != AbilityTargetKind.Position
            || kits.Single().SecondaryAbilityId != burst.Id || kits.Single().SecondaryAbilityTargetKind != AbilityTargetKind.Entity
            || companionLoadout.SecondaryAbilityId != taunt.Id || companionLoadout.SecondaryAbilityTargetKind != AbilityTargetKind.Self
            || companionLoadout.ActiveAbilityId != barrier.Id
            || companionLoadout.ActiveAbilityTargetKind != AbilityTargetKind.Barrier)
        {
            throw new InvalidDataException("Combat identifiers and loadout references must be unique and complete.");
        }
        var solo = encounters.Single(encounter => !encounter.RequiresCompanion);
        if (hostiles.Any(hostile => (hostile.Behavior == HostileBehavior.Sentry)
            != (attacks.Single(attack => attack.Id == hostile.BasicAttackId).ProjectileSpeedMetersPerSecond > 0)))
        { throw new InvalidDataException("Sentry attacks require projectile speed; melee attacks are immediate."); }
        var soloHostile = hostiles.Single(hostile => hostile.Id == solo.HostileIds.Single());
        if (soloHostile.Behavior != HostileBehavior.Melee || hostiles.Count(hostile => hostile.Behavior == HostileBehavior.Sentry) != 1)
        {
            throw new InvalidDataException("The solo encounter uses melee; the party encounter adds one sentry.");
        }
        return new StationCombatDefinition(Array.AsReadOnly(attacks), ability, Array.AsReadOnly(hostiles),
            Array.AsReadOnly(encounters), barrier,
            RequirePositiveBounded(combat.CompanionMaximumHealth, "combat.companion_maximum_health", 10000), burst, taunt);
    }

    private static int RequirePositiveBounded(int value, string field, int maximum)
    {
        if (value <= 0 || value > maximum)
        {
            throw new InvalidDataException($"{field} must be greater than zero and at most {maximum}.");
        }

        return value;
    }

    private static double RequirePositiveBounded(double value, string field, double maximum)
    {
        if (!double.IsFinite(value) || value <= 0 || value > maximum)
        {
            throw new InvalidDataException($"{field} must be greater than zero and at most {maximum}.");
        }

        return value;
    }

    private static void ValidateInteractionSet(
        EntityId protagonistId,
        EntityId companionId,
        IEnumerable<EntityId> hostileIds,
        IReadOnlyCollection<StationInteractionDefinition> interactions)
    {
        var identifiers = new HashSet<EntityId>(hostileIds) { protagonistId, companionId };
        foreach (var interaction in interactions)
        {
            if (!identifiers.Add(interaction.Id))
            {
                throw new InvalidDataException($"Gameplay entity ID '{interaction.Id}' is duplicated.");
            }
        }

        RequireExactlyOne(interactions, StationInteractionEffect.BeginSurvivorDialogue);
        RequireExactlyOne(interactions, StationInteractionEffect.BeginRecruitmentDialogue);
        RequireExactlyOne(interactions, StationInteractionEffect.RecordObservation);
        RequireExactlyOne(interactions, StationInteractionEffect.OpenEntryServiceDoor);
        RequireExactlyOne(interactions, StationInteractionEffect.OpenSoloExitServiceDoor);
        RequireExactlyOne(interactions, StationInteractionEffect.CompleteScenario);

        var responseIds = interactions
            .Where(interaction => interaction.Dialogue is not null)
            .SelectMany(interaction => interaction.Dialogue!.Responses)
            .Select(response => response.Id)
            .ToArray();
        if (responseIds.Distinct().Count() != responseIds.Length)
        {
            throw new InvalidDataException("Dialogue response IDs must be unique across the station route.");
        }
    }

    private static void RequireExactlyOne(
        IEnumerable<StationInteractionDefinition> interactions,
        StationInteractionEffect effect)
    {
        if (interactions.Count(interaction => interaction.Effect == effect) != 1)
        {
            throw new InvalidDataException(
                $"Station route content must define exactly one '{effect}' interaction.");
        }
    }

    private static string RequireText(string? value, string field, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximumLength)
        {
            throw new InvalidDataException(
                $"{field} must contain between 1 and {maximumLength} characters.");
        }

        return value;
    }

    private static string? OptionalText(string? value, string field)
    {
        return value is null ? null : RequireText(value, field, MaximumTextLength);
    }

    private sealed class StationRouteDto
    {
        [JsonPropertyName("schema_version")]
        public int SchemaVersion { get; init; }

        [JsonPropertyName("content_revision")]
        public string? ContentRevision { get; init; }

        [JsonPropertyName("scenario_id")]
        public string? ScenarioId { get; init; }

        [JsonPropertyName("protagonist")]
        public ActorDto? Protagonist { get; init; }

        [JsonPropertyName("companion")]
        public ActorDto? Companion { get; init; }

        [JsonPropertyName("protagonist_kits")]
        public List<KitDto?>? ProtagonistKits { get; init; }

        [JsonPropertyName("briefing_objective")]
        public ObjectiveDto? BriefingObjective { get; init; }

        [JsonPropertyName("entry_door_objective")]
        public ObjectiveDto? EntryDoorObjective { get; init; }

        [JsonPropertyName("combat_threshold_objective")]
        public ObjectiveDto? CombatThresholdObjective { get; init; }

        [JsonPropertyName("combat_objective")]
        public ObjectiveDto? CombatObjective { get; init; }

        [JsonPropertyName("solo_exit_door_objective")]
        public ObjectiveDto? SoloExitDoorObjective { get; init; }

        [JsonPropertyName("recruitment_objective")]
        public ObjectiveDto? RecruitmentObjective { get; init; }

        [JsonPropertyName("main_combat_objective")]
        public ObjectiveDto? MainCombatObjective { get; init; }

        [JsonPropertyName("destination_objective")]
        public ObjectiveDto? DestinationObjective { get; init; }

        [JsonPropertyName("interactions")]
        public List<InteractionDto?>? Interactions { get; init; }

        [JsonPropertyName("combat")]
        public CombatDto? Combat { get; init; }
    }

    private sealed class ActorDto
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("display_name")]
        public string? DisplayName { get; init; }

        [JsonPropertyName("movement_speed_meters_per_second")]
        public double MovementSpeedMetersPerSecond { get; init; }

        [JsonPropertyName("loadout")]
        public LoadoutDto? Loadout { get; init; }
    }

    private sealed class LoadoutDto
    {
        [JsonPropertyName("weapon_name")]
        public string? WeaponName { get; init; }

        [JsonPropertyName("basic_attack_id")]
        public string? BasicAttackId { get; init; }

        [JsonPropertyName("active_ability_id")]
        public string? ActiveAbilityId { get; init; }

        [JsonPropertyName("active_ability_name")]
        public string? ActiveAbilityName { get; init; }

        [JsonPropertyName("active_ability_target_kind")]
        public string? ActiveAbilityTargetKind { get; init; }

        [JsonPropertyName("secondary_ability_id")] public string? SecondaryAbilityId { get; init; }
        [JsonPropertyName("secondary_ability_name")] public string? SecondaryAbilityName { get; init; }
        [JsonPropertyName("secondary_ability_target_kind")] public string? SecondaryAbilityTargetKind { get; init; }
    }

    private sealed class KitDto
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("display_name")]
        public string? DisplayName { get; init; }

        [JsonPropertyName("role")]
        public string? Role { get; init; }

        [JsonPropertyName("weapon_name")]
        public string? WeaponName { get; init; }

        [JsonPropertyName("basic_attack_id")]
        public string? BasicAttackId { get; init; }

        [JsonPropertyName("active_ability_id")]
        public string? ActiveAbilityId { get; init; }

        [JsonPropertyName("active_ability_name")]
        public string? ActiveAbilityName { get; init; }

        [JsonPropertyName("active_ability_target_kind")]
        public string? ActiveAbilityTargetKind { get; init; }

        [JsonPropertyName("secondary_ability_id")] public string? SecondaryAbilityId { get; init; }
        [JsonPropertyName("secondary_ability_name")] public string? SecondaryAbilityName { get; init; }
        [JsonPropertyName("secondary_ability_target_kind")] public string? SecondaryAbilityTargetKind { get; init; }
    }

    private sealed class ObjectiveDto
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("text")]
        public string? Text { get; init; }
    }

    private sealed class InteractionDto
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("kind")]
        public string? Kind { get; init; }

        [JsonPropertyName("prompt")]
        public string? Prompt { get; init; }

        [JsonPropertyName("use_radius_meters")]
        public double UseRadiusMeters { get; init; }

        [JsonPropertyName("effect")]
        public string? Effect { get; init; }

        [JsonPropertyName("result_text")]
        public string? ResultText { get; init; }

        [JsonPropertyName("preserved_result_text")]
        public string? PreservedResultText { get; init; }

        [JsonPropertyName("dialogue")]
        public DialogueDto? Dialogue { get; init; }
    }

    private sealed class DialogueDto
    {
        [JsonPropertyName("speaker")]
        public string? Speaker { get; init; }

        [JsonPropertyName("line")]
        public string? Line { get; init; }

        [JsonPropertyName("responses")]
        public List<ResponseDto?>? Responses { get; init; }
    }

    private sealed class ResponseDto
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("text")]
        public string? Text { get; init; }

        [JsonPropertyName("effect")]
        public string? Effect { get; init; }
    }

    private sealed class CombatDto
    {
        [JsonPropertyName("attacks")]
        public List<AttackDto?>? Attacks { get; init; }

        [JsonPropertyName("protagonist_ability")]
        public AbilityDto? ProtagonistAbility { get; init; }

        [JsonPropertyName("hostiles")]
        public List<HostileDto?>? Hostiles { get; init; }

        [JsonPropertyName("encounters")]
        public List<EncounterDto?>? Encounters { get; init; }

        [JsonPropertyName("barrier")]
        public BarrierDto? Barrier { get; init; }
        [JsonPropertyName("burst")] public BurstDto? Burst { get; init; }
        [JsonPropertyName("taunt")] public TauntDto? Taunt { get; init; }

        [JsonPropertyName("companion_maximum_health")]
        public int CompanionMaximumHealth { get; init; }
    }

    private sealed class BarrierDto
    {
        [JsonPropertyName("id")] public string? Id { get; init; }
        [JsonPropertyName("range_meters")] public double RangeMeters { get; init; }
        [JsonPropertyName("windup_ticks")] public int WindupTicks { get; init; }
        [JsonPropertyName("recovery_ticks")] public int RecoveryTicks { get; init; }
        [JsonPropertyName("duration_ticks")] public int DurationTicks { get; init; }
        [JsonPropertyName("cooldown_ticks")] public int CooldownTicks { get; init; }
        [JsonPropertyName("width_meters")] public double WidthMeters { get; init; }
        [JsonPropertyName("height_meters")] public double HeightMeters { get; init; }
        [JsonPropertyName("center_height_meters")] public double CenterHeightMeters { get; init; }
    }

    private sealed class BurstDto
    {
        [JsonPropertyName("id")] public string? Id { get; init; }
        [JsonPropertyName("range_meters")] public double RangeMeters { get; init; }
        [JsonPropertyName("damage_per_shot")] public int DamagePerShot { get; init; }
        [JsonPropertyName("shot_count")] public int ShotCount { get; init; }
        [JsonPropertyName("shot_interval_ticks")] public int ShotIntervalTicks { get; init; }
        [JsonPropertyName("windup_ticks")] public int WindupTicks { get; init; }
        [JsonPropertyName("recovery_ticks")] public int RecoveryTicks { get; init; }
        [JsonPropertyName("cooldown_ticks")] public int CooldownTicks { get; init; }
    }

    private sealed class TauntDto
    {
        [JsonPropertyName("id")] public string? Id { get; init; }
        [JsonPropertyName("radius_meters")] public double RadiusMeters { get; init; }
        [JsonPropertyName("duration_ticks")] public int DurationTicks { get; init; }
        [JsonPropertyName("windup_ticks")] public int WindupTicks { get; init; }
        [JsonPropertyName("recovery_ticks")] public int RecoveryTicks { get; init; }
        [JsonPropertyName("cooldown_ticks")] public int CooldownTicks { get; init; }
    }

    private sealed class AttackDto
    {
        [JsonPropertyName("projectile_speed_meters_per_second")]
        public double ProjectileSpeedMetersPerSecond { get; init; }
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("range_meters")]
        public double RangeMeters { get; init; }

        [JsonPropertyName("damage")]
        public int Damage { get; init; }

        [JsonPropertyName("windup_ticks")]
        public int WindupTicks { get; init; }

        [JsonPropertyName("recovery_ticks")]
        public int RecoveryTicks { get; init; }
    }

    private sealed class AbilityDto
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("target_kind")]
        public string? TargetKind { get; init; }

        [JsonPropertyName("range_meters")]
        public double RangeMeters { get; init; }

        [JsonPropertyName("radius_meters")]
        public double RadiusMeters { get; init; }

        [JsonPropertyName("damage")]
        public int Damage { get; init; }

        [JsonPropertyName("windup_ticks")]
        public int WindupTicks { get; init; }

        [JsonPropertyName("recovery_ticks")]
        public int RecoveryTicks { get; init; }

        [JsonPropertyName("cooldown_ticks")]
        public int CooldownTicks { get; init; }

        [JsonPropertyName("interrupts_windup")]
        public bool InterruptsWindup { get; init; }
    }

    private sealed class HostileDto
    {
        [JsonPropertyName("behavior")]
        public string? Behavior { get; init; }
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("display_name")]
        public string? DisplayName { get; init; }

        [JsonPropertyName("movement_speed_meters_per_second")]
        public double MovementSpeedMetersPerSecond { get; init; }

        [JsonPropertyName("maximum_health")]
        public int MaximumHealth { get; init; }

        [JsonPropertyName("basic_attack_id")]
        public string? BasicAttackId { get; init; }
    }

    private sealed class EncounterDto
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("hostile_ids")]
        public List<string?>? HostileIds { get; init; }

        [JsonPropertyName("requires_companion")]
        public bool RequiresCompanion { get; init; }

        [JsonPropertyName("protagonist_maximum_health")]
        public int ProtagonistMaximumHealth { get; init; }

        [JsonPropertyName("readying_ticks")]
        public int ReadyingTicks { get; init; }

        [JsonPropertyName("securing_ticks")]
        public int SecuringTicks { get; init; }

    }
}
