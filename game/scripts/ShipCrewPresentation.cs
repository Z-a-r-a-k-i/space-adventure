using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

/// <summary>
/// Full-size crew model sampled from the battle tick through <see cref="HumanoidPresentation"/>, with
/// small procedural work poses layered over the authored idle. Nothing here advances rules.
/// </summary>
public sealed class ShipCrewPresentation
{
    private const string IdleClip = "anim_humanoid_idle_holstered";
    private const string WalkClip = "anim_humanoid_walk_holstered";
    private const string DownClip = "anim_humanoid_down";
    private const double WorkBlendTicks = 6;
    private readonly Skeleton3D _skeleton;
    private readonly int _spine, _head, _rightArm, _rightForeArm, _rightHand, _leftArm, _leftForeArm, _leftHand;
    private double _lastTick = double.NegativeInfinity;

    public ShipCrewPresentation(Node3D parent, string crewId, PackedScene model, float strideRate, Color accent)
    {
        Root = new Node3D { Name = crewId.Replace('.', '_') };
        Presentation = new HumanoidPresentation
        {
            Name = "Presentation", IdleAnimationName = IdleClip, LocomotionAnimationName = WalkClip, DownAnimationName = DownClip,
            LocomotionPlaybackRate = strideRate,
        };
        Presentation.AddChild(model.Instantiate<Node3D>());
        Root.AddChild(Presentation);
        SelectionRing = ShipBattleView.Ring(.44f, new Color(.55f, 1, .75f, .95f));
        SelectionRing.Position = new Vector3(0, .03f, 0);
        SelectionRing.Visible = false;
        Root.AddChild(SelectionRing);
        // Role colour under every crew member so the top-down silhouettes read at a glance.
        RoleRing = ShipBattleView.Ring(.3f, new Color(accent, .85f));
        RoleRing.Position = new Vector3(0, .02f, 0);
        Root.AddChild(RoleRing);
        parent.AddChild(Root);
        _skeleton = ShipBattleView.Descendants(Presentation).OfType<Skeleton3D>().First();
        _spine = _skeleton.FindBone("mixamorig_Spine2");
        _head = _skeleton.FindBone("mixamorig_Head");
        _rightArm = _skeleton.FindBone("mixamorig_RightArm");
        _rightForeArm = _skeleton.FindBone("mixamorig_RightForeArm");
        _rightHand = _skeleton.FindBone("mixamorig_RightHand");
        _leftArm = _skeleton.FindBone("mixamorig_LeftArm");
        _leftForeArm = _skeleton.FindBone("mixamorig_LeftForeArm");
        _leftHand = _skeleton.FindBone("mixamorig_LeftHand");
    }

    public Node3D Root { get; }
    public HumanoidPresentation Presentation { get; }
    public MeshInstance3D SelectionRing { get; }
    public MeshInstance3D RoleRing { get; }
    public ShipTaskKind PosedWork { get; private set; }
    public float WorkWeight { get; private set; }

    public void Synchronize(ShipCrewObservation crew, double tick, double fraction, bool paused, Vector3? workTarget)
    {
        var presentationTick = tick + fraction;
        var reset = presentationTick < _lastTick;
        _lastTick = presentationTick;
        var position = new Vector3((float)Mathf.Lerp(crew.PreviousX, crew.X, fraction), ShipBattleView.FloorY, (float)Mathf.Lerp(crew.PreviousZ, crew.Z, fraction));
        Root.Position = position;
        var travel = new Vector3((float)(crew.X - crew.PreviousX), 0, (float)(crew.Z - crew.PreviousZ));
        if (travel.LengthSquared() > 1e-8f) { Root.Rotation = new Vector3(0, Mathf.Atan2(travel.X, travel.Z), 0); }
        else if (crew.CurrentWork != ShipTaskKind.None && workTarget is { } target && new Vector2(target.X - position.X, target.Z - position.Z).LengthSquared() > .01f)
        { Root.Rotation = new Vector3(0, Mathf.Atan2(target.X - position.X, target.Z - position.Z), 0); }

        RoleRing.Visible = !crew.Downed;
        var action = crew.Downed ? HumanoidPresentationAction.Down : crew.Moving ? HumanoidPresentationAction.Locomotion : HumanoidPresentationAction.Idle;
        Presentation.Synchronize(true, action, paused, Vector3.Zero, presentationTick: presentationTick, snapToPose: reset);

        // The blend clock is the authoritative tick at which the current work began.
        var work = crew.Downed || crew.Moving ? ShipTaskKind.None : crew.CurrentWork;
        WorkWeight = work == ShipTaskKind.None ? 0 : (float)Math.Clamp((presentationTick - crew.WorkSinceTick) / WorkBlendTicks, 0, 1);
        PosedWork = work;
        if (WorkWeight > 0) { ApplyWorkPose(work, presentationTick / ShipCombatSession.TicksPerSecond, WorkWeight); }
    }

    /// <summary>Bounded arm/spine offsets over the sampled idle; the pose player restores the authored sample next frame.</summary>
    private void ApplyWorkPose(ShipTaskKind work, double seconds, float weight)
    {
        var basis = Root.GlobalBasis;
        var forward = basis.Z.Normalized();
        var up = Vector3.Up;
        var right = -basis.X.Normalized();
        var t = (float)seconds;
        var (lean, upper, fore) = work switch
        {
            ShipTaskKind.Man => (8f, new Vector3(.12f, -.72f, .58f), new Vector3(-.04f, -.22f + .07f * Mathf.Sin(t * 13), .96f)),
            ShipTaskKind.Repair => (16f, new Vector3(.12f, -.5f, .72f), new Vector3(0, -.05f + .35f * Mathf.Sin(t * 7), .9f)),
            ShipTaskKind.Extinguish => (10f, new Vector3(.05f, -.35f, .82f), new Vector3(.28f * Mathf.Sin(t * 3.2f), -.12f, .95f)),
            ShipTaskKind.Seal => (38f, new Vector3(.14f, -.9f, .35f), new Vector3(.12f * Mathf.Sin(t * 5), -.82f, .5f)),
            ShipTaskKind.Treat => (30f, new Vector3(.1f, -.8f, .52f), new Vector3(0, -.55f + .05f * Mathf.Sin(t * 2.4f), .82f)),
            _ => (0f, Vector3.Zero, Vector3.Zero),
        };
        if (_spine >= 0) { RotateAboutAxis(_spine, right, Mathf.DegToRad(-lean) * weight); }
        foreach (var side in new[] { 1, -1 })
        {
            var (arm, foreArm, hand) = side > 0 ? (_rightArm, _rightForeArm, _rightHand) : (_leftArm, _leftForeArm, _leftHand);
            if (arm < 0 || foreArm < 0 || hand < 0) { continue; }
            // Repair keeps the left hand as a steady brace; other tasks mirror both arms.
            var armFore = work == ShipTaskKind.Repair && side < 0 ? new Vector3(-.08f, -.3f, .95f) : fore;
            Aim(arm, foreArm, World(upper, side, right, up, forward), weight);
            Aim(foreArm, hand, World(armFore, side, right, up, forward), weight);
        }
        if (_head >= 0) { RotateAboutAxis(_head, right, Mathf.DegToRad(-lean * .6f) * weight); }
    }

    private static Vector3 World(Vector3 local, int side, Vector3 right, Vector3 up, Vector3 forward) =>
        (right * local.X * side + up * local.Y + forward * local.Z).Normalized();

    private Transform3D BoneWorld(int bone) => _skeleton.GlobalTransform * _skeleton.GetBoneGlobalPose(bone);

    private void Aim(int bone, int child, Vector3 direction, float weight)
    {
        var world = BoneWorld(bone);
        var from = BoneWorld(child).Origin - world.Origin;
        if (from.LengthSquared() < 1e-6f) { return; }
        var rotation = Quaternion.Identity.Slerp(new Quaternion(from.Normalized(), direction), weight);
        world.Basis = new Basis(rotation) * world.Basis;
        _skeleton.SetBoneGlobalPose(bone, _skeleton.GlobalTransform.AffineInverse() * world);
    }

    private void RotateAboutAxis(int bone, Vector3 axis, float angle)
    {
        var world = BoneWorld(bone);
        world.Basis = new Basis(axis, angle) * world.Basis;
        _skeleton.SetBoneGlobalPose(bone, _skeleton.GlobalTransform.AffineInverse() * world);
    }
}
