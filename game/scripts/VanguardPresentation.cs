using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

public partial class VanguardPresentation : Node3D
{
    // Godot's glTF importer sanitizes the canonical dotted action names.
    private static readonly StringName IdleHolstered = "anim_humanoid_idle_holstered";
    private static readonly StringName WalkHolstered = "anim_humanoid_walk_holstered";
    private static readonly StringName DrawPrimary = "anim_humanoid_draw_primary";
    private static readonly StringName IdleArmed = "anim_humanoid_idle_armed";
    private static readonly StringName LocomotionArmed = "anim_humanoid_locomotion_armed";
    private static readonly StringName AttackPrimary = "anim_humanoid_attack_primary";
    private static readonly StringName HolsterPrimary = "anim_humanoid_holster_primary";
    private static readonly StringName Down = "anim_humanoid_down";

    private AnimationPlayer _animationPlayer = null!;
    private Node3D _weapon = null!;
    private Node3D _handSocket = null!;
    private Node3D _holsterSocket = null!;
    private StringName? _currentAnimation;
    private bool? _weaponInHand;

    public override void _Ready()
    {
        _animationPlayer = FindDescendant<AnimationPlayer>(this)
            ?? throw new InvalidOperationException(
                "The Vanguard presentation must contain an imported AnimationPlayer.");
        _weapon = GetNode<Node3D>("Weapon");
        _handSocket = FindSocket("socket.weapon.hand_primary");
        _holsterSocket = FindSocket("socket.weapon.holster_primary");

        foreach (var (animation, loop) in new[]
        {
            (IdleHolstered, true),
            (WalkHolstered, true),
            (DrawPrimary, false),
            (IdleArmed, true),
            (LocomotionArmed, true),
            (AttackPrimary, false),
            (HolsterPrimary, false),
            (Down, false),
        })
        {
            ValidateAnimation(animation, loop);
        }

        AttachWeapon(inHand: false);
        Play(IdleHolstered);
    }

    public void Synchronize(
        bool active,
        bool moving,
        bool paused,
        Vector3 direction,
        EncounterObservation? encounter,
        PrimaryActionObservation? currentAction)
    {
        Visible = active;
        if (!active)
        {
            return;
        }

        var animation = IdleHolstered;
        var speed = 1.0f;
        var seekToEnd = false;
        var weaponInHand = false;

        if (encounter is not null)
        {
            switch (encounter.Phase)
            {
                case EncounterPhase.Readying:
                    animation = DrawPrimary;
                    speed = SpeedForTicks(DrawPrimary, encounter.TransitionTicksTotal);
                    weaponInHand = TransitionProgress(encounter) >= 0.48f;
                    break;
                case EncounterPhase.Active:
                    weaponInHand = true;
                    if (currentAction is
                    {
                        Kind: PrimaryActionKind.Attack or PrimaryActionKind.Ability,
                        Phase: PrimaryActionPhase.Windup,
                    })
                    {
                        animation = AttackPrimary;
                        speed = SpeedForTicks(AttackPrimary, currentAction.PhaseTicksTotal);
                    }
                    else
                    {
                        animation = moving ? LocomotionArmed : IdleArmed;
                    }
                    break;
                case EncounterPhase.Securing:
                    animation = HolsterPrimary;
                    speed = SpeedForTicks(HolsterPrimary, encounter.TransitionTicksTotal);
                    weaponInHand = TransitionProgress(encounter) < 0.58f;
                    break;
                case EncounterPhase.Defeat:
                    animation = Down;
                    weaponInHand = true;
                    seekToEnd = paused;
                    break;
                case EncounterPhase.Victory:
                case EncounterPhase.Dormant:
                    animation = moving ? WalkHolstered : IdleHolstered;
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unsupported encounter phase '{encounter.Phase}'.");
            }
        }
        else
        {
            animation = moving ? WalkHolstered : IdleHolstered;
        }

        AttachWeapon(weaponInHand);
        _animationPlayer.SpeedScale = paused ? 0.0f : speed;
        Play(animation);
        if (seekToEnd)
        {
            _animationPlayer.Seek(_animationPlayer.GetAnimation(animation).Length, update: true);
        }
        FaceDirection(direction);
    }

    private static float TransitionProgress(EncounterObservation encounter)
    {
        if (encounter.TransitionTicksTotal <= 0)
        {
            return 1.0f;
        }

        return 1.0f - ((float)encounter.TransitionTicksRemaining / encounter.TransitionTicksTotal);
    }

    private float SpeedForTicks(StringName animation, int ticks)
    {
        if (ticks <= 0)
        {
            return 1.0f;
        }

        return Mathf.Max(0.05f, (float)_animationPlayer.GetAnimation(animation).Length * 30.0f / ticks);
    }

    private void FaceDirection(Vector3 direction)
    {
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

    private void AttachWeapon(bool inHand)
    {
        if (_weaponInHand == inHand)
        {
            return;
        }

        _weapon.Reparent(inHand ? _handSocket : _holsterSocket, keepGlobalTransform: false);
        _weapon.Transform = Transform3D.Identity;
        _weaponInHand = inHand;
    }

    private Node3D FindSocket(string canonicalName)
    {
        var sanitizedName = canonicalName.Replace('.', '_');
        return FindDescendants<Node3D>(this).FirstOrDefault(node =>
                string.Equals(node.Name, canonicalName, StringComparison.Ordinal)
                || string.Equals(node.Name, sanitizedName, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"The Vanguard GLB is missing required socket '{canonicalName}'.");
    }

    private void ValidateAnimation(StringName animationName, bool loop)
    {
        if (!_animationPlayer.HasAnimation(animationName))
        {
            var available = string.Join(", ", _animationPlayer.GetAnimationList()
                .Select(name => name.ToString()));
            throw new InvalidOperationException(
                $"The Vanguard GLB is missing required animation '{animationName}'. "
                + $"Available animations: {available}.");
        }

        _animationPlayer.GetAnimation(animationName).LoopMode = loop
            ? Animation.LoopModeEnum.Linear
            : Animation.LoopModeEnum.None;
    }

    private void Play(StringName animationName)
    {
        if (_currentAnimation == animationName)
        {
            return;
        }

        _animationPlayer.Play(animationName, customBlend: 0.10);
        _currentAnimation = animationName;
    }

    private static T? FindDescendant<T>(Node root)
        where T : Node => FindDescendants<T>(root).FirstOrDefault();

    private static IEnumerable<T> FindDescendants<T>(Node root)
        where T : Node
    {
        foreach (var child in root.GetChildren())
        {
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in FindDescendants<T>(child))
            {
                yield return descendant;
            }
        }
    }
}
