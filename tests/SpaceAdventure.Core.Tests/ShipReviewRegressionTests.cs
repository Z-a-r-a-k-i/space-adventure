using SpaceAdventure.Core;
using SpaceAdventure.SimCli;
using Xunit;

namespace SpaceAdventure.Core.Tests;

public sealed class ShipReviewRegressionTests
{
    private static readonly ShipBattleDefinition Content = ShipBattlePilot.LoadDefinition(AppContext.BaseDirectory);

    [Theory]
    [InlineData("weapon")]
    [InlineData("passage")]
    [InlineData("shields")]
    public void InvalidSystemRoomIsRejectedWhileParsingContent(string room)
    {
        var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "content", "ship-battle.json")))!;
        json["player"]!["systems"]![0]!["room"] = room;
        Assert.Throws<InvalidDataException>(() => ShipBattleDefinition.ParseJson(json.ToJsonString()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AdditionalRoomRequiresItsOwnPassageDoor(bool connected)
    {
        var extraRoom = Content.Rooms[0] with { Id = "cargo" };
        var door = Content.Doors.First(item => !item.Exterior) with
        { Id = "door_cargo", RoomA = "cargo", RoomB = ShipBattleDefinition.Passage };
        var definition = Content with
        {
            Rooms = Content.Rooms.Append(extraRoom).ToArray(),
            Doors = connected ? Content.Doors.Append(door).ToArray() : Content.Doors,
        };
        if (connected) { definition.Validate(); }
        else { Assert.Throws<InvalidDataException>(() => definition.Validate()); }
    }

    [Fact]
    public void DuplicateDoorIdsAreRejectedWhileParsingContent()
    {
        var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "content", "ship-battle.json")))!;
        json["doors"]![1]!["id"] = json["doors"]![0]!["id"]!.GetValue<string>();
        Assert.Throws<InvalidDataException>(() => ShipBattleDefinition.ParseJson(json.ToJsonString()));
    }

    private static CommandId Id() => new(Guid.NewGuid().ToString());
    private static ShipRoomObservation Room(ShipCombatSession session, string id) => session.Observe().Rooms.Single(r => r.Id == id);
    private static void Send(ShipCombatSession session, IShipCommand command) => Assert.True(session.Execute(command).Accepted);
    private static Dictionary<string,int> Power(int weapons, int shields, int life, int engines) => new()
    { ["weapons"]=weapons, ["shields"]=shields, ["life_support"]=life, ["engines"]=engines };

    [Fact]
    public void RelitFireGetsFreshSpreadAndSystemDamageIntervals()
    {
        var definition = Content with
        {
            Work = Content.Work with { ExtinguishSecondsPerSeverity = 1 },
            Hazards = Content.Hazards with { FireCrewDamagePerSecondPerSeverity = 0 },
            Enemy = Content.Enemy with
            {
                Side = Content.Enemy.Side with { Weapons = [ShipCombatTests.TestGun("shields", ShipPayload.Incendiary, chargeSeconds: 30)] },
            },
        };
        var session = new ShipCombatSession(definition, ShipBattlePilot.StationCrew);
        Send(session, new ShipSetPowerCommand(Id(), Power(0,0,2,0)));
        foreach (var crew in ShipBattlePilot.StationCrew) Send(session, new ShipSetCrewModeCommand(Id(), crew.Id, ShipCrewMode.Hold));
        Send(session, new ShipSetPauseCommand(Id(), false));
        while (session.Tick < 35*30 && Room(session,"shields").FireSeverity == 0) session.AdvanceTicks(1);
        Assert.Equal(1, Room(session,"shields").FireSeverity);
        session.AdvanceTicks(5*30);
        Send(session, new ShipAssignTaskCommand(Id(), "actor.companion.protector", ShipTaskKind.Extinguish, "shields"));
        for (var i=0; i<90 && Room(session,"shields").FireSeverity>0; i++) session.AdvanceTicks(1);
        Assert.Equal(0, Room(session,"shields").FireSeverity);
        Send(session, new ShipSetCrewModeCommand(Id(), "actor.companion.protector", ShipCrewMode.Hold));
        while (session.Tick < 65*30 && Room(session,"shields").FireSeverity == 0) session.AdvanceTicks(1);
        Assert.Equal(1, Room(session,"shields").FireSeverity);
        var relit = session.Tick;
        Send(session, new ShipSetDoorCommand(Id(), "door_shields", true));
        session.AdvanceTicks(8*30-2);
        Assert.DoesNotContain(session.Events, e => e.Tick>=relit && e.Kind is "fire_spread" or "fire_damaged_system");
        session.AdvanceTicks(1);
        Assert.Contains(session.Events, e => e.Kind=="fire_spread" && e.Subject=="passage" && e.Tick-relit==8*30-1);
        session.AdvanceTicks(4*30);
        Assert.Contains(session.Events, e => e.Kind=="fire_damaged_system" && e.Subject=="shields" && e.Tick-relit==12*30-1);
    }

    [Theory]
    [InlineData(ShipTaskKind.Extinguish)]
    [InlineData(ShipTaskKind.Seal)]
    [InlineData(ShipTaskKind.Repair)]
    [InlineData(ShipTaskKind.Treat)]
    public void NoWorkOrdersDoNotMoveCrewOrUndoHold(ShipTaskKind task)
    {
        var session = new ShipCombatSession(Content, ShipBattlePilot.StationCrew);
        var id = task==ShipTaskKind.Treat ? "actor.companion.medic" : "actor.protagonist";
        var room = task==ShipTaskKind.Treat ? "life_support" : "weapons";
        Send(session, new ShipSetCrewModeCommand(Id(), id, ShipCrewMode.Hold));
        var result=session.Execute(new ShipAssignTaskCommand(Id(), id, task, room, task==ShipTaskKind.Treat ? id : null));
        Assert.Equal(ShipRejection.TaskHasNoWork, result.Rejection);
        Send(session, new ShipSetPauseCommand(Id(), false)); session.AdvanceTicks(1);
        var crew=session.Observe().Crew.Single(c=>c.Id==id);
        Assert.Equal(ShipCrewMode.Hold, crew.Mode);
        Assert.Equal(ShipTaskKind.None, crew.CurrentWork);
        Assert.False(crew.Moving);
        Assert.Null(crew.Destination);
    }

    [Fact]
    public void SameRoomWarningOmitsPassageExceptWhenCompletingADoorCrossing()
    {
        var session = new ShipCombatSession(Content, ShipBattlePilot.StationCrew);
        Send(session, new ShipSetPowerCommand(Id(), Power(3,2,0,2)));
        Send(session, new ShipSetDoorCommand(Id(), "door_airlock", true));
        Send(session, new ShipSetPauseCommand(Id(), false)); session.AdvanceTicks(90);
        Assert.True(Room(session,"passage").OxygenPermille<200);
        Assert.Null(session.Execute(new ShipAssignTaskCommand(Id(), "actor.protagonist", ShipTaskKind.Man, "weapons")).Warning);
        Assert.Contains("passage", session.Execute(new ShipMoveCrewCommand(Id(), "actor.protagonist", "engines")).Warning);
        for(var i=0; i<30 && session.Observe().Crew[0].Z< -2.05; i++) session.AdvanceTicks(1);
        Assert.Equal("weapons", session.Observe().Crew[0].Room);
        Assert.Contains("passage", session.Execute(new ShipMoveCrewCommand(Id(), "actor.protagonist", "weapons")).Warning);
    }

    [Fact]
    public void LongWeaponChargeHasBoundedDisplayOnBothSides()
    {
        var definition=Content with
        {
            Player=Content.Player with { Weapons=Content.Player.Weapons.Select(w=>w with { ChargeSeconds=30 }).ToArray(), ManningBonusPercent=0 },
            Enemy=Content.Enemy with { Side=Content.Enemy.Side with { Weapons=Content.Enemy.Side.Weapons.Select(w=>w with { ChargeSeconds=30 }).ToArray() } },
        };
        var session=new ShipCombatSession(definition, ShipBattlePilot.StationCrew);
        Send(session,new ShipSetPauseCommand(Id(),false)); session.AdvanceTicks(810);
        foreach(var side in new[]{session.Observe().Player,session.Observe().Enemy})
        {
            Assert.All(side.Weapons, weapon => Assert.Equal(900, weapon.ChargePermille));
            Assert.All(side.Weapons, weapon => Assert.Equal(90, weapon.TicksToFire));
        }
    }
}
