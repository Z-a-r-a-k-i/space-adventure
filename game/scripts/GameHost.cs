using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

public partial class GameHost : Node3D
{
    private const string VanguardKitId = "kit.protagonist.vanguard";
    private const uint FloorCollisionLayer = 1;
    private const uint InteractionCollisionLayer = 2;
    private const uint HostileCollisionLayer = 8;
    private const int MaximumNavigationInitializationFrames = 120;
    private const int MaximumVisualCaptureTicks = 600;
    private const int MaximumVisualCaptureSettleFrames = 600;
    private const int VisualCaptureWidth = 1280;
    private const int VisualCaptureHeight = 720;
    private const float ServiceDoorAnimationSeconds = 0.25f;
    private const float ServiceDoorLeafTravelMeters = 0.94f;
    private const float WallCutawayCaptureYawRadians = -1.5707964f;
    private const float WallCutawayCapturePitchRadians = 0.90f;
    private const float WallCutawayCaptureDistanceMeters = 14.5f;
    private const float WallCutawayClearViewYawRadians = 1.5707964f;
    private const float HitReactionPresentationSeconds = 0.55f;
    private const string WallCutawayCaptureArgument = "--visual-capture=wall-cutaway";
    private const string WallCutawayCaptureId = "wall-cutaway";
    private const string WallCutawayExpectedOccluderId = "presentation.wall.start.west";
    private const string WallCutawayMoveCommandId = "visual.capture.wall-cutaway.move";
    private const string WallCutawayImageRelativePath = "artifacts/visual/captures/wall-cutaway.png";
    private const string WallCutawayManifestRelativePath = "artifacts/visual/captures/wall-cutaway.json";

    private static readonly Vector3 WallCutawayCaptureFocus = new(-10.0f, 0.0f, 7.0f);

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
    private readonly Dictionary<string, Node3D> _actorViews = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Color> _interactionLabelColors = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ServiceDoorPresentation> _serviceDoors = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StationInteractionDefinition> _interactionDefinitions =
        new(StringComparer.Ordinal);
    private readonly Dictionary<StationInteractionEffect, StationInteractionDefinition>
        _interactionDefinitionsByEffect = [];
    private readonly HashSet<string> _reportedCompletedInteractions = new(StringComparer.Ordinal);
    private readonly HashSet<EntityId> _selectedActorIds = [];
    private readonly Dictionary<string, Button> _partyButtons = new(StringComparer.Ordinal);
    private readonly List<TimedPresentationEffect> _combatPresentationEffects = [];

    private GameSession? _session;
    private StationRouteDefinition? _definition;
    private AutomationBridge? _automationBridge;
    private TacticalCameraController _camera = null!;
    private OmniLight3D _airlockLight = null!;
    private Node3D _protagonistView = null!;
    private Node3D? _airlockLeftDoor;
    private Node3D? _airlockRightDoor;
    private Node3D? _airlockNorthLeaf;
    private Node3D? _airlockSouthLeaf;
    private Node3D? _airlockCenterLock;
    private StandardMaterial3D _serviceDoorLockedMaterial = null!;
    private StandardMaterial3D _serviceDoorOpenMaterial = null!;
    private VanguardPresentation _vanguardPresentation = null!;
    private HumanoidPresentation _survivorPresentation = null!;
    private HumanoidPresentation _protectorPartyPresentation = null!;
    private HumanoidPresentation _protectorWaitingPresentation = null!;
    private Node3D _securityEnforcerView = null!;
    private HumanoidPresentation _securityEnforcerPresentation = null!;
    private CollisionObject3D _securityEnforcerTarget = null!;
    private MeshInstance3D _securityEnforcerThreatRing = null!;
    private MeshInstance3D _destinationMarker = null!;
    private MeshInstance3D _abilityTargetPreview = null!;
    private Label _objectiveLabel = null!;
    private Label _pauseLabel = null!;
    private Label _actionLabel = null!;
    private Label _feedbackLabel = null!;
    private Label _combatLabel = null!;
    private Button _retryButton = null!;
    private VBoxContainer _partyList = null!;
    private CenterContainer _dialogueOverlay = null!;
    private Label _dialogueSpeaker = null!;
    private Label _dialogueLine = null!;
    private VBoxContainer _dialogueResponses = null!;
    private CenterContainer _completionOverlay = null!;
    private string[] _developmentArguments = [];
    private string? _visibleDialogueInteractionId;
    private string? _visibleDialogueResponseSignature;
    private string? _hoveredInteractionId;
    private bool _abilityTargeting;
    private long _humanCommandSequence;
    private int _navigationInitializationFrames;
    private double _autoQuitSeconds;
    private bool _visualCaptureRequested;
    private bool? _airlockOpenState;
    private string? _environmentInitializationError;
    private long _presentationEventSequence;
    private float _vanguardHitReactionSeconds;
    private float _enforcerHitReactionSeconds;

    public override void _EnterTree()
    {
        try
        {
            ConfigureProductionEnvironment();
        }
        catch (Exception exception)
        {
            _environmentInitializationError =
                $"Production environment initialization failed: {exception.Message}";
            GD.PushError($"{_environmentInitializationError}\n{exception}");
        }
    }

    public override void _Ready()
    {
        _developmentArguments = OS.GetCmdlineUserArgs();
        _visualCaptureRequested = _developmentArguments.Any(argument =>
            argument.StartsWith("--visual-capture=", StringComparison.Ordinal));
        _camera = GetNode<TacticalCameraController>("TacticalCamera");
        _camera.InputEnabled = !_visualCaptureRequested;
        _airlockLight = GetNode<OmniLight3D>("AirlockLight");
        _protagonistView = GetNode<Node3D>("Actors/Protagonist");
        _camera.FocusOn(_protagonistView.GlobalPosition);
        _vanguardPresentation = GetNode<VanguardPresentation>(
            "Actors/Protagonist/VanguardPresentation");
        _survivorPresentation = GetNode<HumanoidPresentation>(
            "Interactions/Survivor/SurvivorPresentation");
        _protectorPartyPresentation = GetNode<HumanoidPresentation>(
            "Actors/Protector/ProtectorPresentation");
        _protectorWaitingPresentation = GetNode<HumanoidPresentation>(
            "Interactions/Protector/ProtectorPresentation");
        _securityEnforcerView = GetNode<Node3D>("Hostiles/SecurityEnforcer");
        _securityEnforcerPresentation = GetNode<HumanoidPresentation>(
            "Hostiles/SecurityEnforcer/Presentation");
        _securityEnforcerTarget = GetNode<CollisionObject3D>(
            "Hostiles/SecurityEnforcer/TargetBody");
        _securityEnforcerThreatRing = GetNode<MeshInstance3D>(
            "Hostiles/SecurityEnforcer/ThreatRing");
        foreach (var actorView in GetNode<Node3D>("Actors").GetChildren().OfType<Node3D>())
        {
            _actorViews.Add(GetStableId(actorView), actorView);
        }
        CreateDestinationMarker();
        CreateAbilityTargetPreview();
        CreateHud();
        try
        {
            CacheInteractionViews();
            CacheServiceDoorPresentationNodes();
            CacheAirlockPresentationNodes();
        }
        catch (Exception exception)
        {
            _environmentInitializationError ??=
                $"Station presentation initialization failed: {exception.Message}";
            GD.PushError($"{_environmentInitializationError}\n{exception}");
        }
        if (_environmentInitializationError is not null)
        {
            InitializationFailed(_environmentInitializationError);
            return;
        }
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
        AdvanceCombatPresentationClock((float)delta, observation.Paused);
        ProcessCombatPresentationEvents(observation);
        UpdateHoveredInteraction(observation);
        RenderObservation(observation);
        UpdateAbilityTargetPreview(observation);
        AdvanceServiceDoorPresentation((float)delta);

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
            if (IsKey(key, Key.Escape) && _abilityTargeting)
            {
                _abilityTargeting = false;
                _abilityTargetPreview.Visible = false;
                SetFeedback("Suppressive Fire targeting cancelled.", new Color("9eb6ce"));
                GetViewport().SetInputAsHandled();
                return;
            }

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
                if (ChooseVisibleDialogueResponse(0))
                {
                    GetViewport().SetInputAsHandled();
                    return;
                }
                BeginAbilityTargeting();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (IsKey(key, Key.Key2))
            {
                if (ChooseVisibleDialogueResponse(1))
                {
                    GetViewport().SetInputAsHandled();
                    return;
                }
                UseFieldAid();
                GetViewport().SetInputAsHandled();
                return;
            }

        }

        if (@event is InputEventMouseButton
            {
                Pressed: true,
                ButtonIndex: MouseButton.Left,
            } abilityClick
            && _abilityTargeting)
        {
            ConfirmAbilityTarget(abilityClick.Position);
            GetViewport().SetInputAsHandled();
            return;
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
        _interactionDefinitions.Clear();
        _interactionDefinitionsByEffect.Clear();
        foreach (var interaction in _definition.Interactions)
        {
            _interactionDefinitions.Add(interaction.Id.Value, interaction);
            _interactionDefinitionsByEffect.Add(interaction.Effect, interaction);
        }
        ValidateServiceDoorContentBindings(_definition);
        var layout = CreateLayout(_definition);
        _session = GameSession.CreateStationRoute(
            _definition,
            layout,
            new GodotSpatialPathfinder(navigationMap));

        var vanguardKit = _definition.ProtagonistKits.SingleOrDefault(kit =>
            string.Equals(kit.Id.Value, VanguardKitId, StringComparison.Ordinal))
            ?? throw new InvalidDataException(
                $"The station route must define the default Vanguard kit '{VanguardKitId}'.");
        var kitAcknowledgement = _session.Execute(new ChooseProtagonistKitCommand(
            new CommandId("godot.route.default-vanguard"),
            vanguardKit.Id));
        if (!kitAcknowledgement.Accepted)
        {
            throw new InvalidOperationException(
                $"Default Vanguard activation was rejected: {kitAcknowledgement.RejectionCode}.");
        }

        _automationBridge = new AutomationBridge { Name = "AutomationBridge" };
        _automationBridge.Initialize(_session, ProjectStableIdToScreen);
        AddChild(_automationBridge);

        var initialObservation = _session.Observe();
        _presentationEventSequence = initialObservation.LatestEventSequence;
        RenderObservation(initialObservation);
        SetFeedback("Vanguard deployed. The station route is active.", new Color("8fe6ff"));
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

        if (!markers.TryGetValue(definition.Companion.Id.Value, out var companionMarker))
        {
            throw new InvalidDataException(
                $"Scene marker for companion '{definition.Companion.Id}' is missing.");
        }

        var triggerMarker = GetNode<Marker3D>("Markers/SoloEncounterTrigger");
        var restartMarker = GetNode<Marker3D>("Markers/SoloEncounterRestart");
        var hostileMarker = GetNode<Marker3D>("Markers/SecurityEnforcerSpawn");
        ValidateStableId(triggerMarker, definition.Combat.Encounter.Id.Value);
        ValidateStableId(hostileMarker, definition.Combat.Hostile.Id.Value);
        var triggerRadius = triggerMarker.GetMeta("trigger_radius_meters").AsDouble();

        return new StationRouteLayout(
            ToCore(WithGroundHeight(startMarker.GlobalPosition)),
            [new StationActorPlacement(
                definition.Companion.Id,
                ToCore(WithGroundHeight(companionMarker.GlobalPosition)))],
            placements,
            new StationEncounterPlacement(
                definition.Combat.Encounter.Id,
                ToCore(WithGroundHeight(triggerMarker.GlobalPosition)),
                triggerRadius,
                ToCore(WithGroundHeight(restartMarker.GlobalPosition)),
                ToCore(WithGroundHeight(hostileMarker.GlobalPosition))));
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

    private void CacheServiceDoorPresentationNodes()
    {
        _serviceDoorLockedMaterial = CreateServiceDoorStatusMaterial(new Color("f58f29"));
        _serviceDoorOpenMaterial = CreateServiceDoorStatusMaterial(new Color("19bde8"));
        CacheServiceDoorPresentation(
            "interaction.service_door.entry",
            "NavigationLinks/EntryServiceDoor");
        CacheServiceDoorPresentation(
            "interaction.service_door.solo_exit",
            "NavigationLinks/SoloExitServiceDoor");
    }

    private void CacheServiceDoorPresentation(string interactionId, string navigationLinkPath)
    {
        if (!_interactionViews.TryGetValue(interactionId, out var view))
        {
            throw new InvalidDataException($"Service-door interaction view '{interactionId}' is missing.");
        }

        var productionAsset = view.GetNodeOrNull<Node3D>("ProductionAsset")
            ?? throw new InvalidDataException(
                $"Service-door interaction '{interactionId}' has no production asset.");
        var left = productionAsset.FindChild("Door_Left", recursive: true, owned: false) as Node3D
            ?? throw new InvalidDataException(
                $"Service-door interaction '{interactionId}' is missing Door_Left.");
        var right = productionAsset.FindChild("Door_Right", recursive: true, owned: false) as Node3D
            ?? throw new InvalidDataException(
                $"Service-door interaction '{interactionId}' is missing Door_Right.");
        var status = productionAsset.FindChild("Status_Strip", recursive: true, owned: false)
            as GeometryInstance3D
            ?? throw new InvalidDataException(
                $"Service-door interaction '{interactionId}' is missing Status_Strip.");
        var blocker = view.GetNodeOrNull<CollisionShape3D>("DoorBlocker/CollisionShape3D")
            ?? throw new InvalidDataException(
                $"Service-door interaction '{interactionId}' is missing its collision blocker.");
        var navigationLink = GetNodeOrNull<NavigationLink3D>(navigationLinkPath)
            ?? throw new InvalidDataException(
                $"Service-door interaction '{interactionId}' is missing navigation link '{navigationLinkPath}'.");

        var presentation = new ServiceDoorPresentation(
            left,
            right,
            status,
            blocker,
            navigationLink,
            left.Position,
            right.Position);
        if (!_serviceDoors.TryAdd(interactionId, presentation))
        {
            throw new InvalidDataException($"Service-door presentation '{interactionId}' is duplicated.");
        }
    }

    private void ValidateServiceDoorContentBindings(StationRouteDefinition definition)
    {
        var missingPresentationIds = definition.Interactions
            .Where(interaction => interaction.Effect is
                StationInteractionEffect.OpenEntryServiceDoor
                or StationInteractionEffect.OpenSoloExitServiceDoor)
            .Select(interaction => interaction.Id.Value)
            .Where(interactionId => !_serviceDoors.ContainsKey(interactionId))
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (missingPresentationIds.Length > 0)
        {
            throw new InvalidDataException(
                "Station route content has no cached service-door presentation for: "
                + string.Join(", ", missingPresentationIds));
        }
    }

    private static StandardMaterial3D CreateServiceDoorStatusMaterial(Color color)
    {
        return new StandardMaterial3D
        {
            AlbedoColor = color,
            EmissionEnabled = true,
            Emission = color,
            EmissionEnergyMultiplier = 4.0f,
            Metallic = 0.10f,
            Roughness = 0.28f,
        };
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

    private void CreateAbilityTargetPreview()
    {
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.12f, 0.78f, 1.0f, 0.38f),
            EmissionEnabled = true,
            Emission = new Color("1cb8ea"),
            EmissionEnergyMultiplier = 2.2f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        _abilityTargetPreview = new MeshInstance3D
        {
            Name = "SuppressiveFirePreview",
            Mesh = new CylinderMesh
            {
                TopRadius = 2.0f,
                BottomRadius = 2.0f,
                Height = 0.025f,
                RadialSegments = 48,
            },
            MaterialOverride = material,
            Visible = false,
        };
        AddChild(_abilityTargetPreview);
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
            CustomMinimumSize = new Vector2(575, 238),
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

        _combatLabel = new Label
        {
            Position = new Vector2(0, 176),
            CustomMinimumSize = new Vector2(535, 38),
        };
        _combatLabel.AddThemeFontSizeOverride("font_size", 17);
        statusContent.AddChild(_combatLabel);

        _retryButton = new Button
        {
            Position = new Vector2(405, 198),
            CustomMinimumSize = new Vector2(130, 34),
            Text = "RETRY FIGHT",
            Visible = false,
            FocusMode = Control.FocusModeEnum.None,
        };
        _retryButton.Pressed += RestartEncounter;
        statusContent.AddChild(_retryButton);

        var controlsPanel = new PanelContainer
        {
            Position = new Vector2(22, 575),
            CustomMinimumSize = new Vector2(720, 0),
            ZIndex = 10,
        };
        canvas.AddChild(controlsPanel);
        var controls = new Label
        {
            Text = "Party cards: select crew   Right-click: move / interact / attack   Space: pause\n"
                + "1: target Suppressive Fire   2: use Field Aid   Esc: cancel targeting\n"
                + "WASD: pan   Q/E or middle-drag: yaw   PgUp/PgDn: pitch   Wheel: zoom   Home/R: reset   F: focus",
            Modulate = new Color("b9cce0"),
        };
        controlsPanel.AddChild(controls);

        var partyPanel = new PanelContainer
        {
            Position = new Vector2(940, 22),
            CustomMinimumSize = new Vector2(315, 0),
            ZIndex = 10,
        };
        canvas.AddChild(partyPanel);
        var partyContent = new VBoxContainer();
        partyContent.AddThemeConstantOverride("separation", 8);
        partyPanel.AddChild(partyContent);
        var partyTitle = new Label { Text = "ACTIVE PARTY" };
        partyTitle.AddThemeFontSizeOverride("font_size", 20);
        partyContent.AddChild(partyTitle);
        _partyList = new VBoxContainer();
        _partyList.AddThemeConstantOverride("separation", 6);
        partyContent.AddChild(_partyList);

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
        _dialogueResponses = new VBoxContainer();
        _dialogueResponses.AddThemeConstantOverride("separation", 8);
        dialogueContent.AddChild(_dialogueResponses);

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

    private void RefreshPartyUi(StationRouteObservation route)
    {
        _selectedActorIds.RemoveWhere(actorId => route.Party.All(actor => actor.Id != actorId));
        foreach (var actor in route.Party)
        {
            if (_partyButtons.ContainsKey(actor.Id.Value))
            {
                continue;
            }

            var actorId = actor.Id;
            var button = new Button
            {
                FocusMode = Control.FocusModeEnum.None,
                CustomMinimumSize = new Vector2(285, 64),
            };
            button.Pressed += () => ToggleActorSelection(actorId);
            _partyButtons.Add(actor.Id.Value, button);
            _partyList.AddChild(button);
            if (route.Phase == ScenarioPhase.InProgress)
            {
                _selectedActorIds.Add(actor.Id);
            }
        }

        foreach (var obsoleteId in _partyButtons.Keys
            .Where(actorId => route.Party.All(actor => actor.Id.Value != actorId))
            .ToArray())
        {
            _partyButtons[obsoleteId].QueueFree();
            _partyButtons.Remove(obsoleteId);
        }

        if (route.Phase == ScenarioPhase.InProgress && _selectedActorIds.Count == 0)
        {
            _selectedActorIds.Add(route.Protagonist.Id);
        }

        foreach (var actor in route.Party)
        {
            var selected = _selectedActorIds.Contains(actor.Id);
            var loadout = actor.Loadout is null
                ? "Kit not selected"
                : $"{actor.Loadout.WeaponName}  •  {actor.Loadout.ActiveAbilityName}";
            var combat = actor.Combat is null
                ? string.Empty
                : $"\nHP {actor.Combat.Health}/{actor.Combat.MaximumHealth}";
            _partyButtons[actor.Id.Value].Text = $"{(selected ? "●" : "○")} {actor.DisplayName}\n{loadout}{combat}";
            _partyButtons[actor.Id.Value].Modulate = selected
                ? Colors.White
                : new Color("8090a0");
        }
    }

    private void ToggleActorSelection(EntityId actorId)
    {
        if (!_selectedActorIds.Remove(actorId))
        {
            _selectedActorIds.Add(actorId);
        }

        if (_selectedActorIds.Count == 0)
        {
            _selectedActorIds.Add(actorId);
        }

        RenderObservation(_session!.Observe());
    }

    private void RenderObservation(GameObservation observation)
    {
        if (observation.StationRoute is not StationRouteObservation route)
        {
            return;
        }
        var definition = _definition
            ?? throw new InvalidOperationException("The station route definition is unavailable.");

        RefreshPartyUi(route);
        foreach (var actorView in _actorViews)
        {
            var actor = route.Party.SingleOrDefault(candidate => candidate.Id.Value == actorView.Key);
            actorView.Value.Visible = actor is not null;
            if (actor is not null)
            {
                actorView.Value.GlobalPosition = ToGodot(actor.Position);
                if (actorView.Value.GetNodeOrNull<Node3D>("SelectionBeacon") is Node3D beacon)
                {
                    beacon.Visible = _selectedActorIds.Contains(actor.Id);
                }
            }
        }

        SynchronizeProtagonistPresentation(observation, route);
        SynchronizeProductionHumanoids(observation, route, definition);
        SynchronizeCombatPresentation(observation, route);

        var selectedActors = route.Party
            .Where(actor => _selectedActorIds.Contains(actor.Id))
            .ToArray();
        if (selectedActors.Length == 0)
        {
            selectedActors = [route.Protagonist];
        }

        _camera.FollowTarget = new Vector3(
            selectedActors.Average(actor => (float)actor.Position.X),
            selectedActors.Average(actor => (float)actor.Position.Y),
            selectedActors.Average(actor => (float)actor.Position.Z));

        var actionActor = selectedActors.FirstOrDefault(actor =>
            actor.PendingAction is not null || actor.CurrentAction is not null);
        var visibleAction = actionActor?.PendingAction ?? actionActor?.CurrentAction;
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

        if (route.Encounter is EncounterObservation encounter)
        {
            var protagonistCombat = route.Protagonist.Combat!;
            var hostile = route.Hostiles!.Single();
            var cooldown = protagonistCombat.Cooldowns.Single().RemainingTicks;
            var itemCharges = protagonistCombat.Items.Single().Charges;
            _combatLabel.Text = encounter.Phase == EncounterPhase.Dormant
                ? "COMBAT — dormant beyond the entry door"
                : $"COMBAT {encounter.Phase.ToString().ToUpperInvariant()}  •  "
                    + $"Vanguard {protagonistCombat.Health}/{protagonistCombat.MaximumHealth} HP  •  "
                    + $"Enforcer {hostile.Combat.Health}/{hostile.Combat.MaximumHealth} HP  •  "
                    + $"Suppressive Fire CD {cooldown}  •  Field Aid ×{itemCharges}";
            _combatLabel.Modulate = encounter.Phase == EncounterPhase.Defeat
                ? new Color("ff6b6b")
                : encounter.Phase is EncounterPhase.Securing or EncounterPhase.Victory
                    ? new Color("72f2a8")
                    : new Color("9edfff");
            _retryButton.Visible = encounter.Phase == EncounterPhase.Defeat;
        }
        else
        {
            _combatLabel.Text = string.Empty;
            _retryButton.Visible = false;
        }

        _actionLabel.Text = visibleAction is null
            ? $"{selectedActors.Length} selected crew member(s) awaiting an order."
            : $"{(actionActor!.PendingAction is null ? "Current" : "Pending")} order — "
                + DescribeAction(route, visibleAction);

        var objectiveTargetId = GetObjectiveTargetId(route.Objective.Id);
        var combatSuppressesInteractionLabels = route.Encounter?.Phase is
            EncounterPhase.Readying
            or EncounterPhase.Active
            or EncounterPhase.Securing
            or EncounterPhase.Defeat;

        foreach (var interaction in route.Interactions)
        {
            if (!_interactionViews.TryGetValue(interaction.Id.Value, out var view))
            {
                continue;
            }

            var interactionDefinition = _interactionDefinitions[interaction.Id.Value];
            var isServiceDoor = interactionDefinition.Effect is
                StationInteractionEffect.OpenEntryServiceDoor
                or StationInteractionEffect.OpenSoloExitServiceDoor;
            var isRecruitedProtector = interactionDefinition.Effect
                    == StationInteractionEffect.BeginRecruitmentDialogue
                && route.Party.Any(actor => actor.Id == definition.Companion.Id);
            view.Visible = !isRecruitedProtector;
            if (view is CollisionObject3D collisionObject)
            {
                collisionObject.CollisionLayer = isRecruitedProtector ? 0u : InteractionCollisionLayer;
            }
            if (isRecruitedProtector)
            {
                continue;
            }

            if (view.GetNodeOrNull<Label3D>("Label") is Label3D label)
            {
                label.Visible = !combatSuppressesInteractionLabels;
                var labelText = interaction.State switch
                {
                    InteractionState.Unavailable => $"{interaction.Prompt.ToUpperInvariant()}  [LOCKED]",
                    InteractionState.DialogueActive => $"{interaction.Prompt.ToUpperInvariant()}  [TALKING]",
                    InteractionState.Completed when isServiceDoor
                        => $"{interaction.Prompt.ToUpperInvariant()}  [OPEN]",
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
                    && (interaction.State is InteractionState.Available or InteractionState.DialogueActive
                        || (route.Objective.Id == definition.CombatThresholdObjective.Id
                            && interactionDefinition.Effect
                                == StationInteractionEffect.OpenSoloExitServiceDoor));
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

            if (!isServiceDoor)
            {
                var unavailableTransparency = interaction.State == InteractionState.Unavailable ? 0.58f : 0.0f;
                SetInteractionTransparency(view, unavailableTransparency);
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
                SetFeedback(
                    interaction.ResultText,
                    isServiceDoor ? new Color("8fe6ff") : new Color("d1b5ff"));
            }
        }

        if (route.ActiveDialogue is DialogueObservation dialogue)
        {
            _dialogueOverlay.Visible = true;
            _dialogueSpeaker.Text = dialogue.Speaker.ToUpperInvariant();
            _dialogueLine.Text = dialogue.Line;
            var responseSignature = string.Join(
                '\u001f',
                dialogue.Responses.Select(response => response.Id.Value));
            if (!string.Equals(
                    _visibleDialogueInteractionId,
                    dialogue.InteractionId.Value,
                    StringComparison.Ordinal)
                || !string.Equals(
                    _visibleDialogueResponseSignature,
                    responseSignature,
                    StringComparison.Ordinal))
            {
                foreach (var child in _dialogueResponses.GetChildren())
                {
                    _dialogueResponses.RemoveChild(child);
                    child.QueueFree();
                }

                for (var index = 0; index < dialogue.Responses.Count; index++)
                {
                    var responseIndex = index;
                    var response = dialogue.Responses[index];
                    var button = new Button
                    {
                        Text = $"{index + 1} — {response.Text}",
                        FocusMode = Control.FocusModeEnum.None,
                    };
                    button.Pressed += () => _ = ChooseVisibleDialogueResponse(responseIndex);
                    _dialogueResponses.AddChild(button);
                }
            }
            _visibleDialogueInteractionId = dialogue.InteractionId.Value;
            _visibleDialogueResponseSignature = responseSignature;
        }
        else
        {
            _dialogueOverlay.Visible = false;
            _visibleDialogueInteractionId = null;
            _visibleDialogueResponseSignature = null;
        }

        _completionOverlay.Visible = route.Phase == ScenarioPhase.Completed;
        SynchronizeServiceDoorAuthority(route);
        SetAirlockOpen(route.Phase == ScenarioPhase.Completed);
    }

    private void SynchronizeCombatPresentation(
        GameObservation observation,
        StationRouteObservation route)
    {
        var encounter = route.Encounter;
        var hostile = route.Hostiles?.SingleOrDefault();
        var active = encounter is not null
            && hostile is not null
            && encounter.Phase != EncounterPhase.Dormant;
        _securityEnforcerView.Visible = active;
        _securityEnforcerTarget.CollisionLayer = active && hostile!.Combat.Health > 0
            ? HostileCollisionLayer
            : 0u;
        if (!active)
        {
            return;
        }

        _securityEnforcerView.GlobalPosition = ToGodot(hostile!.Position);
        var action = hostile.CurrentAction;
        var direction = action is null
            ? ToGodot(route.Protagonist.Position) - ToGodot(hostile.Position)
            : ToGodot(action.Destination) - ToGodot(hostile.Position);
        var presentationAction = hostile.Combat.IsDefeated
            ? HumanoidPresentationAction.Down
            : action is
                {
                    Kind: PrimaryActionKind.Attack,
                    Phase: PrimaryActionPhase.Windup,
                }
                    ? HumanoidPresentationAction.MeleeStrike
                    : _enforcerHitReactionSeconds > 0.0f
                        ? HumanoidPresentationAction.HitReaction
                        : action?.HasRemainingMovement == true
                            ? HumanoidPresentationAction.Locomotion
                            : HumanoidPresentationAction.Idle;
        _securityEnforcerPresentation.Synchronize(
            true,
            presentationAction,
            observation.Paused,
            direction,
            playbackSpeed: presentationAction == HumanoidPresentationAction.MeleeStrike
                ? 1.42f
                : 1.0f,
            seekToEndWhenPaused: hostile.Combat.IsDefeated && observation.Paused);
        if (action?.HasRemainingMovement != true)
        {
            _securityEnforcerPresentation.FaceDirection(direction);
        }

        _securityEnforcerThreatRing.Visible = action is
        {
            Kind: PrimaryActionKind.Attack,
            Phase: PrimaryActionPhase.Windup,
        };
        if (_securityEnforcerView.GetNodeOrNull<Label3D>("Label") is Label3D label)
        {
            label.Text = hostile.Combat.IsDefeated
                ? "SECURITY ENFORCER  [DOWN]"
                : $"SECURITY ENFORCER  {hostile.Combat.Health}/{hostile.Combat.MaximumHealth}";
        }
    }

    private void ProcessCombatPresentationEvents(GameObservation observation)
    {
        if (_session is null || observation.StationRoute is not StationRouteObservation route)
        {
            return;
        }

        foreach (var gameEvent in _session.EventsSince(_presentationEventSequence))
        {
            switch (gameEvent.Detail)
            {
                case AttackEventDetail attack when attack.Hit:
                    if (TryGetCombatantPosition(route, attack.SourceId, out var source)
                        && TryGetCombatantPosition(route, attack.TargetId, out var target))
                    {
                        if (attack.SourceId == route.Protagonist.Id)
                        {
                            SpawnTracer(
                                source + new Vector3(0.0f, 1.25f, 0.0f),
                                target + new Vector3(0.0f, 1.05f, 0.0f),
                                new Color("57ddff"));
                        }
                    }
                    break;
                case AbilityReleasedEventDetail ability:
                    SpawnSuppressionPulse(
                        ToGodot(ability.TargetPosition) + new Vector3(0.0f, 0.06f, 0.0f));
                    if (TryGetCombatantPosition(route, ability.SourceId, out var abilitySource))
                    {
                        SpawnTracer(
                            abilitySource + new Vector3(0.0f, 1.25f, 0.0f),
                            ToGodot(ability.TargetPosition) + new Vector3(0.0f, 0.65f, 0.0f),
                            new Color("66f5ff"));
                    }
                    break;
                case DamageAppliedEventDetail damage:
                    if (damage.RemainingHealth > 0)
                    {
                        if (damage.TargetId == route.Protagonist.Id)
                        {
                            _vanguardHitReactionSeconds = HitReactionPresentationSeconds;
                        }
                        else if (route.Hostiles?.Any(hostile => hostile.Id == damage.TargetId) == true)
                        {
                            _enforcerHitReactionSeconds = HitReactionPresentationSeconds;
                        }
                    }
                    if (TryGetCombatantPosition(route, damage.TargetId, out var impact))
                    {
                        SpawnImpact(
                            impact + new Vector3(0.0f, 1.05f, 0.0f),
                            damage.TargetId == route.Protagonist.Id
                                ? new Color("ff654f")
                                : new Color("75eeff"));
                    }
                    break;
            }

            _presentationEventSequence = gameEvent.Sequence;
        }
    }

    private void AdvanceCombatPresentationClock(float deltaSeconds, bool paused)
    {
        if (paused)
        {
            return;
        }

        _vanguardHitReactionSeconds = Math.Max(
            0.0f,
            _vanguardHitReactionSeconds - deltaSeconds);
        _enforcerHitReactionSeconds = Math.Max(
            0.0f,
            _enforcerHitReactionSeconds - deltaSeconds);
        for (var index = _combatPresentationEffects.Count - 1; index >= 0; index--)
        {
            var effect = _combatPresentationEffects[index];
            effect.RemainingSeconds -= deltaSeconds;
            if (effect.RemainingSeconds > 0.0f)
            {
                continue;
            }

            effect.Node.QueueFree();
            _combatPresentationEffects.RemoveAt(index);
        }
    }

    private static bool TryGetCombatantPosition(
        StationRouteObservation route,
        EntityId entityId,
        out Vector3 position)
    {
        var actor = route.Party.SingleOrDefault(candidate => candidate.Id == entityId);
        if (actor is not null)
        {
            position = ToGodot(actor.Position);
            return true;
        }

        var hostile = route.Hostiles?.SingleOrDefault(candidate => candidate.Id == entityId);
        if (hostile is not null)
        {
            position = ToGodot(hostile.Position);
            return true;
        }

        position = Vector3.Zero;
        return false;
    }

    private void SpawnTracer(Vector3 origin, Vector3 destination, Color color)
    {
        var mesh = new ImmediateMesh();
        mesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
        mesh.SurfaceSetColor(color);
        mesh.SurfaceAddVertex(origin);
        mesh.SurfaceAddVertex(destination);
        mesh.SurfaceEnd();
        mesh.SurfaceSetMaterial(0, CreateCombatEffectMaterial(color));
        var node = new MeshInstance3D { Mesh = mesh };
        AddChild(node);
        _combatPresentationEffects.Add(new TimedPresentationEffect(node, 0.12f));
        SpawnImpact(origin, color);
    }

    private void SpawnImpact(Vector3 position, Color color)
    {
        var node = new MeshInstance3D
        {
            Position = position,
            Mesh = new SphereMesh
            {
                Radius = 0.09f,
                Height = 0.18f,
                RadialSegments = 12,
                Rings = 6,
                Material = CreateCombatEffectMaterial(color),
            },
        };
        AddChild(node);
        _combatPresentationEffects.Add(new TimedPresentationEffect(node, 0.16f));
    }

    private void SpawnSuppressionPulse(Vector3 position)
    {
        var color = new Color("5cecff");
        var node = new MeshInstance3D
        {
            Position = position,
            Mesh = new TorusMesh
            {
                InnerRadius = 1.86f,
                OuterRadius = 2.0f,
                Rings = 8,
                RingSegments = 40,
                Material = CreateCombatEffectMaterial(color),
            },
        };
        AddChild(node);
        _combatPresentationEffects.Add(new TimedPresentationEffect(node, 0.28f));
    }

    private static StandardMaterial3D CreateCombatEffectMaterial(Color color) => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        VertexColorUseAsAlbedo = true,
        AlbedoColor = color,
        EmissionEnabled = true,
        Emission = color,
        EmissionEnergyMultiplier = 4.0f,
    };

    private void SynchronizeServiceDoorAuthority(StationRouteObservation route)
    {
        foreach (var (interactionId, door) in _serviceDoors)
        {
            var interaction = route.Interactions.Single(candidate =>
                string.Equals(candidate.Id.Value, interactionId, StringComparison.Ordinal));
            var unlocked = interaction.State is InteractionState.Available
                or InteractionState.Completed;
            var open = interaction.State == InteractionState.Completed;
            door.NavigationLink.Enabled = unlocked;
            if (door.Blocker.Disabled != open)
            {
                door.Blocker.SetDeferred(CollisionShape3D.PropertyName.Disabled, open);
            }
            door.StatusStrip.MaterialOverride = open
                ? _serviceDoorOpenMaterial
                : _serviceDoorLockedMaterial;

            if (door.TargetOpen is null)
            {
                door.TargetOpen = open;
                door.Left.Position = open ? door.OpenLeftPosition : door.ClosedLeftPosition;
                door.Right.Position = open ? door.OpenRightPosition : door.ClosedRightPosition;
            }
            else
            {
                door.TargetOpen = open;
            }
        }
    }

    private void AdvanceServiceDoorPresentation(float deltaSeconds)
    {
        var movement = ServiceDoorLeafTravelMeters
            * Math.Max(0.0f, deltaSeconds)
            / ServiceDoorAnimationSeconds;
        foreach (var door in _serviceDoors.Values)
        {
            var open = door.TargetOpen == true;
            door.Left.Position = door.Left.Position.MoveToward(
                open ? door.OpenLeftPosition : door.ClosedLeftPosition,
                movement);
            door.Right.Position = door.Right.Position.MoveToward(
                open ? door.OpenRightPosition : door.ClosedRightPosition,
                movement);
        }
    }

    private void SynchronizeProtagonistPresentation(
        GameObservation observation,
        StationRouteObservation route)
    {
        var useVanguard = string.Equals(
            route.SelectedProtagonistKit?.Id.Value,
            VanguardKitId,
            StringComparison.Ordinal);
        foreach (var geometry in _protagonistView.GetChildren().OfType<GeometryInstance3D>())
        {
            if (!string.Equals(geometry.Name, "SelectionBeacon", StringComparison.Ordinal))
            {
                geometry.Visible = !useVanguard;
            }
        }

        var action = route.Protagonist.CurrentAction;
        var hostile = route.Hostiles?.SingleOrDefault();
        var direction = action?.HasRemainingMovement == true
            ? ToGodot(action.Destination) - ToGodot(route.Protagonist.Position)
            : hostile is not null && route.Encounter?.Phase is
                EncounterPhase.Readying or EncounterPhase.Active or EncounterPhase.Securing
                ? ToGodot(hostile.Position) - ToGodot(route.Protagonist.Position)
                : Vector3.Zero;
        _vanguardPresentation.Synchronize(
            useVanguard,
            action?.HasRemainingMovement == true,
            observation.Paused,
            direction,
            route.Encounter,
            action,
            _vanguardHitReactionSeconds > 0.0f);
    }

    private void SynchronizeProductionHumanoids(
        GameObservation observation,
        StationRouteObservation route,
        StationRouteDefinition definition)
    {
        var survivorSpeaking = route.ActiveDialogue is not null
            && string.Equals(
                route.ActiveDialogue.InteractionId.Value,
                "interaction.survivor",
                StringComparison.Ordinal);
        _survivorPresentation.Synchronize(
            true,
            survivorSpeaking
                ? HumanoidPresentationAction.DialogueSpeak
                : HumanoidPresentationAction.Idle,
            observation.Paused,
            Vector3.Zero);

        var protector = route.Party.SingleOrDefault(actor => actor.Id == definition.Companion.Id);
        var protectorAction = protector?.CurrentAction;
        var protectorDirection = protectorAction is null || protector is null
            ? Vector3.Zero
            : ToGodot(protectorAction.Destination) - ToGodot(protector.Position);
        _protectorPartyPresentation.Synchronize(
            protector is not null,
            protectorAction?.HasRemainingMovement == true
                ? HumanoidPresentationAction.Locomotion
                : HumanoidPresentationAction.Idle,
            observation.Paused,
            protectorDirection);
        _protectorWaitingPresentation.Synchronize(
            protector is null,
            HumanoidPresentationAction.Idle,
            observation.Paused,
            Vector3.Zero);
    }

    private void SetAirlockOpen(bool open)
    {
        if (_airlockOpenState == open)
        {
            return;
        }
        _airlockOpenState = open;

        if (_airlockLeftDoor is not null)
        {
            _airlockLeftDoor.Position = new Vector3(open ? -1.08f : -0.62f, 1.30f, 0.0f);
        }
        if (_airlockRightDoor is not null)
        {
            _airlockRightDoor.Position = new Vector3(open ? 1.08f : 0.62f, 1.30f, 0.0f);
        }

        if (_airlockNorthLeaf is not null)
        {
            _airlockNorthLeaf.Position = new Vector3(1, 1.2f, open ? 1.45f : 0.59f);
        }
        if (_airlockSouthLeaf is not null)
        {
            _airlockSouthLeaf.Position = new Vector3(1, 1.2f, open ? -1.45f : -0.59f);
        }
        if (_airlockCenterLock is not null)
        {
            _airlockCenterLock.Visible = !open;
        }
    }

    private void CacheAirlockPresentationNodes()
    {
        if (!_interactionViews.TryGetValue("interaction.evacuation_airlock", out var airlock))
        {
            GD.PushError("The evacuation-airlock interaction view is missing.");
            return;
        }

        var productionAsset = airlock.GetNodeOrNull<Node3D>("ProductionAsset");
        _airlockLeftDoor = productionAsset?.FindChild(
            "Door_Left",
            recursive: true,
            owned: false) as Node3D;
        _airlockRightDoor = productionAsset?.FindChild(
            "Door_Right",
            recursive: true,
            owned: false) as Node3D;
        if (_airlockLeftDoor is null || _airlockRightDoor is null)
        {
            GD.PushError("The production airlock is missing Door_Left or Door_Right.");
        }

        _airlockNorthLeaf = airlock.GetNodeOrNull<Node3D>("DoorNorthLeaf");
        _airlockSouthLeaf = airlock.GetNodeOrNull<Node3D>("DoorSouthLeaf");
        _airlockCenterLock = airlock.GetNodeOrNull<Node3D>("CenterLock");
    }

    private static void SetInteractionTransparency(Node3D view, float transparency)
    {
        foreach (var child in view.GetChildren().OfType<Node3D>())
        {
            if (child is GeometryInstance3D geometry and not Label3D)
            {
                geometry.Transparency = transparency;
            }

            SetInteractionTransparency(child, transparency);
        }
    }

    private void ConfigureProductionEnvironment()
    {
        var structure = GetNodeOrNull<Node3D>("Environment/ProductionStructure");
        if (structure is null)
        {
            throw new InvalidDataException("The production station structure node is missing.");
        }

        var occluderIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var importedNode in EnumerateDescendants(structure))
        {
            var stableId = ReadImportedOccluderId(importedNode);
            if (stableId is null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(stableId)
                || !stableId.StartsWith("presentation.wall.", StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Production wall '{importedNode.GetPath()}' has invalid occluder_id '{stableId}'.");
            }
            if (!occluderIds.Add(stableId))
            {
                throw new InvalidDataException(
                    $"Production station structure duplicates occluder_id '{stableId}'.");
            }

            var geometryCandidates = importedNode is GeometryInstance3D geometry
                ? new[] { geometry }
                : EnumerateDescendants(importedNode).OfType<GeometryInstance3D>().ToArray();
            if (geometryCandidates.Length != 1)
            {
                throw new InvalidDataException(
                    $"Production wall '{importedNode.GetPath()}' has "
                    + $"{geometryCandidates.Length} geometry candidates; expected exactly one.");
            }

            var wall = geometryCandidates[0];
            wall.SetMeta("occluder_id", stableId);
            wall.AddToGroup("camera_occluder");
        }

        if (occluderIds.Count == 0)
        {
            throw new InvalidDataException(
                "Production station structure contains no recursively discoverable wall occluders.");
        }
    }

    private static string? ReadImportedOccluderId(Node node)
    {
        if (node.HasMeta("occluder_id"))
        {
            return node.GetMeta("occluder_id").AsString();
        }

        if (!node.HasMeta("extras"))
        {
            return null;
        }

        var extras = node.GetMeta("extras");
        if (extras.VariantType != Variant.Type.Dictionary)
        {
            return null;
        }

        var dictionary = extras.AsGodotDictionary();
        return dictionary.TryGetValue("occluder_id", out var occluderId)
            ? occluderId.AsString()
            : null;
    }

    private static IEnumerable<Node> EnumerateDescendants(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            yield return child;
            foreach (var descendant in EnumerateDescendants(child))
            {
                yield return descendant;
            }
        }
    }

    private void HandleContextClick(Vector2 screenPosition)
    {
        var route = _session!.Observe().StationRoute!;
        if (route.Phase == ScenarioPhase.Completed)
        {
            SetFeedback("The route is already complete.", new Color("72f2a8"));
            return;
        }
        if (route.Phase == ScenarioPhase.AwaitingProtagonistSelection)
        {
            SetFeedback("Choose a protagonist kit before issuing orders.", new Color("ffb36b"));
            return;
        }
        if (route.ActiveDialogue is not null)
        {
            SetFeedback("Choose the dialogue response before issuing another order.", new Color("ffb36b"));
            return;
        }

        var rayOrigin = _camera.ProjectRayOrigin(screenPosition);
        var rayEnd = rayOrigin + (_camera.ProjectRayNormal(screenPosition) * 200.0f);
        var hostileHit = CastRay(rayOrigin, rayEnd, HostileCollisionLayer);
        if (hostileHit.Count > 0
            && hostileHit["collider"].AsGodotObject() is Node hostileCollider
            && hostileCollider.HasMeta("stable_id"))
        {
            Dispatch(new AssignBasicAttackTargetCommand(
                NextHumanCommandId("attack"),
                route.Protagonist.Id,
                new EntityId(hostileCollider.GetMeta("stable_id").AsString())));
            return;
        }

        var interactionHit = CastRay(rayOrigin, rayEnd, InteractionCollisionLayer);
        var selectedActors = route.Party
            .Where(actor => _selectedActorIds.Contains(actor.Id))
            .ToArray();
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
        Dispatch(new MovePartyCommand(
            NextHumanCommandId("move-party"),
            selectedActors.Length == 0
                ? [route.Protagonist.Id]
                : selectedActors.Select(actor => actor.Id),
            ToCore(WithGroundHeight(hitPosition))));
    }

    private void BeginAbilityTargeting()
    {
        var route = _session!.Observe().StationRoute!;
        if (route.Encounter?.Phase is not (EncounterPhase.Readying or EncounterPhase.Active))
        {
            SetFeedback("Suppressive Fire is available only during the encounter.", new Color("ff8b8b"));
            return;
        }

        _abilityTargeting = true;
        SetFeedback("Suppressive Fire: left-click a point inside the cyan radius. Esc cancels.", new Color("8fe6ff"));
    }

    private void ConfirmAbilityTarget(Vector2 screenPosition)
    {
        var rayOrigin = _camera.ProjectRayOrigin(screenPosition);
        var rayEnd = rayOrigin + (_camera.ProjectRayNormal(screenPosition) * 200.0f);
        var floorHit = CastRay(rayOrigin, rayEnd, FloorCollisionLayer);
        if (floorHit.Count == 0)
        {
            SetFeedback("Suppressive Fire needs a station-floor target.", new Color("ff8b8b"));
            return;
        }

        var route = _session!.Observe().StationRoute!;
        var target = ToCore(WithGroundHeight(floorHit["position"].AsVector3()));
        _abilityTargeting = false;
        _abilityTargetPreview.Visible = false;
        Dispatch(new UseAbilityCommand(
            NextHumanCommandId("suppressive-fire"),
            route.Protagonist.Id,
            _definition!.Combat.ProtagonistAbility.Id,
            new PositionAbilityTarget(target)));
    }

    private void UseFieldAid()
    {
        var route = _session!.Observe().StationRoute!;
        Dispatch(new UseItemCommand(
            NextHumanCommandId("field-aid"),
            route.Protagonist.Id,
            _definition!.Combat.HealingItem.Id,
            route.Protagonist.Id));
    }

    private void RestartEncounter()
    {
        _abilityTargeting = false;
        _abilityTargetPreview.Visible = false;
        Dispatch(new RestartEncounterCommand(
            NextHumanCommandId("restart-encounter"),
            _definition!.Combat.Encounter.Id));
    }

    private void UpdateAbilityTargetPreview(GameObservation observation)
    {
        if (!_abilityTargeting
            || observation.StationRoute is not StationRouteObservation route
            || route.Encounter?.Phase is not (EncounterPhase.Readying or EncounterPhase.Active))
        {
            _abilityTargetPreview.Visible = false;
            return;
        }

        var mouse = GetViewport().GetMousePosition();
        var rayOrigin = _camera.ProjectRayOrigin(mouse);
        var rayEnd = rayOrigin + (_camera.ProjectRayNormal(mouse) * 200.0f);
        var floorHit = CastRay(rayOrigin, rayEnd, FloorCollisionLayer);
        if (floorHit.Count == 0)
        {
            _abilityTargetPreview.Visible = false;
            return;
        }

        var hit = WithGroundHeight(floorHit["position"].AsVector3());
        _abilityTargetPreview.GlobalPosition = hit + new Vector3(0, 0.035f, 0);
        var inRange = ToCore(hit).DistanceTo(route.Protagonist.Position)
            <= _definition!.Combat.ProtagonistAbility.RangeMeters;
        _abilityTargetPreview.Visible = inRange;
    }

    private Godot.Collections.Dictionary CastRay(Vector3 origin, Vector3 destination, uint collisionMask)
    {
        var query = PhysicsRayQueryParameters3D.Create(origin, destination, collisionMask);
        return GetWorld3D().DirectSpaceState.IntersectRay(query);
    }

    private bool ChooseVisibleDialogueResponse(int responseIndex)
    {
        var route = _session!.Observe().StationRoute!;
        if (route.ActiveDialogue is not DialogueObservation dialogue
            || responseIndex < 0
            || responseIndex >= dialogue.Responses.Count)
        {
            return false;
        }

        Dispatch(new ChooseDialogueResponseCommand(
            NextHumanCommandId("dialogue"),
            dialogue.ActorId,
            dialogue.InteractionId,
            dialogue.Responses[responseIndex].Id));
        return true;
    }

    private void Dispatch(IGameCommand command)
    {
        var acknowledgement = _session!.Execute(command);
        if (acknowledgement.Accepted)
        {
            var message = command switch
            {
                MoveActorCommand => "Move order accepted.",
                MovePartyCommand moveParty => $"Move order accepted for {moveParty.ActorIds.Count} crew member(s).",
                InteractCommand interact => $"Order accepted — {GetInteractionPrompt(interact.TargetId)}.",
                ChooseDialogueResponseCommand => "Dialogue choice recorded.",
                ChooseProtagonistKitCommand => "Protagonist kit locked. The station route is active.",
                SetPauseCommand pause => pause.Paused ? "Tactical pause engaged." : "Simulation resumed.",
                AssignBasicAttackTargetCommand => "Repeating carbine attack assigned.",
                UseAbilityCommand => "Suppressive Fire queued.",
                UseItemCommand => "Field Aid queued.",
                RestartEncounterCommand => "Encounter reset. Review orders, then resume.",
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

        return action.Kind switch
        {
            PrimaryActionKind.Attack => "Engage the Security Enforcer with repeating carbine fire",
            PrimaryActionKind.Ability => "Suppressive Fire at the selected position",
            PrimaryActionKind.Item => "Use Field Aid",
            _ => "Move to the selected destination",
        };
    }

    private string GetInteractionPrompt(EntityId targetId)
    {
        return _interactionDefinitions[targetId.Value].Prompt;
    }

    private string? GetObjectiveTargetId(ObjectiveId objectiveId)
    {
        StationInteractionEffect? targetEffect = objectiveId == _definition!.BriefingObjective.Id
            ? StationInteractionEffect.BeginSurvivorDialogue
            : objectiveId == _definition.EntryDoorObjective.Id
                ? StationInteractionEffect.OpenEntryServiceDoor
                : objectiveId == _definition.SoloExitDoorObjective.Id
                    ? StationInteractionEffect.OpenSoloExitServiceDoor
                    : objectiveId == _definition.RecruitmentObjective.Id
                        ? StationInteractionEffect.BeginRecruitmentDialogue
                        : objectiveId == _definition.DestinationObjective.Id
                            ? StationInteractionEffect.CompleteScenario
                            : null;
        if (targetEffect is null)
        {
            return null;
        }
        return _interactionDefinitionsByEffect[targetEffect.Value].Id.Value;
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
        var partyActor = route.Party.SingleOrDefault(actor =>
            string.Equals(stableId, actor.Id.Value, StringComparison.Ordinal));
        if (partyActor is not null)
        {
            worldPosition = ToGodot(partyActor.Position) + new Vector3(0, 1.0f, 0);
        }
        else if (_interactionViews.TryGetValue(stableId, out var interactionView))
        {
            worldPosition = interactionView.GetNodeOrNull<CollisionShape3D>("CollisionShape3D")
                is CollisionShape3D collision
                    ? collision.GlobalPosition
                    : interactionView.GlobalPosition + new Vector3(0, 0.8f, 0);
        }
        else if (route.Hostiles?.SingleOrDefault(hostile =>
            string.Equals(hostile.Id.Value, stableId, StringComparison.Ordinal)) is HostileObservation hostile)
        {
            worldPosition = ToGodot(hostile.Position) + new Vector3(0, 1.0f, 0);
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
                _ = RunStationRouteSmokeWithFailureHandlingAsync();
                return;
            }
            if (string.Equals(argument, "--station-combat-defeat-smoke", StringComparison.Ordinal))
            {
                _ = RunStationCombatDefeatSmokeWithFailureHandlingAsync();
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

    private sealed class ServiceDoorPresentation(
        Node3D left,
        Node3D right,
        GeometryInstance3D statusStrip,
        CollisionShape3D blocker,
        NavigationLink3D navigationLink,
        Vector3 closedLeftPosition,
        Vector3 closedRightPosition)
    {
        public Node3D Left { get; } = left;

        public Node3D Right { get; } = right;

        public GeometryInstance3D StatusStrip { get; } = statusStrip;

        public CollisionShape3D Blocker { get; } = blocker;

        public NavigationLink3D NavigationLink { get; } = navigationLink;

        public Vector3 ClosedLeftPosition { get; } = closedLeftPosition;

        public Vector3 ClosedRightPosition { get; } = closedRightPosition;

        public Vector3 OpenLeftPosition { get; } =
            closedLeftPosition + (Vector3.Left * ServiceDoorLeafTravelMeters);

        public Vector3 OpenRightPosition { get; } =
            closedRightPosition + (Vector3.Right * ServiceDoorLeafTravelMeters);

        public bool? TargetOpen { get; set; }
    }

    private void RunBootstrapSmoke()
    {
        var result = _automationBridge!.SubmitCommandJson(JsonSerializer.Serialize(new
        {
            schema_version = 3,
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
            schema_version = 3,
            command_id = "godot.bootstrap.oversized-move",
            type = "move_actor",
            payload = new
            {
                actor_id = _definition!.Protagonist.Id.Value,
                destination = new { x = 1e308, y = 0.0, z = 0.0 },
            },
        }));

        var initialYaw = _camera.YawRadians;
        _camera.YawRadians = 0.0f;
        var northFacingPan = _camera.GetPanBasis();
        _camera.YawRadians = Mathf.Pi / 2.0f;
        var westFacingPan = _camera.GetPanBasis();
        _camera.YawRadians = initialYaw;
        var cameraPanIsYawRelative = northFacingPan.Forward.IsEqualApprox(Vector3.Forward)
            && northFacingPan.Right.IsEqualApprox(Vector3.Right)
            && westFacingPan.Forward.IsEqualApprox(Vector3.Left)
            && westFacingPan.Right.IsEqualApprox(Vector3.Forward);

        var passed = IsAccepted(result)
            && !IsAccepted(invalidResult)
            && !IsAccepted(oversizedStep)
            && !IsAccepted(oversizedMove)
            && cameraPanIsYawRelative
            && _camera.FocusPoint.IsEqualApprox(new Vector3(
                _protagonistView.GlobalPosition.X,
                0.0f,
                _protagonistView.GlobalPosition.Z))
            && _session!.Observe() is { Paused: true, Tick: 0 };
        GD.Print(
            $"SPACEADVENTURE_SMOKE valid={result} invalid={invalidResult} oversized_step={oversizedStep} oversized_move={oversizedMove} camera_pan_yaw_relative={cameraPanIsYawRelative}");
        GetTree().Quit(passed ? 0 : 1);
    }

    private async Task RunStationRouteSmokeAsync()
    {
        var definition = _definition!;
        var automationBridge = _automationBridge
            ?? throw new InvalidOperationException("The automation bridge is unavailable.");
        var actorId = definition.Protagonist.Id.Value;
        var survivor = definition.Interactions.Single(
            interaction => interaction.Effect == StationInteractionEffect.BeginSurvivorDialogue);
        var entryDoor = definition.Interactions.Single(
            interaction => interaction.Effect == StationInteractionEffect.OpenEntryServiceDoor);
        var soloExit = definition.Interactions.Single(
            interaction => interaction.Effect == StationInteractionEffect.OpenSoloExitServiceDoor);
        var terminal = definition.Interactions.Single(
            interaction => interaction.Effect == StationInteractionEffect.RecordObservation);
        var protector = definition.Interactions.Single(
            interaction => interaction.Effect == StationInteractionEffect.BeginRecruitmentDialogue);
        var airlock = definition.Interactions.Single(
            interaction => interaction.Effect == StationInteractionEffect.CompleteScenario);
        var futureRoutePath = new GodotSpatialPathfinder(GetWorld3D().NavigationMap).FindPath(
            definition.Protagonist.Id,
            new WorldPosition(-1.5, 0.0, 0.0),
            new WorldPosition(11.0, 0.0, 8.0));

        var pause = automationBridge.SetPaused(true);
        var survivorOrder = SubmitInteraction("godot.route.survivor", actorId, survivor.Id.Value);
        var survivorSequence = _session!.Observe().LatestEventSequence;
        var dialogueWait = automationBridge.AdvanceUntilEventJson(
            survivorSequence,
            "dialogue_started",
            maximumTicks: 600);
        RenderObservation(_session.Observe());
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var survivorDialoguePresentation =
            _survivorPresentation.CurrentAction == HumanoidPresentationAction.DialogueSpeak;
        var tacticalPauseFreezesHumanoids = _survivorPresentation.PlaybackPaused
            && _protectorWaitingPresentation.PlaybackPaused;
        var waitingProtectorPresentation = _protectorWaitingPresentation.Visible
            && GetNode<Node3D>("Interactions/Protector").Visible
            && !_protectorPartyPresentation.Visible
            && !GetNode<Node3D>("Actors/Protector").Visible;
        var response = automationBridge.SubmitCommandJson(JsonSerializer.Serialize(new
        {
            schema_version = 3,
            command_id = "godot.route.response",
            type = "choose_dialogue_response",
            payload = new
            {
                actor_id = actorId,
                interaction_id = survivor.Id.Value,
                response_id = survivor.Dialogue!.Responses.Single(response =>
                    response.Effect == StationDialogueResponseEffect.RerouteServicePower).Id.Value,
            },
        }));
        RenderObservation(_session.Observe());
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var survivorReturnedToIdle =
            _survivorPresentation.CurrentAction == HumanoidPresentationAction.Idle;
        var entryDoorPresentation = _serviceDoors[entryDoor.Id.Value];
        var soloExitPresentation = _serviceDoors[soloExit.Id.Value];
        var entryPathfinder = new GodotSpatialPathfinder(GetWorld3D().NavigationMap);
        var entryNavigationPath = entryPathfinder.FindPath(
            definition.Protagonist.Id,
            _session.Observe().StationRoute!.Protagonist.Position,
            new WorldPosition(-10.0, 0.0, 0.0));
        for (var frame = 0;
             frame < MaximumNavigationInitializationFrames && !entryNavigationPath.IsReachable;
             frame++)
        {
            // Navigation node setters are queued until a server sync. Wait until
            // the path query itself observes the newly enabled service-door link.
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            entryNavigationPath = entryPathfinder.FindPath(
                definition.Protagonist.Id,
                _session.Observe().StationRoute!.Protagonist.Position,
                new WorldPosition(-10.0, 0.0, 0.0));
        }
        var doorNavigationUnlocked = entryDoorPresentation.NavigationLink.Enabled
            && entryNavigationPath.IsReachable
            && !entryDoorPresentation.Blocker.Disabled
            && entryDoorPresentation.Left.Position.IsEqualApprox(
                entryDoorPresentation.ClosedLeftPosition)
            && entryDoorPresentation.Right.Position.IsEqualApprox(
                entryDoorPresentation.ClosedRightPosition)
            && entryDoorPresentation.StatusStrip.MaterialOverride == _serviceDoorLockedMaterial
            && !soloExitPresentation.NavigationLink.Enabled;

        var terminalOrder = SubmitInteraction("godot.route.terminal", actorId, terminal.Id.Value);
        var terminalSequence = _session.Observe().LatestEventSequence;
        var terminalWait = automationBridge.AdvanceUntilEventJson(
            terminalSequence,
            "interaction_completed",
            maximumTicks: 600);

        var arenaSequence = _session.Observe().LatestEventSequence;
        var arenaMove = automationBridge.SubmitCommandJson(JsonSerializer.Serialize(new
        {
            schema_version = 3,
            command_id = "godot.route.enter-solo-arena",
            type = "move_actor",
            payload = new
            {
                actor_id = actorId,
                destination = new { x = -10.0, y = 0.0, z = 2.75 },
            },
        }));
        var arenaWait = automationBridge.AdvanceUntilEventJson(
            arenaSequence,
            "encounter_started",
            maximumTicks: 600);
        var arenaEvents = _session.EventsSince(arenaSequence);
        var entryDoorOpened = arenaEvents.SingleOrDefault(gameEvent =>
            gameEvent.Detail is InteractionCompletedEventDetail detail
                && detail.InteractionId == entryDoor.Id);
        var encounterStarted = arenaEvents.SingleOrDefault(gameEvent =>
            gameEvent.Type == GameplayEventType.EncounterStarted);
        RenderObservation(_session.Observe());
        AdvanceServiceDoorPresentation(ServiceDoorAnimationSeconds);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var doorPresentationSynchronized = entryDoorPresentation.NavigationLink.Enabled
            && entryDoorPresentation.Blocker.Disabled
            && entryDoorPresentation.Left.Position.IsEqualApprox(
                entryDoorPresentation.OpenLeftPosition)
            && entryDoorPresentation.Right.Position.IsEqualApprox(
                entryDoorPresentation.OpenRightPosition)
            && entryDoorPresentation.StatusStrip.MaterialOverride == _serviceDoorOpenMaterial
            && !soloExitPresentation.NavigationLink.Enabled
            && !soloExitPresentation.Blocker.Disabled
            && soloExitPresentation.Left.Position.IsEqualApprox(
                soloExitPresentation.ClosedLeftPosition)
            && soloExitPresentation.Right.Position.IsEqualApprox(
                soloExitPresentation.ClosedRightPosition)
            && soloExitPresentation.StatusStrip.MaterialOverride == _serviceDoorLockedMaterial;
        var exitLock = SubmitInteraction("godot.route.solo-exit-lock", actorId, soloExit.Id.Value);

        var readinessAdvance = automationBridge.AdvanceExactTicks(
            definition.Combat.Encounter.ReadyingTicks);
        var ability = automationBridge.SubmitCommandJson(JsonSerializer.Serialize(new
        {
            schema_version = 3,
            command_id = "godot.route.suppress-enforcer",
            type = "use_ability",
            payload = new
            {
                actor_id = actorId,
                ability_id = definition.Combat.ProtagonistAbility.Id.Value,
                target_position = new { x = -10.0, y = 0.0, z = -1.4 },
            },
        }));
        var combatResume = automationBridge.SetPaused(false);
        var attackAccepted = false;
        var healAccepted = false;
        for (var tick = 0; tick < 1200; tick++)
        {
            var combatRoute = _session.Observe().StationRoute!;
            if (combatRoute.Encounter!.Phase is EncounterPhase.Securing or EncounterPhase.Victory)
            {
                break;
            }

            if (!attackAccepted
                && combatRoute.Protagonist.CurrentAction is null
                && combatRoute.Protagonist.PendingAction is null
                && combatRoute.Protagonist.Combat!.Cooldowns.Single().RemainingTicks > 0)
            {
                var attack = automationBridge.SubmitCommandJson(JsonSerializer.Serialize(new
                {
                    schema_version = 3,
                    command_id = "godot.route.attack-enforcer",
                    type = "assign_basic_attack_target",
                    payload = new
                    {
                        actor_id = actorId,
                        target_id = definition.Combat.Hostile.Id.Value,
                    },
                }));
                attackAccepted = IsAccepted(attack);
            }

            if (!healAccepted
                && combatRoute.Protagonist.Combat!.Health <= 60
                && combatRoute.Protagonist.Combat.Items.Single().Charges > 0)
            {
                var heal = automationBridge.SubmitCommandJson(JsonSerializer.Serialize(new
                {
                    schema_version = 3,
                    command_id = "godot.route.use-field-aid",
                    type = "use_item",
                    payload = new
                    {
                        actor_id = actorId,
                        item_id = definition.Combat.HealingItem.Id.Value,
                        target_actor_id = actorId,
                    },
                }));
                healAccepted = IsAccepted(heal);
                attackAccepted = false;
            }

            _session.AdvanceTicks(1);
        }

        if (_session.Observe().StationRoute!.Encounter?.Phase == EncounterPhase.Securing)
        {
            _session.AdvanceTicks(definition.Combat.Encounter.SecuringTicks);
        }
        RenderObservation(_session.Observe());
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var victory = _session.Observe().StationRoute!;
        var combatPresentationSynchronized = _securityEnforcerView.Visible
            && _securityEnforcerTarget.CollisionLayer == 0
            && _securityEnforcerPresentation.CurrentAction == HumanoidPresentationAction.Down
            && !_securityEnforcerPresentation.PlaybackPaused
            && soloExitPresentation.NavigationLink.Enabled
            && !soloExitPresentation.Blocker.Disabled;

        var exitOpen = SubmitInteraction("godot.route.open-solo-exit", actorId, soloExit.Id.Value);
        var exitSequence = _session.Observe().LatestEventSequence;
        var exitPause = automationBridge.SetPaused(true);
        var exitWait = automationBridge.AdvanceUntilEventJson(
            exitSequence,
            "interaction_completed",
            maximumTicks: 600);
        RenderObservation(_session.Observe());
        AdvanceServiceDoorPresentation(ServiceDoorAnimationSeconds);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var final = _session.Observe().StationRoute!;
        var passed = IsAccepted(pause)
            && IsAccepted(survivorOrder)
            && IsReached(dialogueWait)
            && survivorDialoguePresentation
            && survivorReturnedToIdle
            && tacticalPauseFreezesHumanoids
            && waitingProtectorPresentation
            && IsAccepted(response)
            && doorNavigationUnlocked
            && IsAccepted(terminalOrder)
            && IsReached(terminalWait)
            && IsAccepted(arenaMove)
            && IsReached(arenaWait)
            && entryDoorOpened is not null
            && encounterStarted is not null
            && entryDoorOpened.Sequence < encounterStarted.Sequence
            && doorPresentationSynchronized
            && IsRejectedWithCode(exitLock, "interaction_unavailable")
            && IsAccepted(readinessAdvance)
            && IsAccepted(ability)
            && IsAccepted(combatResume)
            && healAccepted
            && victory.Encounter?.Phase == EncounterPhase.Victory
            && victory.Hostiles!.Single().Combat.Health == 0
            && combatPresentationSynchronized
            && IsAccepted(exitOpen)
            && IsAccepted(exitPause)
            && IsReached(exitWait)
            && futureRoutePath.IsReachable
            && final.Phase == ScenarioPhase.InProgress
            && final.Objective.Id == definition.RecruitmentObjective.Id
            && final.Party.Count == 1
            && final.Interactions.Single(interaction => interaction.Id == entryDoor.Id).State
                == InteractionState.Completed
            && final.Interactions.Single(interaction => interaction.Id == soloExit.Id).State
                == InteractionState.Completed
            && final.Interactions.Single(interaction => interaction.Id == protector.Id).State
                == InteractionState.Available
            && final.Interactions.Single(interaction => interaction.Id == airlock.Id).State
                == InteractionState.Unavailable
            && final.Interactions.Single(interaction => interaction.Id == terminal.Id).State
                == InteractionState.Completed
            && !_session.EventsSince(0).Any(gameEvent =>
                gameEvent.Type == GameplayEventType.ScenarioCompleted);

        var summary = JsonSerializer.Serialize(new
        {
            passed,
            tick = _session.Observe().Tick,
            phase = final.Phase.ToString(),
            objective = final.Objective.Id.Value,
            entry_door_open = final.Interactions.Single(
                interaction => interaction.Id == entryDoor.Id).State == InteractionState.Completed,
            entry_door_navigation_unlocked_before_open = doorNavigationUnlocked,
            entry_door_auto_opened_before_encounter = entryDoorOpened is not null
                && encounterStarted is not null
                && entryDoorOpened.Sequence < encounterStarted.Sequence,
            entry_door_presentation_synchronized = doorPresentationSynchronized,
            survivor_dialogue_presentation = survivorDialoguePresentation
                && survivorReturnedToIdle,
            tactical_pause_freezes_humanoids = tacticalPauseFreezesHumanoids,
            waiting_protector_visible_party_hidden = waitingProtectorPresentation,
            combat_won = final.Encounter?.Phase == EncounterPhase.Victory,
            field_aid_used = healAccepted,
            combat_presentation_synchronized = combatPresentationSynchronized,
            solo_exit_open = final.Interactions.Single(
                interaction => interaction.Id == soloExit.Id).State == InteractionState.Completed,
            future_route_navigation_connected = futureRoutePath.IsReachable,
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
            GD.Print($"SPACEADVENTURE_ROUTE_DIAGNOSTIC door_navigation_unlocked={doorNavigationUnlocked}");
            GD.Print($"SPACEADVENTURE_ROUTE_DIAGNOSTIC terminal={terminalOrder}");
            GD.Print($"SPACEADVENTURE_ROUTE_DIAGNOSTIC terminal_wait={terminalWait}");
            GD.Print($"SPACEADVENTURE_ROUTE_DIAGNOSTIC arena_move={arenaMove}");
            GD.Print($"SPACEADVENTURE_ROUTE_DIAGNOSTIC arena_wait={arenaWait}");
            GD.Print($"SPACEADVENTURE_ROUTE_DIAGNOSTIC entry_door_opened={entryDoorOpened}");
            GD.Print($"SPACEADVENTURE_ROUTE_DIAGNOSTIC door_presentation={doorPresentationSynchronized}");
            GD.Print($"SPACEADVENTURE_ROUTE_DIAGNOSTIC exit_lock={exitLock}");
            GD.Print($"SPACEADVENTURE_ROUTE_DIAGNOSTIC readiness={readinessAdvance}");
            GD.Print($"SPACEADVENTURE_ROUTE_DIAGNOSTIC ability={ability}");
            GD.Print($"SPACEADVENTURE_ROUTE_DIAGNOSTIC resume={combatResume}");
            GD.Print($"SPACEADVENTURE_ROUTE_DIAGNOSTIC combat_presentation={combatPresentationSynchronized}");
            GD.Print($"SPACEADVENTURE_ROUTE_DIAGNOSTIC exit_open={exitOpen}");
            GD.Print($"SPACEADVENTURE_ROUTE_DIAGNOSTIC exit_pause={exitPause}");
            GD.Print($"SPACEADVENTURE_ROUTE_DIAGNOSTIC exit_wait={exitWait}");
            GD.Print($"SPACEADVENTURE_ROUTE_DIAGNOSTIC future_route_path={futureRoutePath.IsReachable}");
        }
        GetTree().Quit(passed ? 0 : 1);
    }

    private async Task RunStationRouteSmokeWithFailureHandlingAsync()
    {
        try
        {
            await RunStationRouteSmokeAsync();
        }
        catch (Exception exception)
        {
            GD.PushError($"Station-route smoke failed: {exception}");
            GetTree().Quit(1);
        }
    }

    private string SubmitInteraction(string commandId, string actorId, string targetId)
    {
        return _automationBridge!.SubmitCommandJson(JsonSerializer.Serialize(new
        {
            schema_version = 3,
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

    private static bool IsRejectedWithCode(string result, string rejectionCode)
    {
        using var document = JsonDocument.Parse(result);
        return !document.RootElement.GetProperty("accepted").GetBoolean()
            && document.RootElement.TryGetProperty("rejection_code", out var code)
            && string.Equals(code.GetString(), rejectionCode, StringComparison.Ordinal);
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

    private async Task RunStationCombatDefeatSmokeWithFailureHandlingAsync()
    {
        try
        {
            await RunStationCombatDefeatSmokeAsync();
        }
        catch (Exception exception)
        {
            GD.PushError($"Station combat-defeat smoke failed: {exception}");
            GetTree().Quit(1);
        }
    }

    private async Task RunStationCombatDefeatSmokeAsync()
    {
        var session = _session
            ?? throw new InvalidOperationException("The game session is unavailable.");
        var definition = _definition
            ?? throw new InvalidOperationException("The route definition is unavailable.");
        var bridge = _automationBridge
            ?? throw new InvalidOperationException("The automation bridge is unavailable.");
        var actorId = definition.Protagonist.Id.Value;
        var survivor = definition.Interactions.Single(interaction =>
            interaction.Effect == StationInteractionEffect.BeginSurvivorDialogue);
        var entryDoor = definition.Interactions.Single(interaction =>
            interaction.Effect == StationInteractionEffect.OpenEntryServiceDoor);
        var soloExit = definition.Interactions.Single(interaction =>
            interaction.Effect == StationInteractionEffect.OpenSoloExitServiceDoor);

        _ = bridge.SetPaused(true);
        var survivorOrder = SubmitInteraction("defeat.survivor", actorId, survivor.Id.Value);
        var survivorSequence = session.Observe().LatestEventSequence;
        var dialogue = bridge.AdvanceUntilEventJson(
            survivorSequence,
            "dialogue_started",
            maximumTicks: 600);
        var response = session.Execute(new ChooseDialogueResponseCommand(
            new CommandId("defeat.response"),
            definition.Protagonist.Id,
            survivor.Id,
            survivor.Dialogue!.Responses.Single(candidate =>
                candidate.Effect == StationDialogueResponseEffect.RerouteServicePower).Id));
        var entryOrder = SubmitInteraction("defeat.entry", actorId, entryDoor.Id.Value);
        var entrySequence = session.Observe().LatestEventSequence;
        var entryWait = bridge.AdvanceUntilEventJson(
            entrySequence,
            "interaction_completed",
            maximumTicks: 600);
        RenderObservation(session.Observe());
        AdvanceServiceDoorPresentation(ServiceDoorAnimationSeconds);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var arenaSequence = session.Observe().LatestEventSequence;
        var arenaMove = bridge.SubmitCommandJson(JsonSerializer.Serialize(new
        {
            schema_version = 3,
            command_id = "defeat.enter-arena",
            type = "move_actor",
            payload = new
            {
                actor_id = actorId,
                destination = new { x = -10.0, y = 0.0, z = 2.75 },
            },
        }));
        var arenaWait = bridge.AdvanceUntilEventJson(
            arenaSequence,
            "encounter_started",
            maximumTicks: 600);
        var resume = session.Execute(new SetPauseCommand(
            new CommandId("defeat.resume"),
            Paused: false));
        for (var tick = 0;
             tick < 1200
                && session.Observe().StationRoute!.Encounter!.Phase != EncounterPhase.Defeat;
             tick++)
        {
            session.AdvanceTicks(1);
        }

        var defeated = session.Observe().StationRoute!;
        RenderObservation(session.Observe());
        var pausedAfterDefeat = session.IsPaused;
        var retryVisibleAtDefeat = _retryButton.Visible;
        var retry = session.Execute(new RestartEncounterCommand(
            new CommandId("defeat.retry"),
            definition.Combat.Encounter.Id));
        var retried = session.Observe().StationRoute!;
        RenderObservation(session.Observe());
        var passed = IsAccepted(survivorOrder)
            && IsReached(dialogue)
            && response.Accepted
            && IsAccepted(entryOrder)
            && IsReached(entryWait)
            && IsAccepted(arenaMove)
            && IsReached(arenaWait)
            && resume.Accepted
            && defeated.Encounter?.Phase == EncounterPhase.Defeat
            && pausedAfterDefeat
            && retryVisibleAtDefeat
            && retry.Accepted
            && retried.Encounter?.Phase == EncounterPhase.Readying
            && retried.Encounter.Attempt == 2
            && retried.Protagonist.Combat?.Health
                == definition.Combat.Encounter.ProtagonistMaximumHealth
            && retried.Hostiles!.Single().Combat.Health
                == definition.Combat.Hostile.MaximumHealth
            && retried.Interactions.Single(interaction => interaction.Id == entryDoor.Id).State
                == InteractionState.Completed
            && !retried.Interactions.Single(interaction => interaction.Id == soloExit.Id).CanInteract
            && _retryButton.Visible == false;
        GD.Print("SPACEADVENTURE_COMBAT_DEFEAT_SMOKE " + JsonSerializer.Serialize(new
        {
            passed,
            defeated_phase = defeated.Encounter?.Phase.ToString(),
            paused_after_defeat = pausedAfterDefeat,
            survivor_order = IsAccepted(survivorOrder),
            dialogue_reached = IsReached(dialogue),
            response_accepted = response.Accepted,
            entry_order = IsAccepted(entryOrder),
            entry_reached = IsReached(entryWait),
            arena_move = IsAccepted(arenaMove),
            arena_reached = IsReached(arenaWait),
            resume_accepted = resume.Accepted,
            retry_accepted = retry.Accepted,
            retried_phase = retried.Encounter?.Phase.ToString(),
            retried_attempt = retried.Encounter?.Attempt,
            entry_preserved = retried.Interactions.Single(
                interaction => interaction.Id == entryDoor.Id).State.ToString(),
        }, CaptureLogJsonOptions));
        GetTree().Quit(passed ? 0 : 1);
    }

    private sealed class TimedPresentationEffect(Node3D node, float remainingSeconds)
    {
        public Node3D Node { get; } = node;

        public float RemainingSeconds { get; set; } = remainingSeconds;
    }
}
