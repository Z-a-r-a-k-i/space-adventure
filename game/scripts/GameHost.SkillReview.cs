using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

public partial class GameHost
{
    private async Task<bool> CheckPartySkills()
    {
        var combat = _definition!.Combat;
        if (_reviewMode == "input")
        { await InputKey(Key.Key2); }
        else { ReviewOrder(new UseAbilityCommand(new CommandId("review.taunt"), _definition.Companion.Id, combat.Taunt.Id, new SelfAbilityTarget())); }
        InputCheck("Taunt queues independently of Barrier cooldown", ReviewState().Party[1].PendingAction?.AbilityId == combat.Taunt.Id);
        await ReviewTicks(combat.Taunt.WindupTicks);
        InputCheck("nearby threats visibly focus Protector", ReviewState().Hostiles!.All(enemy => enemy.Combat.TauntedBy == _definition.Companion.Id));
        if (await ReviewCapture("taunt")) { return true; }
        var sentry = ReviewState().Hostiles!.Single(enemy => _enemyViews[enemy.Id].Sentry is not null);
        if (_reviewMode == "input")
        {
            await InputClick(_partyButtons[_definition.Protagonist.Id.Value].GetGlobalRect().GetCenter(), MouseButton.Left);
            await InputKey(Key.Key2);
            InputCheck("Vanguard second slot requests an enemy", _abilityTargeting && _targetAbilityKind == AbilityTargetKind.Entity);
            await InputWorldClick(ToGodot(sentry.Position) + Vector3.Up * 1.3f, MouseButton.Left);
            InputCheck("world enemy picking queues Burst", !_abilityTargeting && ReviewState().Protagonist.PendingAction?.AbilityId == combat.Burst.Id);
            await InputKey(Key.Key2);
            await InputWorldClick(_enemyViews[sentry.Id].Root.GlobalPosition + Vector3.Up * 1.3f, MouseButton.Left);
        }
        else { ReviewOrder(new UseAbilityCommand(new CommandId("review.burst"), _definition.Protagonist.Id, combat.Burst.Id, new EntityAbilityTarget(sentry.Id))); }
        InputCheck("Burst queues the selected enemy", ReviewState().Protagonist.PendingAction?.AbilityId == combat.Burst.Id
            && ReviewState().Protagonist.PendingAction?.CombatTargetId == sentry.Id);
        var after = _session!.Observe().LatestEventSequence;
        int ShotCount() => _session.EventsSince(after).Count(item => item.Detail is AbilityReleasedEventDetail release && release.AbilityId == combat.Burst.Id);
        if (_reviewMode == "input")
        {
            await ReviewUntil(state => state.Protagonist.CurrentAction is { Phase: PrimaryActionPhase.Windup } action
                && action.AbilityId == combat.Burst.Id, 80);
            await InputKey(Key.Key2);
            InputCheck("Burst can be inspected before its pending release", _abilityTargeting);
        }
        await ReviewUntil(_ => ShotCount() == 1, 80);
        if (_reviewMode == "input")
        {
            await InputFrame();
            InputCheck("aiming updates when the skill enters cooldown", _abilityContext.Visible
                && _abilityContextDetail.Text.StartsWith("ON COOLDOWN", StringComparison.Ordinal)
                && _affectedTargetRings.Values.All(ring => !ring.Visible));
            await InputKey(Key.Escape);
            var tick = _session.Tick;
            _reviewSampleTick = null; _reviewDrivesClock = false;
            for (var frame = 0; frame < 8; frame++) { await InputFrame(); }
            InputCheck("pause freezes Burst between shots", _session.Tick == tick && ShotCount() == 1);
            _reviewDrivesClock = true; _reviewSampleTick = _session.Tick;
        }
        await ReviewUntil(_ => ShotCount() == combat.Burst.ShotCount, 30);
        InputCheck("Burst emits distinct releases and leaves Interrupt ready", ShotCount() == combat.Burst.ShotCount
            && ReviewState().Protagonist.Combat!.Cooldowns.Single(cd => cd.AbilityId == combat.ProtagonistAbility.Id).RemainingTicks == 0
            && ReviewState().Protagonist.Combat!.Cooldowns.Single(cd => cd.AbilityId == combat.Burst.Id).RemainingTicks > 0);
        InputCheck("all skill tiles fit the viewport", new[] { _abilityButton, _secondaryAbilityButton, _stopButton }
            .All(tile => GetViewport().GetVisibleRect().Encloses(tile.GetGlobalRect())));
        return await ReviewCapture("burst");
    }
}
