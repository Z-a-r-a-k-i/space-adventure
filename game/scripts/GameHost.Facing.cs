using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

public partial class GameHost
{
    private readonly Dictionary<EntityId, FacingSample> _facingSamples = [];

    private Vector3 SampleActorFacing(ActorObservation actor)
    {
        var tick = _session!.Tick;
        var next = Mathf.Atan2((float)actor.Facing.X, (float)actor.Facing.Z);
        if (!_facingSamples.TryGetValue(actor.Id, out var sample) || tick < sample.Tick)
        { sample = new FacingSample(next, next, tick, tick); }
        else if (tick > sample.Tick)
        { sample = new FacingSample(sample.Current, next, sample.Tick, tick); }
        _facingSamples[actor.Id] = sample;
        var fraction = (float)Math.Clamp((_presentationTick - sample.PreviousTick) / Math.Max(1, sample.Tick - sample.PreviousTick), 0, 1);
        var angle = Mathf.LerpAngle(sample.Previous, sample.Current, fraction);
        return new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
    }

    private sealed record FacingSample(float Previous, float Current, long PreviousTick, long Tick);
}
