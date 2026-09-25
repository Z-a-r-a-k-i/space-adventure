using Godot;

namespace SpaceAdventure.Game;

/// <summary>
/// Station combat cues served from the shared sampled library (<see cref="GameAudio"/>, cue names
/// <c>station.*</c>). Takes are chosen by the caller's rotating variant, never by simulation randomness.
/// </summary>
public static class CombatAudio
{
    private const int VariantCount = 4;
    private static readonly System.Text.Json.JsonSerializerOptions ManifestOptions = new() { WriteIndented = true };
    internal static readonly string[] Cues = ["carbine", "shotgun", "sentry", "burst", "interrupt",
        "carbine_hit", "shotgun_hit", "melee_hit", "sentry_hit", "block", "barrier", "taunt", "heal", "departure"];

    private static string Resolve(string cue) => "station." + cue switch { "guard" => "barrier", "impact" => "melee_hit", _ => cue };

    /// <summary>Sampled take for a station cue; a short silent stream stands in when the library is missing.</summary>
    public static AudioStream Get(string cue, int variant = 0) =>
        GameAudio.Take(Resolve(cue), Math.Abs(variant % VariantCount)) ?? new AudioStreamWav { MixRate = 44100, Data = new byte[64] };

    public static float VolumeDb(string cue) => GameAudio.Find(Resolve(cue))?.VolumeDb ?? -80;

    public static string Bus(string cue) => GameAudio.Find(Resolve(cue))?.Bus ?? "Sfx";

    public static void Warmup()
    {
        foreach (var cue in Cues) { _ = Get(cue); }
        _ = Get("ambience");
    }

    /// <summary>Optional inventory of available station takes, in-game levels, and buses; not a playback log.</summary>
    public static void WriteAudition(string directory)
    {
        Directory.CreateDirectory(directory);
        var entries = Cues.Append("ambience").Select(cue => GameAudio.Find(Resolve(cue)) is { } found
            ? new { cue, library = found.Name, takes = found.Takes.Select(take => take.ResourcePath).ToArray(), in_game_volume_db = found.VolumeDb, bus = found.Bus }
            : new { cue, library = Resolve(cue), takes = Array.Empty<string>(), in_game_volume_db = -80f, bus = "missing" }).ToArray();
        File.WriteAllText(Path.Combine(directory, "timing.json"), System.Text.Json.JsonSerializer.Serialize(entries, ManifestOptions));
    }
}
