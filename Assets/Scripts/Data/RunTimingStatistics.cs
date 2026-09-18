using System;
using System.Collections.Generic;

[Serializable]
public readonly struct RunMapSplit
{
    public RunMapSplit(int mapIndex, double elapsedSeconds, bool isComplete)
    {
        MapIndex = mapIndex;
        ElapsedSeconds = Math.Max(0d, elapsedSeconds);
        IsComplete = isComplete;
    }

    public int MapIndex { get; }
    public int MapNumber => MapIndex + 1;
    public double ElapsedSeconds { get; }
    public bool IsComplete { get; }
}

[Serializable]
public sealed class RunTimingStatistics
{
    readonly List<RunMapSplit> _completedSplits = new();

    public bool IsActive { get; private set; }
    public int CurrentMapIndex { get; private set; }
    public double TotalElapsedSeconds { get; private set; }
    public double CurrentMapElapsedSeconds { get; private set; }
    public IReadOnlyList<RunMapSplit> CompletedSplits => _completedSplits;
    public RunMapSplit CurrentIncompleteSplit =>
        new(CurrentMapIndex, CurrentMapElapsedSeconds, false);

    public void StartNewRun(int startingMapIndex = 0)
    {
        Reset();
        CurrentMapIndex = Math.Max(0, startingMapIndex);
        IsActive = true;
    }

    public void Advance(double deltaSeconds)
    {
        if (!IsActive || deltaSeconds <= 0d || double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds))
            return;

        TotalElapsedSeconds += deltaSeconds;
        CurrentMapElapsedSeconds += deltaSeconds;
    }

    public bool CompleteCurrentMap(bool beginNextMap = true)
    {
        if (!IsActive) return false;

        _completedSplits.Add(new RunMapSplit(CurrentMapIndex, CurrentMapElapsedSeconds, true));
        CurrentMapElapsedSeconds = 0d;
        if (beginNextMap)
            CurrentMapIndex++;
        return true;
    }

    public void StopRun()
    {
        IsActive = false;
    }

    public void Reset()
    {
        IsActive = false;
        CurrentMapIndex = 0;
        TotalElapsedSeconds = 0d;
        CurrentMapElapsedSeconds = 0d;
        _completedSplits.Clear();
    }
}

public static class RunDurationFormatter
{
    public static string Format(double elapsedSeconds)
    {
        long totalSeconds = (long)Math.Floor(Math.Max(0d, elapsedSeconds));
        long hours = totalSeconds / 3600;
        long minutes = totalSeconds % 3600 / 60;
        long seconds = totalSeconds % 60;
        return hours > 0
            ? $"{hours}:{minutes:00}:{seconds:00}"
            : $"{minutes:00}:{seconds:00}";
    }
}
