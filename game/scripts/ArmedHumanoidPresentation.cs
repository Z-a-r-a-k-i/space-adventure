using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

public partial class ArmedHumanoidPresentation : Node3D
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
    private Skeleton3D _skeleton = null!;
    private SkeletalPosePlayer _posePlayer = null!;
    private Node3D _weapon = null!;
    private Node3D _handSocket = null!;
    private Node3D _holsterSocket = null!;
    private StringName? _currentAnimation;
    private bool? _weaponInHand;
    private Node3D _muzzle = null!;
    private Node3D _primaryGrip = null!;
    private Node3D _supportGrip = null!;
    private double _sampleTick;
    private TwoBoneIK3D _supportIk = null!;
    private HandOrientationModifier _supportOrientation = null!;
    private Node3D _supportTarget = null!;
    private Node3D _supportPole = null!;
    private BoneAttachment3D[] _boneAttachments = [];
    private int _rightHand;
    private int _leftHand;
    private int _rightArm;
    private int _rightForeArm;
    private int _leftArm;
    private int _leftForeArm;
    private int _spine;
    private float _rightPalmOffset;
    private float _leftPalmOffset;
    private float _primaryGripError;
    private float _supportGripError;
    private double _lastShotTick = double.NegativeInfinity;
    private int _attempt;
    private EncounterId? _encounterId;

    [Export] public bool OneHanded { get; set; }

    public bool StrongRecoil { get; set; }

    internal double LastShotTick => _lastShotTick;

    public float LocomotionPlaybackRate { get; set; } = 1;

    public double DownClipLengthSeconds => _animationPlayer.GetAnimation(Down).Length;

    public double DownDurationSeconds => DownClipLengthSeconds / AnimationPacing.Rate;

    public Vector3 MuzzlePosition => _muzzle.GlobalPosition;

    public Vector3 MuzzleDirection => -_muzzle.GlobalBasis.Z.Normalized();

    public override void _Ready()
    {
        _animationPlayer = FindDescendant<AnimationPlayer>(this)
            ?? throw new InvalidOperationException(
                "The armed presentation must contain an imported AnimationPlayer.");
        _weapon = GetNode<Node3D>("Weapon");
        _weapon.TopLevel = true;
        _skeleton = FindDescendant<Skeleton3D>(this)!;
        _posePlayer = new SkeletalPosePlayer(_animationPlayer, _skeleton);
        _muzzle = FindSocket("socket.attack.muzzle.primary");
        _primaryGrip = FindSocket("socket.grip.primary");
        _supportGrip = OneHanded ? _primaryGrip : FindSocket("socket.grip.support");
        _handSocket = FindSocket("socket.weapon.hand_primary");
        _holsterSocket = FindSocket("socket.weapon.holster_primary");
        ConfigureSupportHand();

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
        PrimaryActionObservation? currentAction,
        double presentationTick = 0,
        float turnDeltaSeconds = 1.0f / 60.0f,
        bool defeated = false,
        long? defeatedAtTick = null,
        Vector3? bodyFacing = null)
    {
        Visible = active;
        if (!active)
        {
            return;
        }

        var animation = IdleHolstered;
        _sampleTick = presentationTick;
        var clipSeconds = presentationTick / GameSession.TicksPerSecond
            * (moving ? LocomotionPlaybackRate : AnimationPacing.Rate);
        long cycle = 0;
        var weaponInHand = false;

        if (encounter is not null)
        {
            switch (encounter.Phase)
            {
                case EncounterPhase.Readying:
                    animation = DrawPrimary;
                    clipSeconds = TransitionProgress(encounter, presentationTick) * _animationPlayer.GetAnimation(DrawPrimary).Length;
                    weaponInHand = TransitionProgress(encounter, presentationTick) >= 0.25f;
                    cycle = encounter.Attempt;
                    break;
                case EncounterPhase.Active:
                    weaponInHand = true;
                    animation = moving ? LocomotionArmed : IdleArmed;
                    break;
                case EncounterPhase.Securing:
                    animation = HolsterPrimary;
                    clipSeconds = TransitionProgress(encounter, presentationTick) * _animationPlayer.GetAnimation(HolsterPrimary).Length;
                    weaponInHand = TransitionProgress(encounter, presentationTick) < 0.75f;
                    cycle = encounter.Attempt;
                    break;
                case EncounterPhase.Defeat:
                    animation = defeated ? Down : IdleArmed;
                    weaponInHand = true;
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

        if (defeated)
        {
            animation = Down;
            weaponInHand = true;
            clipSeconds = Math.Clamp((presentationTick - (defeatedAtTick ?? presentationTick))
                / GameSession.TicksPerSecond * AnimationPacing.Rate, 0, DownClipLengthSeconds);
            cycle = defeatedAtTick ?? 0;
        }
        _animationPlayer.SpeedScale = paused ? 0 : 1;
        var newAttempt = encounter?.Attempt != _attempt || encounter?.Id != _encounterId;
        if (newAttempt)
        {
            _lastShotTick = double.NegativeInfinity;
            _attempt = encounter?.Attempt ?? 0;
            _encounterId = encounter?.Id;
        }
        _posePlayer.Sample(animation, clipSeconds, presentationTick / GameSession.TicksPerSecond, cycle,
            blendSeconds: newAttempt ? 0 : 0.12 / AnimationPacing.Rate);
        if (!defeated)
        {
            if (bodyFacing is { } heading)
            {
                if (HumanoidPresentation.TryLocalYaw(this, heading, out var yaw)) { Rotation = new Vector3(0, yaw, 0); }
            }
            else { FaceDirection(direction, turnDeltaSeconds); }
        }
        if (currentAction is { Kind: PrimaryActionKind.Attack, Phase: PrimaryActionPhase.Recovery })
        {
            NotifyShot(currentAction.PhaseStartedTick);
        }
        if (!defeated && encounter?.Phase == EncounterPhase.Active)
        {
            ApplyAimAndRecoil(direction, moving);
        }
        foreach (var attachment in _boneAttachments) { attachment.OnSkeletonUpdate(); }
        AttachWeapon(weaponInHand);
        if (FitSupportReach(SupportInfluence(defeated ? null : encounter, presentationTick)))
        {
            foreach (var attachment in _boneAttachments) { attachment.OnSkeletonUpdate(); }
            AttachWeapon(weaponInHand);
        }
        SynchronizeSupportHand(defeated ? null : encounter, presentationTick);
    }

    private static float TransitionProgress(EncounterObservation encounter, double presentationTick)
    {
        if (encounter.TransitionTicksTotal <= 0)
        {
            return 1.0f;
        }

        return (float)Math.Clamp((presentationTick - encounter.PhaseStartedTick) / encounter.TransitionTicksTotal, 0, 1);
    }

    public void NotifyShot(long releaseTick) => _lastShotTick = Math.Max(_lastShotTick, releaseTick);

    private void ConfigureSupportHand()
    {
        _rightHand = _skeleton.FindBone("mixamorig_RightHand");
        _leftHand = _skeleton.FindBone("mixamorig_LeftHand");
        _rightArm = _skeleton.FindBone("mixamorig_RightArm");
        _rightForeArm = _skeleton.FindBone("mixamorig_RightForeArm");
        _leftArm = _skeleton.FindBone("mixamorig_LeftArm");
        _leftForeArm = _skeleton.FindBone("mixamorig_LeftForeArm");
        _spine = _skeleton.FindBone("mixamorig_Spine2");
        var rig = FindDescendants<Node3D>(this).First(node => node.HasMeta("extras")
            && node.GetMeta("extras").VariantType == Variant.Type.Dictionary
            && node.GetMeta("extras").AsGodotDictionary().ContainsKey("right_palm_offset_m"));
        var extras = rig.GetMeta("extras").AsGodotDictionary();
        _rightPalmOffset = extras["right_palm_offset_m"].AsGodotArray()[1].AsSingle();
        _leftPalmOffset = extras["left_palm_offset_m"].AsGodotArray()[1].AsSingle();
        _boneAttachments = _skeleton.GetChildren().OfType<BoneAttachment3D>().ToArray();
        _supportTarget = new Node3D { Name = "SupportWristTarget", TopLevel = true };
        _supportPole = new Node3D { Name = "SupportElbowPole", TopLevel = true };
        AddChild(_supportTarget);
        AddChild(_supportPole);
        _supportIk = new TwoBoneIK3D { Name = "SupportHandIK", SettingCount = 1, Active = false };
        _skeleton.AddChild(_supportIk);
        _supportIk.SetRootBoneName(0, "mixamorig_LeftArm");
        _supportIk.SetMiddleBoneName(0, "mixamorig_LeftForeArm");
        _supportIk.SetEndBoneName(0, "mixamorig_LeftHand");
        _supportIk.SetTargetNode(0, _supportIk.GetPathTo(_supportTarget));
        _supportIk.SetPoleNode(0, _supportIk.GetPathTo(_supportPole));
        _supportIk.SetPoleDirection(0, SkeletonModifier3D.SecondaryDirection.None);
        _supportOrientation = new HandOrientationModifier { Name = "SupportGripOrientation", HandBone = _leftHand, Active = false };
        _skeleton.AddChild(_supportOrientation);
        _skeleton.SkeletonUpdated += MeasureGripErrors;
    }

    private Transform3D HandWorld(int bone) => _skeleton.GlobalTransform * _skeleton.GetBoneGlobalPose(bone);

    private static float SupportInfluence(EncounterObservation? encounter, double tick) => encounter?.Phase switch
        {
            EncounterPhase.Active => 1.0f,
            EncounterPhase.Readying => Mathf.SmoothStep(0, 1, (TransitionProgress(encounter, tick) - 0.60f) / 0.30f),
            EncounterPhase.Securing => 1 - Mathf.SmoothStep(0, 1, (TransitionProgress(encounter, tick) - 0.10f) / 0.30f),
            _ => 0,
        };

    private void SynchronizeSupportHand(EncounterObservation? encounter, double tick)
    {
        var amount = OneHanded ? 0 : SupportInfluence(encounter, tick);
        _supportIk.Active = amount > 0;
        _supportOrientation.Active = amount > 0;
        _supportIk.Influence = amount;
        _supportOrientation.Influence = amount;
        var handRotation = HandWorld(_leftHand).Basis.Orthonormalized();
        _supportOrientation.WorldRotation = handRotation;
        _supportTarget.GlobalPosition = _supportGrip.GlobalPosition - handRotation.Y * _leftPalmOffset;
        _supportPole.GlobalPosition = GlobalTransform * new Vector3(0.6f, 1.12f, 0.22f);
    }

    private bool FitSupportReach(float influence)
    {
        if (OneHanded || influence <= 0) { return false; }
        var leftShoulder = HandWorld(_leftArm).Origin;
        var leftElbow = HandWorld(_leftForeArm).Origin;
        var leftWrist = HandWorld(_leftHand);
        var target = _supportGrip.GlobalPosition - leftWrist.Basis.Orthonormalized().Y * _leftPalmOffset;
        // Armed locomotion can carry the foregrip beyond the support arm's reach.
        // Bring the weapon hand inward, retaining its authored orientation and
        // both arm segment lengths, before solving the support arm. A small bend
        // reserve avoids the locked-elbow singularity; no glove or weapon scales.
        var reach = leftShoulder.DistanceTo(leftElbow) + leftElbow.DistanceTo(leftWrist.Origin) - .015f;
        var distance = leftShoulder.DistanceTo(target);
        if (distance <= reach) { return false; }
        var wrist = HandWorld(_rightHand);
        var desiredWrist = wrist.Origin + target.DirectionTo(leftShoulder) * Math.Min(.12f, distance - reach) * influence;
        var root = HandWorld(_rightArm).Origin;
        var middle = HandWorld(_rightForeArm).Origin;
        var upperLength = root.DistanceTo(middle);
        var lowerLength = middle.DistanceTo(wrist.Origin);
        var axis = root.DirectionTo(desiredWrist);
        var span = Math.Clamp(root.DistanceTo(desiredWrist), Math.Abs(upperLength - lowerLength) + .001f,
            upperLength + lowerLength - .001f);
        var pole = middle - root - axis * (middle - root).Dot(axis);
        if (pole.LengthSquared() < .000001f) { return false; }
        var along = (upperLength * upperLength - lowerLength * lowerLength + span * span) / (2 * span);
        var bend = Mathf.Sqrt(Math.Max(0, upperLength * upperLength - along * along));
        var desiredElbow = root + axis * along + pole.Normalized() * bend;
        RotateBoneToward(_rightArm, middle - root, desiredElbow - root);
        var movedElbow = HandWorld(_rightForeArm).Origin;
        RotateBoneToward(_rightForeArm, HandWorld(_rightHand).Origin - movedElbow,
            root + axis * span - movedElbow);
        var hand = HandWorld(_rightHand);
        hand.Basis = wrist.Basis;
        _skeleton.SetBoneGlobalPose(_rightHand, _skeleton.GlobalTransform.AffineInverse() * hand);
        return true;
    }

    private void RotateBoneToward(int bone, Vector3 from, Vector3 to)
    {
        var world = HandWorld(bone);
        world.Basis = new Basis(new Quaternion(from.Normalized(), to.Normalized())) * world.Basis;
        _skeleton.SetBoneGlobalPose(bone, _skeleton.GlobalTransform.AffineInverse() * world);
    }

    private void MeasureGripErrors()
    {
        var right = HandWorld(_rightHand);
        var left = HandWorld(_leftHand);
        _primaryGripError = (right.Origin + right.Basis.Y.Normalized() * _rightPalmOffset).DistanceTo(_primaryGrip.GlobalPosition);
        _supportGripError = OneHanded ? 0 : (left.Origin + left.Basis.Y.Normalized() * _leftPalmOffset).DistanceTo(_supportGrip.GlobalPosition);
    }

    private void ApplyAimAndRecoil(Vector3 direction, bool moving)
    {
        var local = GlobalBasis.Inverse() * direction;
        var yaw = Mathf.Clamp(Mathf.Atan2(local.X, local.Z), -0.9f, 0.9f);
        var distance = new Vector2(local.X, local.Z).Length();
        var pitch = moving || distance < 0.1f ? 0 : -Mathf.Atan2(1.15f - 1.43f, distance);
        var age = (_sampleTick - _lastShotTick) / GameSession.TicksPerSecond * AnimationPacing.Rate;
        var kick = age is >= 0 and < 0.4
            ? (float)(age < 0.045 ? Math.Sin(age / 0.045 * Math.PI / 2) : Math.Exp(-(age - 0.045) * 20))
            : 0;
        var world = _skeleton.GlobalTransform * _skeleton.GetBoneGlobalPose(_spine);
        var rotation = new Basis(GlobalBasis.Y.Normalized(), yaw)
            * new Basis(GlobalBasis.X.Normalized(), pitch - kick * (StrongRecoil ? 0.085f : 0.03f));
        world.Basis = rotation * world.Basis;
        world.Origin -= GlobalBasis.Z.Normalized() * kick * (StrongRecoil ? 0.04f : 0.018f);
        _skeleton.SetBoneGlobalPose(_spine, _skeleton.GlobalTransform.AffineInverse() * world);
    }

    private void FaceDirection(Vector3 direction, float deltaSeconds)
    {
        if (!HumanoidPresentation.TryLocalYaw(this, direction, out var targetYaw))
        {
            return;
        }

        Rotation = new Vector3(0.0f, Mathf.LerpAngle(Rotation.Y, targetYaw, 1 - Mathf.Exp(-16 * AnimationPacing.Rate * deltaSeconds)), 0.0f);
    }

    private void AttachWeapon(bool inHand)
    {
        var socket = inHand ? _handSocket : _holsterSocket;
        // The imported Mixamo armature retains its normalization scale. A rigid
        // metric prop follows socket position and rotation, not that scale.
        _weapon.GlobalTransform = new Transform3D(socket.GlobalBasis.Orthonormalized(), socket.GlobalPosition);
        _weaponInHand = inHand;
    }

    public object GetDiagnostics()
    {
        var body = FindDescendants<MeshInstance3D>(_weapon).OrderByDescending(mesh => mesh.GetAabb().Volume).First();
        var bounds = body.GetAabb();
        return new
        {
            sample_tick = _sampleTick,
            clip = _posePlayer.ClipName,
            clip_seconds = _posePlayer.ClipTime,
            attachment = _weaponInHand == true ? "hand" : "holster",
            weapon_length_m = body.GlobalBasis.Z.Length() * bounds.Size.Z,
            primary_grip_error_m = _primaryGripError,
            support_grip_error_m = _supportGripError,
            recoil_variant = StrongRecoil ? "strong" : "restrained",
            death_pose = new
            {
                head_height_m = HandWorld(_skeleton.FindBone("mixamorig_Head")).Origin.Y - GlobalPosition.Y,
                hips_height_m = HandWorld(_skeleton.FindBone("mixamorig_Hips")).Origin.Y - GlobalPosition.Y,
                lowest_foot_height_m = Math.Min(HandWorld(_skeleton.FindBone("mixamorig_LeftFoot")).Origin.Y,
                    HandWorld(_skeleton.FindBone("mixamorig_RightFoot")).Origin.Y) - GlobalPosition.Y,
                highest_foot_height_m = Math.Max(HandWorld(_skeleton.FindBone("mixamorig_LeftFoot")).Origin.Y,
                    HandWorld(_skeleton.FindBone("mixamorig_RightFoot")).Origin.Y) - GlobalPosition.Y,
            },
            one_handed = OneHanded,
            support_grip_world = OneHanded ? null : VectorValues(_supportGrip.GlobalPosition),
            muzzle_world = VectorValues(MuzzlePosition),
            muzzle_direction = VectorValues(MuzzleDirection),
            bones = Enumerable.Range(0, _skeleton.GetBoneCount())
                .Where(index => _skeleton.GetBoneName(index).Contains("Hand", StringComparison.Ordinal)
                    || _skeleton.GetBoneName(index).Contains("Arm", StringComparison.Ordinal))
                .Select(index => new
                {
                    name = _skeleton.GetBoneName(index),
                    position = VectorValues((_skeleton.GlobalTransform * _skeleton.GetBoneGlobalPose(index)).Origin),
                }),
        };
    }

    private static float[] VectorValues(Vector3 value) => [value.X, value.Y, value.Z];

    private Node3D FindSocket(string canonicalName)
    {
        var sanitizedName = canonicalName.Replace('.', '_');
        return FindDescendants<Node3D>(this).FirstOrDefault(node =>
                string.Equals(node.Name, canonicalName, StringComparison.Ordinal)
                || string.Equals(node.Name, sanitizedName, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"The armed character or weapon GLB is missing required socket '{canonicalName}'.");
    }

    private void ValidateAnimation(StringName animationName, bool loop)
    {
        if (!_animationPlayer.HasAnimation(animationName))
        {
            var available = string.Join(", ", _animationPlayer.GetAnimationList()
                .Select(name => name.ToString()));
            throw new InvalidOperationException(
                $"The armed character GLB is missing required animation '{animationName}'. "
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
