using Godot;

namespace SpaceAdventure.Game;

// Original deterministic synthesis: no sampled recordings or external sound assets.
// Variants affect presentation only and never consume simulation randomness.
public static class CombatAudio
{
    private const int Rate = 44100;
    private const int VariantCount = 4;
    private static readonly Dictionary<(string Cue, int Variant), AudioStreamWav> Streams = [];
    private static readonly System.Text.Json.JsonSerializerOptions ManifestOptions = new() { WriteIndented = true };
    internal static readonly string[] Cues = ["carbine", "shotgun", "sentry", "burst", "interrupt",
        "carbine_hit", "shotgun_hit", "melee_hit", "sentry_hit", "block", "barrier", "taunt"];

    public static AudioStreamWav Get(string cue, int variant = 0)
    {
        cue = cue switch { "guard" => "barrier", "impact" => "melee_hit", _ => cue };
        variant = Math.Abs(variant % VariantCount);
        if (Streams.TryGetValue((cue, variant), out var stream)) { return stream; }
        var duration = cue switch
        {
            "ambience" => 8, "shotgun" => .36, "barrier" => .5, "taunt" => .42,
            "block" => .3, "interrupt" => .3, "melee_hit" => .24, _ => .2,
        };
        var samples = (int)(duration * Rate);
        var data = new byte[samples * 2];
        var seed = cue.Aggregate(427, (value, character) => unchecked(value * 31 + character));
        var random = new Random(unchecked(seed + variant * 701));
        var tune = 1 + (variant - 1.5) * .012;
        double lowNoise = 0;
        for (var index = 0; index < samples; index++)
        {
            var time = (double)index / Rate;
            var t = time * tune;
            var noise = random.NextDouble() * 2 - 1;
            lowNoise += (noise - lowNoise) * .12;
            var highNoise = noise - lowNoise;
            var envelope = Math.Min(1, time / .003) * Math.Pow(1 - time / duration, 2.3);
            var signal = cue switch
            {
                "carbine" => highNoise * .44 * Math.Exp(-t * 65)
                    + Chirp(t, 220, -430) * .33 * Math.Exp(-t * 19)
                    + Math.Sin(Math.Tau * 1700 * t) * .12 * Math.Exp(-t * 70),
                "shotgun" => lowNoise * 1.1 * Math.Exp(-t * 10)
                    + highNoise * .44 * Math.Exp(-t * 35) + Chirp(t, 90, -55) * .48 * Math.Exp(-t * 12),
                "sentry" => Chirp(t, 620, -1150) * .4 + highNoise * .18 * Math.Exp(-t * 20),
                "burst" => highNoise * .32 * Math.Exp(-t * 70) + Chirp(t, 310, -650) * .42
                    + Math.Sin(Math.Tau * 2100 * t) * .12 * Math.Exp(-t * 35),
                "interrupt" => Chirp(t, 1600, -1900) * .35 + Chirp(t, 2350, -2400) * .16
                    + highNoise * .1 * Math.Exp(-t * 20),
                "carbine_hit" => highNoise * .42 * Math.Exp(-t * 42)
                    + Math.Sin(Math.Tau * 950 * t) * .21 * Math.Exp(-t * 35),
                "shotgun_hit" => lowNoise * .65 * Math.Exp(-t * 19)
                    + Math.Sin(Math.Tau * 135 * t) * .42 * Math.Exp(-t * 20),
                "melee_hit" => Chirp(t, 130, -160) * .6 * Math.Exp(-t * 15)
                    + lowNoise * .7 * Math.Exp(-t * 24),
                "sentry_hit" => Chirp(t, 390, -700) * .35 + highNoise * .22 * Math.Exp(-t * 32),
                "block" => (Math.Sin(Math.Tau * 860 * t) + Math.Sin(Math.Tau * 1290 * t)) * .23
                    + highNoise * .22 * Math.Exp(-t * 65),
                "barrier" => (Chirp(t, 190, 310) + Chirp(t, 285, 465)) * .25
                    * Math.Min(1, t / .055),
                "taunt" => (Math.Sin(Math.Tau * 155 * t) + Math.Sin(Math.Tau * 233 * t)) * .3
                    * (.7 + .3 * Math.Cos(Math.Tau * 14 * t)),
                // Whole-number partials and periodic modulation keep the station loop seamless.
                "ambience" => Math.Sin(Math.Tau * 41 * time) * .075
                    + Math.Sin(Math.Tau * 82 * time) * .035
                    + Math.Sin(Math.Tau * 347 * time + .5 * Math.Sin(Math.Tau * .125 * time)) * .012
                    + Math.Sin(Math.Tau * 913 * time + 3 * Math.Sin(Math.Tau * 3 * time)) * .006,
                _ => throw new ArgumentOutOfRangeException(nameof(cue), cue, "Unknown combat audio cue."),
            };
            var sample = (short)Math.Clamp(signal * (cue == "ambience" ? 1 : envelope) * 26000,
                short.MinValue, short.MaxValue);
            data[index * 2] = (byte)(sample & 255);
            data[index * 2 + 1] = (byte)((sample >> 8) & 255);
        }
        stream = new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = Rate, Stereo = false, Data = data,
            LoopMode = cue == "ambience" ? AudioStreamWav.LoopModeEnum.Forward : AudioStreamWav.LoopModeEnum.Disabled,
            LoopBegin = 0, LoopEnd = samples,
        };
        Streams.Add((cue, variant), stream);
        return stream;
    }

    public static float VolumeDb(string cue) => cue switch
    {
        "shotgun" => -9, "taunt" => -12, "barrier" => -12, "block" => -10,
        "carbine_hit" or "shotgun_hit" or "melee_hit" or "sentry_hit" => -12, _ => -10,
    };

    public static void Warmup()
    {
        foreach (var cue in Cues)
        for (var variant = 0; variant < VariantCount; variant++) { _ = Get(cue, variant); }
        _ = Get("ambience");
    }

    // Optional local listening evidence from the same PCM streams that Godot plays.
    public static void WriteAudition(string directory)
    {
        Directory.CreateDirectory(directory);
        using var montage = new MemoryStream();
        var entries = new List<object>();
        foreach (var cue in Cues)
        for (var variant = 0; variant < VariantCount; variant++)
        {
            var stream = Get(cue, variant);
            entries.Add(new { cue, variant, starts_seconds = (double)montage.Length / (Rate * 2),
                duration_seconds = stream.GetLength(), in_game_volume_db = VolumeDb(cue) });
            montage.Write(stream.Data);
            montage.Write(new byte[(int)(Rate * .24) * 2]);
        }
        using var output = new AudioStreamWav
        { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = Rate, Stereo = false, Data = montage.ToArray() };
        if (output.SaveToWav(Path.Combine(directory, "combat-cues.wav")) != Error.Ok
            || Get("ambience").SaveToWav(Path.Combine(directory, "station-ambience.wav")) != Error.Ok)
        { throw new IOException("Godot could not write the local audio audition."); }
        File.WriteAllText(Path.Combine(directory, "timing.json"), System.Text.Json.JsonSerializer.Serialize(entries,
            ManifestOptions));
    }

    private static double Chirp(double time, double start, double slope) =>
        Math.Sin(Math.Tau * (start * time + slope * time * time / 2));
}
