using Godot;

namespace SpaceAdventure.Game;

public readonly record struct OcclusionPositionObservation(float X, float Y, float Z);

public sealed record WallOcclusionObservation(
    string Id,
    bool Desired,
    float Blend,
    bool Settled);

public sealed record CameraOcclusionObservation(
    int SchemaVersion,
    string Algorithm,
    bool TargetAvailable,
    OcclusionPositionObservation? Target,
    string[] DesiredCutawayIds,
    bool AllSettled,
    WallOcclusionObservation[] Walls);

public partial class TacticalCameraController : Camera3D
{
    private const string OcclusionAlgorithm = "expanded_world_aabb_segment_v1";
    private const float MinimumPitch = 0.45f;
    private const float MaximumPitch = 1.15f;
    private const float MinimumDistance = 7.5f;
    private const float MaximumDistance = 20.0f;
    private const float DefaultPitch = 0.90f;
    private const float DefaultYaw = 0.68f;
    private const float DefaultDistance = 14.5f;
    private const float OcclusionEntryRadius = 0.75f;
    private const float OcclusionReleaseRadius = 0.90f;
    private const float OcclusionTargetHeight = 0.90f;
    private const float CutawayStubHeight = 0.45f;
    private const float CutawayTransitionSeconds = 0.15f;

    private static readonly StringName CameraOccluderGroup = new("camera_occluder");
    private static readonly StringName OccluderIdMetadata = new("occluder_id");
    private static readonly Vector3 InitialFocus = new(2.7f, 0.0f, 2.3f);

    private readonly List<OccludingWall> _occludingWalls = [];

    private Vector3 _focus = InitialFocus;
    private Vector3 _followTarget;
    private float _yaw = DefaultYaw;
    private float _pitch = DefaultPitch;
    private float _distance = DefaultDistance;
    private bool _hasFollowTarget;
    private bool _inputEnabled = true;
    private bool _rotating;

    public bool InputEnabled
    {
        get => _inputEnabled;
        set
        {
            _inputEnabled = value;
            if (!value)
            {
                _rotating = false;
            }
        }
    }

    public Vector3 FocusPoint
    {
        get => _focus;
        set
        {
            ArgumentOutOfRangeException.ThrowIfEqual(value.IsFinite(), false, nameof(value));
            _focus = value;
            UpdateTransform();
        }
    }

    public float YawRadians
    {
        get => _yaw;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNotEqual(float.IsFinite(value), true, nameof(value));
            _yaw = value;
            UpdateTransform();
        }
    }

    public float PitchRadians
    {
        get => _pitch;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNotEqual(float.IsFinite(value), true, nameof(value));
            _pitch = Mathf.Clamp(value, MinimumPitch, MaximumPitch);
            UpdateTransform();
        }
    }

    public float DistanceMeters
    {
        get => _distance;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNotEqual(float.IsFinite(value), true, nameof(value));
            _distance = Mathf.Clamp(value, MinimumDistance, MaximumDistance);
            UpdateTransform();
        }
    }

    public Vector3 FollowTarget
    {
        get => _followTarget;
        set
        {
            ArgumentOutOfRangeException.ThrowIfEqual(value.IsFinite(), false, nameof(value));
            _followTarget = value;
            _hasFollowTarget = true;
        }
    }

    public override void _Ready()
    {
        ProcessPriority = 10;
        CacheOccludingWalls();
        Current = true;
        UpdateTransform();
    }

    public override void _Process(double delta)
    {
        var seconds = (float)delta;
        if (InputEnabled)
        {
            ProcessCameraInput(seconds);
        }

        UpdateTransform();
        RefreshDesiredOcclusion();
        AdvanceCutawayAnimation(seconds);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!InputEnabled)
        {
            return;
        }

        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Middle } middle:
                _rotating = middle.Pressed;
                GetViewport().SetInputAsHandled();
                break;
            case InputEventMouseMotion motion when _rotating:
                _yaw -= motion.Relative.X * 0.008f;
                _pitch = Mathf.Clamp(
                    _pitch + (motion.Relative.Y * 0.006f),
                    MinimumPitch,
                    MaximumPitch);
                GetViewport().SetInputAsHandled();
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp }:
                _distance = Mathf.Clamp(_distance - 1.2f, MinimumDistance, MaximumDistance);
                GetViewport().SetInputAsHandled();
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelDown }:
                _distance = Mathf.Clamp(_distance + 1.2f, MinimumDistance, MaximumDistance);
                GetViewport().SetInputAsHandled();
                break;
            case InputEventKey { Pressed: true, Echo: false } key
                when key.PhysicalKeycode is Key.Home or Key.R:
                ResetOrientation();
                GetViewport().SetInputAsHandled();
                break;
            case InputEventKey { Pressed: true, Echo: false, PhysicalKeycode: Key.F }:
                FocusOn(_followTarget);
                GetViewport().SetInputAsHandled();
                break;
        }
    }

    public void FocusOn(Vector3 worldPosition)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(worldPosition.IsFinite(), false, nameof(worldPosition));
        FocusPoint = new Vector3(worldPosition.X, 0.0f, worldPosition.Z);
    }

    public void ResetOrientation()
    {
        _yaw = DefaultYaw;
        _pitch = DefaultPitch;
        _distance = DefaultDistance;
        UpdateTransform();
    }

    public void SnapOcclusionToDesiredState()
    {
        RefreshDesiredOcclusion();
        foreach (var wall in _occludingWalls)
        {
            wall.Blend = wall.Desired ? 1.0f : 0.0f;
            ApplyCutaway(wall);
        }
    }

    internal (Vector3 Forward, Vector3 Right) GetPanBasis()
    {
        var cameraOutward = new Vector3(Mathf.Sin(_yaw), 0.0f, Mathf.Cos(_yaw));
        var forward = -cameraOutward;
        return (forward, new Vector3(-forward.Z, 0.0f, forward.X));
    }

    public CameraOcclusionObservation ObserveOcclusion()
    {
        RefreshDesiredOcclusion();

        var desiredIds = _occludingWalls
            .Where(wall => wall.Desired)
            .Select(wall => wall.Id)
            .ToArray();
        var walls = _occludingWalls
            .Select(wall => new WallOcclusionObservation(
                wall.Id,
                wall.Desired,
                wall.Blend,
                IsSettled(wall)))
            .ToArray();

        return new CameraOcclusionObservation(
            SchemaVersion: 1,
            Algorithm: OcclusionAlgorithm,
            TargetAvailable: _hasFollowTarget,
            Target: _hasFollowTarget ? ObservePosition(GetOcclusionTarget()) : null,
            DesiredCutawayIds: desiredIds,
            AllSettled: walls.All(wall => wall.Settled),
            Walls: walls);
    }

    private void ProcessCameraInput(float seconds)
    {
        var (forward, right) = GetPanBasis();
        var movement = Vector3.Zero;

        if (Input.IsPhysicalKeyPressed(Key.W) || Input.IsPhysicalKeyPressed(Key.Up))
        {
            movement += forward;
        }
        if (Input.IsPhysicalKeyPressed(Key.S) || Input.IsPhysicalKeyPressed(Key.Down))
        {
            movement -= forward;
        }
        if (Input.IsPhysicalKeyPressed(Key.D) || Input.IsPhysicalKeyPressed(Key.Right))
        {
            movement += right;
        }
        if (Input.IsPhysicalKeyPressed(Key.A) || Input.IsPhysicalKeyPressed(Key.Left))
        {
            movement -= right;
        }

        if (!movement.IsZeroApprox())
        {
            _focus += movement.Normalized() * (5.5f + (_distance * 0.2f)) * seconds;
        }

        var rotationDirection = 0.0f;
        if (Input.IsPhysicalKeyPressed(Key.Q))
        {
            rotationDirection += 1.0f;
        }
        if (Input.IsPhysicalKeyPressed(Key.E))
        {
            rotationDirection -= 1.0f;
        }
        _yaw += rotationDirection * 1.4f * seconds;

        var pitchDirection = 0.0f;
        if (Input.IsPhysicalKeyPressed(Key.Pageup))
        {
            pitchDirection += 1.0f;
        }
        if (Input.IsPhysicalKeyPressed(Key.Pagedown))
        {
            pitchDirection -= 1.0f;
        }
        _pitch = Mathf.Clamp(_pitch + (pitchDirection * 0.9f * seconds), MinimumPitch, MaximumPitch);
    }

    private void CacheOccludingWalls()
    {
        _occludingWalls.Clear();
        var taggedNodes = GetTree().GetNodesInGroup(CameraOccluderGroup);
        foreach (var node in taggedNodes)
        {
            if (node is not MeshInstance3D mesh)
            {
                throw new InvalidOperationException(
                    $"Camera occluder '{node.GetPath()}' must be a MeshInstance3D.");
            }
            if (!mesh.HasMeta(OccluderIdMetadata))
            {
                throw new InvalidOperationException(
                    $"Camera occluder '{mesh.GetPath()}' must define occluder_id metadata.");
            }

            var id = mesh.GetMeta(OccluderIdMetadata).AsString();
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new InvalidOperationException(
                    $"Camera occluder '{mesh.GetPath()}' has an empty occluder_id.");
            }
            if (_occludingWalls.Any(wall => string.Equals(wall.Id, id, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException($"Duplicate camera occluder ID '{id}'.");
            }

            var localBounds = mesh.GetAabb().Abs();
            var fullGlobalTransform = mesh.GlobalTransform;
            var fullHeightMeters = localBounds.Size.Y * fullGlobalTransform.Basis.Y.Length();
            if (!localBounds.IsFinite() || fullHeightMeters <= CutawayStubHeight)
            {
                throw new InvalidOperationException(
                    $"Camera occluder '{id}' must have finite bounds taller than the cutaway stub.");
            }
            if (mesh.Scale.Y <= 0.0f
                || fullGlobalTransform.Basis.Y.Normalized().Dot(Vector3.Up) < 0.999f)
            {
                throw new InvalidOperationException(
                    $"Camera occluder '{id}' must have a positive, world-up local Y axis.");
            }

            _occludingWalls.Add(new OccludingWall(
                id,
                mesh,
                localBounds,
                fullGlobalTransform,
                mesh.Position,
                mesh.Scale,
                fullHeightMeters));
        }

        _occludingWalls.Sort(static (left, right) =>
            string.Compare(left.Id, right.Id, StringComparison.Ordinal));
    }

    private void RefreshDesiredOcclusion()
    {
        if (!_hasFollowTarget)
        {
            foreach (var wall in _occludingWalls)
            {
                wall.Desired = false;
            }
            return;
        }

        var target = GetOcclusionTarget();
        foreach (var wall in _occludingWalls)
        {
            var radius = wall.Desired ? OcclusionReleaseRadius : OcclusionEntryRadius;
            wall.Desired = wall.FullWorldBounds
                .Grow(radius)
                .IntersectsSegment(GlobalPosition, target);
        }
    }

    private void AdvanceCutawayAnimation(float seconds)
    {
        var blendStep = seconds / CutawayTransitionSeconds;
        foreach (var wall in _occludingWalls)
        {
            var targetBlend = wall.Desired ? 1.0f : 0.0f;
            wall.Blend = Mathf.MoveToward(wall.Blend, targetBlend, blendStep);
            ApplyCutaway(wall);
        }
    }

    private static void ApplyCutaway(OccludingWall wall)
    {
        var visibleHeight = Mathf.Lerp(wall.FullHeightMeters, CutawayStubHeight, wall.Blend);
        var scaleY = wall.FullScale.Y * (visibleHeight / wall.FullHeightMeters);
        var scale = wall.FullScale;
        scale.Y = scaleY;
        wall.Mesh.Scale = scale;

        var fullBottom = wall.FullPosition.Y + (wall.LocalBounds.Position.Y * wall.FullScale.Y);
        var position = wall.FullPosition;
        position.Y = fullBottom - (wall.LocalBounds.Position.Y * scaleY);
        wall.Mesh.Position = position;
    }

    private static bool IsSettled(OccludingWall wall)
    {
        return Mathf.IsEqualApprox(wall.Blend, wall.Desired ? 1.0f : 0.0f);
    }

    private static OcclusionPositionObservation ObservePosition(Vector3 position)
    {
        return new OcclusionPositionObservation(position.X, position.Y, position.Z);
    }

    private Vector3 GetOcclusionTarget()
    {
        return _followTarget + (Vector3.Up * OcclusionTargetHeight);
    }

    private void UpdateTransform()
    {
        var horizontalDistance = Mathf.Cos(_pitch) * _distance;
        var offset = new Vector3(
            Mathf.Sin(_yaw) * horizontalDistance,
            Mathf.Sin(_pitch) * _distance,
            Mathf.Cos(_yaw) * horizontalDistance);

        GlobalPosition = _focus + offset;
        LookAt(_focus + new Vector3(0.0f, 0.35f, 0.0f), Vector3.Up);
    }

    private sealed class OccludingWall
    {
        public OccludingWall(
            string id,
            MeshInstance3D mesh,
            Aabb localBounds,
            Transform3D fullGlobalTransform,
            Vector3 fullPosition,
            Vector3 fullScale,
            float fullHeightMeters)
        {
            Id = id;
            Mesh = mesh;
            LocalBounds = localBounds;
            FullGlobalTransform = fullGlobalTransform;
            FullWorldBounds = fullGlobalTransform * localBounds;
            FullPosition = fullPosition;
            FullScale = fullScale;
            FullHeightMeters = fullHeightMeters;
        }

        public string Id { get; }

        public MeshInstance3D Mesh { get; }

        public Aabb LocalBounds { get; }

        public Transform3D FullGlobalTransform { get; }

        public Aabb FullWorldBounds { get; }

        public Vector3 FullPosition { get; }

        public Vector3 FullScale { get; }

        public float FullHeightMeters { get; }

        public bool Desired { get; set; }

        public float Blend { get; set; }
    }
}
