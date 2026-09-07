using Godot;

namespace SpaceAdventure.Game;

// Short original synthesis for prototype feedback. No runtime file generation,
// imported sound library, audio singleton, or gameplay dependency.
public static class CombatAudio
{
    private static readonly Dictionary<string, AudioStreamWav> Streams = [];

    public static AudioStreamWav Get(string cue)
    {
        if (Streams.TryGetValue(cue, out var stream)) { return stream; }
        const int rate = 44100;
        var duration = cue == "aid" ? 0.28 : cue == "interrupt" ? 0.19 : 0.14;
        var samples = (int)(duration * rate);
        var data = new byte[samples * 2];
        var random = new Random(427);
        for (var index = 0; index < samples; index++)
        {
            var time = (double)index / rate;
            var envelope = Math.Min(1, time / 0.002) * Math.Pow(1 - time / duration, 3);
            var noise = random.NextDouble() * 2 - 1;
            var signal = cue switch
            {
                "aid" => Math.Sin(2 * Math.PI * (640 * time + 620 * time * time)) * 0.35,
                "interrupt" => Math.Sin(2 * Math.PI * (1200 * time - 1600 * time * time)) * 0.35 + noise * 0.10,
                "impact" => Math.Sin(2 * Math.PI * 110 * time) * 0.4 + noise * 0.25,
                _ => noise * 0.6 * Math.Exp(-time * 45) + Math.Sin(2 * Math.PI * (180 * time - 400 * time * time)) * 0.4,
            };
            var sample = (short)Math.Clamp(signal * envelope * 28000, short.MinValue, short.MaxValue);
            data[index * 2] = (byte)(sample & 255);
            data[index * 2 + 1] = (byte)((sample >> 8) & 255);
        }
        stream = new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = rate, Stereo = false, Data = data };
        Streams.Add(cue, stream);
        return stream;
    }
}
