using Godot;
using SpaceAdventure.Core;
using System.Text.Json;

namespace SpaceAdventure.Game;

public partial class GameHost
{
    // Reviewed station-layout-v2 void bounds. These assertions deliberately remain
    // independent of the authoring script, so a filled polygon/collider is detected.
    private static readonly LayoutPit[] ReviewedLayoutPits =
    [
        new("solo", -12.1, -9.2, -.7, .9),
        new("party", -.9, .9, 7.1, 9.6),
        new("service", 17, 20, 4, 8),
        new("security", 30, 32, -.5, 2),
        new("security", 33, 35, 5, 7.5),
        new("dock", 44, 48, 9, 12.5),
        new("launch", 60, 62, 8, 12),
        new("launch", 65, 68, 15, 17),
    ];

    private readonly HashSet<(EncounterId Id, int Attempt)> _layoutCommandChecks = [];

    private async Task CheckStationTacticalLayout()
    {
        var map = GetWorld3D().NavigationMap;
        for (var frame = 0; NavigationServer3D.MapGetIterationId(map) == 0
            && frame < MaximumNavigationInitializationFrames; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        // Door link changes and floor collision updates synchronize on physics frames.
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        LayoutRequire(NavigationServer3D.MapGetIterationId(map) > 0, "navigation initialized");
        var pathfinder = new GodotSpatialPathfinder(map);
        var crewId = _definition!.Protagonist.Id;
        var hostileId = _definition.Combat.SoloHostile.Id;

        foreach (var pit in ReviewedLayoutPits)
        {
            var center = pit.Center;
            var west = new WorldPosition(pit.MinX - .7, 0, center.Z);
            var east = new WorldPosition(pit.MaxX + .7, 0, center.Z);
            var south = new WorldPosition(center.X, 0, pit.MinZ - .7);
            var north = new WorldPosition(center.X, 0, pit.MaxZ + .7);
            CheckLayoutPath(pathfinder, crewId, west, east, $"{pit.Area} west/east crew detour");
            CheckLayoutPath(pathfinder, hostileId, east, west, $"{pit.Area} east/west hostile detour");
            CheckLayoutPath(pathfinder, crewId, south, north, $"{pit.Area} south/north crew detour");
            CheckLayoutPath(pathfinder, hostileId, north, south, $"{pit.Area} north/south hostile detour");
            LayoutRequire(!pathfinder.FindPath(crewId, west, center).IsReachable,
                $"{pit.Area} pit center is excluded from navigation");
            LayoutRequire(!LayoutFloorAt(center), $"{pit.Area} pit center has no pickable floor");
            InputCheck($"{pit.Area} pit {center.X:0.0}/{center.Z:0.0}: crew/enemy detours and void rejection", true);
        }

        var layout = CreateLayout(_definition);
        CheckLayoutStandingPoint(layout.ProtagonistStart, "protagonist start");
        foreach (var actor in layout.Actors) { CheckLayoutStandingPoint(actor.Position, actor.ActorId.Value); }
        foreach (var encounter in layout.Encounters)
        {
            // Fights start wherever a hostile sees the crew; the area entry stands in for "the crew inside the room".
            var entry = EncounterEntry(encounter.EncounterId);
            CheckLayoutStandingPoint(entry, $"{encounter.EncounterId} entry");
            var enemies = encounter.HostilePlacements?.Select(enemy => new StationActorPlacement(enemy.ActorId, enemy.Position)).ToArray()
                ?? new[] { new StationActorPlacement(_definition.Combat.Encounters.Single(item => item.Id == encounter.EncounterId).HostileIds[0],
                    encounter.HostileSpawnPosition) }.Concat(encounter.AdditionalHostiles ?? []).ToArray();
            foreach (var enemy in enemies)
            {
                CheckLayoutStandingPoint(enemy.Position, $"{encounter.EncounterId} {enemy.ActorId}");
                CheckLayoutPath(pathfinder, crewId, entry, enemy.Position, $"{encounter.EncounterId} crew approach {enemy.ActorId}");
                CheckLayoutPath(pathfinder, enemy.ActorId, enemy.Position, entry, $"{encounter.EncounterId} hostile approach {enemy.ActorId}");
            }
        }
        InputCheck("All encounter entries and hostile spawns stand on connected navigable floor without snapping", true);

        var route = ReviewState();
        foreach (var (id, door) in _serviceDoors)
        {
            var available = route.Interactions.Single(item => item.Id.Value == id).State
                is InteractionState.Available or InteractionState.Completed;
            CheckLayoutDoor(pathfinder, door.NavigationLink, available, id);
        }
        CheckLayoutDoor(pathfinder, GetNode<NavigationLink3D>("NavigationLinks/Escape_airlock"),
            route.Interactions.Single(item => item.Id.Value == "interaction.evacuation_airlock").State == InteractionState.Completed,
            "evacuation airlock");
        InputCheck("Service doors and final airlock preserve route separation in the actual navigation map", true);
    }

    // Call once after an encounter enters Readying (or Active), before spending skills.
    // Only rejected typed commands are submitted; all gameplay state must stay unchanged.
    private void CheckActiveEncounterPitCommands()
    {
        var route = ReviewState();
        if (route.Encounter is not { Phase: EncounterPhase.Readying or EncounterPhase.Active } encounter
            || !_layoutCommandChecks.Add((encounter.Id, encounter.Attempt))) { return; }
        var area = encounter.Id == _definition!.Combat.SoloEncounter.Id ? "solo" : encounter.Id.Value.Split('.').Last();
        var medic = route.Party.FirstOrDefault(actor => actor.Id == _definition.Medic.Id);
        var before = JsonSerializer.Serialize(route, CaptureManifestJsonOptions);
        foreach (var pit in ReviewedLayoutPits.Where(item => item.Area == area))
        {
            var move = _session!.Execute(new MoveActorCommand(NextHumanCommandId("layout.void.move"),
                _definition.Protagonist.Id, pit.Center));
            LayoutRequire(!move.Accepted && move.RejectionCode == CommandRejectionCode.DestinationUnreachable,
                $"{area} pit movement rejects as unreachable");
            if (medic is null) { continue; }
            var target = new PositionAbilityTarget(pit.Center);
            LayoutRequire(_session.CheckHealingFieldPlacement(medic.Id, target) == CommandRejectionCode.InvalidAbilityTarget,
                $"{area} pit healing preview rejects invalid ground");
            var field = _session.Execute(new UseAbilityCommand(NextHumanCommandId("layout.void.field"),
                medic.Id, _definition.Combat.HealingField.Id, target));
            LayoutRequire(!field.Accepted && field.RejectionCode == CommandRejectionCode.InvalidAbilityTarget,
                $"{area} pit healing command rejects invalid ground");
        }
        LayoutRequire(JsonSerializer.Serialize(ReviewState(), CaptureManifestJsonOptions) == before,
            $"{area} rejected pit commands preserve positions, actions, attack intent, health, and cooldowns");
        InputCheck($"{area} attempt {encounter.Attempt}: pit move/field validation is atomic", true);
    }

    private void CheckLayoutStandingPoint(WorldPosition point, string name)
    {
        var closest = NavigationServer3D.MapGetClosestPoint(GetWorld3D().NavigationMap, ToGodot(point));
        LayoutRequire(ToCore(closest).DistanceTo(point) <= .02, $"{name} is on navigation without snapping ({point})");
        LayoutRequire(LayoutFloorAt(point), $"{name} has pickable supporting floor ({point})");
    }

    private bool LayoutFloorAt(WorldPosition point) => CastRay(ToGodot(point) + Vector3.Up * .5f,
        ToGodot(point) - Vector3.Up * .05f, FloorCollisionLayer).Count > 0;

    private static void CheckLayoutPath(GodotSpatialPathfinder pathfinder, EntityId actorId,
        WorldPosition origin, WorldPosition destination, string name)
    {
        var path = pathfinder.FindPath(actorId, origin, destination);
        LayoutRequire(path.IsReachable && path.Waypoints.Count > 0 && path.Waypoints[^1].DistanceTo(destination) <= .02,
            $"{name} reaches its exact destination");
        var previous = origin;
        foreach (var point in path.Waypoints)
        {
            foreach (var pit in ReviewedLayoutPits)
            { LayoutRequire(!LayoutSegmentCrossesPit(previous, point, pit), $"{name} crosses {pit.Area} pit or its actor clearance"); }
            previous = point;
        }
    }

    private void CheckLayoutDoor(GodotSpatialPathfinder pathfinder, NavigationLink3D link, bool available, string name)
    {
        var path = pathfinder.FindPath(_definition!.Protagonist.Id,
            ToCore(link.ToGlobal(link.StartPosition)), ToCore(link.ToGlobal(link.EndPosition)));
        LayoutRequire(link.Enabled == available && path.IsReachable == available,
            $"{name} navigation is {(available ? "available" : "blocked")} with the route state");
    }

    private static bool LayoutSegmentCrossesPit(WorldPosition start, WorldPosition end, LayoutPit pit)
    {
        // Slab intersection includes the authored 0.4 m clearance, with 5 mm
        // tolerance for floating-point paths that run exactly along a nav edge.
        var minimum = 0.0;
        var maximum = 1.0;
        return IntersectAxis(start.X, end.X - start.X, pit.MinX - .395, pit.MaxX + .395, ref minimum, ref maximum)
            && IntersectAxis(start.Z, end.Z - start.Z, pit.MinZ - .395, pit.MaxZ + .395, ref minimum, ref maximum);

        static bool IntersectAxis(double origin, double delta, double low, double high, ref double minimum, ref double maximum)
        {
            if (Math.Abs(delta) < .000001) { return origin > low && origin < high; }
            var first = (low - origin) / delta;
            var second = (high - origin) / delta;
            minimum = Math.Max(minimum, Math.Min(first, second));
            maximum = Math.Min(maximum, Math.Max(first, second));
            return minimum <= maximum;
        }
    }

    private static void LayoutRequire(bool condition, string message)
    {
        if (!condition) { throw new InvalidOperationException($"Station tactical layout check failed: {message}."); }
    }

    private readonly record struct LayoutPit(string Area, double MinX, double MaxX, double MinZ, double MaxZ)
    {
        public WorldPosition Center => new((MinX + MaxX) / 2, 0, (MinZ + MaxZ) / 2);
    }
}
