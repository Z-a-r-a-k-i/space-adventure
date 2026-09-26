using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

public partial class GameHost
{
    // Boarding 0-3.2 s, closure and engines 3.3-4.4 s, takeoff 4.4-8.0 s, fade to black 6.8-8.2 s, then a title
    // card on black until the battle (which fades in from black) takes over.
    private const double DepartureDuration = 9.6;
    private const double FadeStartSeconds = 6.8;
    private const double TitleCardStartSeconds = 8.2;
    private const float LetterboxHeight = 62;
    private ColorRect _letterboxTop = null!;
    private ColorRect _letterboxBottom = null!;
    private ColorRect _departureFade = null!;
    private Label _departureCaption = null!;
    private VBoxContainer _titleCard = null!;
    private Vector3 _departureFocus;
    private float _departureYaw;
    private double _departureSeconds;
    private Node3D? _departureRamp;
    private Node3D? _departureDoor;
    private readonly Dictionary<EntityId, Vector3> _boardingOrigins = [];
    private bool _departureStarted;
    private bool _departureEnginesStarted;
    private AudioStreamPlayer3D? _departureAudio;
    private readonly List<MeshInstance3D> _departurePlumes = [];

    // Built eagerly so every scene query runs here in the adapter, not later when
    // the core layout enumerates the placements.
    private StationEncounterPlacement[] CreateEscapePlacements(StationRouteDefinition definition)
    {
        var markers = GetNode<Node3D>("Markers").GetChildren().OfType<Marker3D>().ToDictionary(GetStableId);
        var placements = new List<StationEncounterPlacement>();
        foreach (var encounter in definition.Combat.Encounters.Skip(2))
        {
            var enemies = encounter.HostileIds.Select(id => new StationHostilePlacement(id,
                ToCore(markers[id.Value].GlobalPosition), ToCore(-markers[id.Value].GlobalBasis.Z.Normalized()))).ToArray();
            placements.Add(new StationEncounterPlacement(encounter.Id, enemies[0].Position, HostilePlacements: enemies));
        }
        return placements.ToArray();
    }

    /// <summary>
    /// Walkable point just inside an encounter's area (marker whose stable ID is the encounter ID). Rules never
    /// read it: reviews and layout checks use it to lead the crew in until a hostile notices them.
    /// </summary>
    private WorldPosition EncounterEntry(EncounterId encounterId) =>
        ToCore(WithGroundHeight(GetNode<Node3D>("Markers").GetChildren().OfType<Marker3D>()
            .Single(marker => GetStableId(marker) == encounterId.Value).GlobalPosition));

    private string CurrentSector(StationRouteObservation route)
    {
        if (route.Phase == ScenarioPhase.Completed) { return "ESCAPE CUTTER"; }
        var x = FocusedActor(route).Position.X;
        return x >= 74 ? "BOARDING APRON" : x >= 54 ? "LAUNCH BAY" : x >= 40 ? "DOCK CONCOURSE"
            : x >= 26 ? "SECURITY CHECKPOINT" : x >= 12 ? "SERVICE ACCESS" : x >= 6 ? "MEDIC JUNCTION"
            : route.Encounter?.Id == _definition!.Combat.PartyEncounter.Id ? "TRANSIT HALL"
            : route.Party.Count > 1 ? "CREW ACCESS" : route.Encounter?.Phase == EncounterPhase.Dormant ? "ARRIVALS" : "SECURITY";
    }

    private void AdvanceDeparture(GameObservation observation, double delta)
    {
        if (observation.StationRoute is not { Phase: ScenarioPhase.Completed } route) { return; }
        var cutter = GetNode<Node3D>("Environment/EscapeCutter");
        if (!_departureStarted)
        {
            _departureStarted = true;
            _camera.InputEnabled = false;
            _controlsOverlay.Visible = _controlsScrim.Visible = false;
            _feedbackSeconds = 0; _feedbackLabel.Visible = false;
            CancelAbilityTargeting(); CancelSelectionGesture();
            _camera.FocusOn(new Vector3(82, 0, 8));
            _camera.DistanceMeters = 20;
            _departureFocus = _camera.FocusPoint;
            _departureYaw = _camera.YawRadians;
            _objectiveColumn.Visible = false;
            foreach (var actor in route.Party) { _boardingOrigins[actor.Id] = _actorViews[actor.Id.Value].GlobalPosition; }
            _departureDoor = EnumerateDescendants(cutter).OfType<Node3D>().FirstOrDefault(node => node.Name.ToString().Replace('_', '.') == "pivot.door");
            _departureRamp = EnumerateDescendants(cutter).OfType<Node3D>()
                .FirstOrDefault(node => node.Name.ToString().Replace('_', '.') == "pivot.ramp");
            foreach (var side in new[] { -1, 1 })
            {
                var plume = new MeshInstance3D
                {
                    Name = side < 0 ? "PortExhaust" : "StarboardExhaust", Visible = false,
                    Mesh = new CylinderMesh { TopRadius = .12f, BottomRadius = .42f, Height = 1, RadialSegments = 24 },
                    MaterialOverride = CreateCombatEffectMaterial(new Color("79eafa", .7f)),
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                    Position = new Vector3(side * 2.51f, 1.89f, 4.9f), Rotation = new Vector3(Mathf.Pi / 2, 0, 0),
                };
                cutter.AddChild(plume); _departurePlumes.Add(plume);
            }
        }
        _departureSeconds = Math.Min(DepartureDuration, _departureSeconds + Math.Clamp(delta, 0, .1));
        var crewIndex = 0;
        var rampEntry = new Vector3(79.1f, 1.08f, 8);
        foreach (var actor in route.Party)
        {
            var progress = (float)Math.Clamp((_departureSeconds - crewIndex++ * .32) / 2.6, 0, 1);
            var view = _actorViews[actor.Id.Value];
            view.GlobalPosition = _boardingOrigins[actor.Id].Lerp(rampEntry, progress);
            view.Visible = progress < 1;
            // Face the walk to the ramp. The body is snapped because the ordinary crew
            // sync re-applies the final gameplay heading every frame.
            var heading = rampEntry - _boardingOrigins[actor.Id];
            ArmedPresentation(actor.Id)?.Synchronize(progress < 1, true, false, heading, null, null,
                observation.Tick + _departureSeconds * GameSession.TicksPerSecond, (float)delta, bodyFacing: heading);
        }
        if (_departureRamp is not null)
        { _departureRamp.Rotation = new Vector3((float)(Math.Clamp((_departureSeconds - 3.3) / 1.1, 0, 1) * -2.0183163), 0, 0); }
        if (_departureDoor is not null) { _departureDoor.Rotation = new Vector3((float)(Math.Clamp((_departureSeconds - 3.3) / 1.1, 0, 1) * -Math.PI / 2), 0, 0); }
        var takeoff = (float)Math.Clamp((_departureSeconds - 4.4) / 3.6, 0, 1);
        if (!_departureEnginesStarted && _departureSeconds >= 3.3)
        {
            _departureEnginesStarted = true;
            if (_reviewMode != "capture" && DisplayServer.GetName() != "headless")
            {
                _departureAudio = new AudioStreamPlayer3D { Stream = CombatAudio.Get("departure"), Bus = CombatAudio.Bus("departure"),
                    VolumeDb = CombatAudio.VolumeDb("departure"), MaxDb = CombatAudio.VolumeDb("departure"), UnitSize = 18, MaxDistance = 70 };
                cutter.AddChild(_departureAudio); _departureAudio.Play();
            }
        }
        var thrust = (float)Math.Clamp((_departureSeconds - 3.3) / 1.1, 0, 1);
        foreach (var plume in _departurePlumes)
        { plume.Visible = thrust > 0; plume.Scale = new Vector3(thrust, .3f + thrust * 1.7f, thrust); }
        cutter.Position = new Vector3(83 + takeoff * takeoff * 24, takeoff * 6, 8);
        _crewCluster.Visible = _actionPanel.Visible = _pauseButton.Visible = _pauseLabel.Visible = _controlsButton.Visible = false;
        _worldControlsHint.Visible = _tipCard.Visible = false;
        _pauseFrame.Shown = false;
        _destinationMarker.Visible = false;
        foreach (var actor in route.Party)
        {
            _actorViews[actor.Id.Value].GetNode<Node3D>("SelectionBeacon").Visible = false;
        }
        _objectiveLabel.Text = takeoff > 0 ? "Escape cutter departing" : "All crew boarding";
        AdvanceDepartureCinematic(cutter, takeoff);
        _completionOverlay.Visible = _departureSeconds >= TitleCardStartSeconds;
        if (_departureSeconds >= DepartureDuration) { _departureAudio?.Stop(); }
    }

    /// <summary>
    /// Letterbox, cutter-following camera with a slow orbit, fade to black and the title card. Driven by the
    /// departure clock, so reviews that step it reproduce the same frames.
    /// </summary>
    private void AdvanceDepartureCinematic(Node3D cutter, float takeoff)
    {
        var bars = (float)Mathf.SmoothStep(0, 1, Math.Clamp(_departureSeconds / .6, 0, 1)) * LetterboxHeight;
        _letterboxTop.OffsetBottom = bars;
        _letterboxBottom.OffsetTop = -bars;
        _letterboxTop.Visible = _letterboxBottom.Visible = true;
        var follow = Mathf.SmoothStep(0, 1, takeoff);
        var target = new Vector3(cutter.GlobalPosition.X, 0, cutter.GlobalPosition.Z);
        _camera.FocusPoint = _departureFocus.Lerp(target, follow);
        _camera.YawRadians = _departureYaw + .42f * follow;
        _departureCaption.Visible = _departureSeconds < TitleCardStartSeconds;
        _departureCaption.Text = takeoff > 0 ? "ESCAPE CUTTER  ·  DEPARTING FRONTIER STATION" : "ALL CREW ABOARD";
        _departureCaption.Modulate = new Color(1, 1, 1, (float)Math.Clamp((_departureSeconds - .4) / .5, 0, 1));
        var fade = (float)Math.Clamp((_departureSeconds - FadeStartSeconds) / (TitleCardStartSeconds - FadeStartSeconds), 0, 1);
        _departureFade.Color = new Color(0, 0, 0, fade * fade);
        _titleCard.Modulate = new Color(1, 1, 1, (float)Math.Clamp((_departureSeconds - TitleCardStartSeconds) / .45, 0, 1));
    }

    private void CreateDepartureCinematic()
    {
        var cinematic = new CanvasLayer { Name = "Cinematic", Layer = 2 };
        AddChild(cinematic);
        _letterboxTop = new ColorRect { Name = "LetterboxTop", Color = Colors.Black, AnchorRight = 1, MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        _letterboxBottom = new ColorRect { Name = "LetterboxBottom", Color = Colors.Black, AnchorTop = 1, AnchorRight = 1, AnchorBottom = 1,
            MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        cinematic.AddChild(_letterboxTop);
        cinematic.AddChild(_letterboxBottom);
        _departureCaption = TacticalUi.Label("", 13, "a0efd8");
        _departureCaption.HorizontalAlignment = HorizontalAlignment.Center;
        _departureCaption.AnchorTop = _departureCaption.AnchorBottom = 1;
        _departureCaption.AnchorRight = 1;
        _departureCaption.OffsetTop = -LetterboxHeight / 2 - 10; _departureCaption.OffsetBottom = -LetterboxHeight / 2 + 10;
        _departureCaption.Visible = false;
        cinematic.AddChild(_departureCaption);
        _departureFade = new ColorRect { Name = "DepartureFade", Color = new Color(0, 0, 0, 0), AnchorRight = 1, AnchorBottom = 1,
            MouseFilter = Control.MouseFilterEnum.Ignore };
        cinematic.AddChild(_departureFade);
        _completionOverlay = new CenterContainer { Name = "CompletionOverlay", AnchorRight = 1, AnchorBottom = 1,
            MouseFilter = Control.MouseFilterEnum.Stop, Visible = false };
        cinematic.AddChild(_completionOverlay);
        _titleCard = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _titleCard.AddThemeConstantOverride("separation", 10);
        _completionOverlay.AddChild(_titleCard);
        var title = TacticalUi.Label("STATION ESCAPED", 34, "a0efd8");
        title.AddThemeFontOverride("font", TacticalUi.BoldFont);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        _titleCard.AddChild(title);
        var rule = TacticalUi.Rule(new Color(TacticalUi.Cyan, .55f));
        rule.CustomMinimumSize = new Vector2(360, 1);
        rule.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        _titleCard.AddChild(rule);
        var detail = TacticalUi.Label("Three crew aboard. Six encounters cleared.", 15, "c3d0d4");
        detail.HorizontalAlignment = HorizontalAlignment.Center;
        _titleCard.AddChild(detail);
        var teaser = TacticalUi.Label("Unknown contact closing on the cutter...", 13, "e5bc7d");
        teaser.HorizontalAlignment = HorizontalAlignment.Center;
        _titleCard.AddChild(teaser);
    }
}
