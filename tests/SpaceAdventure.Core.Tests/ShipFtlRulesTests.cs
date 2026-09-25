using SpaceAdventure.Core;
using SpaceAdventure.SimCli;
using Xunit;

namespace SpaceAdventure.Core.Tests;

/// <summary>Per-weapon power, shield layers, piercing missiles, seeded evasion, crew injury and Medic automation.</summary>
public sealed class ShipFtlRulesTests
{
    private static readonly ShipBattleDefinition Content = ShipBattlePilot.LoadDefinition(AppContext.BaseDirectory);
    private static int _ids;

    private static CommandId Id() => new($"ftl.{Interlocked.Increment(ref _ids)}");

    private static Dictionary<string, int> Power(int weapons, int shields, int lifeSupport, int engines) =>
        new() { ["weapons"] = weapons, ["shields"] = shields, ["life_support"] = lifeSupport, ["engines"] = engines };

    private static ShipCombatSession Start(ShipBattleDefinition definition, ulong? seed = null)
    {
        var session = new ShipCombatSession(definition, ShipBattlePilot.StationCrew, seed);
        Assert.True(session.Execute(new ShipSetPauseCommand(Id(), false)).Accepted);
        return session;
    }

    private static ShipBattleDefinition EnemyWeapons(params ShipWeaponDefinition[] weapons) =>
        Content with { Enemy = Content.Enemy with { Side = Content.Enemy.Side with { Weapons = weapons } } };

    private static void RunUntil(ShipCombatSession session, Func<bool> condition, int limit = 30 * 60)
    {
        for (var tick = 0; tick < limit && !condition(); tick++) { session.AdvanceTicks(1); }
        Assert.True(condition(), "condition not reached");
    }

    [Fact]
    public void ShieldLayersStopLasersOneShotEachWhileMissilesPierceForFullSystemDamage()
    {
        var laser = ShipCombatTests.TestGun("engines", ShipPayload.Normal, shots: 1, chargeSeconds: 4, id: "laser");
        var missile = ShipCombatTests.TestGun("engines", ShipPayload.Normal, damage: 2, chargeSeconds: 8, pierces: true, kind: ShipWeaponKind.Missile, id: "missile");
        var session = Start(EnemyWeapons(laser, missile));
        foreach (var crew in ShipBattlePilot.StationCrew) { session.Execute(new ShipSetCrewModeCommand(Id(), crew.Id, ShipCrewMode.Hold)); }
        Assert.Equal(2, session.Observe().Player.ShieldLayers);
        RunUntil(session, () => session.Events.Any(item => item.Kind == "shield_absorbed" && item.Detail.Contains(":laser:", StringComparison.Ordinal)));
        Assert.Equal(1, session.Observe().Player.ShieldLayers);
        Assert.Equal(Content.Player.Hull, session.Observe().Player.Hull);

        RunUntil(session, () => session.Events.Any(item => item.Kind == "hull_hit" && item.Detail.Contains(":missile:", StringComparison.Ordinal)));
        var observation = session.Observe();
        Assert.Equal(Content.Player.Hull - 2, observation.Player.Hull);
        Assert.Equal(2, observation.Player.Systems.Single(system => system.Id == "engines").Damage);
        Assert.DoesNotContain(session.Events, item => item.Kind == "shield_absorbed" && item.Detail.Contains(":missile:", StringComparison.Ordinal));
        Assert.Equal(0, observation.Player.EvasionPercent);
    }

    [Fact]
    public void HullHitsInjureEveryCrewMemberInTheTargetRoomByDamage()
    {
        var gun = ShipCombatTests.TestGun("weapons", ShipPayload.Normal, damage: 2, chargeSeconds: 6, pierces: true);
        var session = Start(EnemyWeapons(gun));
        session.Execute(new ShipSetCrewModeCommand(Id(), "actor.companion.medic", ShipCrewMode.Hold));
        session.Execute(new ShipMoveCrewCommand(Id(), "actor.companion.protector", "weapons"));
        RunUntil(session, () => !session.Observe().Crew[1].Moving);
        RunUntil(session, () => session.Events.Any(item => item.Kind == "hull_hit"));
        var crew = session.Observe().Crew;
        Assert.Equal(100 - 2 * Content.Crew.HitDamagePerPoint, crew[0].Health);
        Assert.Equal(100 - 2 * Content.Crew.HitDamagePerPoint, crew[1].Health);
        Assert.Equal(100, crew[2].Health);
        Assert.Equal(2, session.Events.Count(item => item.Kind == "crew_hit"));
    }

    [Fact]
    public void EvasionRollsAreSeededPerAttemptAndMissilesNeverMiss()
    {
        // Hits land on weapons so evasion (engines) stays constant; crew injury is off to keep everyone standing.
        var laser = ShipCombatTests.TestGun("weapons", ShipPayload.Normal, shots: 3, chargeSeconds: 2, evadable: true, id: "laser");
        var missile = ShipCombatTests.TestGun("weapons", ShipPayload.Normal, chargeSeconds: 3, pierces: true, kind: ShipWeaponKind.Missile, id: "missile");
        var definition = EnemyWeapons(laser, missile) with
        {
            Player = Content.Player with { Hull = 999, EvasionPercentPerEnginePower = 25, EvasionMaxPercent = 50, ManningBonusPercent = 0 },
            Crew = Content.Crew with { HitDamagePerPoint = 0 },
        };
        string Rolls(ShipCombatSession session) => string.Concat(session.Events
            .Where(item => item.Attempt == session.Attempt && item.Kind is "shot_missed" or "shield_absorbed" or "hull_hit" && item.Detail.Contains(":laser:", StringComparison.Ordinal))
            .Select(item => item.Kind == "shot_missed" ? 'm' : 'h'));

        var first = Start(definition, seed: 7);
        var twin = Start(definition, seed: 7);
        var other = Start(definition, seed: 8);
        foreach (var session in new[] { first, twin, other })
        {
            foreach (var crew in ShipBattlePilot.StationCrew) { session.Execute(new ShipSetCrewModeCommand(Id(), crew.Id, ShipCrewMode.Hold)); }
            session.AdvanceTicks(30 * 40);
        }
        Assert.Equal(50, first.Observe().Player.EvasionPercent);
        Assert.Equal(Rolls(first), Rolls(twin));
        Assert.NotEqual(Rolls(first), Rolls(other));
        Assert.InRange(Rolls(first).Count(roll => roll == 'm'), 20, 40);
        Assert.DoesNotContain(first.Events, item => item.Kind == "shot_missed" && item.Detail.Contains(":missile:", StringComparison.Ordinal));
        Assert.Contains(first.Events, item => item.Kind == "hull_hit" && item.Detail.Contains(":missile:", StringComparison.Ordinal));

        var firstAttempt = Rolls(first);
        first.Execute(new ShipRestartCommand(Id()));
        first.Execute(new ShipSetPauseCommand(Id(), false));
        foreach (var crew in ShipBattlePilot.StationCrew) { first.Execute(new ShipSetCrewModeCommand(Id(), crew.Id, ShipCrewMode.Hold)); }
        first.AdvanceTicks(30 * 40);
        Assert.NotEqual(firstAttempt, Rolls(first));
    }

    [Fact]
    public void ArmedWeaponsDrawSystemPowerInMountingOrderAndRepowerWhenCapacityReturns()
    {
        var session = new ShipCombatSession(Content, ShipBattlePilot.StationCrew);
        bool[] Powered() => session.Observe().Player.Weapons.Select(weapon => weapon.Powered).ToArray();
        Assert.Equal([true, true], Powered());
        session.Execute(new ShipSetPowerCommand(Id(), Power(2, 2, 2, 2)));
        Assert.Equal([true, false], Powered());
        session.Execute(new ShipSetPowerCommand(Id(), Power(1, 3, 2, 2)));
        Assert.Equal([false, true], Powered());
        session.Execute(new ShipSetPowerCommand(Id(), Power(2, 2, 2, 2)));
        Assert.True(session.Execute(new ShipSetWeaponPowerCommand(Id(), "burst_laser", Armed: false)).Accepted);
        Assert.Equal([false, true], Powered());
        Assert.False(session.Observe().Player.Weapons[0].Armed);
        session.Execute(new ShipSetWeaponPowerCommand(Id(), "burst_laser", Armed: true));
        Assert.Equal([true, false], Powered());
        Assert.Equal(ShipRejection.UnknownWeapon, session.Execute(new ShipSetWeaponPowerCommand(Id(), "railgun", true)).Rejection);
        Assert.Equal(ShipRejection.UnknownWeapon, session.Execute(new ShipSetWeaponTargetCommand(Id(), "railgun", "shields")).Rejection);
        Assert.Equal(ShipRejection.UnknownSystem, session.Execute(new ShipSetWeaponTargetCommand(Id(), "burst_laser", "life_support")).Rejection);
        Assert.Equal(0, session.Tick);
    }

    [Fact]
    public void HoldFireLetsBothWeaponsReleaseOneSynchronizedVolley()
    {
        var session = Start(EnemyWeapons(ShipCombatTests.TestGun("engines", ShipPayload.Normal, chargeSeconds: 600)));
        session.Execute(new ShipSetHoldFireCommand(Id(), true));
        foreach (var weapon in Content.Player.Weapons) { session.Execute(new ShipSetWeaponTargetCommand(Id(), weapon.Id, "shields")); }
        RunUntil(session, () => session.Observe().Player.Weapons.All(weapon => weapon.ChargePermille == 1000));
        Assert.DoesNotContain(session.Events, item => item.Kind == "weapon_fired" && item.Subject == "player");
        session.Execute(new ShipSetHoldFireCommand(Id(), false));
        session.AdvanceTicks(1);
        var volleys = session.Events.Where(item => item.Kind == "weapon_fired" && item.Subject == "player").ToArray();
        Assert.Equal(Content.Player.Weapons.Count, volleys.Length);
        Assert.Single(volleys.Select(item => item.Tick).Distinct());
        Assert.Equal(Content.Player.Weapons.Sum(weapon => weapon.Shots), session.Observe().Shots.Count(shot => shot.Source == "player"));
    }

    [Fact]
    public void MissileAmmoIsSpentPerVolleyAndAnEmptyLauncherStopsFiring()
    {
        var definition = Content with
        {
            Player = Content.Player with { Weapons = Content.Player.Weapons.Select(weapon => weapon.Kind == ShipWeaponKind.Missile ? weapon with { Ammo = 1 } : weapon).ToArray() },
            Enemy = Content.Enemy with { Side = Content.Enemy.Side with { Weapons = [ShipCombatTests.TestGun("engines", ShipPayload.Normal, chargeSeconds: 600)] } },
        };
        var missile = definition.Player.Weapons.Single(weapon => weapon.Kind == ShipWeaponKind.Missile);
        var session = Start(definition);
        session.Execute(new ShipSetWeaponTargetCommand(Id(), missile.Id, "engines"));
        session.AdvanceTicks(30 * 60);
        Assert.Single(session.Events, item => item.Kind == "weapon_fired" && item.Detail.StartsWith(missile.Id, StringComparison.Ordinal));
        var observed = session.Observe().Player.Weapons.Single(weapon => weapon.Id == missile.Id);
        Assert.Equal(0, observed.Ammo);
        Assert.Equal(1000, observed.ChargePermille);
    }

    [Fact]
    public void AutoMedicTreatsTheMostInjuredSettledAllyInItsRoomBeforeManning()
    {
        var gun = ShipCombatTests.TestGun("life_support", ShipPayload.Normal, damage: 2, chargeSeconds: 3, pierces: true, ammo: 1);
        var session = Start(EnemyWeapons(gun));
        RunUntil(session, () => session.Events.Any(item => item.Kind == "crew_hit"));
        session.AdvanceTicks(1);
        var medic = session.Observe().Crew[2];
        Assert.Equal(ShipTaskKind.Treat, medic.CurrentWork);
        Assert.Equal(medic.Id, medic.TreatTarget);
        var hurt = medic.Health;
        session.AdvanceTicks(30);
        Assert.True(session.Observe().Crew[2].Health > hurt);
        RunUntil(session, () => session.Observe().Crew[2].CurrentWork != ShipTaskKind.Treat);
        Assert.Equal(100, session.Observe().Crew[2].Health);
        // Healed; the damaged life support comes next, still without an explicit order.
        Assert.Equal(ShipTaskKind.Repair, session.Observe().Crew[2].CurrentWork);
        Assert.Equal(ShipTaskKind.None, session.Observe().Crew[2].ExplicitTask);
    }
}
