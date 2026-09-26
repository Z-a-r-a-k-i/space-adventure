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
    private Action? _synchronizePresentation;
    private Func<string>? _presentationDiagnostics;

    public void Initialize(
        GameSession session,
        Func<string, ScreenPositionProjection?>? screenProjector = null,
        Action? synchronizePresentation = null,
        Func<string>? presentationDiagnostics = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _screenProjector = screenProjector;
        _synchronizePresentation = synchronizePresentation;
        _presentationDiagnostics = presentationDiagnostics;
    }

    public string GetObservationJson()
    {
        _synchronizePresentation?.Invoke();
        return _session is null
            ? Error("automation_bridge_unavailable")
            : SerializeAcceptedObservation(_session.Observe());
    }

    public string GetPresentationDiagnosticsJson()
    {
        _synchronizePresentation?.Invoke();
        return _presentationDiagnostics?.Invoke() ?? Error("presentation_diagnostics_unavailable");
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
                || schemaVersion.GetInt32() != StationRouteContent.SupportedSchemaVersion)
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
                "stop_actors" => new StopActorsCommand(
                    commandId,
                    payload.GetProperty("actor_ids").EnumerateArray()
                        .Select(actorId => new EntityId(actorId.GetString()
                            ?? throw new JsonException("'actor_ids' cannot contain null.")))),
                "interact" => new InteractCommand(
                    commandId,
                    new EntityId(RequiredString(payload, "actor_id")),
                    new EntityId(RequiredString(payload, "target_id"))),
                "choose_dialogue_response" => new ChooseDialogueResponseCommand(
                    commandId,
                    new EntityId(RequiredString(payload, "actor_id")),
                    new EntityId(RequiredString(payload, "interaction_id")),
                    new DialogueResponseId(RequiredString(payload, "response_id"))),
                "assign_basic_attack_target" => new AssignBasicAttackTargetCommand(
                    commandId,
                    new EntityId(RequiredString(payload, "actor_id")),
                    new EntityId(RequiredString(payload, "target_id"))),
                "use_ability" => new UseAbilityCommand(
                    commandId,
                    new EntityId(RequiredString(payload, "actor_id")),
                    new AbilityId(RequiredString(payload, "ability_id")),
                    ReadAbilityTarget(payload)),
                "restart_encounter" => new RestartEncounterCommand(
                    commandId,
                    new EncounterId(RequiredString(payload, "encounter_id"))),
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
            for (var index = 0; index < count; index++)
            {
                _session.StepWhilePaused(1);
                _synchronizePresentation?.Invoke();
            }
            var advanced = count;
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
            _synchronizePresentation?.Invoke();
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

    private static AbilityTarget ReadAbilityTarget(JsonElement payload)
    {
        var hasPosition = payload.TryGetProperty("target_position", out var position);
        var hasActor = payload.TryGetProperty("target_actor_id", out var actor);
        var hasFacing = payload.TryGetProperty("target_facing", out var facing);
        var hasSelf = payload.TryGetProperty("target_self", out var self);
        if (hasFacing)
        {
            if (!hasPosition || hasActor || hasSelf) { throw new JsonException("Barrier requires a ground position and facing."); }
            return new BarrierAbilityTarget(ReadPosition(position), ReadPosition(facing));
        }
        if ((hasActor ? 1 : 0) + (hasPosition ? 1 : 0) + (hasSelf ? 1 : 0) != 1)
        { throw new JsonException("Use one target: position, actor, or self."); }
        if (hasActor) { return new EntityAbilityTarget(new EntityId(actor.GetString()!)); }
        if (hasSelf)
        { if (!self.GetBoolean()) { throw new JsonException("target_self must be true."); } return new SelfAbilityTarget(); }
        return new PositionAbilityTarget(ReadPosition(position));
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

    private string SerializeAcknowledgement(CommandAcknowledgement acknowledgement)
    {
        _synchronizePresentation?.Invoke();
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
                    SecondaryAbilityId = kit.SecondaryAbilityId.Value,
                    kit.SecondaryAbilityName,
                    SecondaryAbilityTargetKind = ToExternalName(kit.SecondaryAbilityTargetKind),
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
            CompletedEncounterIds = route.CompletedEncounterIds?.Select(id => id.Value),
            Hostiles = route.Hostiles?.Select(ProjectHostile),
            VisibleHostiles = route.VisibleHostiles.Select(ProjectHostile),
            Encounter = route.Encounter is null
                ? null
                : new
                {
                    Id = route.Encounter.Id.Value,
                    Phase = ToExternalName(route.Encounter.Phase),
                    route.Encounter.Attempt,
                    route.Encounter.TransitionTicksRemaining,
                    route.Encounter.TransitionTicksTotal,
                    route.Encounter.PhaseStartedTick,
                    SpotterId = route.Encounter.SpotterId?.Value,
                    SpottedActorId = route.Encounter.SpottedActorId?.Value,
                    HostileIds = route.Encounter.HostileIds.Select(id => id.Value),
                    Barrier = route.Encounter.Barrier is { } barrier ? new
                    {
                        SourceId = barrier.SourceId.Value, Position = ProjectPosition(barrier.Position), Facing = ProjectPosition(barrier.Facing),
                        barrier.RemainingTicks, barrier.TotalTicks, barrier.WidthMeters, barrier.HeightMeters, barrier.DeployedAtTick,
                    } : null,
                    HealingField = route.Encounter.HealingField is { } field ? new { SourceId = field.SourceId.Value, Position = ProjectPosition(field.Position), field.RadiusMeters, field.DeployedAtTick, field.RemainingTicks, field.TotalTicks, field.PulseIntervalTicks } : null,
                    Projectiles = route.Encounter.Projectiles?.Select(projectile => new
                    {
                        projectile.Id, SourceId = projectile.SourceId.Value, TargetId = projectile.TargetId.Value,
                        AttackId = projectile.AttackId.Value, Origin = ProjectPosition(projectile.Origin),
                        Destination = ProjectPosition(projectile.Destination), Position = ProjectPosition(projectile.Position),
                        projectile.ReleasedAtTick, projectile.FlightTicks,
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
                    SecondaryAbilityId = actor.Loadout.SecondaryAbilityId.Value,
                    actor.Loadout.SecondaryAbilityName,
                    SecondaryAbilityTargetKind = ToExternalName(actor.Loadout.SecondaryAbilityTargetKind),
                },
            Position = ProjectPosition(actor.Position),
            Facing = ProjectPosition(actor.Facing),
            CurrentAction = ProjectAction(actor.CurrentAction),
            PendingAction = ProjectAction(actor.PendingAction),
            Combat = ProjectCombatant(actor.Combat),
        };
    }

    private static object ProjectHostile(HostileObservation hostile)
    {
        return new
        {
            Id = hostile.Id.Value,
            hostile.DisplayName,
            EncounterId = hostile.EncounterId.Value,
            EncounterPhase = ToExternalName(hostile.EncounterPhase),
            hostile.EncounterAttempt,
            Facing = ProjectPosition(hostile.Facing),
            Position = ProjectPosition(hostile.Position),
            hostile.MovementSpeedMetersPerSecond,
            Combat = ProjectCombatant(hostile.Combat),
            CurrentAction = ProjectAction(hostile.CurrentAction),
        };
    }

    private static object? ProjectCombatant(CombatantStateObservation? combat)
    {
        return combat is null
            ? null
            : new
            {
                combat.Health,
                combat.MaximumHealth,
                combat.IsDefeated,
                RememberedAttackTargetId = combat.RememberedAttackTargetId?.Value,
                combat.OffensiveRecoveryUntilTick, combat.DefeatedAtTick,
                TauntedBy = combat.TauntedBy?.Value, combat.TauntRemainingTicks,
                BasicAttackId = combat.BasicAttackId.Value,
                Cooldowns = combat.Cooldowns.Select(cooldown => new
                {
                    AbilityId = cooldown.AbilityId.Value,
                    cooldown.RemainingTicks,
                    cooldown.TotalTicks,
                }),
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
                action.HasRemainingMovement,
                InteractionTargetId = action.InteractionTargetId?.Value,
                CombatTargetId = action.CombatTargetId?.Value,
                AbilityFacing = action.AbilityFacing is { } facing ? ProjectPosition(facing) : null,
                AttackId = action.AttackId?.Value,
                AbilityId = action.AbilityId?.Value,
                Phase = ToExternalName(action.Phase),
                action.PhaseTicksRemaining,
                action.PhaseTicksTotal,
                action.InstanceId,
                action.PhaseStartedTick,
                WaitingReason = action.WaitingReason is { } reason ? ToExternalName(reason) : null,
                action.Interrupted,
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
            HealingAppliedEventDetail value => new { SourceId = value.SourceId.Value, TargetId = value.TargetId.Value, AbilityId = value.AbilityId.Value, value.Amount, value.RemainingHealth },
            HealingFieldEventDetail value => new { SourceId = value.SourceId.Value, AbilityId = value.AbilityId.Value, Position = ProjectPosition(value.Position), value.RadiusMeters, value.DurationTicks },
            TauntEventDetail value => new { SourceId = value.SourceId.Value, TargetId = value.TargetId.Value, value.DurationTicks },
            BarrierEventDetail value => new
            {
                SourceId = value.SourceId.Value, AbilityId = value.AbilityId.Value,
                Position = ProjectPosition(value.Position), Facing = ProjectPosition(value.Facing),
                EndReason = value.EndReason is { } reason ? ToExternalName(reason) : null,
            },
            ProjectileEventDetail value => new
            {
                value.Id, SourceId = value.SourceId.Value, TargetId = value.TargetId.Value, AttackId = value.AttackId.Value,
                Origin = ProjectPosition(value.Origin), Destination = ProjectPosition(value.Destination), value.FlightTicks,
                ImpactPosition = value.ImpactPosition is { } impact ? ProjectPosition(impact) : null, value.Blocked,
            },
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
            EncounterEventDetail value => new
            {
                EncounterId = value.EncounterId.Value,
                value.Attempt,
                SpotterId = value.SpotterId?.Value,
                SpottedActorId = value.SpottedActorId?.Value,
            },
            AttackEventDetail value => new
            {
                SourceId = value.SourceId.Value,
                TargetId = value.TargetId.Value,
                AttackId = value.AttackId.Value,
                value.Hit,
            },
            AbilityReleasedEventDetail value => new
            {
                SourceId = value.SourceId.Value,
                TargetId = value.TargetId?.Value,
                TargetPosition = ProjectPosition(value.TargetPosition),
                AbilityId = value.AbilityId.Value,
                value.Hit,
            },
            DamageAppliedEventDetail value => new
            {
                SourceId = value.SourceId.Value,
                TargetId = value.TargetId.Value,
                value.Amount,
                value.RemainingHealth,
                AttackId = value.AttackId?.Value,
                AbilityId = value.AbilityId?.Value,
            },
            ActionInterruptedEventDetail value => new
            {
                ActorId = value.ActorId.Value,
                SourceId = value.SourceId.Value,
                AbilityId = value.AbilityId.Value,
            },
            CombatantDefeatedEventDetail value => new
            {
                CombatantId = value.CombatantId.Value,
                SourceId = value.SourceId.Value,
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
