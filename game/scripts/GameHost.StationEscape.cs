using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

public partial class GameHost
{
    private const double DepartureDuration = 8;
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
            var area = encounter.Id.Value.Split('.').Last();
            var trigger = markers[encounter.Id.Value];
            var crew = encounter.RequiredCrewIds.Select((id, index) => new StationActorPlacement(id,
                ToCore(GetNode<Marker3D>($"Markers/{area}_crew{index}").GlobalPosition))).ToArray();
            var enemies = encounter.HostileIds.Select(id => new StationHostilePlacement(id,
                ToCore(markers[id.Value].GlobalPosition), ToCore(-markers[id.Value].GlobalBasis.Z.Normalized()))).ToArray();
            placements.Add(new StationEncounterPlacement(encounter.Id, ToCore(trigger.GlobalPosition),
                trigger.GetMeta("trigger_radius_meters").AsDouble(), crew[0].Position, enemies[0].Position,
                crew[1].Position, enemies.Skip(1).Select(enemy => new StationActorPlacement(enemy.ActorId, enemy.Position)).ToArray(),
                new WorldPosition(-1, 0, 0), crew, enemies));
        }
        return placements.ToArray();
    }

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
        _worldControlsHint.Visible = false;
        _destinationMarker.Visible = false;
        foreach (var actor in route.Party)
        {
            _actorViews[actor.Id.Value].GetNode<Node3D>("SelectionBeacon").Visible = false;
        }
        _objectiveLabel.Text = takeoff > 0 ? "Escape cutter departing" : "All crew boarding";
        _completionOverlay.Visible = _departureSeconds >= DepartureDuration;
        if (_completionOverlay.Visible) { _departureAudio?.Stop(); }
    }
}
