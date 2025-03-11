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

	private readonly BetterTimer _timer = new(ticksPerSecond);
	// private readonly TimeBarrier _time = new(ticksPerSecond);
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

	private static Span<sbyte> CastBytesToSBytes(Span<byte> byteMem)
	{
		return MemoryMarshal.Cast<byte, sbyte>(byteMem);
	}

	private static Span<short> CastBytesToShorts(Span<byte> byteMem)
	{
		return MemoryMarshal.Cast<byte, short>(byteMem);
	}

	private static Span<int> CastBytesToInts(Span<byte> byteMem)
	{
		return MemoryMarshal.Cast<byte, int>(byteMem);
	}

	private static Span<Int24> CastBytesToInt24s(Span<byte> byteMem)
	{
		return MemoryMarshal.Cast<byte, Int24>(byteMem);
	}

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

		if (Engine.Instance is null)
		{
			return StreamCallbackResult.Continue;
		}

		var player = Engine.Instance!.Player;

		Wave d = Engine.Instance.Mixer!.Stream!.GetUserData<Wave>(userData);
		if (d.Buffer is null)
		{
			ReadPos = 0;
			return StreamCallbackResult.Continue;
		}

		if (!Engine.Instance.Mixer.Stream.UDHandle.IsAllocated)
		{
			ReadPos = 0;
			return StreamCallbackResult.Abort;
		}

		// RealignBufferPos(d);
		
		Option1(player, d, output, frameCount);

		if (!Engine.Instance!.Mixer!.IsDisposing)
		{
			// Continue if the mixer isn't being disposed
			return StreamCallbackResult.Continue;
		}
		else
		{
			// Complete the callback if the mixer is being disposed
			d.ResetBuffer();
			ReadPos = 0;
			Engine.Instance!.Mixer!.IsDisposing = false;
			return StreamCallbackResult.Complete;
		}
	}

    private static void Option1(Player player, Wave d, nint output, uint frameCount)
    {
        
		switch (Engine.Instance!.Mixer!.OParams.SampleFormat)
		{
			case SampleFormat.UInt8:
				{
					Span<byte> buffer;
					unsafe
					{
						// Apply buffer value
						buffer = new Span<byte>((byte*)output, (int)(frameCount * 2));
					}

					ReadPos %= d.Buffer!.Length;

					// If we're reading data, play it back
					if (player.State == PlayerState.Playing)
					{
						for (int i = 0; i < buffer.Length; i++)
						{
							buffer[i] = (byte)(d.Buffer[ReadPos + i] * Engine.Instance.Mixer.Volume);
						}
					}
					else
					{
						buffer.Clear();
					}

					ReadPos += buffer.Length;

					if (ReadPos >= d.Buffer.Length)
					{
						ReadPos = 0;
					}
					
					if (Engine.Instance!.Mixer!.IsDisposing)
					{
						buffer.Clear();
					}

					break;
				}
			case SampleFormat.Int8:
				{
					Span<sbyte> buffer;
					Span<sbyte> waveBuffer = CastBytesToSBytes(d.Buffer);
					unsafe
					{
						// Apply buffer value
						buffer = new Span<sbyte>((sbyte*)output, (int)(frameCount * 2));
					}

					ReadPos %= waveBuffer.Length;

					// If we're reading data, play it back
					if (player.State == PlayerState.Playing)
					{
						for (int i = 0; i < buffer.Length; i++)
						{
							buffer[i] = (sbyte)(waveBuffer[ReadPos + i] * Engine.Instance.Mixer.Volume);
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
					
					if (Engine.Instance!.Mixer!.IsDisposing)
					{
						buffer.Clear();
					}

					break;
				}
			case SampleFormat.Int16:
				{
					Span<short> buffer;
					Span<short> waveBuffer = CastBytesToShorts(d.Buffer);
					unsafe
					{
						// Apply buffer value
						buffer = new Span<short>((short*)output, (int)(frameCount * 2));
					}

					ReadPos %= waveBuffer.Length;

					// If we're reading data, play it back
					if (player.State == PlayerState.Playing)
					{
						for (int i = 0; i < buffer.Length; i++)
						{
							buffer[i] = (short)(waveBuffer[ReadPos + i] * Engine.Instance.Mixer.Volume);
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
					
					if (Engine.Instance!.Mixer!.IsDisposing)
					{
						buffer.Clear();
					}

					break;
				}
			case SampleFormat.Int24:
				{
					Span<Int24> buffer;
					Span<Int24> waveBuffer = CastBytesToInt24s(d.Buffer);
					unsafe
					{
						// Apply buffer value
						buffer = new Span<Int24>((Int24*)output, (int)(frameCount * 2));
					}

					ReadPos %= waveBuffer.Length;

					// If we're reading data, play it back
					if (player.State == PlayerState.Playing)
					{
						for (int i = 0; i < buffer.Length; i++)
						{
							buffer[i] = (Int24)(waveBuffer[ReadPos + i] * Engine.Instance.Mixer.Volume);
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
					
					if (Engine.Instance!.Mixer!.IsDisposing)
					{
						buffer.Clear();
					}

					break;
				}
			case SampleFormat.Int32:
				{
					Span<int> buffer;
					Span<int> waveBuffer = CastBytesToInts(d.Buffer);
					unsafe
					{
						// Apply buffer value
						buffer = new Span<int>((int*)output, (int)(frameCount * 2));
					}

					ReadPos %= waveBuffer.Length;

					// If we're reading data, play it back
					if (player.State == PlayerState.Playing)
					{
						for (int i = 0; i < buffer.Length; i++)
						{
							buffer[i] = (int)(waveBuffer[ReadPos + i] * Engine.Instance.Mixer.Volume);
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
					
					if (Engine.Instance!.Mixer!.IsDisposing)
					{
						buffer.Clear();
					}

					break;
				}
			case SampleFormat.Float32:
				{
					Span<float> buffer;
					Span<float> waveBuffer = CastBytesToFloats(d.Buffer);
					unsafe
					{
						// Apply buffer value
						buffer = new Span<float>((float*)output, (int)(frameCount * 2));
					}

					ReadPos %= waveBuffer.Length;

					// If we're reading data, play it back
					if (player.State == PlayerState.Playing)
					{
						for (int i = 0; i < buffer.Length; i++)
						{
							buffer[i] = (float)(waveBuffer[ReadPos + i] * Engine.Instance.Mixer.Volume);
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
					
					if (Engine.Instance!.Mixer!.IsDisposing)
					{
						buffer.Clear();
					}

					break;
				}
		}
    }

    // // Experimental realignment func to prevent reading from buffers being written to
    // protected static void RealignBufferPos(Wave waveData)
    // {
    // 	var count = waveData.Count / 4;
    // 	var writePos = waveData.WritePosition / 4;

    // 	if (writePos - count < 0)
    // 	{
    // 		if (ReadPos.Equals((writePos - count + (waveData.BufferLength / 4))..^(waveData.BufferLength / 4)))
    // 		{
    // 			if (ReadPos < writePos)
    // 			{
    // 				ReadPos -= count;
    // 				if (ReadPos <= 0)
    // 				{
    // 					ReadPos += waveData.BufferLength / 4;
    // 				}
    // 			}
    // 			else
    // 			{
    // 				ReadPos += count;
    // 				if (ReadPos + count >= (waveData.BufferLength / 4))
    // 				{
    // 					ReadPos -= waveData.BufferLength / 4;
    // 				}
    // 			}
    // 		}
    // 		else if (ReadPos.Equals(writePos..^(writePos + count)))
    // 		{
    // 			if (ReadPos < writePos)
    // 			{
    // 				ReadPos -= count;
    // 				if (ReadPos <= 0)
    // 				{
    // 					ReadPos += waveData.BufferLength / 4;
    // 				}
    // 			}
    // 			else
    // 			{
    // 				ReadPos += count;
    // 				if (ReadPos + count >= (waveData.BufferLength / 4))
    // 				{
    // 					ReadPos -= waveData.BufferLength / 4;
    // 				}
    // 			}
    // 		}
    // 	}
    // 	if (writePos > count && writePos < (waveData.BufferLength / 4))
    // 	{
    // 		if (ReadPos.Equals((writePos - count)..^(writePos + count)))
    // 		{
    // 			if (ReadPos < writePos)
    // 			{
    // 				ReadPos -= count;
    // 				if (ReadPos <= 0)
    // 				{
    // 					ReadPos += waveData.BufferLength / 4;
    // 				}
    // 			}
    // 			else
    // 			{
    // 				ReadPos += count;
    // 				if (ReadPos + count >= (waveData.BufferLength / 4))
    // 				{
    // 					ReadPos -= waveData.BufferLength / 4;
    // 				}
    // 			}
    // 		}
    // 	}
    // }

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
		while (true)
		{
			var state = State;
			var playing = state == PlayerState.Playing;
			var recording = state == PlayerState.Recording;
			if (!playing && !recording)
			{
				break;
			}
			_deltaTimeElapsed += _timer.GetDeltaTime();
			while (_deltaTimeElapsed >= _timer.GetDeltaTick())
			{
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
