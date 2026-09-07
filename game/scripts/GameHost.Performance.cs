using System.Diagnostics;
using System.Text.Json;
using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

public partial class GameHost
{
    private readonly Stopwatch _startupWatch = Stopwatch.StartNew();
    private bool _performanceRunning;
    private long _lastRenderedStamp;
    private readonly List<double> _renderIntervals = [];
    private readonly List<double> _simulationCosts = [];
    private readonly List<double> _presentationCosts = [];
    private readonly List<CommandTiming> _commandTimings = [];
    private readonly List<object> _frameSpikes = [];

    private static double Milliseconds(long from, long to) => (to - from) * 1000.0 / Stopwatch.Frequency;

    private void MeasureRenderedFrame()
    {
        if (!_performanceRunning) { return; }
        var now = Stopwatch.GetTimestamp();
        if (_lastRenderedStamp != 0)
        {
            var interval = Milliseconds(_lastRenderedStamp, now);
            _renderIntervals.Add(interval);
            if (interval > 33.333)
            {
                _frameSpikes.Add(new { interval_ms = interval, tick = _session!.Tick,
                    phase = ReviewState().Encounter!.Phase.ToString(), latest_event = _session.Observe().LatestEventSequence });
            }
        }
        _lastRenderedStamp = now;
        foreach (var command in _commandTimings.Where(item => item.FirstFrameMs is null))
        {
            command.FirstFrameMs = Milliseconds(command.Start, now);
        }
    }

    private void MeasureCommand(IGameCommand command, CommandAcknowledgement acknowledgement, long started)
    {
        if (!_performanceRunning) { return; }
        _commandTimings.Add(new CommandTiming(command.GetType().Name, acknowledgement.Accepted,
            started, Milliseconds(started, Stopwatch.GetTimestamp())));
    }

    private async Task RunRealtimePerformanceAsync()
    {
        var startupSeconds = _startupWatch.Elapsed.TotalSeconds;
        var warmup = Stopwatch.StartNew();
        while (warmup.Elapsed.TotalSeconds < 2) { await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
        _reviewDrivesClock = false;
        _reviewSampleTick = null;
        _camera.InputEnabled = true;
        _performanceRunning = true;
        RenderingServer.FramePostDraw += MeasureRenderedFrame;
        var watch = Stopwatch.StartNew();
        var startTick = _session!.Tick;
        var initialMeshCompilations = Performance.GetMonitor(Performance.Monitor.PipelineCompilationsMesh);
        var initialSurfaceCompilations = Performance.GetMonitor(Performance.Monitor.PipelineCompilationsSurface);
        var initialDrawCompilations = Performance.GetMonitor(Performance.Monitor.PipelineCompilationsDraw);
        var attempt = -1;
        var relocated = false;
        var suppressed = false;
        Dispatch(new SetPauseCommand(NextHumanCommandId("perf.resume"), false));
        while (watch.Elapsed.TotalSeconds < 30)
        {
            var route = ReviewState();
            if (route.Encounter!.Phase == EncounterPhase.Defeat)
            {
                Dispatch(new RestartEncounterCommand(NextHumanCommandId("perf.retry"), _definition!.Combat.Encounter.Id));
                Dispatch(new SetPauseCommand(NextHumanCommandId("perf.resume"), false));
            }
            else if (route.Encounter.Phase == EncounterPhase.Active)
            {
                if (attempt != route.Encounter.Attempt)
                {
                    attempt = route.Encounter.Attempt;
                    relocated = false;
                    suppressed = false;
                    Dispatch(new AssignBasicAttackTargetCommand(NextHumanCommandId("perf.attack"),
                        _definition!.Protagonist.Id, _definition.Combat.Hostile.Id));
                }
                else if (!relocated && route.Hostiles![0].Combat.Health <= 70)
                {
                    relocated = true;
                    var origin = route.Protagonist.Position;
                    Dispatch(new MoveActorCommand(NextHumanCommandId("perf.move"), _definition!.Protagonist.Id,
                        new WorldPosition(origin.X + 1.2, origin.Y, origin.Z)));
                }
                else if (relocated && !suppressed && route.Protagonist.CurrentAction is null
                    && route.Hostiles![0].CurrentAction?.Phase == PrimaryActionPhase.Windup)
                {
                    suppressed = true;
                    Dispatch(new UseAbilityCommand(NextHumanCommandId("perf.ability"), _definition!.Protagonist.Id,
                        _definition.Combat.ProtagonistAbility.Id, new PositionAbilityTarget(route.Hostiles[0].Position)));
                }
                if (route.Protagonist.Combat!.Health <= 70 && route.Protagonist.Combat.Items[0].Charges > 0
                    && route.Protagonist.CurrentAction?.Kind != PrimaryActionKind.Item)
                {
                    Dispatch(new UseItemCommand(NextHumanCommandId("perf.aid"), _definition!.Protagonist.Id,
                        _definition.Combat.HealingItem.Id, _definition.Protagonist.Id));
                }
            }
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        _performanceRunning = false;
        RenderingServer.FramePostDraw -= MeasureRenderedFrame;
        var report = new
        {
            schema_version = 1,
            passed = _renderIntervals.Count > 300 && _commandTimings.All(item => item.Accepted),
            method = "Stopwatch wall time between FramePostDraw signals in ordinary real-time play; no MovieWriter or fixed frame delta",
            startup_to_checkpoint_seconds = startupSeconds,
            excluded_warmup_seconds = warmup.Elapsed.TotalSeconds - watch.Elapsed.TotalSeconds,
            sample_wall_seconds = watch.Elapsed.TotalSeconds,
            simulation_ticks = _session.Tick - startTick,
            resolution = new { width = GetWindow().Size.X, height = GetWindow().Size.Y },
            render_intervals_ms = TimingSummary(_renderIntervals),
            simulation_advance_cpu_ms = TimingSummary(_simulationCosts),
            presentation_cpu_ms = TimingSummary(_presentationCosts),
            pipeline_compilations_during_sample = new
            {
                mesh = Performance.GetMonitor(Performance.Monitor.PipelineCompilationsMesh) - initialMeshCompilations,
                surface = Performance.GetMonitor(Performance.Monitor.PipelineCompilationsSurface) - initialSurfaceCompilations,
                draw = Performance.GetMonitor(Performance.Monitor.PipelineCompilationsDraw) - initialDrawCompilations,
            },
            command_dispatch_cpu_ms = TimingSummary(_commandTimings.Select(item => item.AcknowledgementMs)),
            command_to_first_render_ms = TimingSummary(_commandTimings.Where(item => item.FirstFrameMs is not null).Select(item => item.FirstFrameMs!.Value)),
            command_count = _commandTimings.Count,
            rejected_commands = _commandTimings.Count(item => !item.Accepted),
            frame_spikes = _frameSpikes,
            limitation = "Command timing starts in the game handler; OS input latency, compositor display latency, GPU duration, and external tool round trips are not measured.",
        };
        File.WriteAllText(Path.Combine(_reviewOutput, "performance.json"), JsonSerializer.Serialize(report, CaptureManifestJsonOptions));
        WriteReviewManifest(report.passed);
        GD.Print(JsonSerializer.Serialize(report, CaptureLogJsonOptions));
        GetTree().Quit(report.passed ? 0 : 1);
    }

    private static object TimingSummary(IEnumerable<double> measurements)
    {
        var values = measurements.Order().ToArray();
        double Percentile(double percentile) => values.Length == 0 ? 0 : values[(int)Math.Clamp(Math.Ceiling(values.Length * percentile) - 1, 0, values.Length - 1)];
        return new
        {
            count = values.Length, p50 = Percentile(0.50), p95 = Percentile(0.95), p99 = Percentile(0.99),
            maximum = Percentile(1), over_33_ms = values.Count(value => value > 33.333),
            over_50_ms = values.Count(value => value > 50),
        };
    }

    private sealed class CommandTiming(string kind, bool accepted, long start, double acknowledgementMs)
    {
        public string Kind { get; } = kind;
        public bool Accepted { get; } = accepted;
        public long Start { get; } = start;
        public double AcknowledgementMs { get; } = acknowledgementMs;
        public double? FirstFrameMs { get; set; }
    }
}
