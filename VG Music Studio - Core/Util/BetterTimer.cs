using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kermalis.VGMusicStudio.Core.Util;

internal sealed class BetterTimer(double emulatedTicksPerSecond)
{
    private readonly Stopwatch _sw = new();
    private static readonly double _swIntervalDouble = 1d / Stopwatch.Frequency;
    private static readonly float _swIntervalFloat = 1f / Stopwatch.Frequency;
    private static readonly long _swFrequency = Stopwatch.Frequency;
    private readonly double _emulatedTicksPerSecond = emulatedTicksPerSecond;
    private readonly double _tickDelta = 1d / emulatedTicksPerSecond;
    private long _swPrevTimerFrame;
    private long _swCurTimerFrame;

    public double GetTicksPerSecond()
    {
        return _emulatedTicksPerSecond;
    }
    public double GetDeltaTick()
    {
        return _tickDelta;
    }
    public double GetDeltaTime()
    {
        _swCurTimerFrame = GetElapsedTicks();
        var delta = (_swCurTimerFrame - _swPrevTimerFrame) * _swIntervalDouble;

        _swPrevTimerFrame = _swCurTimerFrame;
        return delta;
    }
    public static long GetFrequency()
    {
        return _swFrequency;
    }
    public static float GetIntervalFloat()
    {
        return _swIntervalFloat;
    }
    public static double GetInterval()
    {
        return _swIntervalDouble;
    }
    public long GetElapsedTicks()
    {
        return _sw.Elapsed.Ticks;
    }
    public int GetElapsedNanoseconds()
    {
        return _sw.Elapsed.Nanoseconds;
    }
    public int GetElapsedMicroseconds()
    {
        return _sw.Elapsed.Microseconds;
    }
    public int GetElapsedMilliseconds()
    {
        return _sw.Elapsed.Milliseconds;
    }
    public int GetElapsedSeconds()
    {
        return _sw.Elapsed.Seconds;
    }
    public int GetElapsedMinutes()
    {
        return _sw.Elapsed.Minutes;
    }
    public int GetElapsedHours()
    {
        return _sw.Elapsed.Hours;
    }
    public int GetElapsedDays()
    {
        return _sw.Elapsed.Days;
    }
    public double GetTotalElapsedNanoseconds()
    {
        return _sw.Elapsed.TotalNanoseconds;
    }
    public double GetTotalElapsedMicroseconds()
    {
        return _sw.Elapsed.TotalMicroseconds;
    }
    public double GetTotalElapsedMilliseconds()
    {
        return _sw.Elapsed.TotalMilliseconds;
    }
    public double GetTotalElapsedSeconds()
    {
        return _sw.Elapsed.TotalSeconds;
    }
    public double GetTotalElapsedMinutes()
    {
        return _sw.Elapsed.TotalMinutes;
    }
    public double GetTotalElapsedHours()
    {
        return _sw.Elapsed.TotalHours;
    }
    public double GetTotalElapsedDays()
    {
        return _sw.Elapsed.TotalDays;
    }

    public void Start()
    {
        _sw.Start();
    }
    public void Restart()
    {
        _sw.Restart();
    }
    public void Reset()
    {
        _sw.Reset();
    }
    public void Stop()
    {
        _sw.Stop();
    }
}
