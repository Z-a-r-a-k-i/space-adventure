using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

public partial class GameHost
{
    private async Task<bool> CheckPartyFacing()
    {
        var owner = ReviewState().Party[1];
        var before = ReviewState().Encounter!.Barrier!;
        var direction = Vector3.Right;
        if (_reviewMode == "input")
        {
            await InputClick(_partyButtons[owner.Id.Value].GetGlobalRect().GetCenter(), MouseButton.Left);
            await InputKey(Key.T);
            await InputWorldClick(ToGodot(owner.Position) + direction * 2.5f, MouseButton.Left);
            InputCheck("facing preview turns only the character",
                !_facingTargeting && ReviewState().Encounter!.Barrier == before && ReviewState().Party[1].Facing == owner.Facing
                && _facingArrows[owner.Id].Order.Visible && !_barrierQueued.Visible && !_barrierPreview.Visible);
        }
        else { ReviewOrder(new FaceActorsCommand(new CommandId("review.face.right"), [owner.Id], ToCore(direction))); }
        await ReviewTicks(8);
        var turned = ReviewState().Party[1];
        InputCheck("crew turns while the deployed shield keeps its position and facing", turned.Position == owner.Position
            && turned.FacingHeld && turned.Facing.X > .999 && ReviewState().Encounter!.Barrier!.DeployedAtTick == before.DeployedAtTick
            && _protectorPartyPresentation.GlobalBasis.Z.Normalized().Dot(direction) > .999
            && ReviewState().Encounter!.Barrier!.Position == before.Position && ReviewState().Encounter!.Barrier!.Facing == before.Facing
            && -_barrierView.GlobalBasis.Z.Normalized().Dot(ToGodot(before.Facing)) > .999);
        InputCheck("visible shield stays at its fixed authoritative center", _barrierView.GlobalPosition.DistanceTo(ToGodot(before.Position)) < .01);
        if (await ReviewCapture("facing")) { return true; }
        if (_reviewMode == "input")
        {
            await InputClick(_camera.UnprojectPosition(ToGodot(owner.Position) + Vector3.Back * 2.5f), MouseButton.Right, alt: true);
            InputCheck("Alt right-click queues rotation instead of movement", ReviewState().Party[1].PendingAction?.Kind == PrimaryActionKind.Face
                && ReviewState().Party[1].PendingAction?.Facing is { Z: > .99 });
        }
        else { ReviewOrder(new FaceActorsCommand(new CommandId("review.face.front"), [owner.Id], new WorldPosition(0, 0, 1))); }
        await ReviewTicks(8);
        var destination = ToGodot(owner.Position) + Vector3.Right;
        if (_reviewMode == "input") { await InputWorldClick(destination, MouseButton.Right); }
        else { ReviewOrder(new MoveActorCommand(new CommandId("review.leave.barrier"), owner.Id, ToCore(destination))); }
        await ReviewUntil(state => state.Party[1].Position.DistanceTo(owner.Position) > .6, 30);
        InputCheck("walking away leaves the barrier at its deployed world transform", ReviewState().Encounter!.Barrier is { } placed
            && placed.Position == before.Position && placed.Facing == before.Facing && placed.DeployedAtTick == before.DeployedAtTick
            && _barrierView.GlobalPosition.DistanceTo(ToGodot(before.Position)) < .01
            && -_barrierView.GlobalBasis.Z.Normalized().Dot(ToGodot(before.Facing)) > .999);
        return false;
    }
}
