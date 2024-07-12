using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wave;
using PortAudio;
using Kermalis.EndianBinaryIO;

namespace Kermalis.VGMusicStudio.Core;

public class Backend
{
	/// <summary>
	/// The output parameters for an output stream
	/// </summary>
	internal static StreamParameters OParams;

	/// <summary>
	/// The default settings for an output stream
	/// </summary>
	public StreamParameters DefaultOutputParams { get; private set; }

	/// <summary>
	/// How many frames to give each audio buffer
	/// </summary>
	public int FramesPerBuffer { get; private set; } = 4096;

	/// <summary>
	/// The sample rate of the audio data
	/// </summary>
	public int SampleRate { get; private set; }

	/// <summary>
	/// The instance of the backend currently running.
	/// </summary>
	/// <value>`null` if the backend isn't activated. Otherwise, it should contain a value.</value>
	public static Backend Instance { get; private set; } = null;

	public Backend()
	{
	}

	public void Init(BufferedWaveProvider waveProvider)
	{
		// Check if a backend is already initialized
		if (Instance == null)
			return;

		Pa.Initialize();

		// Try setting up an output device
		OParams.device = Pa.DefaultOutputDevice;
		if (OParams.device == Pa.NoDevice)
			throw new Exception("PortAudio Error:\nThere's no default audio device available.");

		OParams.channelCount = 2;
		OParams.sampleFormat = SampleFormat.Float32;
		OParams.suggestedLatency = Pa.GetDeviceInfo(OParams.device).defaultLowOutputLatency;
		OParams.hostApiSpecificStreamInfo = IntPtr.Zero;

		// Set it as the default audio device
		DefaultOutputParams = OParams;


		Instance = this;
	}

	public void OnVolumeChanged(float volume, bool isMuted)
	{

	}
	public void OnDisplayNameChanged(string displayName)
	{
		throw new NotImplementedException();
	}
	public void OnIconPathChanged(string iconPath)
	{
		throw new NotImplementedException();
	}
	public void OnChannelVolumeChanged(uint channelCount, IntPtr newVolumes, uint channelIndex)
	{
		throw new NotImplementedException();
	}
	public void OnGroupingParamChanged(ref Guid groupingId)
	{
		throw new NotImplementedException();
	}
	// Fires on @out.Play() and @out.Stop()
	public void OnStateChanged(uint state)
	{

	}
	public void OnSessionDisconnected(uint disconnectReason)
	{
		throw new NotImplementedException();
	}
	public void SetVolume(float volume)
	{

	}

	public virtual void Dispose()
	{

	}

	public class WaveBuffer
	{
		//
		// Summary:
		//     Number of Bytes
		public int numberOfBytes;

		private byte[] byteBuffer;

		private float[] floatBuffer;

		private short[] shortBuffer;

		private int[] intBuffer;

		//
		// Summary:
		//     Gets the byte buffer.
		//
		// Value:
		//     The byte buffer.
		public byte[] ByteBuffer => byteBuffer;

		//
		// Summary:
		//     Gets the float buffer.
		//
		// Value:
		//     The float buffer.
		public float[] FloatBuffer => floatBuffer;

		//
		// Summary:
		//     Gets the short buffer.
		//
		// Value:
		//     The short buffer.
		public short[] ShortBuffer => shortBuffer;

		//
		// Summary:
		//     Gets the int buffer.
		//
		// Value:
		//     The int buffer.
		public int[] IntBuffer => intBuffer;

		//
		// Summary:
		//     Gets the max size in bytes of the byte buffer..
		//
		// Value:
		//     Maximum number of bytes in the buffer.
		public int MaxSize => byteBuffer.Length;

		//
		// Summary:
		//     Gets or sets the byte buffer count.
		//
		// Value:
		//     The byte buffer count.
		public int ByteBufferCount
		{
			get
			{
				return numberOfBytes;
			}
			set
			{
				numberOfBytes = CheckValidityCount("ByteBufferCount", value, 1);
			}
		}

		//
		// Summary:
		//     Gets or sets the float buffer count.
		//
		// Value:
		//     The float buffer count.
		public int FloatBufferCount
		{
			get
			{
				return numberOfBytes / 4;
			}
			set
			{
				numberOfBytes = CheckValidityCount("FloatBufferCount", value, 4);
			}
		}

		//
		// Summary:
		//     Gets or sets the short buffer count.
		//
		// Value:
		//     The short buffer count.
		public int ShortBufferCount
		{
			get
			{
				return numberOfBytes / 2;
			}
			set
			{
				numberOfBytes = CheckValidityCount("ShortBufferCount", value, 2);
			}
		}

		//
		// Summary:
		//     Gets or sets the int buffer count.
		//
		// Value:
		//     The int buffer count.
		public int IntBufferCount
		{
			get
			{
				return numberOfBytes / 4;
			}
			set
			{
				numberOfBytes = CheckValidityCount("IntBufferCount", value, 4);
			}
		}

		//
		// Summary:
		//     Checks the validity of the count parameters.
		//
		// Parameters:
		//   argName:
		//     Name of the arg.
		//
		//   value:
		//     The value.
		//
		//   sizeOfValue:
		//     The size of value.
		private int CheckValidityCount(string argName, int value, int sizeOfValue)
		{
			int num = value * sizeOfValue;
			if (num % 4 != 0)
			{
				throw new ArgumentOutOfRangeException(argName, $"{argName} cannot set a count ({num}) that is not 4 bytes aligned ");
			}

			if (value < 0 || value > byteBuffer.Length / sizeOfValue)
			{
				throw new ArgumentOutOfRangeException(argName, $"{argName} cannot set a count that exceed max count {byteBuffer.Length / sizeOfValue}");
			}

			return num;
		}

		//
		// Summary:
		//     Initializes a new instance of the NAudio.Wave.WaveBuffer class.
		//
		// Parameters:
		//   sizeToAllocateInBytes:
		//     The number of bytes. The size of the final buffer will be aligned on 4 Bytes
		//     (upper bound)
		public WaveBuffer(int sizeToAllocateInBytes)
		{
			Instance.FramesPerBuffer = sizeToAllocateInBytes / sizeof(float);

			int num = sizeToAllocateInBytes % 4;
			sizeToAllocateInBytes = ((num == 0) ? sizeToAllocateInBytes : (sizeToAllocateInBytes + 4 - num));
			byteBuffer = new byte[sizeToAllocateInBytes];
			numberOfBytes = 0;
		}
	}

	public class BufferedWaveProvider
	{
		private readonly WaveFormat waveFormat;

		//
		// Summary:
		//     If true, always read the amount of data requested, padding with zeroes if necessary
		//     By default is set to true
		public bool ReadFully { get; set; }

		//
		// Summary:
		//     Buffer length in bytes
		public int BufferLength { get; set; }

		//
		// Summary:
		//     If true, when the buffer is full, start throwing away data if false, AddSamples
		//     will throw an exception when buffer is full
		public bool DiscardOnBufferOverflow { get; set; }

		public BufferedWaveProvider(WaveFormat waveFormat)
		{
			//this.waveFormat = waveFormat;
			//Instance.FramesPerBuffer = BufferLength = waveFormat.averageBytesPerSecond * 5;
			//ReadFully = true;
		}

		public void AddSamples(byte[] buffer, int offset, int count)
		{
			
		}
	}

	public class WaveFormat
	{
		//
		// Summary:
		//     number of channels
		protected short Channels;

		//
		// Summary:
		//     sample rate
		protected int SampleRate;

		//
		// Summary:
		//     for buffer estimation
		public int AverageBytesPerSecond;

		//
		// Summary:
		//     block size of data
		protected short BlockAlign;

		//
		// Summary:
		//     number of bits per sample of mono data
		protected short BitsPerSample;

		//
		// Summary:
		//     number of following bytes
		protected short ExtraSize;

		public WaveFormat CreateIeeeFloatWaveFormat(int sampleRate, int channels)
		{
			WaveFormat waveFormat = new WaveFormat();
			waveFormat.Channels = (short)channels;
			waveFormat.BitsPerSample = 32;
			Instance.SampleRate = waveFormat.SampleRate = sampleRate;
			waveFormat.BlockAlign = (short)(4 * channels);
			waveFormat.AverageBytesPerSecond = sampleRate * waveFormat.BlockAlign;
			waveFormat.ExtraSize = 0;
			return waveFormat;
		}
	}

	public class WaveFileWriter
	{
		private Stream outStream;

		private readonly EndianBinaryWriter writer;

		private long dataSizePos;

		private long factSampleCountPos;

		private long dataChunkSize;

		private readonly WaveFormat format;

		private readonly string filename;

		private readonly byte[] value24 = new byte[3];

		//
		// Summary:
		//     The wave file name or null if not applicable
		public string Filename => filename;

		//
		// Summary:
		//     Number of bytes of audio in the data chunk
		public long Length => dataChunkSize;

		//
		// Summary:
		//     Total time (calculated from Length and average bytes per second)
		public TimeSpan TotalTime => TimeSpan.FromSeconds((double)Length / (double)WaveFormat.AverageBytesPerSecond);

		//
		// Summary:
		//     WaveFormat of this wave file
		public WaveFormat WaveFormat => format;

		//
		// Summary:
		//     Returns false: Cannot read from a WaveFileWriter
		public bool CanRead => false;

		//
		// Summary:
		//     Returns true: Can write to a WaveFileWriter
		public bool CanWrite => true;

		//
		// Summary:
		//     Returns false: Cannot seek within a WaveFileWriter
		public bool CanSeek => false;

		//
		// Summary:
		//     Gets the Position in the WaveFile (i.e. number of bytes written so far)
		public long Position
		{
			get
			{
				return dataChunkSize;
			}
			set
			{
				throw new InvalidOperationException("Repositioning a WaveFileWriter is not supported");
			}
		}

		public void Write(byte[] data, int offset, int count)
		{

		}
	}

	public class PortAudio : IDisposable
	{
		// License:     APL 2.0
		// Author:      Benjamin N. Summerton <https://16bpp.net>
		// Based on the code from Bassoon, modified for use in VGMS

		// Flag used for the IDispoable interface
		private bool disposed = false;

		/// <summary>
		/// Audio level, should be between [0.0, 1.0].
		/// 0.0 = silent, 1.0 = full volume
		/// </summary>
		internal float volume = 1;

		/// <summary>
		/// Where in the audio (in bytes) we are.
		/// </summary>
		internal long Cursor = 0;

		/// <summary>
		/// If we should be currently playing audio
		/// </summary>
		internal bool playingBack = false;

		private Stream stream;

		/// <summary>
		/// How much data needs to be read when doing a playback
		/// </summary>
		internal int finalFrameSize;

		/// <summary>
		/// How many frames of audio are in the loaded file.
		/// </summary>
		private readonly long totalFrames;

		public PortAudio()
		{
			// Setup the playback stream
			// Get the channel count
			StreamParameters oParams = Instance.DefaultOutputParams;
			oParams.channelCount = OParams.channelCount;

			// Create the stream
			stream = new Stream(
				null,
				oParams,
				Instance.SampleRate,
				(uint)Instance.FramesPerBuffer,
				StreamFlags.ClipOff,
				PlayCallback,
				this
			);
		}

		~PortAudio()
		{
			Dispose(false);
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}
		protected virtual void Dispose(bool disposing)
		{

		}

		#region Math
		/// <summary>
		/// Clamp a value between a range (inclusive).
		///
		/// This exists in .NET Core, but not in .NET standard
		/// </summary>
		/// <param name="value">Valume to clamp</param>
		/// <param name="min">minimum possible value</param>
		/// <param name="max">maximum possible value</param>
		/// <returns>The orignal value (clamped between the range)</returns>
		public static float Clamp(float value, float min, float max) =>
			Math.Max(Math.Min(value, max), min);

		/// <summary>
		/// Clamp a value between a range (inclusive).
		///
		/// This exists in .NET Core, but not in .NET standard
		/// </summary>
		/// <param name="value">Valume to clamp</param>
		/// <param name="min">minimum possible value</param>
		/// <param name="max">maximum possible value</param>
		/// <returns>The orignal value (clamped between the range)</returns>
		public static long Clamp(long value, long min, long max) =>
			Math.Max(Math.Min(value, max), min);

		#endregion

		#region Properties
		/// <summary>
		/// Level to play back the audio at. default is 100%.
		///
		/// When setting, this will be clamped within range in the `value`.
		/// </summary>
		/// <value>[0.0, 1.0]</value>
		public float Volume
		{
			get => volume;
			set => volume = Clamp(value, 0, 1);
		}

		/// <summary>
		/// See if the sound is being played back rightnow
		/// </summary>
		/// <value>true if so, false otherwise</value>
		public bool IsPlaying
		{
			get => playingBack;
		}


		#endregion // Properties

		#region Methods
		/// <summary>
		/// Start playing the sound
		/// </summary>
		public void Play()
		{
			playingBack = true;

			if (stream.IsStopped)
				stream.Start();
		}

		/// <summary>
		/// Stop audio playback
		/// </summary>
		public void Pause()
		{
			playingBack = false;

			if (stream.IsActive)
				stream.Stop();
		}

		/// <summary>
		/// Pause audio playback
		/// </summary>
		public void Stop()
		{
			playingBack = false;

			if (stream.IsActive)
				Cursor = 0;
				stream.Stop();
		}
		#endregion // Methods

		#region PortAudio Callbacks
		/// <summary>
		/// Performs the actual audio playback
		/// </summary>
		private static StreamCallbackResult PlayCallback(
			IntPtr input, IntPtr output,
			uint frameCount,
			ref StreamCallbackTimeInfo timeInfo,
			StreamCallbackFlags statusFlags,
			IntPtr dataPtr
		)
		{
			// NOTE: make sure there are no malloc in this block, as it can cause issues.
			PortAudio data = Stream.GetUserData<PortAudio>(dataPtr);

			long numRead = 0;
			unsafe
			{
				// Do a zero-out memset
				float* buffer = (float*)output;
				for (uint i = 0; i < data.finalFrameSize; i++)
					*buffer++ = 0;

				// If we are reading data, then play it back
				if (data.playingBack)
				{
					// Read data
					//numRead = data.audioFile.readFloat(output, data.finalFrameSize);


					// Apply volume
					buffer = (float*)output;
					for (int i = 0; i < numRead; i++)
						*buffer++ *= data.volume;
				}
			}

			// Increment the counter
			data.Cursor += numRead;

			// Did we hit the end?
			if (data.playingBack && (numRead < frameCount))
			{
				// Stop playback, and reset to the beginning
				data.Cursor = 0;
				data.playingBack = false;
			}

			// Continue on
			return StreamCallbackResult.Continue;
		}
		#endregion // PortAudio Callbacks
	}

	public abstract class NAudio : IAudioSessionEventsHandler, IDisposable
	{
		public static event Action<float>? VolumeChanged;

		public readonly bool[] Mutes;
		private IWavePlayer _out;
		private AudioSessionControl _appVolume;

		private bool _shouldSendVolUpdateEvent = true;

		protected WaveFileWriter? _waveWriter;
		protected abstract WaveFormat WaveFormat { get; }


		protected NAudio()
		{
			Mutes = new bool[SongState.MAX_TRACKS];
			_out = null!;
			_appVolume = null!;
		}

		protected void Init(IWaveProvider waveProvider)
		{
			_out = new WasapiOut();
			_out.Init(waveProvider);
			using (var en = new MMDeviceEnumerator())
			{
				SessionCollection sessions = en.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia).AudioSessionManager.Sessions;
				int id = Environment.ProcessId;
				for (int i = 0; i < sessions.Count; i++)
				{
					AudioSessionControl session = sessions[i];
					if (session.GetProcessID == id)
					{
						_appVolume = session;
						_appVolume.RegisterEventClient(this);
						break;
					}
				}
			}
			_out.Play();
		}

		public void OnVolumeChanged(float volume, bool isMuted)
		{
			if (_shouldSendVolUpdateEvent)
			{
				VolumeChanged?.Invoke(volume);
			}
			_shouldSendVolUpdateEvent = true;
		}
		public void OnDisplayNameChanged(string displayName)
		{
			throw new NotImplementedException();
		}
		public void OnIconPathChanged(string iconPath)
		{
			throw new NotImplementedException();
		}
		public void OnChannelVolumeChanged(uint channelCount, IntPtr newVolumes, uint channelIndex)
		{
			throw new NotImplementedException();
		}
		public void OnGroupingParamChanged(ref Guid groupingId)
		{
			throw new NotImplementedException();
		}
		// Fires on @out.Play() and @out.Stop()
		public void OnStateChanged(AudioSessionState state)
		{
			if (state == AudioSessionState.AudioSessionStateActive)
			{
				OnVolumeChanged(_appVolume.SimpleAudioVolume.Volume, _appVolume.SimpleAudioVolume.Mute);
			}
		}
		public void OnSessionDisconnected(AudioSessionDisconnectReason disconnectReason)
		{
			throw new NotImplementedException();
		}
		public void SetVolume(float volume)
		{
			_shouldSendVolUpdateEvent = false;
			_appVolume.SimpleAudioVolume.Volume = volume;
		}

		public virtual void Dispose()
		{
			GC.SuppressFinalize(this);
			_out.Stop();
			_out.Dispose();
			_appVolume.Dispose();
		}
	}
}
