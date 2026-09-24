using System.Text.Json;
using Godot;

namespace SpaceAdventure.Game;

/// <summary>
/// Sampled cue library shared by the station and the ship battle. <c>res://audio/cues.json</c> (written by
/// <c>tools/audio/build_game_audio.py</c>) names each cue's takes, bus, level, pitch variation and voice limits;
/// buses come from <c>default_bus_layout.tres</c>. Variation uses a presentation-only random stream and never
/// touches simulation randomness. Missing cues are silent so a partial library never breaks gameplay.
/// </summary>
public static class GameAudio
{
    public const string ManifestPath = "res://audio/cues.json";
    private static readonly Dictionary<string, Cue> Cues = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, AudioStreamPlayer> Loops = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, ulong> LastPlayed = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, int> Voices = new(StringComparer.Ordinal);
    private static readonly Random Variation = new(20260925);
    private static Node? _owner;
    private static bool _loaded;

    public sealed record Cue(string Name, string Bus, float VolumeDb, float PitchJitter, int MaxVoices, int MinIntervalMs, bool Loop, IReadOnlyList<AudioStream> Takes);

    public static IReadOnlyDictionary<string, Cue> Library { get { Load(); return Cues; } }

    /// <summary>Loads the manifest once and parents one-shot and loop players to <paramref name="owner"/> (freed with its scene).</summary>
    public static void Ensure(Node owner)
    {
        Load();
        if (_owner == owner) { return; }
        StopLoops();
        Loops.Clear();
        Voices.Clear();
        _owner = owner;
    }

    public static Cue? Find(string name) { Load(); return Cues.GetValueOrDefault(name); }

    /// <summary>One-shot cue. <paramref name="pan"/> runs from -1 (left, player ship) to 1 (right, enemy ship).</summary>
    public static void Play(string name, float pan = 0, float volumeDb = 0)
    {
        if (_owner is null || !GodotObject.IsInstanceValid(_owner) || DisplayServer.GetName() == "headless") { return; }
        if (Find(name) is not { Takes.Count: > 0 } cue) { return; }
        var now = Time.GetTicksMsec();
        if (LastPlayed.TryGetValue(name, out var last) && now - last < (ulong)cue.MinIntervalMs) { return; }
        if (Voices.GetValueOrDefault(name) >= cue.MaxVoices) { return; }
        LastPlayed[name] = now;
        Voices[name] = Voices.GetValueOrDefault(name) + 1;
        var stream = cue.Takes[Variation.Next(cue.Takes.Count)];
        var pitch = 1 + (float)(Variation.NextDouble() * 2 - 1) * cue.PitchJitter;
        // 2D players pan against the screen centre; distance attenuation is disabled so only the side changes.
        var player = new AudioStreamPlayer2D
        {
            Stream = stream, Bus = cue.Bus, VolumeDb = cue.VolumeDb + volumeDb, PitchScale = pitch, Attenuation = 0, MaxDistance = 100_000,
            PanningStrength = 1.4f, Position = new Vector2(640 + Math.Clamp(pan, -1, 1) * 420, 360),
        };
        _owner.AddChild(player);
        player.Finished += () =>
        {
            Voices[name] = Math.Max(0, Voices.GetValueOrDefault(name) - 1);
            player.QueueFree();
        };
        player.Play();
    }

    /// <summary>Keeps a looping cue playing while <paramref name="active"/>; <paramref name="paused"/> suspends it (tactical pause).</summary>
    public static void SetLoop(string name, bool active, bool paused)
    {
        if (_owner is null || !GodotObject.IsInstanceValid(_owner) || DisplayServer.GetName() == "headless") { return; }
        if (!Loops.TryGetValue(name, out var player) || !GodotObject.IsInstanceValid(player))
        {
            if (!active || Find(name) is not { Takes.Count: > 0 } cue) { return; }
            player = new AudioStreamPlayer { Stream = cue.Takes[0], Bus = cue.Bus, VolumeDb = cue.VolumeDb, Name = $"Loop_{name.Replace('.', '_')}" };
            _owner.AddChild(player);
            Loops[name] = player;
        }
        if (!active) { if (player.Playing) { player.Stop(); } return; }
        if (!player.Playing) { player.Play(); }
        player.StreamPaused = paused;
    }

    public static void StopLoops()
    {
        foreach (var player in Loops.Values.Where(GodotObject.IsInstanceValid)) { player.Stop(); }
    }

    /// <summary>Takes of a cue for positional (3D) playback owned by a caller, e.g. station combat.</summary>
    public static AudioStream? Take(string name, int variant) => Find(name) is { Takes.Count: > 0 } cue ? cue.Takes[Math.Abs(variant) % cue.Takes.Count] : null;

    private static void Load()
    {
        if (_loaded) { return; }
        _loaded = true;
        if (!Godot.FileAccess.FileExists(ManifestPath)) { GD.PushWarning($"Audio manifest {ManifestPath} is missing; the game is silent."); return; }
        try
        {
            using var document = JsonDocument.Parse(Godot.FileAccess.GetFileAsString(ManifestPath));
            foreach (var property in document.RootElement.GetProperty("cues").EnumerateObject())
            {
                try
                {
                    var value = property.Value;
                    var loop = value.TryGetProperty("loop", out var loopValue) && loopValue.GetBoolean();
                    var takes = value.GetProperty("files").EnumerateArray().Select(file => file.GetString() ?? "")
                        .Where(path => ResourceLoader.Exists(path)).Select(path => ResourceLoader.Load<AudioStream>(path)).Where(stream => stream is not null).ToList();
                    var cue = new Cue(property.Name, value.GetProperty("bus").GetString() ?? "Sfx", (float)value.GetProperty("volume_db").GetDouble(),
                        (float)value.GetProperty("pitch_jitter").GetDouble(), value.GetProperty("max_voices").GetInt32(), value.GetProperty("min_interval_ms").GetInt32(), loop, takes);
                    foreach (var take in takes.OfType<AudioStreamOggVorbis>()) { take.Loop = loop; }
                    Cues[property.Name] = cue;
                }
                catch (Exception exception) when (exception is InvalidOperationException or KeyNotFoundException or FormatException or OverflowException or ArgumentException)
                { GD.PushWarning($"Skipping malformed audio cue '{property.Name}': {exception.Message}"); }
            }
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException)
        { GD.PushWarning($"Audio manifest {ManifestPath} is invalid: {exception.Message}"); }
    }
}
