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
    BeginBriefingDialogue,
    RecordObservation,
    CompleteScenario,
}

public sealed record StationActorDefinition(
    EntityId Id,
    string DisplayName,
    double MovementSpeedMetersPerSecond);

public sealed record StationObjectiveDefinition(ObjectiveId Id, string Text);

public sealed record StationDialogueResponseDefinition(DialogueResponseId Id, string Text);

public sealed record StationDialogueDefinition(
    string Speaker,
    string Line,
    StationDialogueResponseDefinition Response);

public sealed record StationInteractionDefinition(
    EntityId Id,
    StationInteractionKind Kind,
    string Prompt,
    double UseRadiusMeters,
    StationInteractionEffect Effect,
    string? ResultText,
    StationDialogueDefinition? Dialogue);

public sealed class StationRouteDefinition
{
    internal StationRouteDefinition(
        int schemaVersion,
        string contentRevision,
        ScenarioId scenarioId,
        StationActorDefinition protagonist,
        StationObjectiveDefinition briefingObjective,
        StationObjectiveDefinition destinationObjective,
        IEnumerable<StationInteractionDefinition> interactions)
    {
        SchemaVersion = schemaVersion;
        ContentRevision = contentRevision;
        ScenarioId = scenarioId;
        Protagonist = protagonist;
        BriefingObjective = briefingObjective;
        DestinationObjective = destinationObjective;
        Interactions = new ReadOnlyCollection<StationInteractionDefinition>(interactions.ToArray());
    }

    public int SchemaVersion { get; }

    public string ContentRevision { get; }

    public ScenarioId ScenarioId { get; }

    public StationActorDefinition Protagonist { get; }

    public StationObjectiveDefinition BriefingObjective { get; }

    public StationObjectiveDefinition DestinationObjective { get; }

    public IReadOnlyList<StationInteractionDefinition> Interactions { get; }
}

public static class StationRouteContent
{
    public const int SupportedSchemaVersion = 1;

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
            throw new InvalidDataException("Station route content is not valid schema-v1 JSON.", exception);
        }

        if (dto.SchemaVersion != SupportedSchemaVersion)
        {
            throw new InvalidDataException(
                $"Unsupported station route schema version '{dto.SchemaVersion}'.");
        }

        var contentRevision = RequireText(dto.ContentRevision, "content_revision", MaximumIdLength);
        var scenarioId = new ScenarioId(RequireText(dto.ScenarioId, "scenario_id", MaximumIdLength));
        var protagonist = ParseActor(dto.Protagonist);
        var briefingObjective = ParseObjective(dto.BriefingObjective, "briefing_objective");
        var destinationObjective = ParseObjective(dto.DestinationObjective, "destination_objective");
        if (briefingObjective.Id == destinationObjective.Id)
        {
            throw new InvalidDataException("Briefing and destination objective IDs must be distinct.");
        }

        if (dto.Interactions is null || dto.Interactions.Count == 0)
        {
            throw new InvalidDataException("Station route content must define interactions.");
        }

        var interactions = dto.Interactions.Select(ParseInteraction).ToArray();
        ValidateInteractionSet(protagonist.Id, interactions);

        return new StationRouteDefinition(
            dto.SchemaVersion,
            contentRevision,
            scenarioId,
            protagonist,
            briefingObjective,
            destinationObjective,
            interactions);
    }

    private static StationActorDefinition ParseActor(ActorDto? actor)
    {
        if (actor is null)
        {
            throw new InvalidDataException("Station route content requires a protagonist.");
        }

        var speed = actor.MovementSpeedMetersPerSecond;
        if (!double.IsFinite(speed) || speed <= 0 || speed > 20)
        {
            throw new InvalidDataException(
                "protagonist.movement_speed_meters_per_second must be greater than zero and at most 20.");
        }

        return new StationActorDefinition(
            new EntityId(RequireText(actor.Id, "protagonist.id", MaximumIdLength)),
            RequireText(actor.DisplayName, "protagonist.display_name", MaximumTextLength),
            speed);
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

        var idText = RequireText(interaction.Id, "interactions[].id", MaximumIdLength);
        var id = new EntityId(idText);
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
            if (interaction.Dialogue.Response is null)
            {
                throw new InvalidDataException($"Interaction '{id}' dialogue requires one response.");
            }

            dialogue = new StationDialogueDefinition(
                RequireText(interaction.Dialogue.Speaker, $"interactions[{id}].dialogue.speaker", MaximumTextLength),
                RequireText(interaction.Dialogue.Line, $"interactions[{id}].dialogue.line", MaximumTextLength),
                new StationDialogueResponseDefinition(
                    new DialogueResponseId(RequireText(
                        interaction.Dialogue.Response.Id,
                        $"interactions[{id}].dialogue.response.id",
                        MaximumIdLength)),
                    RequireText(
                        interaction.Dialogue.Response.Text,
                        $"interactions[{id}].dialogue.response.text",
                        MaximumTextLength)));
        }

        var resultText = interaction.ResultText is null
            ? null
            : RequireText(interaction.ResultText, $"interactions[{id}].result_text", MaximumTextLength);

        ValidateInteractionShape(id, kind, effect, dialogue, resultText);
        return new StationInteractionDefinition(
            id,
            kind,
            RequireText(interaction.Prompt, $"interactions[{id}].prompt", MaximumTextLength),
            radius,
            effect,
            resultText,
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
            "begin_briefing_dialogue" => StationInteractionEffect.BeginBriefingDialogue,
            "record_observation" => StationInteractionEffect.RecordObservation,
            "complete_scenario" => StationInteractionEffect.CompleteScenario,
            _ => throw new InvalidDataException($"Interaction '{id}' has unknown effect '{value}'."),
        };
    }

    private static void ValidateInteractionShape(
        EntityId id,
        StationInteractionKind kind,
        StationInteractionEffect effect,
        StationDialogueDefinition? dialogue,
        string? resultText)
    {
        if (effect == StationInteractionEffect.BeginBriefingDialogue)
        {
            if (kind != StationInteractionKind.Npc || dialogue is null || resultText is not null)
            {
                throw new InvalidDataException(
                    $"Briefing interaction '{id}' must be an NPC with dialogue and no result_text.");
            }

            return;
        }

        if (dialogue is not null || string.IsNullOrWhiteSpace(resultText))
        {
            throw new InvalidDataException(
                $"Non-dialogue interaction '{id}' requires result_text and cannot define dialogue.");
        }

        if (effect == StationInteractionEffect.RecordObservation
            && kind != StationInteractionKind.Environment)
        {
            throw new InvalidDataException(
                $"Observation interaction '{id}' must have kind 'environment'.");
        }

        if (effect == StationInteractionEffect.CompleteScenario
            && kind != StationInteractionKind.Destination)
        {
            throw new InvalidDataException(
                $"Completion interaction '{id}' must have kind 'destination'.");
        }
    }

    private static void ValidateInteractionSet(
        EntityId protagonistId,
        IReadOnlyCollection<StationInteractionDefinition> interactions)
    {
        var identifiers = new HashSet<EntityId>();
        foreach (var interaction in interactions)
        {
            if (interaction.Id == protagonistId || !identifiers.Add(interaction.Id))
            {
                throw new InvalidDataException(
                    $"Gameplay entity ID '{interaction.Id}' is duplicated.");
            }
        }

        RequireExactlyOne(interactions, StationInteractionEffect.BeginBriefingDialogue);
        RequireExactlyOne(interactions, StationInteractionEffect.RecordObservation);
        RequireExactlyOne(interactions, StationInteractionEffect.CompleteScenario);
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

        [JsonPropertyName("briefing_objective")]
        public ObjectiveDto? BriefingObjective { get; init; }

        [JsonPropertyName("destination_objective")]
        public ObjectiveDto? DestinationObjective { get; init; }

        [JsonPropertyName("interactions")]
        public List<InteractionDto?>? Interactions { get; init; }
    }

    private sealed class ActorDto
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("display_name")]
        public string? DisplayName { get; init; }

        [JsonPropertyName("movement_speed_meters_per_second")]
        public double MovementSpeedMetersPerSecond { get; init; }
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

        [JsonPropertyName("dialogue")]
        public DialogueDto? Dialogue { get; init; }
    }

    private sealed class DialogueDto
    {
        [JsonPropertyName("speaker")]
        public string? Speaker { get; init; }

        [JsonPropertyName("line")]
        public string? Line { get; init; }

        [JsonPropertyName("response")]
        public ResponseDto? Response { get; init; }
    }

    private sealed class ResponseDto
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("text")]
        public string? Text { get; init; }
    }
}
