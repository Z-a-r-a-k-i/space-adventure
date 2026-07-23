using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

public partial class GameHost : Node3D
{
    private const uint FloorCollisionLayer = 1;
    private const uint InteractionCollisionLayer = 2;
    private const int MaximumNavigationInitializationFrames = 120;
    private const int MaximumVisualCaptureTicks = 600;
    private const int MaximumVisualCaptureSettleFrames = 600;
    private const int VisualCaptureWidth = 1280;
    private const int VisualCaptureHeight = 720;
    private const float WallCutawayCaptureYawRadians = 0.68f;
    private const float WallCutawayCapturePitchRadians = 0.90f;
    private const float WallCutawayCaptureDistanceMeters = 14.5f;
    private const float WallCutawayClearViewYawRadians = 3.1415927f;
    private const string WallCutawayCaptureArgument = "--visual-capture=wall-cutaway";
    private const string WallCutawayCaptureId = "wall-cutaway";
    private const string WallCutawayExpectedOccluderId = "presentation.wall.branch_north";
    private const string WallCutawayMoveCommandId = "visual.capture.wall-cutaway.move";
    private const string WallCutawayImageRelativePath = "artifacts/visual/captures/wall-cutaway.png";
    private const string WallCutawayManifestRelativePath = "artifacts/visual/captures/wall-cutaway.json";

    private static readonly Vector3 WallCutawayCaptureFocus = new(2.7f, 0.0f, 2.3f);

    private static readonly JsonSerializerOptions CaptureManifestJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    private static readonly JsonSerializerOptions CaptureLogJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly Dictionary<string, Node3D> _interactionViews = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Color> _interactionLabelColors = new(StringComparer.Ordinal);
    private readonly HashSet<string> _reportedCompletedInteractions = new(StringComparer.Ordinal);

    private GameSession? _session;
    private StationRouteDefinition? _definition;
    private AutomationBridge? _automationBridge;
    private TacticalCameraController _camera = null!;
    private OmniLight3D _airlockLight = null!;
    private Node3D _protagonistView = null!;
    private MeshInstance3D _destinationMarker = null!;
    private Label _objectiveLabel = null!;
    private Label _pauseLabel = null!;
    private Label _actionLabel = null!;
    private Label _feedbackLabel = null!;
    private CenterContainer _dialogueOverlay = null!;
    private Label _dialogueSpeaker = null!;
    private Label _dialogueLine = null!;
    private Button _dialogueResponse = null!;
    private CenterContainer _completionOverlay = null!;
    private string[] _developmentArguments = [];
    private string? _visibleDialogueInteractionId;
    private string? _hoveredInteractionId;
    private long _humanCommandSequence;
    private int _navigationInitializationFrames;
    private double _autoQuitSeconds;
    private bool _visualCaptureRequested;

    public override void _Ready()
    {
        _developmentArguments = OS.GetCmdlineUserArgs();
        _visualCaptureRequested = _developmentArguments.Any(argument =>
            argument.StartsWith("--visual-capture=", StringComparison.Ordinal));
        _camera = GetNode<TacticalCameraController>("TacticalCamera");
        _camera.InputEnabled = !_visualCaptureRequested;
        _airlockLight = GetNode<OmniLight3D>("AirlockLight");
        _protagonistView = GetNode<Node3D>("Actors/Protagonist");
        CacheInteractionViews();
        CreateDestinationMarker();
        CreateHud();
        SetFeedback("Synchronizing station navigation…", new Color("9eb6ce"));
    }

    public override void _PhysicsProcess(double delta)
    {
        _ = delta;
        if (_session is not null)
        {
            return;
        }

        _navigationInitializationFrames++;
        var navigationMap = GetWorld3D().NavigationMap;
        var navigationRegion = GetNode<NavigationRegion3D>("NavigationRegion");
        var startPosition = GetNode<Marker3D>("Markers/ProtagonistStart").GlobalPosition;
        var navigationReady = NavigationServer3D.MapGetIterationId(navigationMap) > 0
            && NavigationServer3D.RegionOwnsPoint(navigationRegion.GetRid(), startPosition);
        if (!navigationReady)
        {
            if (_navigationInitializationFrames >= MaximumNavigationInitializationFrames)
            {
                InitializationFailed("The station navigation map did not synchronize in time.");
            }
            return;
        }

        try
        {
            InitializeRoute(navigationMap);
        }
        catch (Exception exception)
        {
            InitializationFailed($"Station initialization failed: {exception.Message}");
        }
    }

    public override void _Process(double delta)
    {
        if (_session is null)
        {
            return;
        }

        _session.Advance(TimeSpan.FromSeconds(delta));
        var observation = _session.Observe();
        UpdateHoveredInteraction(observation);
        RenderObservation(observation);

        if (_autoQuitSeconds <= 0)
        {
            return;
        }

        _autoQuitSeconds -= delta;
        if (_autoQuitSeconds <= 0)
        {
            GetTree().Quit();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_session is null || _visualCaptureRequested)
        {
            return;
        }

        if (@event is InputEventKey { Pressed: true, Echo: false } key)
        {
            if (IsKey(key, Key.Space))
            {
                Dispatch(new SetPauseCommand(
                    NextHumanCommandId("pause"),
                    !_session.IsPaused));
                GetViewport().SetInputAsHandled();
                return;
            }

            if (IsKey(key, Key.Enter) || IsKey(key, Key.KpEnter) || IsKey(key, Key.Key1))
            {
                if (ChooseVisibleDialogueResponse())
                {
                    GetViewport().SetInputAsHandled();
                }
                return;
            }

        }

        if (@event is InputEventMouseButton
            {
                Pressed: true,
                ButtonIndex: MouseButton.Right,
            } mouseButton)
        {
            HandleContextClick(mouseButton.Position);
            GetViewport().SetInputAsHandled();
        }
    }

    private void InitializeRoute(Rid navigationMap)
    {
        var navigationRegion = GetNode<NavigationRegion3D>("NavigationRegion");
        GD.Print($"SPACEADVENTURE_NAV_READY iteration={NavigationServer3D.MapGetIterationId(navigationMap)} regions={NavigationServer3D.MapGetRegions(navigationMap).Count} vertices={navigationRegion.NavigationMesh?.GetVertices().Length ?? 0} polygons={navigationRegion.NavigationMesh?.GetPolygonCount() ?? 0}");
        var contentJson = Godot.FileAccess.GetFileAsString("res://content/station-route.json");
        _definition = StationRouteContent.ParseJson(contentJson);
        var layout = CreateLayout(_definition);
        _session = GameSession.CreateStationRoute(
            _definition,
            layout,
            new GodotSpatialPathfinder(navigationMap));

        _automationBridge = new AutomationBridge { Name = "AutomationBridge" };
        _automationBridge.Initialize(_session, ProjectStableIdToScreen);
        AddChild(_automationBridge);

        RenderObservation(_session.Observe());
        SetFeedback("Right-click the survivor to begin.", new Color("8fe6ff"));
        ProcessDevelopmentArguments();
    }

    private StationRouteLayout CreateLayout(StationRouteDefinition definition)
    {
        var startMarker = GetNode<Marker3D>("Markers/ProtagonistStart");
        ValidateStableId(startMarker, definition.Protagonist.Id.Value);

        var markers = GetNode<Node3D>("Markers").GetChildren()
            .OfType<Marker3D>()
            .ToDictionary(GetStableId, StringComparer.Ordinal);
        var placements = new List<StationInteractionPlacement>();
        foreach (var interaction in definition.Interactions)
        {
            if (!markers.TryGetValue(interaction.Id.Value, out var marker))
            {
                throw new InvalidDataException(
                    $"Scene marker for '{interaction.Id}' is missing.");
            }
            if (!_interactionViews.TryGetValue(interaction.Id.Value, out var view))
            {
                throw new InvalidDataException(
                    $"Scene interaction view for '{interaction.Id}' is missing.");
            }

            var targetPosition = GetInteractionGroundPosition(view);
            placements.Add(new StationInteractionPlacement(
                interaction.Id,
                ToCore(targetPosition),
                ToCore(WithGroundHeight(marker.GlobalPosition))));
        }

        return new StationRouteLayout(
            ToCore(WithGroundHeight(startMarker.GlobalPosition)),
            placements);
    }

    private void CacheInteractionViews()
    {
        foreach (var child in GetNode<Node3D>("Interactions").GetChildren().OfType<Node3D>())
        {
            var stableId = GetStableId(child);
            if (!_interactionViews.TryAdd(stableId, child))
            {
                throw new InvalidDataException($"Interaction view '{stableId}' is duplicated.");
            }

            if (child.GetNodeOrNull<Label3D>("Label") is not Label3D label)
            {
                throw new InvalidDataException($"Interaction view '{stableId}' has no Label3D child.");
            }

            _interactionLabelColors.Add(stableId, label.Modulate);
        }
    }

    private void CreateDestinationMarker()
    {
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color("55e6ff"),
            EmissionEnabled = true,
            Emission = new Color("1b7890"),
            EmissionEnergyMultiplier = 1.4f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };
        material.AlbedoColor = new Color(material.AlbedoColor, 0.72f);

        _destinationMarker = new MeshInstance3D
        {
            Name = "DestinationMarker",
            Mesh = new CylinderMesh
            {
                TopRadius = 0.34f,
                BottomRadius = 0.34f,
                Height = 0.035f,
            },
            MaterialOverride = material,
            Visible = false,
        };
        AddChild(_destinationMarker);
    }

    private void CreateHud()
    {
        var canvas = new CanvasLayer { Name = "StationHud" };
        AddChild(canvas);

        var statusPanel = new PanelContainer
        {
            Position = new Vector2(22, 22),
            CustomMinimumSize = new Vector2(575, 0),
            ZIndex = 10,
        };
        canvas.AddChild(statusPanel);

        var statusContent = new Control
        {
            CustomMinimumSize = new Vector2(575, 174),
        };
        statusPanel.AddChild(statusContent);

        var title = new Label
        {
            Position = new Vector2(0, 0),
            Text = "DISABLED FRONTIER STATION — ROUTE 01",
        };
        title.AddThemeFontSizeOverride("font_size", 21);
        statusContent.AddChild(title);

        _objectiveLabel = new Label { Position = new Vector2(0, 37) };
        _objectiveLabel.AddThemeFontSizeOverride("font_size", 19);
        statusContent.AddChild(_objectiveLabel);

        _pauseLabel = new Label { Position = new Vector2(0, 71) };
        _pauseLabel.AddThemeFontSizeOverride("font_size", 17);
        statusContent.AddChild(_pauseLabel);

        _actionLabel = new Label { Position = new Vector2(0, 102) };
        statusContent.AddChild(_actionLabel);

        _feedbackLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Position = new Vector2(0, 128),
            CustomMinimumSize = new Vector2(535, 42),
        };
        statusContent.AddChild(_feedbackLabel);

        var controlsPanel = new PanelContainer
        {
            Position = new Vector2(22, 575),
            CustomMinimumSize = new Vector2(720, 0),
            ZIndex = 10,
        };
        canvas.AddChild(controlsPanel);
        var controls = new Label
        {
            Text = "Right-click: move / interact   Space: pause\n"
                + "WASD: pan   Q/E or middle-drag: yaw   PgUp/PgDn: pitch   Wheel: zoom   Home/R: reset   F: focus",
            Modulate = new Color("b9cce0"),
        };
        controlsPanel.AddChild(controls);

        _dialogueOverlay = new CenterContainer
        {
            Name = "DialogueOverlay",
            AnchorRight = 1,
            AnchorBottom = 1,
            MouseFilter = Control.MouseFilterEnum.Stop,
            Visible = false,
            ZIndex = 5,
        };
        canvas.AddChild(_dialogueOverlay);
        var dialoguePanel = new PanelContainer { CustomMinimumSize = new Vector2(680, 0) };
        _dialogueOverlay.AddChild(dialoguePanel);
        var dialogueContent = new VBoxContainer();
        dialogueContent.AddThemeConstantOverride("separation", 12);
        dialoguePanel.AddChild(dialogueContent);
        _dialogueSpeaker = new Label();
        _dialogueSpeaker.AddThemeFontSizeOverride("font_size", 22);
        dialogueContent.AddChild(_dialogueSpeaker);
        _dialogueLine = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(620, 80),
        };
        _dialogueLine.AddThemeFontSizeOverride("font_size", 18);
        dialogueContent.AddChild(_dialogueLine);
        _dialogueResponse = new Button { FocusMode = Control.FocusModeEnum.None };
        _dialogueResponse.Pressed += OnDialogueResponsePressed;
        dialogueContent.AddChild(_dialogueResponse);

        _completionOverlay = new CenterContainer
        {
            Name = "CompletionOverlay",
            AnchorRight = 1,
            AnchorBottom = 1,
            MouseFilter = Control.MouseFilterEnum.Stop,
            Visible = false,
            ZIndex = 5,
        };
        canvas.AddChild(_completionOverlay);
        var completionPanel = new PanelContainer { CustomMinimumSize = new Vector2(650, 0) };
        _completionOverlay.AddChild(completionPanel);
        var completion = new Label
        {
            Text = "ROUTE COMPLETE\nEvacuation airlock reached.\n\nThe station route is secure.",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        completion.AddThemeFontSizeOverride("font_size", 28);
        completionPanel.AddChild(completion);
    }

    private void RenderObservation(GameObservation observation)
    {
        if (observation.StationRoute is not StationRouteObservation route)
        {
            return;
        }

        var protagonistPosition = ToGodot(route.Protagonist.Position);
        _protagonistView.GlobalPosition = protagonistPosition;
        _camera.FollowTarget = protagonistPosition;

        var visibleAction = route.Protagonist.PendingAction ?? route.Protagonist.CurrentAction;
        _destinationMarker.Visible = visibleAction is not null;
        if (visibleAction is not null)
        {
            _destinationMarker.GlobalPosition = ToGodot(visibleAction.Destination)
                + new Vector3(0, 0.025f, 0);
        }

        _objectiveLabel.Text = route.Objective.Status == ObjectiveStatus.Completed
            ? $"OBJECTIVE COMPLETE — {route.Objective.Text}"
            : $"OBJECTIVE — {route.Objective.Text}";
        _objectiveLabel.Modulate = route.Objective.Status == ObjectiveStatus.Completed
            ? new Color("72f2a8")
            : new Color("f2dc72");

        _pauseLabel.Text = observation.Paused
            ? "TACTICAL PAUSE"
            : "RUNNING";
        _pauseLabel.Modulate = observation.Paused
            ? new Color("ffc45c")
            : new Color("72f2a8");

        _actionLabel.Text = visibleAction is null
            ? $"{route.Protagonist.DisplayName} is awaiting an order."
            : $"{(route.Protagonist.PendingAction is null ? "Current" : "Pending")} order — "
                + DescribeAction(route, visibleAction);

        var objectiveTargetId = GetObjectiveTargetId(route.Objective.Id);

        foreach (var interaction in route.Interactions)
        {
            if (!_interactionViews.TryGetValue(interaction.Id.Value, out var view))
            {
                continue;
            }

            if (view.GetNodeOrNull<Label3D>("Label") is Label3D label)
            {
                var labelText = interaction.State switch
                {
                    InteractionState.Unavailable => $"{interaction.Prompt.ToUpperInvariant()}  [LOCKED]",
                    InteractionState.DialogueActive => $"{interaction.Prompt.ToUpperInvariant()}  [TALKING]",
                    InteractionState.Completed when interaction.Kind == StationInteractionKind.Environment
                        => $"{interaction.Prompt.ToUpperInvariant()}  [INSPECTED]",
                    InteractionState.Completed => $"{interaction.Prompt.ToUpperInvariant()}  [DONE]",
                    _ => interaction.Prompt.ToUpperInvariant(),
                };

                var isHovered = string.Equals(
                    interaction.Id.Value,
                    _hoveredInteractionId,
                    StringComparison.Ordinal);
                var isObjectiveTarget = string.Equals(
                    interaction.Id.Value,
                    objectiveTargetId,
                    StringComparison.Ordinal)
                    && interaction.State is InteractionState.Available or InteractionState.DialogueActive;
                if (isHovered && interaction.CanInteract)
                {
                    labelText += "  [RIGHT-CLICK]";
                }
                else if (isObjectiveTarget)
                {
                    labelText += "  [OBJECTIVE]";
                }

                var baseColor = _interactionLabelColors[interaction.Id.Value];
                label.Text = labelText;
                label.Modulate = interaction.State == InteractionState.Unavailable
                    ? baseColor.Darkened(0.42f)
                    : isHovered
                        ? Colors.White
                        : isObjectiveTarget
                            ? baseColor.Lightened(0.18f)
                            : baseColor;
                label.Scale = Vector3.One * (isHovered ? 1.18f : isObjectiveTarget ? 1.10f : 1.0f);
                label.OutlineSize = isHovered || isObjectiveTarget ? 12 : 8;
            }

            var unavailableTransparency = interaction.State == InteractionState.Unavailable ? 0.58f : 0.0f;
            foreach (var geometry in view.GetChildren().OfType<GeometryInstance3D>())
            {
                if (geometry is not Label3D)
                {
                    geometry.Transparency = unavailableTransparency;
                }
            }

            if (interaction.Kind == StationInteractionKind.Destination)
            {
                _airlockLight.LightEnergy = interaction.State == InteractionState.Unavailable ? 0.65f : 2.5f;
            }

            if (interaction.State == InteractionState.Completed
                && interaction.Kind == StationInteractionKind.Environment
                && interaction.ResultText is not null
                && _reportedCompletedInteractions.Add(interaction.Id.Value))
            {
                SetFeedback(interaction.ResultText, new Color("d1b5ff"));
            }
        }

        if (route.ActiveDialogue is DialogueObservation dialogue)
        {
            _dialogueOverlay.Visible = true;
            _dialogueSpeaker.Text = dialogue.Speaker.ToUpperInvariant();
            _dialogueLine.Text = dialogue.Line;
            _dialogueResponse.Text = $"1 — {dialogue.Response.Text}";
            _visibleDialogueInteractionId = dialogue.InteractionId.Value;
        }
        else
        {
            _dialogueOverlay.Visible = false;
            _visibleDialogueInteractionId = null;
        }

        _completionOverlay.Visible = route.Phase == ScenarioPhase.Completed;
    }

    private void HandleContextClick(Vector2 screenPosition)
    {
        var route = _session!.Observe().StationRoute!;
        if (route.Phase == ScenarioPhase.Completed)
        {
            SetFeedback("The route is already complete.", new Color("72f2a8"));
            return;
        }
        if (route.ActiveDialogue is not null)
        {
            SetFeedback("Choose the dialogue response before issuing another order.", new Color("ffb36b"));
            return;
        }

        var rayOrigin = _camera.ProjectRayOrigin(screenPosition);
        var rayEnd = rayOrigin + (_camera.ProjectRayNormal(screenPosition) * 200.0f);
        var interactionHit = CastRay(rayOrigin, rayEnd, InteractionCollisionLayer);
        if (interactionHit.Count > 0
            && interactionHit["collider"].AsGodotObject() is Node collider
            && collider.HasMeta("stable_id"))
        {
            var stableId = collider.GetMeta("stable_id").AsString();
            Dispatch(new InteractCommand(
                NextHumanCommandId("interact"),
                route.Protagonist.Id,
                new EntityId(stableId)));
            return;
        }

        var floorHit = CastRay(rayOrigin, rayEnd, FloorCollisionLayer);
        if (floorHit.Count == 0)
        {
            SetFeedback("No navigable station floor under that pointer.", new Color("ff8b8b"));
            return;
        }

        var hitPosition = floorHit["position"].AsVector3();
        Dispatch(new MoveActorCommand(
            NextHumanCommandId("move"),
            route.Protagonist.Id,
            ToCore(WithGroundHeight(hitPosition))));
    }

    private Godot.Collections.Dictionary CastRay(Vector3 origin, Vector3 destination, uint collisionMask)
    {
        var query = PhysicsRayQueryParameters3D.Create(origin, destination, collisionMask);
        return GetWorld3D().DirectSpaceState.IntersectRay(query);
    }

    private bool ChooseVisibleDialogueResponse()
    {
        var route = _session!.Observe().StationRoute!;
        if (route.ActiveDialogue is not DialogueObservation dialogue)
        {
            return false;
        }

        Dispatch(new ChooseDialogueResponseCommand(
            NextHumanCommandId("dialogue"),
            route.Protagonist.Id,
            dialogue.InteractionId,
            dialogue.Response.Id));
        return true;
    }

    private void OnDialogueResponsePressed()
    {
        _ = ChooseVisibleDialogueResponse();
    }

    private void Dispatch(IGameCommand command)
    {
        var acknowledgement = _session!.Execute(command);
        if (acknowledgement.Accepted)
        {
            var message = command switch
            {
                MoveActorCommand => "Move order accepted.",
                InteractCommand interact => $"Order accepted — {GetInteractionPrompt(interact.TargetId)}.",
                ChooseDialogueResponseCommand => "Airlock route unlocked. Optional: inspect the purple service terminal.",
                SetPauseCommand pause => pause.Paused ? "Tactical pause engaged." : "Simulation resumed.",
                _ => "Order accepted.",
            };
            SetFeedback(message, new Color("8fe6ff"));
        }
        else
        {
            SetFeedback(
                $"ORDER REJECTED — {acknowledgement.RejectionCode}",
                new Color("ff8b8b"));
        }

        RenderObservation(acknowledgement.Observation);
    }

    private void UpdateHoveredInteraction(GameObservation observation)
    {
        if (_visualCaptureRequested
            || observation.StationRoute is not StationRouteObservation route
            || route.Phase == ScenarioPhase.Completed
            || route.ActiveDialogue is not null)
        {
            _hoveredInteractionId = null;
            return;
        }

        var screenPosition = GetViewport().GetMousePosition();
        var rayOrigin = _camera.ProjectRayOrigin(screenPosition);
        var rayEnd = rayOrigin + (_camera.ProjectRayNormal(screenPosition) * 200.0f);
        var interactionHit = CastRay(rayOrigin, rayEnd, InteractionCollisionLayer);
        _hoveredInteractionId = interactionHit.Count > 0
            && interactionHit["collider"].AsGodotObject() is Node collider
            && collider.HasMeta("stable_id")
                ? collider.GetMeta("stable_id").AsString()
                : null;
    }

    private static string DescribeAction(StationRouteObservation route, PrimaryActionObservation action)
    {
        if (action.InteractionTargetId is EntityId targetId)
        {
            return route.Interactions.Single(interaction => interaction.Id == targetId).Prompt;
        }

        return "Move to the selected destination";
    }

    private string GetInteractionPrompt(EntityId targetId)
    {
        return _definition!.Interactions.Single(interaction => interaction.Id == targetId).Prompt;
    }

    private string? GetObjectiveTargetId(ObjectiveId objectiveId)
    {
        var targetEffect = objectiveId == _definition!.BriefingObjective.Id
            ? StationInteractionEffect.BeginBriefingDialogue
            : StationInteractionEffect.CompleteScenario;
        return _definition.Interactions.Single(interaction => interaction.Effect == targetEffect).Id.Value;
    }

    private CommandId NextHumanCommandId(string commandType)
    {
        _humanCommandSequence++;
        return new CommandId($"input.{commandType}.{_humanCommandSequence}");
    }

    private ScreenPositionProjection? ProjectStableIdToScreen(string stableId)
    {
        if (_session?.Observe().StationRoute is not StationRouteObservation route)
        {
            return null;
        }

        Vector3 worldPosition;
        if (string.Equals(stableId, route.Protagonist.Id.Value, StringComparison.Ordinal))
        {
            worldPosition = ToGodot(route.Protagonist.Position) + new Vector3(0, 1.0f, 0);
        }
        else if (_interactionViews.TryGetValue(stableId, out var interactionView))
        {
            worldPosition = interactionView.GetNodeOrNull<CollisionShape3D>("CollisionShape3D")
                is CollisionShape3D collision
                    ? collision.GlobalPosition
                    : interactionView.GlobalPosition + new Vector3(0, 0.8f, 0);
        }
        else
        {
            return null;
        }

        var screen = _camera.UnprojectPosition(worldPosition);
        var visible = !_camera.IsPositionBehind(worldPosition)
            && GetViewport().GetVisibleRect().HasPoint(screen);
        return new ScreenPositionProjection(screen.X, screen.Y, visible);
    }

    private void ProcessDevelopmentArguments()
    {
        foreach (var argument in _developmentArguments)
        {
            if (argument.StartsWith("--visual-capture=", StringComparison.Ordinal))
            {
                if (string.Equals(argument, WallCutawayCaptureArgument, StringComparison.Ordinal))
                {
                    _ = RunWallCutawayCaptureAsync();
                }
                else
                {
                    FailVisualCapture(
                        "unsupported_capture_profile",
                        $"Unsupported visual capture argument '{argument}'.");
                }
                return;
            }

            if (string.Equals(argument, "--bootstrap-smoke", StringComparison.Ordinal))
            {
                RunBootstrapSmoke();
                return;
            }
            if (string.Equals(argument, "--station-route-smoke", StringComparison.Ordinal))
            {
                RunStationRouteSmoke();
                return;
            }

            const string prefix = "--auto-quit-seconds=";
            if (argument.StartsWith(prefix, StringComparison.Ordinal)
                && double.TryParse(
                    argument[prefix.Length..],
                    NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out var seconds)
                && seconds > 0)
            {
                _autoQuitSeconds = seconds;
            }
        }
    }

    private async Task RunWallCutawayCaptureAsync()
    {
        string? temporaryImagePath = null;
        string? temporaryManifestPath = null;

        try
        {
            if (string.Equals(DisplayServer.GetName(), "headless", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Visual capture requires a graphical display server.");
            }

            var session = _session
                ?? throw new InvalidOperationException("The game session is unavailable for capture.");
            var definition = _definition
                ?? throw new InvalidOperationException("The route definition is unavailable for capture.");
            var route = session.Observe().StationRoute
                ?? throw new InvalidOperationException("The station-route observation is unavailable for capture.");

            var pauseCommand = new SetPauseCommand(
                new CommandId("visual.capture.wall-cutaway.pause"),
                Paused: true);
            var pauseAcknowledgement = session.Execute(pauseCommand);
            if (!pauseAcknowledgement.Accepted)
            {
                throw new InvalidOperationException(
                    $"Capture pause was rejected: {pauseAcknowledgement.RejectionCode}.");
            }

            var terminalApproach = GetNode<Marker3D>("Markers/TerminalApproach");
            ValidateStableId(terminalApproach, "interaction.service_terminal");
            var destination = ToCore(WithGroundHeight(terminalApproach.GlobalPosition));
            var moveCommandId = new CommandId(WallCutawayMoveCommandId);
            var eventSequenceBeforeMove = session.Observe().LatestEventSequence;
            var moveAcknowledgement = session.Execute(new MoveActorCommand(
                moveCommandId,
                route.Protagonist.Id,
                destination));
            if (!moveAcknowledgement.Accepted)
            {
                throw new InvalidOperationException(
                    $"Capture movement was rejected: {moveAcknowledgement.RejectionCode}.");
            }

            var ticksAdvanced = 0;
            while (!HasMovementArrived(
                       session,
                       eventSequenceBeforeMove,
                       moveCommandId)
                   && ticksAdvanced < MaximumVisualCaptureTicks)
            {
                session.StepWhilePaused(1);
                ticksAdvanced++;
            }

            if (!HasMovementArrived(session, eventSequenceBeforeMove, moveCommandId))
            {
                throw new TimeoutException(
                    $"Capture movement did not arrive within {MaximumVisualCaptureTicks} ticks.");
            }

            var stableObservation = session.Observe();
            if (!stableObservation.Paused
                || stableObservation.StationRoute is not StationRouteObservation stableRoute)
            {
                throw new InvalidOperationException("Capture state was not paused and observable.");
            }

            RenderObservation(stableObservation);
            _hoveredInteractionId = null;

            _camera.FocusPoint = WallCutawayCaptureFocus;
            _camera.PitchRadians = WallCutawayCapturePitchRadians;
            _camera.DistanceMeters = WallCutawayCaptureDistanceMeters;

            var cutPhase = await RunCameraOcclusionTransitionPhaseAsync(
                "initial_cut",
                WallCutawayCaptureYawRadians,
                [WallCutawayExpectedOccluderId]);
            var clearViewPhase = await RunCameraOcclusionTransitionPhaseAsync(
                "clear_view_restore",
                WallCutawayClearViewYawRadians,
                Array.Empty<string>());
            var recutPhase = await RunCameraOcclusionTransitionPhaseAsync(
                "recut",
                WallCutawayCaptureYawRadians,
                [WallCutawayExpectedOccluderId]);
            AssertExpectedWallCutaway(recutPhase.After);

            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);

            var finalObservation = session.Observe();
            if (!finalObservation.Paused
                || finalObservation.Tick != stableObservation.Tick
                || finalObservation.LatestEventSequence != stableObservation.LatestEventSequence)
            {
                throw new InvalidOperationException(
                    "Authoritative gameplay state changed while the capture frame was synchronizing.");
            }

            var cutawayObservation = _camera.ObserveOcclusion();
            AssertExpectedWallCutaway(cutawayObservation);

            var viewport = GetViewport();
            var image = viewport.GetTexture().GetImage();
            if (image.IsEmpty()
                || image.GetWidth() != VisualCaptureWidth
                || image.GetHeight() != VisualCaptureHeight)
            {
                throw new InvalidOperationException(
                    $"Expected a {VisualCaptureWidth}x{VisualCaptureHeight} viewport image, "
                    + $"received {image.GetWidth()}x{image.GetHeight()}.");
            }

            var paths = PrepareWallCutawayCapturePaths();
            temporaryImagePath = paths.TemporaryImagePath;
            temporaryManifestPath = paths.TemporaryManifestPath;

            var saveError = image.SavePng(ToGodotPath(temporaryImagePath));
            if (saveError != Error.Ok)
            {
                throw new IOException($"Godot failed to save the capture PNG: {saveError}.");
            }

            var imageInfo = new FileInfo(temporaryImagePath);
            if (!imageInfo.Exists || imageInfo.Length <= 0)
            {
                throw new IOException("The capture PNG was not written or is empty.");
            }

            var imageSha256 = ComputeSha256(temporaryImagePath);
            var observationJson = ReadAutomationObservation();
            var cameraTransform = _camera.GlobalTransform;
            var manifest = new
            {
                schema_version = 1,
                capture_id = WallCutawayCaptureId,
                passed = true,
                scenario_id = stableRoute.ScenarioId.Value,
                content_revision = definition.ContentRevision,
                stable_state = new
                {
                    predicate = "movement_arrived",
                    command_id = WallCutawayMoveCommandId,
                    target_marker_id = GetStableId(terminalApproach),
                    ticks_advanced = ticksAdvanced,
                    tick = finalObservation.Tick,
                    paused = finalObservation.Paused,
                    latest_event_sequence = finalObservation.LatestEventSequence,
                },
                viewport = new
                {
                    width = image.GetWidth(),
                    height = image.GetHeight(),
                    display_server = DisplayServer.GetName(),
                    rendering_method = RenderingServer.GetCurrentRenderingMethod(),
                    rendering_driver = RenderingServer.GetCurrentRenderingDriverName(),
                },
                camera = new
                {
                    stable_id = "presentation.camera.tactical",
                    position = ProjectVector3(cameraTransform.Origin),
                    basis = new
                    {
                        x = ProjectVector3(cameraTransform.Basis.X),
                        y = ProjectVector3(cameraTransform.Basis.Y),
                        z = ProjectVector3(cameraTransform.Basis.Z),
                    },
                    focus = ProjectVector3(_camera.FocusPoint),
                    yaw_radians = _camera.YawRadians,
                    pitch_radians = _camera.PitchRadians,
                    distance_meters = _camera.DistanceMeters,
                    projection = _camera.Projection.ToString(),
                    fov_degrees = _camera.Fov,
                    near = _camera.Near,
                    far = _camera.Far,
                },
                cutaway = cutawayObservation,
                cutaway_lifecycle = new
                {
                    schema_version = 1,
                    algorithm = "process_frame_animation_v1",
                    maximum_process_frames_per_phase = MaximumVisualCaptureSettleFrames,
                    gameplay_remained_paused = true,
                    phases = new[]
                    {
                        cutPhase,
                        clearViewPhase,
                        recutPhase,
                    },
                },
                observation = observationJson,
                image = new
                {
                    path = WallCutawayImageRelativePath,
                    width = image.GetWidth(),
                    height = image.GetHeight(),
                    byte_length = imageInfo.Length,
                    sha256 = imageSha256,
                },
            };

            var manifestJson = JsonSerializer.Serialize(manifest, CaptureManifestJsonOptions);
            File.WriteAllText(
                temporaryManifestPath,
                manifestJson + System.Environment.NewLine,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            ReplaceCaptureFile(temporaryImagePath, paths.ImagePath);
            temporaryImagePath = null;
            ReplaceCaptureFile(temporaryManifestPath, paths.ManifestPath);
            temporaryManifestPath = null;

            var summary = JsonSerializer.Serialize(new
            {
                schema_version = 1,
                capture_id = WallCutawayCaptureId,
                passed = true,
                image_path = paths.ImagePath,
                manifest_path = paths.ManifestPath,
                sha256 = imageSha256,
            }, CaptureLogJsonOptions);
            GD.Print($"SPACEADVENTURE_CAPTURE {summary}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            TryDeleteCaptureTemporaryFile(temporaryImagePath);
            TryDeleteCaptureTemporaryFile(temporaryManifestPath);
            FailVisualCapture("capture_failed", exception.Message);
        }
    }

    private static bool HasMovementArrived(
        GameSession session,
        long afterSequence,
        CommandId commandId)
    {
        return session.EventsSince(afterSequence).Any(gameEvent =>
            gameEvent.Type == GameplayEventType.MovementArrived
            && gameEvent.CommandId == commandId);
    }

    private async Task<CutawayTransitionPhase> RunCameraOcclusionTransitionPhaseAsync(
        string phaseId,
        float yawRadians,
        IReadOnlyList<string> expectedDesiredIds)
    {
        _camera.YawRadians = yawRadians;
        var before = _camera.ObserveOcclusion();
        if (!before.TargetAvailable
            || !before.DesiredCutawayIds.SequenceEqual(
                expectedDesiredIds,
                StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Camera occlusion phase '{phaseId}' began with unexpected desired walls "
                + $"[{string.Join(", ", before.DesiredCutawayIds)}].");
        }
        if (before.AllSettled)
        {
            throw new InvalidOperationException(
                $"Camera occlusion phase '{phaseId}' began already settled; "
                + "the live transition was not exercised.");
        }

        var after = before;
        var processFramesWaited = 0;

        while (processFramesWaited < MaximumVisualCaptureSettleFrames)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            processFramesWaited++;
            after = _camera.ObserveOcclusion();
            if (IsExpectedSettledOcclusion(after, expectedDesiredIds))
            {
                break;
            }
        }

        if (!IsExpectedSettledOcclusion(after, expectedDesiredIds))
        {
            throw new TimeoutException(
                $"Camera occlusion phase '{phaseId}' did not settle within "
                + $"{MaximumVisualCaptureSettleFrames} process frames. Expected walls: "
                + $"[{string.Join(", ", expectedDesiredIds)}]; last desired walls: "
                + $"[{string.Join(", ", after.DesiredCutawayIds)}]; "
                + $"all_settled={after.AllSettled}.");
        }
        if (before.Target != after.Target)
        {
            throw new InvalidOperationException(
                $"Camera occlusion target changed during phase '{phaseId}'.");
        }

        return new CutawayTransitionPhase(
            phaseId,
            yawRadians,
            expectedDesiredIds.ToArray(),
            processFramesWaited,
            before,
            after);
    }

    private static bool IsExpectedSettledOcclusion(
        CameraOcclusionObservation observation,
        IReadOnlyList<string> expectedDesiredIds)
    {
        return observation.TargetAvailable
            && observation.AllSettled
            && observation.DesiredCutawayIds.SequenceEqual(
                expectedDesiredIds,
                StringComparer.Ordinal);
    }

    private static void AssertExpectedWallCutaway(CameraOcclusionObservation observation)
    {
        if (!observation.TargetAvailable)
        {
            throw new InvalidOperationException("The camera cutaway target is unavailable.");
        }
        if (!observation.DesiredCutawayIds.SequenceEqual(
                [WallCutawayExpectedOccluderId],
                StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Expected only '{WallCutawayExpectedOccluderId}' to be cut away, found "
                + $"[{string.Join(", ", observation.DesiredCutawayIds)}].");
        }
        if (!observation.AllSettled)
        {
            throw new InvalidOperationException("The camera cutaway did not settle before capture.");
        }
    }

    private JsonElement ReadAutomationObservation()
    {
        var response = _automationBridge?.GetObservationJson()
            ?? throw new InvalidOperationException("The automation observation is unavailable.");
        using var document = JsonDocument.Parse(response);
        if (!document.RootElement.GetProperty("accepted").GetBoolean())
        {
            throw new InvalidOperationException("The automation observation was rejected.");
        }
        return document.RootElement.GetProperty("observation").Clone();
    }

    private static CapturePaths PrepareWallCutawayCapturePaths()
    {
        var resourceRoot = Path.GetFullPath(ProjectSettings.GlobalizePath("res://"));
        var repositoryRoot = Path.GetFullPath(Path.Combine(resourceRoot, ".."));
        var captureDirectory = Path.GetFullPath(Path.Combine(
            repositoryRoot,
            "artifacts",
            "visual",
            "captures"));
        EnsureSafeCaptureDirectory(repositoryRoot, captureDirectory);

        var imagePath = Path.GetFullPath(Path.Combine(
            repositoryRoot,
            WallCutawayImageRelativePath));
        var manifestPath = Path.GetFullPath(Path.Combine(
            repositoryRoot,
            WallCutawayManifestRelativePath));
        AssertCaptureFileParent(captureDirectory, imagePath);
        AssertCaptureFileParent(captureDirectory, manifestPath);
        AssertNotReparsePointIfPresent(imagePath);
        AssertNotReparsePointIfPresent(manifestPath);

        var temporaryImagePath = Path.Combine(captureDirectory, "wall-cutaway.tmp.png");
        var temporaryManifestPath = Path.Combine(captureDirectory, "wall-cutaway.tmp.json");
        PrepareCaptureTemporaryFile(temporaryImagePath);
        PrepareCaptureTemporaryFile(temporaryManifestPath);

        return new CapturePaths(
            imagePath,
            manifestPath,
            temporaryImagePath,
            temporaryManifestPath);
    }

    private static void EnsureSafeCaptureDirectory(string repositoryRoot, string captureDirectory)
    {
        var pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var repositoryPrefix = Path.TrimEndingDirectorySeparator(repositoryRoot)
            + Path.DirectorySeparatorChar;
        if (!captureDirectory.StartsWith(repositoryPrefix, pathComparison))
        {
            throw new IOException("The capture directory is outside the repository.");
        }

        var relativeDirectory = Path.GetRelativePath(repositoryRoot, captureDirectory);
        var currentDirectory = repositoryRoot;
        foreach (var segment in relativeDirectory.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            currentDirectory = Path.Combine(currentDirectory, segment);
            if (!Directory.Exists(currentDirectory))
            {
                Directory.CreateDirectory(currentDirectory);
            }
            AssertNotReparsePointIfPresent(currentDirectory);
        }
    }

    private static void AssertCaptureFileParent(string captureDirectory, string filePath)
    {
        var parent = Path.GetDirectoryName(filePath)
            ?? throw new IOException("A capture file has no parent directory.");
        var pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!Path.GetFullPath(parent).Equals(
                Path.GetFullPath(captureDirectory),
                pathComparison))
        {
            throw new IOException("A capture file is outside the fixed capture directory.");
        }
    }

    private static void AssertNotReparsePointIfPresent(string path)
    {
        if ((File.Exists(path) || Directory.Exists(path))
            && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"Capture path '{path}' must not be a reparse point.");
        }
    }

    private static void PrepareCaptureTemporaryFile(string path)
    {
        AssertNotReparsePointIfPresent(path);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void ReplaceCaptureFile(string sourcePath, string destinationPath)
    {
        AssertNotReparsePointIfPresent(sourcePath);
        AssertNotReparsePointIfPresent(destinationPath);
        File.Move(sourcePath, destinationPath, overwrite: true);
    }

    private static void TryDeleteCaptureTemporaryFile(string? path)
    {
        if (path is null)
        {
            return;
        }

        try
        {
            AssertNotReparsePointIfPresent(path);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Preserve the original capture failure; the fixed temporary file can be inspected manually.
        }
        catch (UnauthorizedAccessException)
        {
            // Preserve the original capture failure; the fixed temporary file can be inspected manually.
        }
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string ToGodotPath(string path)
    {
        return path.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static object ProjectVector3(Vector3 value)
    {
        return new { x = value.X, y = value.Y, z = value.Z };
    }

    private void FailVisualCapture(string error, string message)
    {
        var failure = JsonSerializer.Serialize(new
        {
            schema_version = 1,
            capture_id = WallCutawayCaptureId,
            passed = false,
            error,
            message,
        }, CaptureLogJsonOptions);
        GD.PushError($"Visual capture failed: {message}");
        GD.Print($"SPACEADVENTURE_CAPTURE_FAILURE {failure}");
        GetTree().Quit(1);
    }

    private sealed record CapturePaths(
        string ImagePath,
        string ManifestPath,
        string TemporaryImagePath,
        string TemporaryManifestPath);

    private sealed record CutawayTransitionPhase(
        string PhaseId,
        float YawRadians,
        string[] ExpectedDesiredCutawayIds,
        int ProcessFramesWaited,
        CameraOcclusionObservation Before,
        CameraOcclusionObservation After);

    private void RunBootstrapSmoke()
    {
        var result = _automationBridge!.SubmitCommandJson(JsonSerializer.Serialize(new
        {
            schema_version = 1,
            command_id = "godot.bootstrap.pause",
            type = "set_pause",
            payload = new { paused = true },
        }));
        var invalidResult = _automationBridge.SubmitCommandJson("""
            {"schema_version":1.5,"command_id":"godot.bootstrap.invalid","type":"set_pause","payload":{"paused":false}}
            """);
        var oversizedStep = _automationBridge.AdvanceExactTicks(
            GameSession.MaximumDirectTickAdvance + 1);
        var oversizedMove = _automationBridge.SubmitCommandJson(JsonSerializer.Serialize(new
        {
            schema_version = 1,
            command_id = "godot.bootstrap.oversized-move",
            type = "move_actor",
            payload = new
            {
                actor_id = _definition!.Protagonist.Id.Value,
                destination = new { x = 1e308, y = 0.0, z = 0.0 },
            },
        }));

        var passed = IsAccepted(result)
            && !IsAccepted(invalidResult)
            && !IsAccepted(oversizedStep)
            && !IsAccepted(oversizedMove)
            && _session!.Observe() is { Paused: true, Tick: 0 };
        GD.Print(
            $"SPACEADVENTURE_SMOKE valid={result} invalid={invalidResult} oversized_step={oversizedStep} oversized_move={oversizedMove}");
        GetTree().Quit(passed ? 0 : 1);
    }

    private void RunStationRouteSmoke()
    {
        var definition = _definition!;
        var actorId = definition.Protagonist.Id.Value;
        var survivor = definition.Interactions.Single(
            interaction => interaction.Effect == StationInteractionEffect.BeginBriefingDialogue);
        var terminal = definition.Interactions.Single(
            interaction => interaction.Effect == StationInteractionEffect.RecordObservation);
        var airlock = definition.Interactions.Single(
            interaction => interaction.Effect == StationInteractionEffect.CompleteScenario);

        var pause = _automationBridge!.SetPaused(true);
        var survivorOrder = SubmitInteraction("godot.route.survivor", actorId, survivor.Id.Value);
        var survivorSequence = _session!.Observe().LatestEventSequence;
        var dialogueWait = _automationBridge.AdvanceUntilEventJson(
            survivorSequence,
            "dialogue_started",
            maximumTicks: 600);
        var response = _automationBridge.SubmitCommandJson(JsonSerializer.Serialize(new
        {
            schema_version = 1,
            command_id = "godot.route.response",
            type = "choose_dialogue_response",
            payload = new
            {
                actor_id = actorId,
                interaction_id = survivor.Id.Value,
                response_id = survivor.Dialogue!.Response.Id.Value,
            },
        }));

        var terminalOrder = SubmitInteraction("godot.route.terminal", actorId, terminal.Id.Value);
        var terminalSequence = _session.Observe().LatestEventSequence;
        var terminalWait = _automationBridge.AdvanceUntilEventJson(
            terminalSequence,
            "interaction_completed",
            maximumTicks: 600);

        var airlockOrder = SubmitInteraction("godot.route.airlock", actorId, airlock.Id.Value);
        var airlockSequence = _session.Observe().LatestEventSequence;
        var completionWait = _automationBridge.AdvanceUntilEventJson(
            airlockSequence,
            "scenario_completed",
            maximumTicks: 600);

        var final = _session.Observe().StationRoute!;
        var passed = IsAccepted(pause)
            && IsAccepted(survivorOrder)
            && IsReached(dialogueWait)
            && IsAccepted(response)
            && IsAccepted(terminalOrder)
            && IsReached(terminalWait)
            && IsAccepted(airlockOrder)
            && IsReached(completionWait)
            && final.Phase == ScenarioPhase.Completed
            && final.Interactions.Single(interaction => interaction.Id == terminal.Id).State
                == InteractionState.Completed;

        var summary = JsonSerializer.Serialize(new
        {
            passed,
            tick = _session.Observe().Tick,
            phase = final.Phase.ToString(),
            objective = final.Objective.Id.Value,
            terminal_inspected = final.Interactions.Single(
                interaction => interaction.Id == terminal.Id).State == InteractionState.Completed,
        });
        GD.Print($"SPACEADVENTURE_ROUTE_SMOKE {summary}");
        if (!passed)
        {
            GD.Print($"SPACEADVENTURE_ROUTE_DIAGNOSTIC pause={pause}");
            GD.Print($"SPACEADVENTURE_ROUTE_DIAGNOSTIC survivor={survivorOrder}");
            GD.Print($"SPACEADVENTURE_ROUTE_DIAGNOSTIC dialogue_wait={dialogueWait}");
            GD.Print($"SPACEADVENTURE_ROUTE_DIAGNOSTIC response={response}");
            GD.Print($"SPACEADVENTURE_ROUTE_DIAGNOSTIC terminal={terminalOrder}");
            GD.Print($"SPACEADVENTURE_ROUTE_DIAGNOSTIC terminal_wait={terminalWait}");
            GD.Print($"SPACEADVENTURE_ROUTE_DIAGNOSTIC airlock={airlockOrder}");
            GD.Print($"SPACEADVENTURE_ROUTE_DIAGNOSTIC completion_wait={completionWait}");
        }
        GetTree().Quit(passed ? 0 : 1);
    }

    private string SubmitInteraction(string commandId, string actorId, string targetId)
    {
        return _automationBridge!.SubmitCommandJson(JsonSerializer.Serialize(new
        {
            schema_version = 1,
            command_id = commandId,
            type = "interact",
            payload = new { actor_id = actorId, target_id = targetId },
        }));
    }

    private static bool IsAccepted(string result)
    {
        using var document = JsonDocument.Parse(result);
        return document.RootElement.GetProperty("accepted").GetBoolean();
    }

    private static bool IsReached(string result)
    {
        using var document = JsonDocument.Parse(result);
        return document.RootElement.GetProperty("accepted").GetBoolean()
            && document.RootElement.GetProperty("reached").GetBoolean();
    }

    private void InitializationFailed(string message)
    {
        GD.PushError(message);
        SetFeedback(message, new Color("ff6b6b"));
        SetPhysicsProcess(false);
        if (_visualCaptureRequested
            || _developmentArguments.Any(argument => argument.EndsWith("-smoke", StringComparison.Ordinal)))
        {
            GetTree().Quit(1);
        }
    }

    private void SetFeedback(string text, Color color)
    {
        _feedbackLabel.Text = text;
        _feedbackLabel.Modulate = color;
    }

    private static bool IsKey(InputEventKey key, Key expected)
    {
        return key.Keycode == expected || key.PhysicalKeycode == expected;
    }

    private static string GetStableId(Node node)
    {
        if (!node.HasMeta("stable_id"))
        {
            throw new InvalidDataException($"Scene node '{node.GetPath()}' has no stable_id metadata.");
        }
        return node.GetMeta("stable_id").AsString();
    }

    private static void ValidateStableId(Node node, string expected)
    {
        var actual = GetStableId(node);
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Scene node '{node.GetPath()}' maps '{actual}', expected '{expected}'.");
        }
    }

    private static Vector3 GetInteractionGroundPosition(Node3D view)
    {
        var position = view.GetNodeOrNull<CollisionShape3D>("CollisionShape3D")
            is CollisionShape3D collision
                ? collision.GlobalPosition
                : view.GlobalPosition;
        return WithGroundHeight(position);
    }

    private static Vector3 WithGroundHeight(Vector3 position)
    {
        return new Vector3(position.X, 0, position.Z);
    }

    private static WorldPosition ToCore(Vector3 position)
    {
        return new WorldPosition(position.X, position.Y, position.Z);
    }

    private static Vector3 ToGodot(WorldPosition position)
    {
        return new Vector3((float)position.X, (float)position.Y, (float)position.Z);
    }
}
