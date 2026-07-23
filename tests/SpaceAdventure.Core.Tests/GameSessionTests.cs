using SpaceAdventure.Core;
using Xunit;

namespace SpaceAdventure.Core.Tests;

public sealed class GameSessionTests
{
    [Fact]
    public void SetPauseCommandIsObservableAndStopsGameplayTicks()
    {
        var session = new GameSession();
        var acknowledgement = session.Execute(
            new SetPauseCommand(new CommandId("test.pause"), Paused: true));

        Assert.True(acknowledgement.Accepted);
        Assert.True(acknowledgement.Observation.Paused);
        Assert.Equal(0, session.AdvanceTicks(5));
        Assert.Equal(0, session.Tick);

        var pauseEvent = Assert.Single(
            session.EventsSince(0),
            gameEvent => gameEvent.Type == GameplayEventType.PauseChanged);
        Assert.True(pauseEvent.Paused);
    }

    [Fact]
    public void PausedSessionCanBeSteppedExplicitlyForDevelopment()
    {
        var session = new GameSession();
        session.Execute(new SetPauseCommand(new CommandId("test.pause"), Paused: true));

        Assert.Equal(3, session.StepWhilePaused(3));
        Assert.Equal(3, session.Observe().Tick);
    }

    [Fact]
    public void DirectTickAdvancementRejectsAnUnboundedCount()
    {
        var session = new GameSession();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => session.AdvanceTicks(GameSession.MaximumDirectTickAdvance + 1));

        session.Execute(new SetPauseCommand(new CommandId("test.pause"), Paused: true));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => session.StepWhilePaused(GameSession.MaximumDirectTickAdvance + 1));
        Assert.Equal(0, session.Tick);
    }

    [Fact]
    public void EventHistoryIsBounded()
    {
        var session = new GameSession();

        for (var index = 0; index < GameSession.MaximumRetainedEvents; index++)
        {
            session.Execute(
                new SetPauseCommand(new CommandId($"test.pause.{index}"), Paused: index % 2 == 0));
        }

        var retained = session.EventsSince(0);
        Assert.Equal(GameSession.MaximumRetainedEvents, retained.Count);
        Assert.True(retained[0].Sequence > 1);
        Assert.Equal(retained[0].Sequence, session.OldestRetainedEventSequence);
        Assert.True(session.WasEventHistoryTruncatedAfter(0));
        Assert.False(session.WasEventHistoryTruncatedAfter(retained[0].Sequence - 1));
    }
}
