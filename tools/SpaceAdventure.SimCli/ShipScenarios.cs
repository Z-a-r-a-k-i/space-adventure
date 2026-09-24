using System.Diagnostics;
using SpaceAdventure.Core;

namespace SpaceAdventure.SimCli;

internal static class ShipScenarios
{
    public const int MaximumBattleTicks = 5 * 60 * ShipCombatSession.TicksPerSecond;

    public static bool Handles(string id) => id is "ship-victory" or "ship-overwhelm" or "ship-defeat" or "ship-retry" or "ship-balance";

    public static int Run(string id, JsonLinesOutput output)
    {
        if (id == "ship-balance") { return RunBalance(output); }
        var stopwatch = Stopwatch.StartNew();
        var definition = ShipBattlePilot.LoadDefinition(AppContext.BaseDirectory);
        var session = new ShipCombatSession(definition, ShipBattlePilot.StationCrew);
        output.Emit(new { kind = "run_metadata", schema_version = 1, scenario_id = id, content_id = definition.Id, tick_rate = ShipCombatSession.TicksPerSecond });
        var strategy = id switch
        {
            "ship-overwhelm" => ShipBattleStrategy.OverwhelmDefenses,
            "ship-defeat" or "ship-retry" => ShipBattleStrategy.Passive,
            _ => ShipBattleStrategy.SuppressWeapons,
        };
        var pausedAtStart = session.Paused && session.AdvanceTicks(10) == 0 && session.Tick == 0;
        var pilot = new ShipBattlePilot(session, strategy);
        var phase = pilot.RunToEnd(MaximumBattleTicks);
        var assertions = new List<(string Name, bool Passed)>
        {
            ("battle_starts_paused_at_tick_zero", pausedAtStart),
            (strategy == ShipBattleStrategy.Passive ? "passive_play_is_defeated" : "strategy_wins",
                phase == (strategy == ShipBattleStrategy.Passive ? ShipBattlePhase.Defeat : ShipBattlePhase.Victory)),
        };
        var terminalTick = session.Tick;
        if (strategy != ShipBattleStrategy.Passive)
        {
            assertions.Add(($"unpaused_duration_between_{ShipBattlePilot.MinimumWinSeconds}_and_{ShipBattlePilot.MaximumWinSeconds}_seconds",
                terminalTick >= ShipBattlePilot.MinimumWinSeconds * 30L && terminalTick <= ShipBattlePilot.MaximumWinSeconds * 30L));
            assertions.Add(("enemy_repair_reserve_exhausted", session.Observe().EnemyRepair.ReserveBars == 0 && session.Events.Count(item => item.Kind == "enemy_repaired") == definition.Enemy.Repair.ReserveBars));
            assertions.Add(("no_crew_downed_after_victory", session.Observe().Crew.All(crew => !crew.Downed)));
        }
        var frozen = session.AdvanceTicks(30) == 0 && session.Tick == terminalTick;
        assertions.Add(("terminal_outcome_freezes_simulation", frozen));
        if (id == "ship-retry")
        {
            var retry = session.Execute(new ShipRestartCommand(new CommandId("retry")));
            var fresh = session.Observe();
            assertions.Add(("retry_resets_battle_only", retry.Accepted && fresh.Attempt == 2 && fresh.Tick == 0 && fresh.Paused
                && fresh.Phase == ShipBattlePhase.Active && fresh.Player.Hull == definition.Player.Hull
                && fresh.Rooms.All(room => room.OxygenPermille == 1000 && room.FireSeverity == 0 && room.BreachSeverity == 0)
                && fresh.Crew.All(crew => crew.Health == crew.MaxHealth) && fresh.Shots.Count == 0));
        }
        foreach (var gameEvent in session.Events.Where(item => item.Kind is not ("command_rejected" or "crew_arrived" or "door_traversal" or "shield_restored")))
        {
            output.Emit(new { kind = "gameplay_event", gameEvent.Sequence, gameEvent.Attempt, gameEvent.Tick, type = gameEvent.Kind, gameEvent.Subject, gameEvent.Detail });
        }
        foreach (var (name, passed) in assertions) { output.Emit(new { kind = "assertion", name, passed }); }
        var final = session.Observe();
        var allPassed = assertions.All(item => item.Passed);
        output.Emit(new
        {
            kind = "final_snapshot", passed = allPassed, duration_ms = stopwatch.ElapsedMilliseconds, terminal_tick = terminalTick,
            terminal_seconds = terminalTick / (double)ShipCombatSession.TicksPerSecond, phase = final.Phase.ToString(), player_hull = final.Player.Hull,
            enemy_hull = final.Enemy.Hull, enemy_repair_reserve = final.EnemyRepair.ReserveBars,
            crew = final.Crew.Select(crew => new { crew.Id, crew.Health, crew.Downed }),
            rooms = final.Rooms.Select(room => new { room.Id, room.OxygenPermille, room.FireSeverity, room.BreachSeverity }),
        });
        return allPassed ? 0 : 1;
    }

    /// <summary>Tuning report: every scripted strategy across many seeds (evasion and hazard rolls differ per seed).</summary>
    private static int RunBalance(JsonLinesOutput output)
    {
        var definition = ShipBattlePilot.LoadDefinition(AppContext.BaseDirectory);
        const int Seeds = 40;
        foreach (var strategy in Enum.GetValues<ShipBattleStrategy>())
        {
            var runs = Enumerable.Range(1, Seeds).Select(seed =>
            {
                var session = new ShipCombatSession(definition, ShipBattlePilot.StationCrew, (ulong)seed);
                var phase = new ShipBattlePilot(session, strategy).RunToEnd(MaximumBattleTicks);
                var final = session.Observe();
                return (Phase: phase, Seconds: session.Tick / 30.0, Hull: final.Player.Hull,
                    Downed: session.Events.Count(item => item.Kind == "crew_downed"), Fires: session.Events.Count(item => item.Kind == "fire_started"),
                    Breaches: session.Events.Count(item => item.Kind == "breach_opened"), Misses: session.Events.Count(item => item.Kind == "shot_missed"),
                    EnemyVolleys: session.Events.Count(item => item.Kind == "weapon_fired" && item.Subject == "enemy"));
            }).ToArray();
            double Median(IEnumerable<double> values)
            {
                var ordered = values.Order().ToArray();
                if (ordered.Length == 0) { return 0; }
                var middle = ordered.Length / 2;
                return ordered.Length % 2 == 0 ? (ordered[middle - 1] + ordered[middle]) / 2 : ordered[middle];
            }
            var wins = runs.Where(run => run.Phase == ShipBattlePhase.Victory).ToArray();
            output.Emit(new
            {
                kind = "balance", strategy = strategy.ToString(), seeds = Seeds, wins = wins.Length,
                seconds_min = runs.Min(run => run.Seconds), seconds_median = Median(runs.Select(run => run.Seconds)), seconds_max = runs.Max(run => run.Seconds),
                hull_min = runs.Min(run => run.Hull), hull_median = Median(runs.Select(run => (double)run.Hull)), hull_max = runs.Max(run => run.Hull),
                downed_total = runs.Sum(run => run.Downed), fires_median = Median(runs.Select(run => (double)run.Fires)),
                breaches_median = Median(runs.Select(run => (double)run.Breaches)), misses_median = Median(runs.Select(run => (double)run.Misses)),
                enemy_volleys_median = Median(runs.Select(run => (double)run.EnemyVolleys)),
            });
        }
        return 0;
    }
}
