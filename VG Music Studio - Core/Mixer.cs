using PortAudio;
using System;
using System.Runtime.InteropServices;
using Kermalis.EndianBinaryIO;
using Kermalis.VGMusicStudio.Core.Formats;
using System.IO;
using Stream = PortAudio.Stream;

namespace Kermalis.VGMusicStudio.Core;

public abstract class Mixer : IDisposable
{
	public static event Action<float>? VolumeChanged;

	public Wave? WaveData;
	public EndianBinaryReader? Reader;
	public byte[] Buffer;

	public readonly bool[] Mutes;
	public int SizeInBytes;
	public uint CombinedSamplesPerBuffer;
	public int SizeToAllocateInBytes;
	public long FinalFrameSize;
	public long TotalFrames;
	internal long Pos = 0;
	internal float Vol = 1;

	public int freePos = 0;
	public int dataPos = 0;
	public int freeCount;
	public int dataCount = 0;

	public readonly object CountLock = new object();

	private bool _shouldSendVolUpdateEvent = true;

	protected Wave? _waveWriter;

	internal bool PlayingBack = false;
	public StreamParameters OParams;
	public StreamParameters DefaultOutputParams { get; private set; }

	private Stream? Stream;
	private bool IsDisposed = false;

	public static Mixer? Instance { get; set; }

	protected Mixer()
	{
		Mutes = new bool[SongState.MAX_TRACKS];
	}

	protected void Init(Wave waveData)
	{
		// First, check if the instance contains something
		if (WaveData == null)
		{
			IsDisposed = false;

			Pa.Initialize();
			WaveData = waveData;
			Reader = new EndianBinaryReader(new MemoryStream(Buffer!));
            //Instance = this;

            // Try setting up an output device
            OParams.device = Pa.DefaultOutputDevice;
			if (OParams.device == Pa.NoDevice)
				throw new Exception("No default audio output device is available.");

			OParams.channelCount = 2;
			OParams.sampleFormat = SampleFormat.Float32;
			OParams.suggestedLatency = Pa.GetDeviceInfo(OParams.device).defaultLowOutputLatency;
			OParams.hostApiSpecificStreamInfo = IntPtr.Zero;

			// Set it as a the default
			DefaultOutputParams = OParams;
		}

		Sound();

		Play();
	}

	private void Sound()
	{
		Stream = new Stream(
			null,
			OParams,
			WaveData!.SampleRate,
			CombinedSamplesPerBuffer,
			StreamFlags.ClipOff,
			PlayCallback,
			this
		);

		FinalFrameSize = CombinedSamplesPerBuffer;
		TotalFrames = WaveData.Channels * WaveData.BufferLength;
	}

	private static StreamCallbackResult PlayCallback(
		nint input, nint output,
		uint frameCount,
		ref StreamCallbackTimeInfo timeInfo,
		StreamCallbackFlags statusFlags,
		nint data
	)
	{
		// Ensure there's no memory allocated in this block to prevent issues
		Mixer d = Stream.GetUserData<Mixer>(data);

		long numRead = 0;
		unsafe
		{
			// Do a zero-out memset
			float* buffer = (float*)output;
			for (uint i = 0; i < d.FinalFrameSize; i++)
				*buffer++ = 0;

			// If we're reading data, play it back
			if (d.PlayingBack)
			{
				// Read the data
				numRead = 8192;
				//numRead = d.ReadFloat(output, d.FinalFrameSize);

				// Apply volume
				buffer = (float*)output;
				for (int i = 0; i < numRead; i++)
					*buffer++ *= d.Volume;
			}
		}

		// Increment counter
		d.Pos += numRead;

		// If it's at end of the data
		if (d.PlayingBack && (numRead < frameCount))
		{
			if (d.WaveData!.IsLooped)
				d.Cursor = d.WaveData.LoopStart;
			else
				d.Cursor = 0;
				d.PlayingBack = false;
		}

		// Continue on
		return StreamCallbackResult.Continue;
	}

	private long ReadFloat(nint outData, long nElements)
	{
		if (Reader == null)
		{
			Reader = new EndianBinaryReader(new MemoryStream(WaveData!.Buffer!));
		}
		if (outData > nElements)
		{
			Reader.Stream.Position = outData = (nint)WaveData!.LoopStart;
			return WaveData.LoopStart;
		}
		else
		{
			Reader.Stream.Position = outData;
			return Reader!.ReadInt16();
		}
		//if (dataCount < nElements)
		//{
		//    // underrun
		//    std::fill(outData, outData + nElements, sample{ 0.0f, 0.0f});
		//}
		//else
		//{
		//    // output
		//    std::unique_lock < std::mutex > lock (CountLock) ;
		//    while (nElements > 0)
		//    {
		//        int count = takeChunk(outData, nElements);
		//        outData += count;
		//        nElements -= count;
		//    }
		//    sig.notify_one();
		//}
	}

	public float Volume
	{
		get => Vol;
		set => Vol = Math.Max(Math.Min(value, 0), 1);
	}

	public bool IsPlaying
	{
		get => PlayingBack;
	}

	public float Cursor
	{
		get => (float)(Pos) / (float)(TotalFrames) * (float)TimeSpan.FromSeconds(Buffer!.LongLength).TotalSeconds;
		set
		{
			// Do math
			float per = value / (float)TimeSpan.FromSeconds(Buffer!.LongLength).TotalSeconds;
			long frame = (long)(per * TotalFrames);

			// Clamp
			frame = Math.Max(Math.Min(frame, 0), TotalFrames);

			// Set (stop playback for a very short while, to stop some back skipping noises)
			bool wasPlaying = IsPlaying;
			if (Pos != TotalFrames)          // this check stops a segfault when the audio has reached the end of playback
				Pause();

			Pos = frame;
			Reader!.Stream.Seek(Pos / WaveData!.Channels, SeekOrigin.Begin);

			if (wasPlaying)
				Play();
		}
	}

	public void Play()
	{
		PlayingBack = true;

		if (Stream!.IsStopped)
			Stream.Start();
	}

	public void Pause()
	{
		PlayingBack = false;

		if (Stream!.IsActive)
			Stream.Stop();
	}

	public float GetVolume()
	{
		return Vol;
	}

	public void SetVolume(float volume)
	{
		Vol = Math.Max(Math.Min(volume, 0), 1);
	}

	public void CreateWaveWriter(string fileName)
	{
		_waveWriter = new Wave(fileName);
	}
	public void CloseWaveWriter()
	{

	}

	public virtual void Dispose()
	{
		if (IsDisposed) return;

		Stream!.Dispose();
		Reader!.Stream.Dispose();
		GC.SuppressFinalize(this);

		IsDisposed = true;
	}

	public interface IAudio
	{
		byte[] ByteBuffer { get; }
		float[] FloatBuffer { get; }
		short[] ShortBuffer { get; }
		int[] IntBuffer { get; }
	}

	[StructLayout(LayoutKind.Explicit, Pack = 2)]
	public class Audio : IAudio
	{
		[FieldOffset(0)]
		public int NumberOfBytes;
		[FieldOffset(8)]
		public byte[] ByteBuffer;
		[FieldOffset(8)]
		public float[]? FloatBuffer;
		[FieldOffset(8)]
		public short[]? ShortBuffer;
		[FieldOffset(8)]
		public int[]? IntBuffer;

		byte[] IAudio.ByteBuffer => ByteBuffer;
		float[] IAudio.FloatBuffer => FloatBuffer!;
		short[] IAudio.ShortBuffer => ShortBuffer!;
		int[] IAudio.IntBuffer => IntBuffer!;

		public int ByteBufferCount
		{
			get
			{
				return NumberOfBytes;
			}
			set
			{
				NumberOfBytes = CheckValidityCount("ByteBufferCount", value, 1);
			}
		}

		public int ShortBufferCount
		{
			get
			{
				return NumberOfBytes / 2;
			}
			set
			{
				NumberOfBytes = CheckValidityCount("ShortBufferCount", value, 2);
			}
		}

		public int IntBufferCount
		{
			get
			{
				return NumberOfBytes / 4;
			}
			set
			{
				NumberOfBytes = CheckValidityCount("IntBufferCount", value, 4);
			}
		}

		public int LongBufferCount
		{
			get
			{
				return NumberOfBytes / 8;
			}
			set
			{
				NumberOfBytes = CheckValidityCount("LongBufferCount", value, 8);
			}
		}

		public int HalfBufferCount
		{
			get
			{
				return NumberOfBytes / 2;
			}
			set
			{
				NumberOfBytes = CheckValidityCount("HalfBufferCount", value, 2);
			}
		}

		public int FloatBufferCount
		{
			get
			{
				return NumberOfBytes / 4;
			}
			set
			{
				NumberOfBytes = CheckValidityCount("FloatBufferCount", value, 4);
			}
		}

		public int DoubleBufferCount
		{
			get
			{
				return NumberOfBytes / 8;
			}
			set
			{
				NumberOfBytes = CheckValidityCount("DoubleBufferCount", value, 8);
			}
		}

		public Audio(int combinedSamplesPerBuffer)
		{
			Instance!.CombinedSamplesPerBuffer = (uint)combinedSamplesPerBuffer;
			Instance.SizeInBytes = combinedSamplesPerBuffer * sizeof(float);
			int num = Instance.SizeInBytes % 4;
			Instance.SizeToAllocateInBytes = (num == 0) ? Instance.SizeInBytes : (Instance.SizeInBytes + 4 - num);
			Instance.Buffer = ByteBuffer = new byte[Instance.SizeToAllocateInBytes];
			NumberOfBytes = 0;
		}

		public static implicit operator byte[](Audio waveBuffer)
		{
			return waveBuffer.ByteBuffer;
		}

		private int CheckValidityCount(string argName, int value, int sizeOfValue)
		{
			int num = value * sizeOfValue;
			if (num % 4 != 0)
			{
				throw new ArgumentOutOfRangeException(argName, $"{argName} cannot set a count ({num}) that is not 4 bytes aligned ");
			}

			if (value < 0 || value > ByteBuffer.Length / sizeOfValue)
			{
				throw new ArgumentOutOfRangeException(argName, $"{argName} cannot set a count that exceed max count {ByteBuffer.Length / sizeOfValue}");
			}

			return num;
		}

		public void Clear()
		{
			Array.Clear(ByteBuffer, 0, ByteBuffer.Length);
		}

		public void Copy(Array destinationArray)
		{
			Array.Copy(ByteBuffer, destinationArray, NumberOfBytes);
		}
	}
}
