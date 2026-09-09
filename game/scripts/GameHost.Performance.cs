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
    private readonly List<double> _engineProcessCosts = [];
    private readonly List<double> _renderCpuCosts = [];
    private readonly List<double> _renderGpuCosts = [];
    private int _unfocusedFrames;
    private readonly List<CommandTiming> _commandTimings = [];
    private readonly List<object> _frameSpikes = [];

    private static double Milliseconds(long from, long to) => (to - from) * 1000.0 / Stopwatch.Frequency;

    private void MeasureRenderedFrame()
    {
        if (!_performanceRunning) { return; }
        var now = Stopwatch.GetTimestamp();
        _engineProcessCosts.Add(Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000);
        _renderCpuCosts.Add(RenderingServer.ViewportGetMeasuredRenderTimeCpu(GetViewport().GetViewportRid())
            + RenderingServer.GetFrameSetupTimeCpu());
        _renderGpuCosts.Add(RenderingServer.ViewportGetMeasuredRenderTimeGpu(GetViewport().GetViewportRid()));
        if (!GetWindow().HasFocus()) { _unfocusedFrames++; }
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
        RenderingServer.ViewportSetMeasureRenderTime(GetViewport().GetViewportRid(), true);
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
            if (IsPartyReview)
            {
                DrivePartyPerformance(route);
            }
            else if (route.Encounter!.Phase == EncounterPhase.Defeat)
            {
                Dispatch(new RestartEncounterCommand(NextHumanCommandId("perf.retry"), _definition!.Combat.SoloEncounter.Id));
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
                        _definition!.Protagonist.Id, _definition.Combat.SoloHostile.Id));
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
            }
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        _performanceRunning = false;
        RenderingServer.FramePostDraw -= MeasureRenderedFrame;
        var report = new
        {
            schema_version = 2,
            measurement_completed = _renderIntervals.Count > 300 && _commandTimings.All(item => item.Accepted),
            frame_pacing_warning = _renderIntervals.Count(interval => interval > 50) > _renderIntervals.Count * .01,
            method = "Stopwatch wall time between FramePostDraw signals in ordinary real-time play; no MovieWriter or fixed frame delta",
            startup_to_checkpoint_seconds = startupSeconds,
            excluded_warmup_seconds = warmup.Elapsed.TotalSeconds - watch.Elapsed.TotalSeconds,
            sample_wall_seconds = watch.Elapsed.TotalSeconds,
            simulation_ticks = _session.Tick - startTick,
            resolution = new { width = GetWindow().Size.X, height = GetWindow().Size.Y },
            render_intervals_ms = TimingSummary(_renderIntervals),
            simulation_advance_cpu_ms = TimingSummary(_simulationCosts),
            presentation_cpu_ms = TimingSummary(_presentationCosts),
            engine_process_ms = TimingSummary(_engineProcessCosts),
            render_cpu_ms = TimingSummary(_renderCpuCosts),
            render_gpu_ms = TimingSummary(_renderGpuCosts),
            unfocused_frames = _unfocusedFrames,
            runtime = new { max_fps = Engine.MaxFps, low_processor = OS.LowProcessorUsageMode,
                vsync = DisplayServer.WindowGetVsyncMode().ToString(),
                renderer = RenderingServer.GetCurrentRenderingMethod(),
                driver = RenderingServer.GetCurrentRenderingDriverName(),
                gpu = RenderingServer.GetVideoAdapterName(),
                primitives = Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame),
                draw_calls = Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame) },
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
            limitation = "Command timing starts in the game handler; OS input latency, compositor display latency, and external tool round trips are not measured. Completion is not a frame-rate acceptance gate.",
        };
        File.WriteAllText(Path.Combine(_reviewOutput, "performance.json"), JsonSerializer.Serialize(report, CaptureManifestJsonOptions));
        WriteReviewManifest(report.measurement_completed);
        GD.Print(JsonSerializer.Serialize(new { report.measurement_completed, report.frame_pacing_warning, report.render_intervals_ms,
            report.engine_process_ms, report.render_cpu_ms, report.render_gpu_ms, report.unfocused_frames, report.runtime }, CaptureLogJsonOptions));
        GetTree().Quit(report.measurement_completed ? 0 : 1);
    }

    private void DrivePartyPerformance(StationRouteObservation route)
    {
        if (route.Encounter!.Phase == EncounterPhase.Defeat)
        {
            Dispatch(new RestartEncounterCommand(NextHumanCommandId("perf.retry"), route.Encounter.Id));
            Dispatch(new SetPauseCommand(NextHumanCommandId("perf.resume"), false));
            return;
        }
        if (route.Encounter.Phase != EncounterPhase.Active) { return; }
        foreach (var actor in route.Party.Where(actor => !actor.Combat!.IsDefeated))
        {
            if (actor.PendingAction is not null) { continue; }
            if (actor.Combat!.RememberedAttackTargetId is null && actor.CurrentAction is null)
            {
                var target = route.Hostiles!.Where(hostile => !hostile.Combat.IsDefeated)
                    .OrderBy(hostile => hostile.Position.DistanceTo(actor.Position)).FirstOrDefault();
                if (target is not null) { Dispatch(new AssignBasicAttackTargetCommand(NextHumanCommandId("perf.attack"), actor.Id, target.Id)); }
            }
            else if (actor.Id == _definition!.Companion.Id && actor.Combat.Cooldowns.Single(cd => cd.AbilityId == _definition.Combat.Barrier.Id).RemainingTicks == 0
                && actor.CurrentAction?.Kind != PrimaryActionKind.Ability)
            {
                var direction = ToGodot(route.Hostiles!.Single(hostile => _enemyViews[hostile.Id].Sentry is not null).Position) - ToGodot(actor.Position);
                direction.Y = 0;
                var target = new BarrierAbilityTarget(new WorldPosition(actor.Position.X, actor.Position.Y, actor.Position.Z + .8), ToCore(direction.Normalized()));
                if (_session!.CheckBarrierPlacement(actor.Id, target) is null)
                { Dispatch(new UseAbilityCommand(NextHumanCommandId("perf.barrier"), actor.Id, _definition.Combat.Barrier.Id, target)); }
            }
            else if (actor.CurrentAction?.Kind != PrimaryActionKind.Ability
                && actor.Combat.Cooldowns.Single(cd => cd.AbilityId == actor.Loadout!.SecondaryAbilityId).RemainingTicks == 0)
            {
                if (actor.Id == _definition.Companion.Id)
                { Dispatch(new UseAbilityCommand(NextHumanCommandId("perf.taunt"), actor.Id, _definition.Combat.Taunt.Id, new SelfAbilityTarget())); }
                else
                {
                    var target = route.Hostiles!.FirstOrDefault(enemy => !enemy.Combat.IsDefeated
                        && actor.Position.DistanceTo(enemy.Position) <= _definition.Combat.Burst.RangeMeters);
                    if (target is not null)
                    { Dispatch(new UseAbilityCommand(NextHumanCommandId("perf.burst"), actor.Id, _definition.Combat.Burst.Id, new EntityAbilityTarget(target.Id))); }
                }
            }
        }
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
