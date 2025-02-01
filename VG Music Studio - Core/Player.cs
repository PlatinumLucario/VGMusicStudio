using PortAudio;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Kermalis.EndianBinaryIO;
using Kermalis.VGMusicStudio.Core.Formats;
using Kermalis.VGMusicStudio.Core.Util;
using System.Timers;
using System.Runtime.InteropServices;

namespace Kermalis.VGMusicStudio.Core;

public enum PlayerState : byte
{
	Stopped,
	Playing,
	Paused,
	Recording,
	ShutDown,
}

public interface ILoadedSong
{
	List<SongEvent>?[] Events { get; }
	long MaxTicks { get; }
}

public abstract class Player(double ticksPerSecond) : IDisposable
{
	protected abstract string Name { get; }
	protected abstract Mixer Mixer { get; }
	protected abstract Mixer_NAudio Mixer_NAudio { get; }

	public abstract ILoadedSong? LoadedSong { get; }
	public abstract ushort Tempo { get; set; }
	public bool ShouldFadeOut { get; set; }
	public long NumLoops { get; set; }

	public long ElapsedTicks { get; internal set; }
	public PlayerState State { get; protected set; }
	public event Action? SongEnded;

	private readonly BetterTimer _timer = new BetterTimer(ticksPerSecond);
	//private readonly TimeBarrier _time;
	private Thread? _thread;
	private double? _deltaTimeElapsed;
	public bool IsStreamStopped = true;
	public bool IsPauseToggled = false;

	public abstract void LoadSong(int index);
	public abstract void UpdateSongState(SongState info);
	internal abstract void InitEmulation();
	protected abstract void SetCurTick(long ticks);
	protected abstract void OnStopped();

	protected abstract bool Tick(bool playing, bool recording);

	private static int ReadPos = 0;

	private static Span<float> CastBytesToFloats(Span<byte> byteMem)
	{
		return MemoryMarshal.Cast<byte, float>(byteMem);
	}

	internal static StreamCallbackResult PlayCallback(
		nint input, nint output,
		uint frameCount,
		ref StreamCallbackTimeInfo timeInfo,
		StreamCallbackFlags statusFlags,
		nint userData
	)
	{
		// Marshal.AllocHGlobal() or any related functions cannot and must not be used
		// in this callback, otherwise it will cause an OutOfMemoryException.
		//
		// The memory is already allocated by the output and userData params by
		// the native PortAudio library itself.

		var player = Engine.Instance!.Player;

		Wave d = Mixer.Instance!.Stream!.GetUserData<Wave>(userData);
		if (d.Buffer is null)
		{
			ReadPos = 0;
			return StreamCallbackResult.Continue;
		}

		if (!Mixer.Instance.Stream.UDHandle.IsAllocated)
		{
			ReadPos = 0;
			return StreamCallbackResult.Abort;
		}

		RealignBufferPos(d);
		Span<float> buffer;
		Span<float> waveBuffer = CastBytesToFloats(d.Buffer);
		unsafe
		{
			// Apply buffer value
			buffer = new Span<float>((float*)output, (int)(frameCount * 2));
		}

		ReadPos = ReadPos % waveBuffer.Length;

		// If we're reading data, play it back
		if (player.State == PlayerState.Playing)
		{
			for (int i = 0; i < buffer.Length; i++)
			{
				buffer[i] = waveBuffer[ReadPos + i] * Mixer.Instance.Volume;
			}
		}
		else
		{
			buffer.Clear();
		}

		ReadPos += buffer.Length;

		if (ReadPos >= waveBuffer.Length)
		{
			ReadPos = 0;
		}

		if (!Engine.Instance!.Mixer!.IsDisposing)
		{
			// Continue if the mixer isn't being disposed
			return StreamCallbackResult.Continue;
		}
		else
		{
			// Complete the callback if the mixer is being disposed
			buffer.Clear();
			d.ResetBuffer();
			ReadPos = 0;
			Engine.Instance!.Mixer!.IsDisposing = false;
			return StreamCallbackResult.Complete;
		}
	}

	// Experimental realignment func to prevent reading from buffers being written to
	protected static void RealignBufferPos(Wave waveData)
	{
        var count = waveData.Count / 4;
        var writePos = waveData.WritePosition / 4;

        if (writePos - count < 0)
        {
            if (ReadPos.Equals((writePos - count + (waveData.BufferLength / 4))..^(waveData.BufferLength / 4)))
            {
                if (ReadPos < writePos)
                {
                    ReadPos -= count;
                    if (ReadPos <= 0)
                    {
                        ReadPos = ReadPos + (waveData.BufferLength / 4);
                    }
                }
                else
                {
                    ReadPos += count * 2;
                    if (ReadPos + count >= (waveData.BufferLength / 4))
                    {
                        ReadPos = ReadPos - (waveData.BufferLength / 4);
                    }
                }
            }
            else if (ReadPos.Equals(writePos..^(writePos + count)))
            {
                if (ReadPos < writePos)
                {
                    ReadPos -= count;
                    if (ReadPos <= 0)
                    {
                        ReadPos = ReadPos + (waveData.BufferLength / 4);
                    }
                }
                else
                {
                    ReadPos += count * 2;
                    if (ReadPos + count >= (waveData.BufferLength / 4))
                    {
                        ReadPos = ReadPos - (waveData.BufferLength / 4);
                    }
                }
            }
        }
        if (writePos > count && writePos < (waveData.BufferLength / 4))
        {
            if (ReadPos.Equals((writePos - count)..^(writePos + count)))
            {
                if (ReadPos < writePos)
                {
                    ReadPos -= count;
                    if (ReadPos <= 0)
                    {
                        ReadPos = ReadPos + (waveData.BufferLength / 4);
                    }
                }
                else
                {
                    ReadPos += count * 2;
                    if (ReadPos + count >= (waveData.BufferLength / 4))
                    {
                        ReadPos = ReadPos - (waveData.BufferLength / 4);
                    }
                }
            }
        }
    }

	protected void CreateThread()
	{
		_thread = new Thread(TimerTick) { Name = Name + " Tick" };
		_thread.Start();
	}
	protected void WaitThread()
	{
		if (_thread is not null && (_thread.ThreadState is ThreadState.Running or ThreadState.WaitSleepJoin))
		{
			_thread.Join();
		}
	}
	protected void UpdateElapsedTicksAfterLoop(List<SongEvent> evs, long trackEventOffset, long trackRest)
	{
		for (int i = 0; i < evs.Count; i++)
		{
			SongEvent ev = evs[i];
			if (ev.Offset == trackEventOffset)
			{
				ElapsedTicks = ev.Ticks[0] - trackRest;
				return;
			}
		}
		throw new InvalidDataException("No loop point found");
	}

	public void Play()
	{
		if (LoadedSong is null)
		{
			SongEnded?.Invoke();
			return;
		}

		if (State is not PlayerState.ShutDown)
		{
			if (State is not PlayerState.Stopped)
			{
				Stop();
			}
			InitEmulation();
			State = PlayerState.Playing;
			CreateThread();
		}
	}
	public void TogglePlaying()
	{
		switch (State)
		{
			case PlayerState.Playing:
				{
					State = PlayerState.Paused;
					_timer.Stop();
					break;
				}
			case PlayerState.Paused:
			case PlayerState.Stopped:
				{
					State = PlayerState.Playing;
					_timer.Start();
					break;
				}
		}
	}
	public void Stop()
	{
		if (State is PlayerState.Playing or PlayerState.Paused)
		{
			State = PlayerState.Stopped;
			WaitThread();
			OnStopped();
			ElapsedTicks = 0L;
		}
	}
	public void Record(string fileName)
	{
		Mixer.CreateWaveWriter(fileName);

		InitEmulation();
		State = PlayerState.Recording;
		CreateThread();
		WaitThread();

		Mixer.CloseWaveWriter();
	}
	public void SetSongPosition(long ticks)
	{
		if (LoadedSong is null)
		{
			SongEnded?.Invoke();
			return;
		}

		if (State is not PlayerState.Playing and not PlayerState.Paused and not PlayerState.Stopped)
		{
			return;
		}

		if (State is PlayerState.Stopped)
		{
			Play();
		}

		if (State is PlayerState.Playing)
		{
			TogglePlaying();
		}
		InitEmulation();
		SetCurTick(ticks);
		if (State is PlayerState.Paused && !IsPauseToggled || State is PlayerState.Stopped)
		{
			TogglePlaying();
		}
	}

	private void TimerTick()
	{
		_deltaTimeElapsed = 0;
		_timer.Start();
		while (State is not (PlayerState.Stopped or PlayerState.ShutDown))
		{
			var state = State;
			var playing = state == PlayerState.Playing;
			var recording = state == PlayerState.Recording;
			_deltaTimeElapsed += _timer.GetDeltaTime();
			while (_deltaTimeElapsed >= _timer.GetDeltaTick())
			{
				if (!playing && !recording)
				{
					break;
				}
				_deltaTimeElapsed -= _timer.GetDeltaTick();
				bool allDone = Tick(playing, recording);
				if (allDone)
				{
					// TODO: lock state
					_timer.Stop(); // TODO: Don't need timer if recording
					SongEnded?.Invoke();
					return;
				}
			}
		}
		_timer.Stop();
	}

	public void Dispose()
	{
		GC.SuppressFinalize(this);
		if (State != PlayerState.ShutDown)
		{
			State = PlayerState.ShutDown;
			WaitThread();
		}
		SongEnded = null;
	}
}
