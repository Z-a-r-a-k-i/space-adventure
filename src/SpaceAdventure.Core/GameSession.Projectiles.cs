namespace SpaceAdventure.Core;

public sealed partial class GameSession
{
    private long _projectileSequence;

    private void LaunchSentryProjectile(StationRouteRuntime station, HostileRuntime source, ActorRuntime target, AttackDefinition attack)
    {
        var displacement = new WorldPosition(target.Position.X - source.Position.X, 0, target.Position.Z - source.Position.Z);
        var toward = displacement.DistanceTo(default) < .001 ? new WorldPosition(0, 0, -1) : NormalizeFacing(displacement);
        var origin = new WorldPosition(source.Position.X + toward.X * .65, source.Position.Y + 1.62, source.Position.Z + toward.Z * .65);
        var destination = new WorldPosition(target.Position.X, target.Position.Y + 1.1, target.Position.Z);
        var ticks = Math.Max(1, (int)Math.Ceiling(origin.DistanceTo(destination) / attack.ProjectileSpeedMetersPerSecond * TicksPerSecond));
        var projectile = new ProjectileRuntime(++_projectileSequence, source.Id, target.Id, attack.Id,
            origin, destination, Tick, ticks, attack.Damage);
        station.Combat.Projectiles.Add(projectile);
        Record(GameplayEventType.ProjectileLaunched, detail: projectile.Event());
    }

    private void AdvanceIncomingProjectiles(StationRouteRuntime station)
    {
        foreach (var projectile in station.Combat.Projectiles.ToArray())
        {
            if (station.Combat.Phase != EncounterPhase.Active) { break; }
            var progress = Math.Clamp((double)(Tick - projectile.ReleasedAtTick) / projectile.FlightTicks, 0, 1);
            var next = LerpPosition(projectile.Origin, projectile.Destination, progress);
            if (IntersectBarrier(station, projectile.Position, next, out var blockedAt))
            {
                station.Combat.Projectiles.Remove(projectile);
                Record(GameplayEventType.ProjectileBlocked, detail: projectile.Event(blockedAt, blocked: true));
                continue;
            }
            projectile.Position = next;
            if (progress < 1) { continue; }
            station.Combat.Projectiles.Remove(projectile);
            Record(GameplayEventType.ProjectileImpacted, detail: projectile.Event(next));
            var target = station.Actors[projectile.TargetId];
            var targetCenter = new WorldPosition(target.Position.X, target.Position.Y + 1.1, target.Position.Z);
            if (target.Health > 0 && next.DistanceTo(targetCenter) <= .6)
            { DamagePartyActor(station, target, projectile.SourceId, projectile.Damage, projectile.AttackId); }
        }
    }

    private static WorldPosition LerpPosition(WorldPosition from, WorldPosition to, double fraction) =>
        new(from.X + (to.X - from.X) * fraction, from.Y + (to.Y - from.Y) * fraction, from.Z + (to.Z - from.Z) * fraction);

    private sealed class ProjectileRuntime(long id, EntityId sourceId, EntityId targetId, AttackId attackId,
        WorldPosition origin, WorldPosition destination, long releasedAtTick, int flightTicks, int damage)
    {
        public long Id { get; } = id;
        public EntityId SourceId { get; } = sourceId;
        public EntityId TargetId { get; } = targetId;
        public AttackId AttackId { get; } = attackId;
        public WorldPosition Origin { get; } = origin;
        public WorldPosition Destination { get; } = destination;
        public WorldPosition Position { get; set; } = origin;
        public long ReleasedAtTick { get; } = releasedAtTick;
        public int FlightTicks { get; } = flightTicks;
        public int Damage { get; } = damage;
        public ProjectileObservation Observe() => new(Id, SourceId, TargetId, AttackId, Origin, Destination, Position, ReleasedAtTick, FlightTicks);
        public ProjectileEventDetail Event(WorldPosition? impact = null, bool blocked = false) =>
            new(Id, SourceId, TargetId, AttackId, Origin, Destination, FlightTicks, impact, blocked);
    }
}
