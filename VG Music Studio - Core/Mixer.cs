using PortAudio;
using System;
using System.Runtime.InteropServices;
using Kermalis.EndianBinaryIO;
using Kermalis.VGMusicStudio.Core.Formats;
using Stream = PortAudio.Stream;

namespace Kermalis.VGMusicStudio.Core;

public abstract class Mixer : IDisposable
{
    public static event Action<float>? VolumeChanged;

    public Wave? WaveData;
    public EndianBinaryReader? Reader;
    //public float[] Buffer;

    public readonly bool[] Mutes;
    public int SizeInBytes;
    public uint FramesPerBuffer;
    public int SizeToAllocateInBytes;
    public long FinalFrameSize;
    private float Vol = 1;

    public readonly object CountLock = new object();

    protected Wave? _waveWriter;

    public StreamParameters OParams;
    public StreamParameters DefaultOutputParams { get; private set; }

    public Stream? Stream;
    public bool IsDisposing = false;
    private bool IsDisposed = false;

    public static Mixer? Instance { get; set; }

    protected Mixer()
    {
        Mutes = new bool[SongState.MAX_TRACKS];
        //Buffer = null!;
    }

    protected void Init(Wave waveData, SampleFormat sampleFormat = SampleFormat.Float32)
    {
        // First, check if the instance contains something
        if (WaveData == null)
        {
            IsDisposed = false;

            Pa.Initialize();
            WaveData = waveData;

            // Try setting up an output device
            OParams.device = Pa.DefaultOutputDevice;
            if (OParams.device == Pa.NoDevice)
                throw new Exception("No default audio output device is available.");

            OParams.channelCount = 2;
            OParams.sampleFormat = sampleFormat;
            OParams.suggestedLatency = Pa.GetDeviceInfo(OParams.device).defaultLowOutputLatency;
            OParams.hostApiSpecificStreamInfo = IntPtr.Zero;

            // Set it as a the default
            DefaultOutputParams = OParams;
        }

        Instance!.Stream = new Stream(
            null,
            OParams,
            WaveData!.SampleRate,
            FramesPerBuffer,
            StreamFlags.NoFlag,
            Player.PlayCallback,
            waveData
        );

        FinalFrameSize = FramesPerBuffer * 2;

        Stream!.Start();
    }

    private int ProcessFrame(Span<float> output, Span<float> buffer, int framesPerBuffer)
    {
        float counter = 0;

        counter += framesPerBuffer;
        while (counter >= Instance!.FramesPerBuffer)
        {
            counter -= Instance.FramesPerBuffer;
        }

        framesPerBuffer = (int)(Instance.FramesPerBuffer * 2);
        float[] outBuffer = buffer.ToArray();
        
        float[] outBuf = output.ToArray();
        for (int i = 0; i < framesPerBuffer; i++)
        {
            outBuf[i] = outBuffer[i];
        }

        return 1;
    }

    public float Volume
    {
        get => Vol;
        set => Vol = Math.Clamp(value, 0, 1);
    }

    public float GetVolume()
    {
        return Vol;
    }

    public void SetVolume(float volume)
    {
        if (!Engine.Instance!.UseNewMixer)
            Engine.Instance.Mixer_NAudio!.SetVolume(volume);
        else
            Vol = Math.Clamp(volume, 0, 1);
    }

    public void CreateWaveWriter(string fileName)
    {
        //_waveWriter = new Wave(fileName);
    }
    public void CloseWaveWriter()
    {

    }

    public virtual void Dispose()
    {
        if (IsDisposed || Stream is null) return;

        IsDisposing = true;
        Stream!.Stop();

        Stream!.Dispose();
        GC.SuppressFinalize(this);

        IsDisposed = true;
    }

    public interface IAudio
    {
        Span<byte> ByteBuffer { get; }
        Span<short> Int16Buffer { get; }
        Span<int> Int32Buffer { get; }
        Span<long> Int64Buffer { get; }
        Span<Int128> Int128Buffer { get; }
        Span<Half> Float16Buffer { get; }
        Span<float> Float32Buffer { get; }
        Span<double> Float64Buffer { get; }
        Span<decimal> Float128Buffer { get; }
    }

    [StructLayout(LayoutKind.Explicit, Pack = 2)]
    public class Audio : IAudio
    {
        [FieldOffset(0)]
        public int NumberOfBytes;
        [FieldOffset(8)]
        public byte[]? ByteBuffer;
        [FieldOffset(8)]
        public short[]? Int16Buffer;
        [FieldOffset(8)]
        public int[]? Int32Buffer;
        [FieldOffset(8)]
        public long[]? Int64Buffer;
        [FieldOffset(8)]
        public Int128[]? Int128Buffer;
        [FieldOffset(8)]
        public Half[]? Float16Buffer;
        [FieldOffset(8)]
        public float[]? Float32Buffer;
        [FieldOffset(8)]
        public double[]? Float64Buffer;
        [FieldOffset(8)]
        public decimal[]? Float128Buffer;

        Span<byte> IAudio.ByteBuffer => ByteBuffer!;
        Span<short> IAudio.Int16Buffer => Int16Buffer!;
        Span<int> IAudio.Int32Buffer => Int32Buffer!;
        Span<long> IAudio.Int64Buffer => Int64Buffer!;
        Span<Int128> IAudio.Int128Buffer => Int128Buffer!;
        Span<Half> IAudio.Float16Buffer => Float16Buffer!;
        Span<float> IAudio.Float32Buffer => Float32Buffer!;
        Span<double> IAudio.Float64Buffer => Float64Buffer!;
        Span<decimal> IAudio.Float128Buffer => Float128Buffer!;

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

        public int Int16BufferCount
        {
            get
            {
                return NumberOfBytes / 2;
            }
            set
            {
                NumberOfBytes = CheckValidityCount("Int16BufferCount", value, 2);
            }
        }

        public int Int32BufferCount
        {
            get
            {
                return NumberOfBytes / 4;
            }
            set
            {
                NumberOfBytes = CheckValidityCount("Int32BufferCount", value, 4);
            }
        }

        public int Int64BufferCount
        {
            get
            {
                return NumberOfBytes / 8;
            }
            set
            {
                NumberOfBytes = CheckValidityCount("Int64BufferCount", value, 8);
            }
        }

        public int Int128BufferCount
        {
            get
            {
                return NumberOfBytes / 16;
            }
            set
            {
                NumberOfBytes = CheckValidityCount("Int128BufferCount", value, 16);
            }
        }

        public int Float16BufferCount
        {
            get
            {
                return NumberOfBytes / 2;
            }
            set
            {
                NumberOfBytes = CheckValidityCount("Float16BufferCount", value, 2);
            }
        }

        public int Float32BufferCount
        {
            get
            {
                return NumberOfBytes / 4;
            }
            set
            {
                NumberOfBytes = CheckValidityCount("Float32BufferCount", value, 4);
            }
        }

        public int Float64BufferCount
        {
            get
            {
                return NumberOfBytes / 8;
            }
            set
            {
                NumberOfBytes = CheckValidityCount("Float64BufferCount", value, 8);
            }
        }

        public int Float128BufferCount
        {
            get
            {
                return NumberOfBytes / 16;
            }
            set
            {
                NumberOfBytes = CheckValidityCount("Float128BufferCount", value, 16);
            }
        }

        public Audio(int sizeToAllocateInBytes)
        {
            Instance!.FramesPerBuffer = (uint)(sizeToAllocateInBytes / sizeof(float)) / 2;
            Instance.SizeInBytes = sizeToAllocateInBytes;
            int num = Instance.SizeInBytes % 4;
            Instance.SizeToAllocateInBytes = (num == 0) ? Instance.SizeInBytes : (Instance.SizeInBytes + 4 - num);
            ByteBuffer = new Span<byte>(new byte[Instance.SizeToAllocateInBytes]).ToArray();
            NumberOfBytes = 0;
        }

        public static implicit operator byte[](Audio waveBuffer)
        {
            return waveBuffer.ByteBuffer!;
        }

        private int CheckValidityCount(string argName, int value, int sizeOfValue)
        {
            int num = value * sizeOfValue;
            if (num % 4 != 0)
            {
                throw new ArgumentOutOfRangeException(argName, $"{argName} cannot set a count ({num}) that is not 4 bytes aligned ");
            }

            if (value < 0 || value > ByteBuffer!.Length / sizeOfValue)
            {
                throw new ArgumentOutOfRangeException(argName, $"{argName} cannot set a count that exceeds max count of {ByteBuffer!.Length / sizeOfValue}.");
            }

            return num;
        }

        public void Clear()
        {
            Array.Clear(ByteBuffer!, 0, ByteBuffer!.Length);
        }

        public void Copy(Array destinationArray)
        {
            Array.Copy(ByteBuffer!, destinationArray, NumberOfBytes);
        }
    }
}
