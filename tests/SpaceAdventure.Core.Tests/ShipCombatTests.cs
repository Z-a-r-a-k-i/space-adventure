using SpaceAdventure.Core;
using SpaceAdventure.SimCli;
using Xunit;

namespace SpaceAdventure.Core.Tests;

public sealed class ShipCombatTests
{
    private static readonly ShipBattleDefinition Content = ShipBattlePilot.LoadDefinition(AppContext.BaseDirectory);
    private static int _ids;

    private static CommandId Id() => new($"test.{Interlocked.Increment(ref _ids)}");

    private static ShipCombatSession Start(ShipBattleDefinition? definition = null, bool unpause = true)
    {
        var session = new ShipCombatSession(definition ?? Content, ShipBattlePilot.StationCrew);
        if (unpause) { Assert.True(session.Execute(new ShipSetPauseCommand(Id(), false)).Accepted); }
        return session;
    }

    private static ShipSystemObservation PlayerSystem(ShipCombatSession session, string id) => session.Observe().Player.Systems.Single(item => item.Id == id);

    private static ShipCrewObservation Crew(ShipCombatSession session, int index) => session.Observe().Crew[index];

    private static ShipRoomObservation Room(ShipCombatSession session, string id) => session.Observe().Rooms.Single(item => item.Id == id);

    private static Dictionary<string, int> Power(int weapons, int shields, int lifeSupport, int engines) =>
        new() { ["weapons"] = weapons, ["shields"] = shields, ["life_support"] = lifeSupport, ["engines"] = engines };

    /// <summary>A shield-stopped, never-evaded enemy gun with one authored target and payload.</summary>
    internal static ShipWeaponDefinition TestGun(string target, ShipPayload payload, int shots = 1, int damage = 1, double chargeSeconds = 12,
        bool pierces = false, bool evadable = false, ShipWeaponKind kind = ShipWeaponKind.Laser, int ammo = -1, string id = "test_gun") =>
        new(id, "Test Gun", kind, 1, chargeSeconds, shots, damage, 1.5, .2, pierces, evadable, ammo, 0, 0, [target], [payload]);

    /// <summary>Hazard validation variant: a single authored breach/incendiary volley against one system, no artificial penetration.</summary>
    private static ShipBattleDefinition SingleShot(ShipPayload payload, string target, int shots = 1) => Content with
    {
        Enemy = Content.Enemy with { Side = Content.Enemy.Side with { Weapons = [TestGun(target, payload, shots, ammo: 1)] } },
    };

    [Fact]
    public void ContentIsValidAndReactorCannotMaximizeEverySystem()
    {
        Assert.True(Content.Player.Systems.Sum(system => system.MaxPower) > Content.Player.Reactor);
        Assert.Equal(5, Content.Rooms.Count);
        Assert.Single(Content.Doors, door => door.Exterior);
    }

    [Fact]
    public void BattleStartsPausedAndPausedCommandsNeverAdvanceWork()
    {
        var session = Start(unpause: false);
        Assert.True(session.Paused);
        Assert.Equal(0, session.Advance(TimeSpan.FromSeconds(1)));
        Assert.True(session.Execute(new ShipMoveCrewCommand(Id(), "actor.protagonist", "engines")).Accepted);
        Assert.True(session.Execute(new ShipSetDoorCommand(Id(), "door_airlock", true)).Accepted);
        Assert.Equal(0, session.AdvanceTicks(100));
        var observation = session.Observe();
        Assert.Equal(0, observation.Tick);
        Assert.All(observation.Rooms, room => Assert.Equal(1000, room.OxygenPermille));
        Assert.Equal("weapons", observation.Crew[0].Room);
    }

    [Fact]
    public void FrameChunkingIsDeterministic()
    {
        var a = Start();
        var b = Start();
        new ShipBattlePilot(a, ShipBattleStrategy.SuppressWeapons).Send(new ShipSetWeaponTargetCommand(Id(), "burst_laser", "weapons"));
        new ShipBattlePilot(b, ShipBattleStrategy.SuppressWeapons).Send(new ShipSetWeaponTargetCommand(Id(), "burst_laser", "weapons"));
        for (var index = 0; index < 60 * 40; index++) { a.Advance(TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60)); }
        for (var index = 0; index < 7 * 40; index++) { b.Advance(TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 7)); }
        Assert.Equal(a.Tick, b.Tick);
        Assert.Equal(a.Events.Select(item => (item.Tick, item.Kind, item.Detail)), b.Events.Select(item => (item.Tick, item.Kind, item.Detail)));
        Assert.Equal(a.Observe().Player.Hull, b.Observe().Player.Hull);
    }

    [Fact]
    public void PowerVectorIsAtomicAndDamageCapsEffectivePowerWithoutHidingAllocation()
    {
        var session = Start();
        var over = session.Execute(new ShipSetPowerCommand(Id(), Power(3, 3, 2, 2)));
        Assert.Equal(ShipRejection.PowerExceedsReactor, over.Rejection);
        Assert.Equal(3, PlayerSystem(session, "weapons").AllocatedPower);
        Assert.Equal(ShipRejection.PowerVectorIncomplete, session.Execute(new ShipSetPowerCommand(Id(), new Dictionary<string, int> { ["weapons"] = 1 })).Rejection);
        Assert.True(session.Execute(new ShipSetPowerCommand(Id(), Power(3, 3, 1, 1))).Accepted);
        Assert.Equal(3, PlayerSystem(session, "shields").AllocatedPower);
    }

    [Fact]
    public void UnmannedBaselineRequiresPower()
    {
        var session = Start();
        session.Execute(new ShipSetCrewModeCommand(Id(), "actor.protagonist", ShipCrewMode.Hold));
        session.Execute(new ShipSetPowerCommand(Id(), Power(0, 3, 2, 2)));
        session.AdvanceTicks(120);
        Assert.All(session.Observe().Player.Weapons, weapon => Assert.Equal(0, weapon.ChargePermille));
        session.Execute(new ShipSetPowerCommand(Id(), Power(3, 2, 1, 2)));
        session.AdvanceTicks(30);
        Assert.All(session.Observe().Player.Weapons, weapon => Assert.True(weapon.ChargePermille > 0));
        Assert.False(PlayerSystem(session, "weapons").Manned);
    }

    [Fact]
    public void AutoMansCurrentRoomAndHoldSuppressesAllAutomaticWork()
    {
        var session = Start();
        session.AdvanceTicks(1);
        Assert.True(PlayerSystem(session, "weapons").Manned);
        Assert.Equal(ShipTaskKind.Man, Crew(session, 0).CurrentWork);
        session.Execute(new ShipSetCrewModeCommand(Id(), "actor.protagonist", ShipCrewMode.Hold));
        session.AdvanceTicks(1);
        Assert.False(PlayerSystem(session, "weapons").Manned);
        Assert.Equal(ShipTaskKind.None, Crew(session, 0).CurrentWork);
    }

    [Fact]
    public void AutoPrioritisesFireThenBreachAndExplicitWorkPersistsInDanger()
    {
        var session = Start(SingleShot(ShipPayload.Incendiary, "shields", shots: 1));
        session.Execute(new ShipSetPowerCommand(Id(), Power(3, 0, 1, 2)));
        while (Room(session, "shields").FireSeverity == 0) { session.AdvanceTicks(1); }
        session.AdvanceTicks(1);
        Assert.Equal(ShipTaskKind.Extinguish, Crew(session, 1).CurrentWork);
        // Explicit repair in a burning room keeps working despite danger; no silent evacuation.
        session.Execute(new ShipAssignTaskCommand(Id(), "actor.companion.protector", ShipTaskKind.Man, "shields"));
        var health = Crew(session, 1).Health;
        session.AdvanceTicks(30);
        Assert.Equal(ShipTaskKind.Man, Crew(session, 1).CurrentWork);
        Assert.Equal("shields", Crew(session, 1).Room);
        Assert.False(PlayerSystem(session, "shields").Manned);
        Assert.True(Crew(session, 1).Health < health);
    }

    [Fact]
    public void ClosedDoorOpensForBoundedTraversalWindowAndExchangesGas()
    {
        var session = Start();
        session.Execute(new ShipSetPowerCommand(Id(), Power(3, 2, 0, 2)));
        session.Execute(new ShipSetDoorCommand(Id(), "door_airlock", true));
        session.AdvanceTicks(90);
        var passage = Room(session, "passage").OxygenPermille;
        var weapons = Room(session, "weapons").OxygenPermille;
        Assert.True(passage < weapons);
        session.Execute(new ShipMoveCrewCommand(Id(), "actor.protagonist", "engines"));
        var opened = false;
        var largestOpenExchange = 0;
        for (var tick = 0; tick < 300; tick++)
        {
            var before = Room(session, "weapons").OxygenPermille;
            session.AdvanceTicks(1);
            var door = session.Observe().Doors.Single(item => item.Id == "door_weapons");
            opened |= door.EffectivelyOpen && !door.CommandedOpen;
            if (door.EffectivelyOpen) { largestOpenExchange = Math.Max(largestOpenExchange, before - Room(session, "weapons").OxygenPermille); }
            Assert.True(door.TraversalTicksRemaining <= 36);
        }
        Assert.True(opened);
        Assert.True(largestOpenExchange >= 5, $"exchange {largestOpenExchange}");
        Assert.False(session.Observe().Doors.Single(item => item.Id == "door_weapons").EffectivelyOpen);
        Assert.Equal("engines", Crew(session, 0).Room);
    }

    [Fact]
    public void SingleLifeSupportBreachFromHealthyStateIsRecoverableByOneWorkerWithoutHullRefund()
    {
        var session = Start(SingleShot(ShipPayload.Breach, "life_support"));
        session.Execute(new ShipSetPowerCommand(Id(), Power(3, 0, 1, 2)));
        session.Execute(new ShipSetCrewModeCommand(Id(), "actor.companion.medic", ShipCrewMode.Hold));
        while (Room(session, "life_support").BreachSeverity == 0) { session.AdvanceTicks(1); }
        session.Execute(new ShipSetPowerCommand(Id(), Power(3, 3, 1, 1)));
        var hull = session.Observe().Player.Hull;
        // One worker walks over from shields after a full second of reaction time.
        session.AdvanceTicks(30);
        session.Execute(new ShipAssignTaskCommand(Id(), "actor.companion.protector", ShipTaskKind.Seal, "life_support"));
        var ticks = 0;
        while (Room(session, "life_support").BreachSeverity > 0 && ticks++ < 30 * 30) { session.AdvanceTicks(1); }
        Assert.Equal(0, Room(session, "life_support").BreachSeverity);
        Assert.All(session.Observe().Crew, crew => Assert.False(crew.Downed));
        Assert.True(Room(session, "life_support").OxygenPermille > 200);
        Assert.True(session.Observe().Player.Hull <= hull);
        Assert.Equal(ShipCrewMode.Auto, Crew(session, 1).Mode);
    }

    [Fact]
    public void MedicTreatmentRejectsIneligibleAndHealthyTargets()
    {
        var session = Start();
        Assert.Equal(ShipRejection.NotMedic, session.Execute(new ShipAssignTaskCommand(Id(), "actor.protagonist", ShipTaskKind.Treat, "weapons", "actor.companion.protector")).Rejection);
        Assert.Equal(ShipRejection.InvalidTreatTarget, session.Execute(new ShipAssignTaskCommand(Id(), "actor.companion.medic", ShipTaskKind.Treat, "weapons", "nobody")).Rejection);
        Assert.Equal(ShipRejection.TaskHasNoWork, session.Execute(new ShipAssignTaskCommand(Id(), "actor.companion.medic", ShipTaskKind.Treat, "shields", "actor.companion.protector")).Rejection);
        session.AdvanceTicks(1);
        Assert.Equal(ShipTaskKind.None, Crew(session, 2).ExplicitTask);
    }

    [Fact]
    public void DefeatTakesPrecedenceOverSimultaneousEnemyDestruction()
    {
        var gun = TestGun("weapons", ShipPayload.Normal, chargeSeconds: 10);
        var mirrored = Content with
        {
            Player = Content.Player with
            {
                Hull = 1, Weapons = [gun with { TargetSequence = [], PayloadSequence = [] }], ManningBonusPercent = 0,
                Systems = Content.Player.Systems.Select(system => system.Id == "shields" ? system with { InitialPower = 0 } : system).ToArray(),
            },
            Enemy = Content.Enemy with
            {
                Side = Content.Enemy.Side with
                {
                    Hull = 1, Weapons = [gun],
                    Systems = Content.Enemy.Side.Systems.Select(system => system.Id == "shields" ? system with { InitialPower = 0 } : system).ToArray(),
                },
            },
        };
        var session = Start(mirrored);
        foreach (var crew in ShipBattlePilot.StationCrew) { session.Execute(new ShipSetCrewModeCommand(Id(), crew.Id, ShipCrewMode.Hold)); }
        session.Execute(new ShipSetWeaponTargetCommand(Id(), gun.Id, "weapons"));
        session.AdvanceTicks(30 * 20);
        var observation = session.Observe();
        Assert.Equal(0, observation.Enemy.Hull);
        Assert.Equal(0, observation.Player.Hull);
        Assert.Equal(ShipBattlePhase.Defeat, observation.Phase);
        Assert.Equal(0, session.AdvanceTicks(10));
    }

    [Theory]
    [InlineData(ShipBattleStrategy.SuppressWeapons)]
    [InlineData(ShipBattleStrategy.OverwhelmDefenses)]
    public void TwoStrategiesWinWithinTheTunedBandAndExhaustEnemyRepairs(ShipBattleStrategy strategy)
    {
        var session = Start(unpause: false);
        Assert.Equal(ShipBattlePhase.Victory, new ShipBattlePilot(session, strategy).RunToEnd(5 * 60 * 30));
        Assert.InRange(session.Tick, ShipBattlePilot.MinimumWinSeconds * 30, ShipBattlePilot.MaximumWinSeconds * 30);
        Assert.All(session.Observe().Crew, crew => Assert.False(crew.Downed));
        Assert.Equal(0, session.Observe().EnemyRepair.ReserveBars);
        Assert.Equal(Content.Enemy.Repair.ReserveBars, session.Events.Count(item => item.Kind == "enemy_repaired"));
    }

    [Fact]
    public void PassivePlayLosesAndRetryResetsOnlyTheBattle()
    {
        var session = Start(unpause: false);
        Assert.Equal(ShipBattlePhase.Defeat, new ShipBattlePilot(session, ShipBattleStrategy.Passive).RunToEnd(5 * 60 * 30));
        Assert.Equal(ShipRejection.BattleEnded, session.Execute(new ShipMoveCrewCommand(Id(), "actor.protagonist", "engines")).Rejection);
        Assert.True(session.Execute(new ShipRestartCommand(Id())).Accepted);
        var fresh = session.Observe();
        Assert.Equal(2, fresh.Attempt);
        Assert.True(fresh.Paused);
        Assert.Equal(0, fresh.Tick);
        Assert.Equal(ShipBattlePhase.Active, fresh.Phase);
        Assert.Equal(Content.Player.Hull, fresh.Player.Hull);
        Assert.Empty(fresh.Shots);
        Assert.All(fresh.Crew, crew => Assert.Equal(crew.MaxHealth, crew.Health));
        Assert.All(fresh.Rooms, room => Assert.Equal(1000, room.OxygenPermille));
        Assert.Equal(Content.Enemy.Repair.ReserveBars, fresh.EnemyRepair.ReserveBars);
    }

    [Fact]
    public void EnemyTelegraphsAuthoredTargetAndPayloadPerWeapon()
    {
        var session = Start();
        var laser = Content.Enemy.Side.Weapons[0];
        var intent = session.Observe().Intents.Single(item => item.WeaponId == laser.Id);
        Assert.Equal(laser.TargetSequence[0], intent.TargetSystem);
        Assert.Equal(laser.PayloadSequence[0], intent.Payload);
        Assert.True(intent.TicksToFire > 0);
        Assert.Equal(Content.Enemy.Side.Weapons.Count, session.Observe().Intents.Count);
        while (!session.Events.Any(item => item.Kind == "weapon_fired" && item.Subject == "enemy" && item.Detail.StartsWith(laser.Id, StringComparison.Ordinal)))
        { session.AdvanceTicks(1); }
        Assert.Equal(laser.TargetSequence[1], session.Observe().Intents.Single(item => item.WeaponId == laser.Id).TargetSystem);
        Assert.All(session.Observe().Shots.Where(shot => shot.WeaponId == laser.Id), shot => Assert.Equal(laser.TargetSequence[0], shot.TargetSystem));
    }

    [Fact]
    public void ContinuationEntersExactlyOnceAfterPresentationAndResourcesAndRetriesFailures()
    {
        var continuation = new ShipContinuation();
        Assert.False(continuation.TryEnter());
        Assert.True(continuation.CaptureStationResult(ShipBattlePilot.StationCrew));
        Assert.False(continuation.CaptureStationResult(ShipBattlePilot.StationCrew));
        continuation.MarkLoadFailed("disk");
        Assert.Equal(ShipContinuationState.Failed, continuation.State);
        continuation.MarkDepartureFinished();
        Assert.False(continuation.TryEnter());
        Assert.True(continuation.RetryLoad());
        Assert.False(continuation.TryEnter());
        continuation.MarkResourcesReady();
        Assert.True(continuation.TryEnter());
        Assert.False(continuation.TryEnter());
        Assert.Equal(2, continuation.LoadAttempts);
        Assert.Equal(3, continuation.Crew.Count);
    }

    private static ShipCombatSession BurningShields(out ShipCombatSession session)
    {
        // Player choice (shields unpowered) lets one authored incendiary volley through; no forced penetration.
        session = Start(SingleShot(ShipPayload.Incendiary, "shields"));
        session.Execute(new ShipSetPowerCommand(Id(), Power(3, 0, 1, 2)));
        session.Execute(new ShipSetCrewModeCommand(Id(), "actor.companion.protector", ShipCrewMode.Hold));
        while (Room(session, "shields").FireSeverity == 0) { session.AdvanceTicks(1); }
        return session;
    }

    [Fact]
    public void MedicTreatmentStopsWhenTargetMovesAway()
    {
        BurningShields(out var session);
        session.AdvanceTicks(30 * 4);
        session.Execute(new ShipAssignTaskCommand(Id(), "actor.protagonist", ShipTaskKind.Extinguish, "shields"));
        while (Room(session, "shields").FireSeverity > 0) { session.AdvanceTicks(1); }
        var hurt = Crew(session, 1).Health;
        Assert.True(hurt < 100);
        session.Execute(new ShipSetCrewModeCommand(Id(), "actor.companion.medic", ShipCrewMode.Hold));
        Assert.Equal(ShipRejection.InvalidTreatTarget, session.Execute(new ShipAssignTaskCommand(Id(), "actor.companion.medic", ShipTaskKind.Treat, "weapons", "actor.companion.protector")).Rejection);
        Assert.True(session.Execute(new ShipAssignTaskCommand(Id(), "actor.companion.medic", ShipTaskKind.Treat, "shields", "actor.companion.protector")).Accepted);
        while (Crew(session, 2).Moving) { session.AdvanceTicks(1); }
        session.AdvanceTicks(30);
        Assert.Equal(ShipTaskKind.Treat, Crew(session, 2).CurrentWork);
        Assert.True(Crew(session, 1).Health > hurt);
        session.Execute(new ShipMoveCrewCommand(Id(), "actor.companion.protector", "engines"));
        session.AdvanceTicks(1);
        Assert.Equal(ShipTaskKind.None, Crew(session, 2).ExplicitTask);
        Assert.NotEqual(ShipTaskKind.Treat, Crew(session, 2).CurrentWork);
        Assert.Equal(ShipCrewMode.Hold, Crew(session, 2).Mode);
        var afterLeaving = Crew(session, 1).Health;
        session.AdvanceTicks(60);
        Assert.True(Crew(session, 1).Health <= afterLeaving);
    }

    [Fact]
    public void MedicCannotOuthealFireOrResurrectADownedTarget()
    {
        BurningShields(out var session);
        while (Crew(session, 1).Health > 25) { session.AdvanceTicks(1); }
        session.Execute(new ShipAssignTaskCommand(Id(), "actor.companion.medic", ShipTaskKind.Treat, "shields", "actor.companion.protector"));
        var ticks = 0;
        while (!Crew(session, 1).Downed && ticks++ < 30 * 120) { session.AdvanceTicks(1); }
        Assert.True(Crew(session, 1).Downed);
        Assert.Contains(session.Events, item => item.Kind == "crew_downed" && item.Subject == "actor.companion.protector");
        session.AdvanceTicks(1);
        Assert.Equal(ShipTaskKind.None, Crew(session, 2).ExplicitTask);
        session.Execute(new ShipMoveCrewCommand(Id(), "actor.companion.medic", "passage"));
        session.AdvanceTicks(30 * 5);
        Assert.True(Crew(session, 1).Downed);
        Assert.Equal(0, Crew(session, 1).Health);
        Assert.False(Crew(session, 2).Downed, $"medic {Crew(session, 2).Health} phase {session.Phase}");
        Assert.Equal(ShipRejection.InvalidTreatTarget,
            session.Execute(new ShipAssignTaskCommand(Id(), "actor.companion.medic", ShipTaskKind.Treat, "shields", "actor.companion.protector")).Rejection);
    }

    [Fact]
    public void PauseTogglingNeverCreatesChargeOrWork()
    {
        var steady = Start();
        var toggled = Start();
        steady.AdvanceTicks(150);
        for (var index = 0; index < 150; index++)
        {
            toggled.Execute(new ShipSetPauseCommand(Id(), true));
            toggled.AdvanceTicks(5);
            toggled.Advance(TimeSpan.FromSeconds(1));
            toggled.Execute(new ShipSetPauseCommand(Id(), false));
            toggled.AdvanceTicks(1);
        }
        Assert.Equal(steady.Tick, toggled.Tick);
        Assert.Equal(steady.Observe().Player.Weapons.Select(weapon => weapon.ChargePermille), toggled.Observe().Player.Weapons.Select(weapon => weapon.ChargePermille));
        Assert.Equal(steady.Observe().Intents.Select(intent => intent.TicksToFire), toggled.Observe().Intents.Select(intent => intent.TicksToFire));
    }

    [Fact]
    public void PowerTogglingNeverCreatesChargeOrRefillsShields()
    {
        var session = Start();
        session.AdvanceTicks(90);
        int Charge() => session.Observe().Player.Weapons[0].ChargePermille;
        var charge = Charge();
        session.Execute(new ShipSetPowerCommand(Id(), Power(0, 3, 1, 2)));
        session.Execute(new ShipSetPowerCommand(Id(), Power(3, 2, 1, 2)));
        Assert.Equal(charge, Charge());
        session.Execute(new ShipSetPowerCommand(Id(), Power(0, 3, 1, 2)));
        session.AdvanceTicks(1);
        Assert.True(Charge() < charge);

        Assert.Equal(2, session.Observe().Player.ShieldLayers);
        session.Execute(new ShipSetPowerCommand(Id(), Power(3, 0, 1, 2)));
        session.AdvanceTicks(1);
        Assert.Equal(0, session.Observe().Player.ShieldLayers);
        session.Execute(new ShipSetPowerCommand(Id(), Power(3, 3, 1, 1)));
        session.AdvanceTicks(1);
        Assert.Equal(0, session.Observe().Player.ShieldLayers);
        session.AdvanceTicks(30 * 2 - 2);
        Assert.True(session.Observe().Player.ShieldLayers <= 1);
    }

    [Fact]
    public void ContinuationWaitsForDepartureWhenResourcesArriveFirst()
    {
        var continuation = new ShipContinuation();
        continuation.CaptureStationResult(ShipBattlePilot.StationCrew);
        continuation.MarkResourcesReady();
        Assert.False(continuation.TryEnter());
        continuation.MarkDepartureFinished();
        Assert.True(continuation.TryEnter());
        Assert.False(continuation.TryEnter());
    }

    [Fact]
    public void ContinuationWaitsForResourcesWhenDepartureFinishesFirst()
    {
        var continuation = new ShipContinuation();
        continuation.MarkDepartureFinished();
        continuation.MarkResourcesReady();
        Assert.False(continuation.TryEnter());
        continuation.CaptureStationResult(ShipBattlePilot.StationCrew);
        continuation.MarkDepartureFinished();
        Assert.False(continuation.TryEnter());
        continuation.MarkResourcesReady();
        Assert.True(continuation.TryEnter());
        Assert.Equal(ShipBattlePilot.StationCrew.Select(crew => crew.Id), continuation.Crew.Select(crew => crew.Id));
    }

    [Fact]
    public void ContinuationEntryFailureIsWithdrawnAndRetryable()
    {
        var continuation = new ShipContinuation();
        continuation.CaptureStationResult(ShipBattlePilot.StationCrew);
        continuation.MarkDepartureFinished();
        continuation.MarkResourcesReady();
        Assert.True(continuation.TryEnter());
        continuation.MarkEntryFailed("instantiate");
        Assert.Equal(ShipContinuationState.Failed, continuation.State);
        Assert.False(continuation.TryEnter());
        Assert.True(continuation.RetryLoad());
        Assert.False(continuation.TryEnter());
        continuation.MarkResourcesReady();
        Assert.True(continuation.TryEnter());
        Assert.False(continuation.TryEnter());
        Assert.Equal(2, continuation.LoadAttempts);
    }
}
