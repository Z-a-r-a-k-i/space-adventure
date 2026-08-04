using Godot;

namespace SpaceAdventure.Game;

public partial class VanguardPresentation : Node3D
{
    // Godot's glTF importer sanitizes the canonical dotted action names.
    private static readonly StringName IdleAnimation = "anim_humanoid_idle_holstered";
    private static readonly StringName WalkAnimation = "anim_humanoid_walk_holstered";

    private AnimationPlayer _animationPlayer = null!;
    private StringName? _currentAnimation;

    public override void _Ready()
    {
        _animationPlayer = FindDescendant<AnimationPlayer>(this)
            ?? throw new InvalidOperationException(
                "The Vanguard presentation must contain an imported AnimationPlayer.");

        ConfigureLoop(IdleAnimation);
        ConfigureLoop(WalkAnimation);
        Play(IdleAnimation);
    }

    public void Synchronize(bool active, bool moving, bool paused, Vector3 direction)
    {
        Visible = active;
        if (!active)
        {
            return;
        }

        _animationPlayer.SpeedScale = paused ? 0.0f : 1.0f;
        Play(moving ? WalkAnimation : IdleAnimation);

        var planarDirection = new Vector3(direction.X, 0.0f, direction.Z);
        if (planarDirection.LengthSquared() <= 0.000001f)
        {
            return;
        }

        planarDirection = planarDirection.Normalized();
        // The Mixamo Vanguard faces local +Z after the Blender/glTF conversion.
        var targetYaw = Mathf.Atan2(planarDirection.X, planarDirection.Z);
        Rotation = new Vector3(0.0f, Mathf.LerpAngle(Rotation.Y, targetYaw, 0.28f), 0.0f);
    }

    private void ConfigureLoop(StringName animationName)
    {
        if (!_animationPlayer.HasAnimation(animationName))
        {
            var available = string.Join(", ", _animationPlayer.GetAnimationList()
                .Select(name => name.ToString()));
            throw new InvalidOperationException(
                $"The Vanguard GLB is missing required animation '{animationName}'. "
                + $"Available animations: {available}.");
        }

        _animationPlayer.GetAnimation(animationName).LoopMode = Animation.LoopModeEnum.Linear;
    }

    private void Play(StringName animationName)
    {
        if (_currentAnimation == animationName && _animationPlayer.IsPlaying())
        {
            return;
        }

        _animationPlayer.Play(animationName, customBlend: 0.16);
        _currentAnimation = animationName;
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
