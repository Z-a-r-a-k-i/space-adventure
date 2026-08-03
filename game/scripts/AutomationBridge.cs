using System.Text.Json;
using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

public readonly record struct ScreenPositionProjection(double X, double Y, bool Visible);

public partial class AutomationBridge : Node
{
    private const int MaximumAdvanceUntilTicks = 3000;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private GameSession? _session;
    private Func<string, ScreenPositionProjection?>? _screenProjector;

    public void Initialize(
        GameSession session,
        Func<string, ScreenPositionProjection?>? screenProjector = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _screenProjector = screenProjector;
    }

    public string GetObservationJson()
    {
        return _session is null
            ? Error("automation_bridge_unavailable")
            : SerializeAcceptedObservation(_session.Observe());
    }

    public string GetEventsJson(long sinceSequence = 0)
    {
        if (_session is null)
        {
            return Error("automation_bridge_unavailable");
        }

        try
        {
            var events = _session.EventsSince(sinceSequence).Select(ProjectEvent).ToArray();
            return JsonSerializer.Serialize(new
            {
                accepted = true,
                requestedAfterSequence = sinceSequence,
                oldestRetainedSequence = _session.OldestRetainedEventSequence,
                latestEventSequence = _session.Observe().LatestEventSequence,
                historyGap = _session.WasEventHistoryTruncatedAfter(sinceSequence),
                events,
            }, JsonOptions);
        }
        catch (ArgumentOutOfRangeException)
        {
            return Error("invalid_event_sequence");
        }
    }

    public string SubmitCommandJson(string commandJson)
    {
        if (_session is null)
        {
            return Error("automation_bridge_unavailable");
        }

        try
        {
            using var document = JsonDocument.Parse(commandJson);
            var root = document.RootElement;

            if (!root.TryGetProperty("schema_version", out var schemaVersion)
                || schemaVersion.GetInt32() != 2)
            {
                return Error("unsupported_schema_version");
            }

            var commandId = new CommandId(RequiredString(root, "command_id"));
            var commandType = RequiredString(root, "type");
            if (!root.TryGetProperty("payload", out var payload)
                || payload.ValueKind != JsonValueKind.Object)
            {
                return Error("invalid_command_envelope", commandId.Value);
            }

            IGameCommand command = commandType switch
            {
                "set_pause" => new SetPauseCommand(
                    commandId,
                    payload.GetProperty("paused").GetBoolean()),
                "choose_protagonist_kit" => new ChooseProtagonistKitCommand(
                    commandId,
                    new ProtagonistKitId(RequiredString(payload, "kit_id"))),
                "move_actor" => new MoveActorCommand(
                    commandId,
                    new EntityId(RequiredString(payload, "actor_id")),
                    ReadPosition(payload.GetProperty("destination"))),
                "move_party" => new MovePartyCommand(
                    commandId,
                    payload.GetProperty("actor_ids")
                        .EnumerateArray()
                        .Select(actorId => new EntityId(actorId.GetString()
                            ?? throw new JsonException("'actor_ids' cannot contain null."))),
                    ReadPosition(payload.GetProperty("destination"))),
                "interact" => new InteractCommand(
                    commandId,
                    new EntityId(RequiredString(payload, "actor_id")),
                    new EntityId(RequiredString(payload, "target_id"))),
                "choose_dialogue_response" => new ChooseDialogueResponseCommand(
                    commandId,
                    new EntityId(RequiredString(payload, "actor_id")),
                    new EntityId(RequiredString(payload, "interaction_id")),
                    new DialogueResponseId(RequiredString(payload, "response_id"))),
                _ => null!,
            };

            if (command is null)
            {
                return Error("unknown_command", commandId.Value);
            }

            return SerializeAcknowledgement(_session.Execute(command));
        }
        catch (Exception exception) when (
            exception is JsonException
                or InvalidOperationException
                or ArgumentException
                or KeyNotFoundException
                or FormatException
                or OverflowException)
        {
            return Error("invalid_command_envelope");
        }
    }

    public string SetPaused(bool paused)
    {
        if (_session is null)
        {
            return Error("automation_bridge_unavailable");
        }

        var commandId = new CommandId($"automation.pause.{_session.Observe().LatestEventSequence + 1}");
        return SerializeAcknowledgement(_session.Execute(new SetPauseCommand(commandId, paused)));
    }

    public string AdvanceExactTicks(int count)
    {
        if (_session is null)
        {
            return Error("automation_bridge_unavailable");
        }

        if (!_session.IsPaused)
        {
            return Error("step_requires_pause");
        }

        if (count < 0 || count > GameSession.MaximumDirectTickAdvance)
        {
            return Error("invalid_tick_count");
        }

        try
        {
            var advanced = _session.StepWhilePaused(count);
            return JsonSerializer.Serialize(new
            {
                accepted = true,
                advanced,
                observation = ProjectObservation(_session.Observe()),
            }, JsonOptions);
        }
        catch (ArgumentOutOfRangeException)
        {
            return Error("invalid_tick_count");
        }
    }

    public string AdvanceUntilEventJson(
        long afterSequence,
        string eventType,
        int maximumTicks)
    {
        if (_session is null)
        {
            return Error("automation_bridge_unavailable");
        }

        if (!_session.IsPaused)
        {
            return Error("step_requires_pause");
        }

        if (afterSequence < 0)
        {
            return Error("invalid_event_sequence");
        }

        if (maximumTicks < 1 || maximumTicks > MaximumAdvanceUntilTicks)
        {
            return Error("invalid_tick_budget");
        }

        if (_session.WasEventHistoryTruncatedAfter(afterSequence))
        {
            return Error("event_history_gap");
        }

        if (!TryParseEventType(eventType, out var expectedType))
        {
            return Error("unknown_event_type");
        }

        var advanced = 0;
        var matched = FindEvent(_session, afterSequence, expectedType);
        while (matched is null && advanced < maximumTicks)
        {
            _session.StepWhilePaused(1);
            advanced++;
            matched = FindEvent(_session, afterSequence, expectedType);
        }

        return JsonSerializer.Serialize(new
        {
            accepted = true,
            reached = matched is not null,
            advanced,
            expectedEventType = ToExternalName(expectedType),
            matchedEvent = matched is null ? null : ProjectEvent(matched),
            observation = ProjectObservation(_session.Observe()),
        }, JsonOptions);
    }

    public string GetScreenPositionJson(string stableId)
    {
        if (_session is null)
        {
            return Error("automation_bridge_unavailable");
        }

        if (_screenProjector is null)
        {
            return Error("screen_projection_unavailable");
        }

        if (string.IsNullOrWhiteSpace(stableId))
        {
            return Error("invalid_projection_target");
        }

        var projection = _screenProjector(stableId);
        return projection is null
            ? Error("unknown_projection_target")
            : JsonSerializer.Serialize(new
            {
                accepted = true,
                stableId,
                projection.Value.X,
                projection.Value.Y,
                projection.Value.Visible,
            }, JsonOptions);
    }

    public string InjectContextClickJson(string stableId)
    {
        if (_session is null)
        {
            return Error("automation_bridge_unavailable");
        }

        if (_screenProjector is null)
        {
            return Error("screen_projection_unavailable");
        }

        if (string.IsNullOrWhiteSpace(stableId))
        {
            return Error("invalid_projection_target");
        }

        var projection = _screenProjector(stableId);
        if (projection is null)
        {
            return Error("unknown_projection_target");
        }
        if (!projection.Value.Visible)
        {
            return Error("projection_target_not_visible");
        }

        var eventSequenceBefore = _session.Observe().LatestEventSequence;
        var position = new Vector2((float)projection.Value.X, (float)projection.Value.Y);
        Input.ParseInputEvent(new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Right,
            Position = position,
            Pressed = true,
        });
        Input.ParseInputEvent(new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Right,
            Position = position,
            Pressed = false,
        });

        return JsonSerializer.Serialize(new
        {
            injected = true,
            stableId,
            projection.Value.X,
            projection.Value.Y,
            eventSequenceBefore,
            confirmation = "read_events_after_sequence",
        }, JsonOptions);
    }

    public void Shutdown(int exitCode = 0)
    {
        GetTree().Quit(exitCode);
    }

    private static WorldPosition ReadPosition(JsonElement element)
    {
        var y = element.TryGetProperty("y", out var yElement) ? yElement.GetDouble() : 0.0;
        return new WorldPosition(
            element.GetProperty("x").GetDouble(),
            y,
            element.GetProperty("z").GetDouble());
    }

    private static string RequiredString(JsonElement element, string propertyName)
    {
        return element.GetProperty(propertyName).GetString()
            ?? throw new JsonException($"'{propertyName}' cannot be null.");
    }

    private static GameplayEvent? FindEvent(
        GameSession session,
        long afterSequence,
        GameplayEventType eventType)
    {
        return session.EventsSince(afterSequence).FirstOrDefault(gameEvent => gameEvent.Type == eventType);
    }

    private static bool TryParseEventType(string value, out GameplayEventType eventType)
    {
        foreach (var candidate in Enum.GetValues<GameplayEventType>())
        {
            if (string.Equals(value, candidate.ToString(), StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, ToExternalName(candidate), StringComparison.Ordinal))
            {
                eventType = candidate;
                return true;
            }
        }

        eventType = default;
        return false;
    }

    private static string ToExternalName<T>(T value) where T : struct, Enum
    {
        return JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString());
    }

    private static string SerializeAcknowledgement(CommandAcknowledgement acknowledgement)
    {
        return JsonSerializer.Serialize(new
        {
            acknowledgement.Accepted,
            CommandId = acknowledgement.CommandId.Value,
            RejectionCode = acknowledgement.RejectionCode is null
                ? null
                : ToExternalName(acknowledgement.RejectionCode.Value),
            Observation = ProjectObservation(acknowledgement.Observation),
        }, JsonOptions);
    }

    private static string SerializeAcceptedObservation(GameObservation observation)
    {
        return JsonSerializer.Serialize(new
        {
            accepted = true,
            observation = ProjectObservation(observation),
        }, JsonOptions);
    }

    private static object ProjectObservation(GameObservation observation)
    {
        return new
        {
            observation.Tick,
            Paused = observation.Paused,
            observation.LatestEventSequence,
            StationRoute = observation.StationRoute is null
                ? null
                : ProjectStationRoute(observation.StationRoute),
        };
    }

    private static object ProjectStationRoute(StationRouteObservation route)
    {
        return new
        {
            ScenarioId = route.ScenarioId.Value,
            route.ContentSchemaVersion,
            route.ContentRevision,
            Phase = ToExternalName(route.Phase),
            Protagonist = ProjectActor(route.Protagonist),
            Party = route.Party.Select(ProjectActor),
            AvailableProtagonistKits = route.AvailableProtagonistKits.Select(kit => new
            {
                Id = kit.Id.Value,
                kit.DisplayName,
                kit.Role,
                kit.WeaponName,
                BasicAttackId = kit.BasicAttackId.Value,
                ActiveAbilityId = kit.ActiveAbilityId.Value,
                kit.ActiveAbilityName,
                ActiveAbilityTargetKind = ToExternalName(kit.ActiveAbilityTargetKind),
            }),
            SelectedProtagonistKitId = route.SelectedProtagonistKit?.Id.Value,
            RoutePowerMode = ToExternalName(route.RoutePowerMode),
            Objective = new
            {
                Id = route.Objective.Id.Value,
                route.Objective.Text,
                Status = ToExternalName(route.Objective.Status),
            },
            Interactions = route.Interactions.Select(interaction => new
            {
                Id = interaction.Id.Value,
                Kind = ToExternalName(interaction.Kind),
                interaction.Prompt,
                Position = ProjectPosition(interaction.Position),
                ApproachPosition = ProjectPosition(interaction.ApproachPosition),
                interaction.UseRadiusMeters,
                State = ToExternalName(interaction.State),
                interaction.CanInteract,
                interaction.ResultText,
            }),
            ActiveDialogue = route.ActiveDialogue is null
                ? null
                : new
                {
                    InteractionId = route.ActiveDialogue.InteractionId.Value,
                    ActorId = route.ActiveDialogue.ActorId.Value,
                    route.ActiveDialogue.Speaker,
                    route.ActiveDialogue.Line,
                    Responses = route.ActiveDialogue.Responses.Select(response => new
                    {
                        Id = response.Id.Value,
                        response.Text,
                    }),
                },
        };
    }

    private static object ProjectActor(ActorObservation actor)
    {
        return new
        {
            Id = actor.Id.Value,
            actor.DisplayName,
            Loadout = actor.Loadout is null
                ? null
                : new
                {
                    actor.Loadout.WeaponName,
                    BasicAttackId = actor.Loadout.BasicAttackId.Value,
                    ActiveAbilityId = actor.Loadout.ActiveAbilityId.Value,
                    actor.Loadout.ActiveAbilityName,
                    ActiveAbilityTargetKind = ToExternalName(actor.Loadout.ActiveAbilityTargetKind),
                },
            Position = ProjectPosition(actor.Position),
            CurrentAction = ProjectAction(actor.CurrentAction),
            PendingAction = ProjectAction(actor.PendingAction),
        };
    }

    private static object? ProjectAction(PrimaryActionObservation? action)
    {
        return action is null
            ? null
            : new
            {
                CommandId = action.CommandId.Value,
                Kind = ToExternalName(action.Kind),
                Destination = ProjectPosition(action.Destination),
                InteractionTargetId = action.InteractionTargetId?.Value,
            };
    }

    private static object ProjectEvent(GameplayEvent gameEvent)
    {
        return new
        {
            gameEvent.Sequence,
            gameEvent.Tick,
            Type = ToExternalName(gameEvent.Type),
            CommandId = gameEvent.CommandId?.Value,
            gameEvent.Paused,
            RejectionCode = gameEvent.RejectionCode is null
                ? null
                : ToExternalName(gameEvent.RejectionCode.Value),
            Detail = ProjectEventDetail(gameEvent.Detail),
        };
    }

    private static object? ProjectEventDetail(GameplayEventDetail? detail)
    {
        return detail switch
        {
            ProtagonistKitSelectedEventDetail value => new
            {
                CommandId = value.CommandId.Value,
                KitId = value.KitId.Value,
            },
            PrimaryActionAssignedEventDetail value => new
            {
                CommandId = value.CommandId.Value,
                ActorId = value.ActorId.Value,
                Kind = ToExternalName(value.Kind),
                Destination = ProjectPosition(value.Destination),
                InteractionTargetId = value.InteractionTargetId?.Value,
                value.Pending,
                ReplacedCommandId = value.ReplacedCommandId?.Value,
            },
            MovementArrivedEventDetail value => new
            {
                CommandId = value.CommandId.Value,
                ActorId = value.ActorId.Value,
                Position = ProjectPosition(value.Position),
            },
            PrimaryActionFailedEventDetail value => new
            {
                CommandId = value.CommandId.Value,
                ActorId = value.ActorId.Value,
                Reason = ToExternalName(value.Reason),
            },
            DialogueStartedEventDetail value => new
            {
                CommandId = value.CommandId.Value,
                ActorId = value.ActorId.Value,
                InteractionId = value.InteractionId.Value,
            },
            DialogueResponseChosenEventDetail value => new
            {
                CommandId = value.CommandId.Value,
                ActorId = value.ActorId.Value,
                InteractionId = value.InteractionId.Value,
                ResponseId = value.ResponseId.Value,
            },
            RouteConsequenceSelectedEventDetail value => new
            {
                CommandId = value.CommandId.Value,
                RoutePowerMode = ToExternalName(value.RoutePowerMode),
            },
            PartyMemberRecruitedEventDetail value => new
            {
                CommandId = value.CommandId.Value,
                ActorId = value.ActorId.Value,
            },
            InteractionCompletedEventDetail value => new
            {
                CommandId = value.CommandId.Value,
                ActorId = value.ActorId.Value,
                InteractionId = value.InteractionId.Value,
                Effect = ToExternalName(value.Effect),
            },
            ObjectiveChangedEventDetail value => new
            {
                CommandId = value.CommandId.Value,
                PreviousObjectiveId = value.PreviousObjectiveId.Value,
                CurrentObjectiveId = value.CurrentObjectiveId.Value,
                Status = ToExternalName(value.Status),
            },
            ScenarioCompletedEventDetail value => new
            {
                CommandId = value.CommandId.Value,
                ScenarioId = value.ScenarioId.Value,
            },
            _ => null,
        };
    }

    private static string Error(string code, string? commandId = null)
    {
        return JsonSerializer.Serialize(new
        {
            accepted = false,
            error = code,
            command_id = commandId,
        }, JsonOptions);
    }

    private static object ProjectPosition(WorldPosition position)
    {
        return new { position.X, position.Y, position.Z };
    }
}
