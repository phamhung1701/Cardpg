using NUnit.Framework;

public sealed class RunTimingStatisticsTests
{
    [Test]
    public void ActiveRun_AdvancesRecordsSplitsAndPreservesIncompleteMapWhenStopped()
    {
        var timing = new RunTimingStatistics();
        timing.StartNewRun();

        timing.Advance(12.75d);
        timing.Advance(0d);
        Assert.That(timing.TotalElapsedSeconds, Is.EqualTo(12.75d));
        Assert.That(timing.CurrentMapElapsedSeconds, Is.EqualTo(12.75d));

        Assert.That(timing.CompleteCurrentMap(), Is.True);
        Assert.That(timing.CompletedSplits, Has.Count.EqualTo(1));
        Assert.That(timing.CompletedSplits[0].MapNumber, Is.EqualTo(1));
        Assert.That(timing.CompletedSplits[0].ElapsedSeconds, Is.EqualTo(12.75d));
        Assert.That(timing.CurrentMapIndex, Is.EqualTo(1));
        Assert.That(timing.CurrentMapElapsedSeconds, Is.Zero);

        timing.Advance(6.25d);
        timing.StopRun();
        timing.Advance(10d);

        Assert.That(timing.IsActive, Is.False);
        Assert.That(timing.TotalElapsedSeconds, Is.EqualTo(19d));
        Assert.That(timing.CurrentIncompleteSplit.MapNumber, Is.EqualTo(2));
        Assert.That(timing.CurrentIncompleteSplit.ElapsedSeconds, Is.EqualTo(6.25d));
    }

    [Test]
    public void StartNewRun_ResetsAllTimingAndSupportsMapIndicesBeyondTwelve()
    {
        var timing = new RunTimingStatistics();
        timing.StartNewRun(12);
        timing.Advance(90d);
        timing.CompleteCurrentMap();

        Assert.That(timing.CompletedSplits[0].MapNumber, Is.EqualTo(13));
        Assert.That(timing.CurrentMapIndex, Is.EqualTo(13));

        timing.StartNewRun();

        Assert.That(timing.IsActive, Is.True);
        Assert.That(timing.CurrentMapIndex, Is.Zero);
        Assert.That(timing.TotalElapsedSeconds, Is.Zero);
        Assert.That(timing.CurrentMapElapsedSeconds, Is.Zero);
        Assert.That(timing.CompletedSplits, Is.Empty);
    }

    [TestCase(0d, "00:00")]
    [TestCase(754d, "12:34")]
    [TestCase(5538d, "1:32:18")]
    [TestCase(360000d, "100:00:00")]
    public void DurationFormatter_RemainsReadablePastOneHour(double seconds, string expected)
    {
        Assert.That(RunDurationFormatter.Format(seconds), Is.EqualTo(expected));
    }
}
