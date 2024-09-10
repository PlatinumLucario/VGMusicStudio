using PortAudio;
using Kermalis.VGMusicStudio.Core.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

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

public abstract class Player : IDisposable
{
	protected abstract string Name { get; }
	protected abstract Mixer Mixer { get; }
	protected abstract Mixer_NAudio Mixer_NAudio { get; }

	public abstract ILoadedSong? LoadedSong { get; }
	public bool ShouldFadeOut { get; set; }
	public long NumLoops { get; set; }

	public long ElapsedTicks { get; internal set; }
	public PlayerState State { get; protected set; }
	public event Action? SongEnded;

	private readonly TimeBarrier _time;
	private Thread? _thread;
	public bool IsStopped = true;

	protected Player(double ticksPerSecond)
	{
		_time = new TimeBarrier(ticksPerSecond);
	}

	public abstract void LoadSong(int index);
	public abstract void UpdateSongState(SongState info);
	internal abstract void InitEmulation();
	protected abstract void SetCurTick(long ticks);
	protected abstract void OnStopped();

	protected abstract bool Tick(bool playing, bool recording);
	
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
		
		Mixer.Audio d = Mixer.Instance!.Stream!.GetUserData<Mixer.Audio>(userData);

		if (!Mixer.Instance.Stream.UDHandle.IsAllocated)
		{
			return StreamCallbackResult.Abort;
		}
		
		var frameSize = (int)Mixer.Instance.FinalFrameSize;
		float[] frameBuffer = new float[frameSize];
		for (int i = 0; i < frameSize; i++)
			frameBuffer[i] = d.Float32Buffer![i];
		
		float[] newBuffer = new float[frameSize];
		for (int i = 0; i < frameSize; i++)
		{
			newBuffer[i] = Math.Clamp(frameBuffer[i], -1f, 1f);
		}
		// for (int i = 0; i < newBuffer.Length; i++)
		// {
		// 	System.Diagnostics.Debug.WriteLine("Buffer value: " + newBuffer[i].ToString());
		// }

		Span<float> buffer;
		unsafe
		{
			buffer = new((float*)output, frameSize);
		}

		// Zero out the memory buffer output before applying buffer values
		for (int i = 0; i < frameSize; i++)
			buffer[i] = 0;

		// If we're reading data, play it back
		if (player.State == PlayerState.Playing)
		{
			// Apply buffer value
			for (int i = 0; i < frameSize; i++)
				buffer[i] = newBuffer[i];

			for (int i = 0; i < frameSize; i++)
				buffer[i] *= Mixer.Instance.Volume;
		}

		if (player.State is PlayerState.Playing or PlayerState.Paused)
		{
			// Continue if the song isn't finished
			return StreamCallbackResult.Continue;
		}
		else
		{
			// Complete the callback when song is finished
			return StreamCallbackResult.Complete;
		}
	}

	protected void CreateThread()
	{
		_thread = new Thread(TimerLoop) { Name = Name + " Tick" };
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
			Stop();
			InitEmulation();
			State = PlayerState.Playing;
			if (Engine.Instance!.UseNewMixer)
			{
				if (IsStopped)
				{
					IsStopped = false;
					CreateThread();
					Mixer.Instance!.Stream!.Start();
				}
			}
			else
			{
				CreateThread();
			}
		}
	}
	public void TogglePlaying()
	{
		switch (State)
		{
			case PlayerState.Playing:
				{
					State = PlayerState.Paused;
					WaitThread();
					break;
				}
			case PlayerState.Paused:
			case PlayerState.Stopped:
				{
					State = PlayerState.Playing;
					CreateThread();
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
			if (Engine.Instance!.UseNewMixer)
			{
				if (!IsStopped)
				{
					IsStopped = true;
					//_time.Stop();
					Mixer.Instance!.Stream!.Stop();
				}
			}
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

		if (State is PlayerState.Playing)
		{
			TogglePlaying();
		}
		InitEmulation();
		SetCurTick(ticks);
		TogglePlaying();
	}

	private void TimerLoop()
	{
		_time.Start();
		while (true)
		{
			var state = State;
			var playing = state == PlayerState.Playing;
			var recording = state == PlayerState.Recording;
			if (!playing && !recording)
			{
				break;
			}
			if (TimerTick(_time, playing, recording) is true)
			{
				return;
			}
		}
		_time.Stop();
	}
	private bool TimerTick(TimeBarrier time, bool playing, bool recording)
	{
		bool allDone = Tick(playing, recording);
		if (allDone)
		{
			// TODO: lock state
			time.Stop(); // TODO: Don't need timer if recording
			State = PlayerState.Stopped;
			SongEnded?.Invoke();
			return allDone;
		}
		if (playing)
		{
			time.Wait();
		}
		return false;
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
