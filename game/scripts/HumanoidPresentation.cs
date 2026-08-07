using Godot;

namespace SpaceAdventure.Game;

public enum HumanoidPresentationAction
{
    Idle,
    Locomotion,
    DialogueSpeak,
    DialogueListen,
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

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float BlendSeconds { get; set; } = 0.16f;

    private AnimationPlayer _animationPlayer = null!;
    private StringName? _currentAnimation;

    public HumanoidPresentationAction CurrentAction { get; private set; }

    public bool PlaybackPaused => Mathf.IsZeroApprox(_animationPlayer.SpeedScale);

    public int SkeletonBoneCount => FindDescendant<Skeleton3D>(this)?.GetBoneCount() ?? 0;

    public override void _Ready()
    {
        _animationPlayer = FindDescendant<AnimationPlayer>(this)
            ?? throw new InvalidOperationException(
                $"Humanoid presentation '{GetPath()}' must contain an imported AnimationPlayer.");

        if (string.IsNullOrWhiteSpace(IdleAnimationName))
        {
            throw new InvalidOperationException(
                $"Humanoid presentation '{GetPath()}' requires an idle animation name.");
        }

        foreach (var animationName in ConfiguredAnimationNames())
        {
            ValidateAndLoop(animationName);
        }

        Play(HumanoidPresentationAction.Idle);
    }

    public void Synchronize(
        bool active,
        HumanoidPresentationAction action,
        bool paused,
        Vector3 movementDirection)
    {
        Visible = active;
        if (!active)
        {
            return;
        }

        _animationPlayer.SpeedScale = paused ? 0.0f : 1.0f;
        Play(action);

        if (action != HumanoidPresentationAction.Locomotion)
        {
            return;
        }

        FaceDirection(movementDirection);
    }

    public void FaceDirection(Vector3 direction)
    {
        var planarDirection = new Vector3(direction.X, 0.0f, direction.Z);
        if (planarDirection.LengthSquared() <= 0.000001f)
        {
            return;
        }

        planarDirection = planarDirection.Normalized();
        var targetYaw = Mathf.Atan2(planarDirection.X, planarDirection.Z);
        Rotation = new Vector3(0.0f, Mathf.LerpAngle(Rotation.Y, targetYaw, 0.28f), 0.0f);
    }

    public bool HasConfiguredAnimation(HumanoidPresentationAction action)
    {
        var name = AnimationNameFor(action);
        return !string.IsNullOrWhiteSpace(name) && _animationPlayer.HasAnimation(name);
    }

    private IEnumerable<StringName> ConfiguredAnimationNames()
    {
        return new[]
            {
                IdleAnimationName,
                LocomotionAnimationName,
                DialogueSpeakAnimationName,
                DialogueListenAnimationName,
            }
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .Select(name => new StringName(name));
    }

    private void ValidateAndLoop(StringName animationName)
    {
        if (!_animationPlayer.HasAnimation(animationName))
        {
            var available = string.Join(", ", _animationPlayer.GetAnimationList()
                .Select(name => name.ToString()));
            throw new InvalidOperationException(
                $"Humanoid presentation '{GetPath()}' is missing required animation "
                + $"'{animationName}'. Available animations: {available}.");
        }

        _animationPlayer.GetAnimation(animationName).LoopMode = Animation.LoopModeEnum.Linear;
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
        if (_currentAnimation == name && _animationPlayer.IsPlaying())
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
