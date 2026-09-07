using Godot;

namespace SpaceAdventure.Game;

// Keep the glove's authored grip orientation after the two-bone arm solve.
public partial class HandOrientationModifier : SkeletonModifier3D
{
    public int HandBone { get; set; }

    public Basis WorldRotation { get; set; } = Basis.Identity;

    public override void _ProcessModificationWithDelta(double delta)
    {
        _ = delta;
        var skeleton = GetSkeleton();
        if (skeleton is null || HandBone < 0) { return; }
        var pose = skeleton.GetBoneGlobalPose(HandBone);
        var world = skeleton.GlobalTransform * pose;
        world.Basis = WorldRotation.Scaled(world.Basis.Scale);
        skeleton.SetBoneGlobalPose(HandBone, skeleton.GlobalTransform.AffineInverse() * world);
    }
}
