using Godot;

namespace SpaceAdventure.Game;

public enum HumanoidPresentationAction
{
    Idle,
    Locomotion,
    DialogueSpeak,
    DialogueListen,
    MeleeStrike,
    Down,
}

public partial class HumanoidPresentation : Node3D
{
    [Export]
    public string IdleAnimationName { get; set; } = string.Empty;

    [Export]
    public string LocomotionAnimationName { get; set; } = string.Empty;

    [Export]
    public string DialogueSpeakAnimationName { get; set; } = string.Empty;

    [Export]
    public string DialogueListenAnimationName { get; set; } = string.Empty;

    [Export]
    public string MeleeStrikeAnimationName { get; set; } = string.Empty;

    [Export]
    public string DownAnimationName { get; set; } = string.Empty;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float BlendSeconds { get; set; } = 0.16f;

    private AnimationPlayer _animationPlayer = null!;
    private SkeletalPosePlayer _posePlayer = null!;
    private double _fallbackTick;
    private double _actionStartedTick;
    private StringName? _currentAnimation;

    public HumanoidPresentationAction CurrentAction { get; private set; }

    public float LocomotionPlaybackRate { get; set; } = 1;

    public bool PlaybackPaused => Mathf.IsZeroApprox(_animationPlayer.SpeedScale);

    public int SkeletonBoneCount => FindDescendant<Skeleton3D>(this)?.GetBoneCount() ?? 0;

    public override void _Ready()
    {
        _animationPlayer = FindDescendant<AnimationPlayer>(this)
            ?? throw new InvalidOperationException(
                $"Humanoid presentation '{GetPath()}' must contain an imported AnimationPlayer.");
        _posePlayer = new SkeletalPosePlayer(_animationPlayer, FindDescendant<Skeleton3D>(this)!);

        if (string.IsNullOrWhiteSpace(IdleAnimationName))
        {
            throw new InvalidOperationException(
                $"Humanoid presentation '{GetPath()}' requires an idle animation name.");
        }

        foreach (var (animationName, loop) in ConfiguredAnimations())
        {
            ValidateAnimation(animationName, loop);
        }

        Play(HumanoidPresentationAction.Idle);
    }

    public void Synchronize(
        bool active,
        HumanoidPresentationAction action,
        bool paused,
        Vector3 movementDirection,
        float playbackSpeed = 1.0f,
        bool seekToEndWhenPaused = false,
        double presentationTick = double.NaN,
        double? clipSeconds = null,
        long cycle = 0,
        float turnDeltaSeconds = 1.0f / 60.0f,
        bool snapToPose = false)
    {
        Visible = active;
        if (!active)
        {
            return;
        }

        _animationPlayer.SpeedScale = paused ? 0.0f : playbackSpeed;
        if (double.IsNaN(presentationTick))
        {
            if (!paused) { _fallbackTick += GetProcessDeltaTime() * 30; }
            presentationTick = _fallbackTick;
        }
        var name = new StringName(AnimationNameFor(action));
        if (action != CurrentAction) { _actionStartedTick = presentationTick; }
        var rate = playbackSpeed * (action == HumanoidPresentationAction.Locomotion
            ? LocomotionPlaybackRate : AnimationPacing.Rate);
        var sampleSeconds = clipSeconds ?? (action == HumanoidPresentationAction.Down
            ? Math.Max(0, presentationTick - _actionStartedTick) / 30 * rate
            : presentationTick / 30 * rate);
        if (paused && seekToEndWhenPaused)
        {
            sampleSeconds = _animationPlayer.GetAnimation(name).Length;
        }
        _posePlayer.Sample(name, sampleSeconds, presentationTick / 30, cycle,
            snapToPose || paused && seekToEndWhenPaused ? 0 : BlendSeconds / AnimationPacing.Rate);
        CurrentAction = action;

        if (action != HumanoidPresentationAction.Locomotion)
        {
            return;
        }

        FaceDirection(movementDirection, turnDeltaSeconds);
    }

    public void FaceDirection(Vector3 direction, float deltaSeconds = 1.0f / 60.0f)
    {
        var planarDirection = new Vector3(direction.X, 0.0f, direction.Z);
        if (planarDirection.LengthSquared() <= 0.000001f)
        {
            return;
        }

        planarDirection = planarDirection.Normalized();
        var targetYaw = Mathf.Atan2(planarDirection.X, planarDirection.Z);
        Rotation = new Vector3(0.0f, Mathf.LerpAngle(Rotation.Y, targetYaw, 1 - Mathf.Exp(-16 * AnimationPacing.Rate * deltaSeconds)), 0.0f);
    }

    public double ClipLength(HumanoidPresentationAction action) =>
        _animationPlayer.GetAnimation(new StringName(AnimationNameFor(action))).Length;

    internal double? HeadHeightMeters
    {
        get
        {
            var skeleton = FindDescendant<Skeleton3D>(this)!;
            var head = skeleton.FindBone("mixamorig_Head");
            return head < 0 ? null : (skeleton.GlobalTransform * skeleton.GetBoneGlobalPose(head)).Origin.Y - GlobalPosition.Y;
        }
    }

    public object GetDiagnostics() => new
    { clip = _posePlayer.ClipName, clip_seconds = _posePlayer.ClipTime, head_height_m = HeadHeightMeters };

    public bool HasConfiguredAnimation(HumanoidPresentationAction action)
    {
        var name = AnimationNameFor(action);
        return !string.IsNullOrWhiteSpace(name) && _animationPlayer.HasAnimation(name);
    }

    private IEnumerable<(StringName Name, bool Loop)> ConfiguredAnimations()
    {
        return new[]
            {
                (IdleAnimationName, true),
                (LocomotionAnimationName, true),
                (DialogueSpeakAnimationName, true),
                (DialogueListenAnimationName, true),
                (MeleeStrikeAnimationName, false),
                (DownAnimationName, false),
            }
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Item1))
            .GroupBy(entry => entry.Item1, StringComparer.Ordinal)
            .Select(group => (new StringName(group.Key), group.First().Item2));
    }

    private void ValidateAnimation(StringName animationName, bool loop)
    {
        if (!_animationPlayer.HasAnimation(animationName))
        {
            var available = string.Join(", ", _animationPlayer.GetAnimationList()
                .Select(name => name.ToString()));
            throw new InvalidOperationException(
                $"Humanoid presentation '{GetPath()}' is missing required animation "
                + $"'{animationName}'. Available animations: {available}.");
        }

        _animationPlayer.GetAnimation(animationName).LoopMode = loop
            ? Animation.LoopModeEnum.Linear
            : Animation.LoopModeEnum.None;
    }

    private void Play(HumanoidPresentationAction action)
    {
        var animationName = AnimationNameFor(action);
        if (string.IsNullOrWhiteSpace(animationName))
        {
            throw new InvalidOperationException(
                $"Humanoid presentation '{GetPath()}' does not configure action '{action}'.");
        }

        var name = new StringName(animationName);
        if (_currentAnimation == name)
        {
            CurrentAction = action;
            return;
        }

        _animationPlayer.Play(name, customBlend: BlendSeconds);
        _currentAnimation = name;
        CurrentAction = action;
    }

    private string AnimationNameFor(HumanoidPresentationAction action)
    {
        return action switch
        {
            HumanoidPresentationAction.Idle => IdleAnimationName,
            HumanoidPresentationAction.Locomotion => LocomotionAnimationName,
            HumanoidPresentationAction.DialogueSpeak => DialogueSpeakAnimationName,
            HumanoidPresentationAction.DialogueListen => DialogueListenAnimationName,
            HumanoidPresentationAction.MeleeStrike => MeleeStrikeAnimationName,
            HumanoidPresentationAction.Down => DownAnimationName,
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null),
        };
    }

    private static T? FindDescendant<T>(Node root)
        where T : Node
    {
        foreach (var child in root.GetChildren())
        {
            if (child is T match)
            {
                return match;
            }

            if (FindDescendant<T>(child) is T descendant)
            {
                return descendant;
            }
        }

        return null;
    }
}
