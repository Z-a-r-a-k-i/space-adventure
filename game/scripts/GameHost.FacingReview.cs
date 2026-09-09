using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

public partial class GameHost
{
    private async Task CheckBarrierAfterMovement()
    {
        var owner = ReviewState().Party[1];
        var before = ReviewState().Encounter!.Barrier!;
        var destination = ToGodot(owner.Position) + Vector3.Right;
        if (_reviewMode == "input")
        {
            await InputClick(_partyButtons[owner.Id.Value].GetGlobalRect().GetCenter(), MouseButton.Left);
            var commands = _humanCommandSequence;
            await InputKey(Key.T);
            InputCheck("removed T shortcut leaves selection and targeting unchanged", _humanCommandSequence == commands
                && !_abilityTargeting && _selectedActorIds.SetEquals([owner.Id]));
            await InputClick(_camera.UnprojectPosition(destination), MouseButton.Right, alt: true);
            InputCheck("Alt right-click follows ordinary movement after facing removal",
                ReviewState().Party[1].PendingAction?.Kind == PrimaryActionKind.Move);
        }
        else { ReviewOrder(new MoveActorCommand(new CommandId("review.leave.barrier"), owner.Id, ToCore(destination))); }
        await ReviewUntil(state => state.Party[1].Position.DistanceTo(owner.Position) > .6, 30);
        InputCheck("walking and automatic turning leave the barrier at its deployed world transform",
            ReviewState().Encounter!.Barrier is { } placed && placed.Position == before.Position
            && placed.Facing == before.Facing && placed.DeployedAtTick == before.DeployedAtTick
            && _barrierView.GlobalPosition.DistanceTo(ToGodot(before.Position)) < .01
            && -_barrierView.GlobalBasis.Z.Normalized().Dot(ToGodot(before.Facing)) > .999);
    }
}
