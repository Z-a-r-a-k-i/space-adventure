using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

public partial class GameHost
{
    private bool IsVisionReview => ReviewArgument("review-encounter", "solo") == "vision";

    private async Task RunVisionReviewAsync()
    {
        _reviewDrivesClock = true;
        _camera.InputEnabled = false;
        _camera.DistanceMeters = float.Parse(ReviewArgument("review-distance", "14.5"),
            System.Globalization.CultureInfo.InvariantCulture);
        _camera.FocusOn(new Vector3(-10, 0, 4.5f));
        ReviewOrder(new SetPauseCommand(NextHumanCommandId("vision.pause"), true));
        var protagonist = _definition!.Protagonist.Id;
        var solo = _definition.Combat.SoloHostile.Id;
        var fullWalls = _camera.FullWallBounds.ToArray();
        InputCheck("Production walls and all seven closed gates supply immutable sight blockers",
            fullWalls.Length > 50 && CreateVisionBlockers().Count == fullWalls.Length + 7);
        InputCheck("Closed arrival door conceals the dormant Enforcer", FindVisibleHostile(ReviewState(), solo) is null);
        CheckVisionPresentation();
        if (await ReviewCapture("vision-hidden")) { return; }

        await EscapeInteract("interaction.survivor", dialogue: true);
        ReviewOrder(new ChooseDialogueResponseCommand(NextHumanCommandId("vision.choice"), protagonist,
            new EntityId("interaction.survivor"), new DialogueResponseId("response.reroute_service_power")));
        InputCheck("An available but unopened door still conceals the Enforcer", FindVisibleHostile(ReviewState(), solo) is null);
        await EscapeInteract("interaction.service_door.entry");
        var doorway = ReviewState().Protagonist.Position;
        // The open door reveals an Enforcer close enough to notice the Vanguard: the fight starts on the spot.
        InputCheck("Opening the door reveals the Enforcer, which spots the Vanguard where he stands",
            FindVisibleHostile(ReviewState(), solo) is not null && _session!.IsPaused
            && ReviewState().Encounter is { Phase: EncounterPhase.Readying, SpotterId: { } spotter, SpottedActorId: { } spotted }
            && spotter == solo && spotted == protagonist && ReviewState().Protagonist.Position == doorway);
        CheckVisionPresentation();
        var before = ReviewState().VisibleHostiles.Select(enemy => enemy.Id).ToArray();
        _camera.YawRadians += 1.2f;
        _camera.SnapOcclusionToDesiredState();
        SynchronizePresentation();
        InputCheck("Camera rotation and shortened walls cannot change group sight",
            before.SequenceEqual(ReviewState().VisibleHostiles.Select(enemy => enemy.Id))
            && fullWalls.SequenceEqual(_camera.FullWallBounds));
        _camera.YawRadians -= 1.2f;
        if (await ReviewCapture("vision-revealed")) { return; }
        await FightEscapeEncounter();
        await EscapeInteract("interaction.service_door.solo_exit");
        await EscapeInteract("interaction.protector", dialogue: true);
        ReviewOrder(new ChooseDialogueResponseCommand(NextHumanCommandId("vision.recruit"), protagonist,
            new EntityId("interaction.protector"), new DialogueResponseId("response.recruit_protector")));
        bool SeesNextFight() => ReviewState().VisibleHostiles.Any(enemy =>
            enemy.EncounterId == _definition.Combat.PartyEncounter.Id && !enemy.Combat.IsDefeated);
        bool NextFightDormant() => ReviewState().VisibleHostiles.Where(enemy => enemy.EncounterId == _definition.Combat.PartyEncounter.Id)
            .All(enemy => enemy.EncounterPhase == EncounterPhase.Dormant && enemy.CurrentAction is null);
        InputCheck("The crew see the arena before its enemies notice them", SeesNextFight() && NextFightDormant());
        var facing = ReviewState().VisibleHostiles.First(enemy => enemy.EncounterId == _definition.Combat.PartyEncounter.Id).Facing;
        await VisionMove(protagonist, new WorldPosition(-10, 0, 2));
        InputCheck("Protector shares sight from the junction while Vanguard is behind the arena wall", SeesNextFight());
        CheckVisionPresentation();
        _camera.FocusOn(new Vector3(-4, 0, 3));
        if (await ReviewCapture("vision-shared")) { return; }
        await VisionMove(_definition.Companion.Id, new WorldPosition(-10, 0, 2));
        InputCheck("Enemies disappear when the last seeing crew member leaves the junction", !SeesNextFight());
        CheckVisionPresentation();
        if (await ReviewCapture("vision-occluded")) { return; }
        await VisionMove(_definition.Companion.Id, new WorldPosition(-1.5, 0, 0));
        InputCheck("Returning one teammate restores shared sight without starting the next encounter", SeesNextFight()
            && NextFightDormant() && ReviewState().Encounter!.Phase == EncounterPhase.Victory);
        InputCheck("Reacquired dormant enemies retain their authored heading and remain idle",
            ReviewState().VisibleHostiles.First(enemy => enemy.EncounterId == _definition.Combat.PartyEncounter.Id).Facing == facing);
        await ReviewWaitForPath(new WorldPosition(0, 0, 5));
        ReviewOrder(new MovePartyCommand(NextHumanCommandId("vision.party.enter"),
            ReviewState().Party.Select(actor => actor.Id), new WorldPosition(0, 0, 5)));
        await ReviewUntil(state => state.Encounter!.Id == _definition.Combat.PartyEncounter.Id, 1200, fast: true);
        InputCheck("Walking into sight range starts the fight paused where the crew were noticed", _session!.IsPaused
            && ReviewState().Encounter is { Phase: EncounterPhase.Readying, SpotterId: not null }
            && ReviewState().Party.Any(actor => actor.Position.DistanceTo(ReviewState().VisibleHostiles
                .First(enemy => enemy.Id == ReviewState().Encounter!.SpotterId).Position) <= _definition.Vision.HostileDetectionMeters + .01));
        await FightEscapeEncounter();        await FinishSoloReview();
    }

    private async Task VisionMove(EntityId actorId, WorldPosition destination)
    {
        await ReviewWaitForPath(destination);
        ReviewOrder(new MoveActorCommand(NextHumanCommandId("vision.move"), actorId, destination));
        await ReviewUntil(state => state.Party.Single(actor => actor.Id == actorId).Position.DistanceTo(destination) < .12,
            1200, fast: true);
    }

    private void CheckVisionPresentation()
    {
        var route = ReviewState();
        InputCheck("Persistent combat effects retain no hidden enemy dependencies",
            _effectVisibilitySubjects.Values.All(subjects => subjects.All(id => IsPresentationSubjectVisible(route, id))));
        foreach (var (id, view) in _enemyViews)
        {
            var hostile = FindVisibleHostile(route, id);
            InputCheck($"{id} model follows shared crew sight", view.Root.Visible == (hostile is not null));
            InputCheck($"{id} can be picked only while visible in the current fight",
                (view.Target.CollisionLayer != 0) == (hostile is not null && CanTargetVisibleHostile(route, hostile)));
            if (hostile is null)
            {
                InputCheck($"{id} has no hidden screen projection or health bar",
                    ProjectStableIdToScreen(id.Value) is null
                    && (!_worldHealth.TryGetValue(id, out var health) || !health.Root.Visible)
                    && !_enemyIntentCues[id].Visible);
            }
        }
    }
}
