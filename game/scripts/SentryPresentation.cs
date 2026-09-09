using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

public partial class SentryPresentation : Node3D
{
    internal const float TurnDegreesPerSecond = 150 * AnimationPacing.Rate;
    private Node3D _aim = null!;
    private Node3D _recoil = null!;
    private Node3D _muzzle = null!;
    private Vector3 _rest;
    private float _yaw;
    private float _pitch;
    public Vector3 MuzzlePosition => _muzzle.GlobalPosition;
    public Vector3 MuzzleDirection => -_muzzle.GlobalBasis.Z.Normalized();

    public override void _Ready()
    {
        _aim = FindChild("Aim_Pivot", true, false) as Node3D
            ?? throw new InvalidOperationException("Sentry aim pivot is missing.");
        _recoil = FindChild("Recoil", true, false) as Node3D
            ?? throw new InvalidOperationException("Sentry recoil pivot is missing.");
        _muzzle = FindChild("socket_attack_muzzle_primary", true, false) as Node3D
            ?? FindChild("socket.attack.muzzle.primary", true, false) as Node3D
            ?? throw new InvalidOperationException("Sentry muzzle is missing.");
        // The retained GLB places its empty pivots at the floor. Rebase the
        // mechanical mount without moving the accepted geometry at rest.
        var housing = FindChild("Gun_Housing", true, false) as Node3D
            ?? throw new InvalidOperationException("Sentry gun housing is missing.");
        var barrel = FindChild("Barrel", true, false) as MeshInstance3D
            ?? throw new InvalidOperationException("Sentry barrel is missing.");
        var barrelBounds = Enumerable.Range(0, 8)
            .Select(index => ToLocal(barrel.GlobalTransform * barrel.GetAabb().GetEndpoint(index))).ToArray();
        var muzzle = new Vector3((barrelBounds.Min(point => point.X) + barrelBounds.Max(point => point.X)) / 2,
            (barrelBounds.Min(point => point.Y) + barrelBounds.Max(point => point.Y)) / 2,
            barrelBounds.Min(point => point.Z) - .015f);
        var children = _aim.GetChildren().OfType<Node3D>().Select(node => (Node: node, World: node.GlobalTransform)).ToArray();
        var mount = ToLocal(housing.GlobalPosition);
        _aim.GlobalPosition = ToGlobal(new Vector3(mount.X, muzzle.Y, mount.Z));
        foreach (var child in children) { child.Node.GlobalTransform = child.World; }
        _muzzle.GlobalTransform = new Transform3D(GlobalBasis.Orthonormalized(), ToGlobal(muzzle));
        _rest = _recoil.Position;
    }

    public void Synchronize(HostileObservation hostile, Vector3 target, double tick, float deltaSeconds)
    {
        var direction = ToLocal(target) - ToLocal(_aim.GlobalPosition);
        var horizontal = new Vector2(direction.X, direction.Z).Length();
        // A target under/behind the gun does not flip the head across both stops.
        if (!hostile.Combat.IsDefeated && horizontal >= .6f && direction.Z < 0)
        {
            var yaw = Mathf.Clamp(Mathf.Atan2(-direction.X, -direction.Z), -Mathf.Pi / 3, Mathf.Pi / 3);
            var pitch = Mathf.Clamp(Mathf.Atan2(direction.Y, horizontal), Mathf.DegToRad(-15), Mathf.DegToRad(25));
            var step = Mathf.DegToRad(TurnDegreesPerSecond) * Math.Max(0, deltaSeconds);
            _yaw = Mathf.MoveToward(_yaw, yaw, step);
            _pitch = Mathf.MoveToward(_pitch, pitch, step);
        }
        _aim.Rotation = new Vector3(hostile.Combat.IsDefeated ? -.26f : _pitch, _yaw, 0);
        var age = hostile.CurrentAction is { Phase: PrimaryActionPhase.Recovery, Interrupted: false } action
            ? (tick - action.PhaseStartedTick) / GameSession.TicksPerSecond * AnimationPacing.Rate : -1;
        var kick = age is >= 0 and < .3 ? .08f * (float)Math.Exp(-age * 15) : 0;
        _recoil.Position = _rest + Vector3.Back * kick;
    }

    public object GetDiagnostics() => new
    {
        aim_degrees = new[] { _aim.RotationDegrees.X, _aim.RotationDegrees.Y },
        recoil_m = _recoil.Position.Z - _rest.Z,
        muzzle_world = new[] { MuzzlePosition.X, MuzzlePosition.Y, MuzzlePosition.Z },
        mount_world = new[] { _aim.GlobalPosition.X, _aim.GlobalPosition.Y, _aim.GlobalPosition.Z },
    };
}
