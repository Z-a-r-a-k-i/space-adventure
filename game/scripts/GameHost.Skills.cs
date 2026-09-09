using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

public partial class GameHost
{
    private void PresentTaunt(AbilityReleasedEventDetail ability, long tick)
    {
        var radius = (float)_definition!.Combat.Taunt.RadiusMeters;
        var ring = new MeshInstance3D
        {
            Position = ToGodot(ability.TargetPosition) + Vector3.Up * .07f,
            Mesh = new TorusMesh { InnerRadius = radius - .035f, OuterRadius = radius + .035f, Rings = 64, RingSegments = 4 },
            MaterialOverride = CreateCombatEffectMaterial(new Color(.98f, .57f, .2f, .32f)),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(ring); _combatPresentationEffects.Add(new TimedPresentationEffect(ring, .5f / AnimationPacing.Rate, tick));
        foreach (var hostile in _session!.Observe().StationRoute!.Hostiles!.Where(enemy => enemy.Combat.TauntedBy == ability.SourceId))
        {
            var label = new Label3D { Text = "TAUNTED", Position = ToGodot(hostile.Position) + Vector3.Up * 2,
                FontSize = 28, OutlineSize = 7, Modulate = new Color("efb263"), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled };
            AddChild(label); _combatPresentationEffects.Add(new TimedPresentationEffect(label, .9f, tick));
        }
        PlayCombatCue("guard", ToGodot(ability.TargetPosition));
    }
}
