using PortAudio;
using System;
using System.Runtime.InteropServices;
using System.Linq;
using System.IO;
using Kermalis.EndianBinaryIO;
using Kermalis.VGMusicStudio.Core.Formats;
using Stream = PortAudio.Stream;
using NAudio.Wave;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using SoundFlow.Abstracts;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Components;
using SoundFlow.Enums;
using SoundFlow.Providers;
using SoundFlow.Structs;
using SoundFlow.Abstracts.Devices;
using SoundFlow.Interfaces;

namespace Kermalis.VGMusicStudio.Core;

public abstract class Mixer : IAudioSessionEventsHandler, IDisposable
{
    public readonly bool[] Mutes;

    #region MiniAudio Fields
    // MiniAudio Fields
    private AudioPlaybackDevice? _playbackDevice;
    internal SoundPlayer? MiniAudioPlayer;
    internal QueueDataProvider? DataProvider;
    #endregion

    #region PortAudio Fields
    // PortAudio Fields
    public Wave? WaveData;
    internal abstract int SamplesPerBuffer { get; }
    private float Vol = 1;

    public readonly object CountLock = new();

    protected Wave? _waveWriterPortAudio;

    public StreamParameters OParams;
    public StreamParameters DefaultOutputParams { get; private set; }

    public Stream? Stream;
    public bool IsDisposing = false;
    private bool _isDisposed = false;
    #endregion

    #region NAudio Fields
    // NAudio Fields
    public static event Action<float>? VolumeChanged;
    private WasapiOut? _out;
    private AudioSessionControl? _appVolume;

    private bool _shouldSendVolUpdateEvent = true;

    protected WaveFileWriter? _waveWriterNAudio;
    protected abstract WaveFormat? WaveFormat { get; }
    #endregion

    // Audio Backend
    public static AudioBackend PlaybackBackend { get; set; }

    public enum AudioBackend
    {
        PortAudio,
        MiniAudio,
        NAudio
    }

    protected Mixer()
    {
        Mutes = new bool[SongState.MAX_TRACKS];
        if (PlaybackBackend is AudioBackend.NAudio)
        {
            _out = null!;
            _appVolume = null!;
        }
    }

    public class MiniAudioBuffer : ISoundDataProvider, IDisposable
    {
        public int Position { get; set; }

        public int Length { get; }

        public bool CanSeek { get; }

        public SoundFlow.Enums.SampleFormat SampleFormat { get; }

        public int SampleRate { get; set; }

        public bool IsDisposed { get; private set; }

        public event EventHandler<EventArgs> EndOfStreamReached;
        public event EventHandler<PositionChangedEventArgs> PositionChanged;

        public void Dispose()
        {
            if (!IsDisposed)
            {
                GC.SuppressFinalize(this);
            }
        }

        public int ReadBytes(Span<float> buffer)
        {
            throw new NotImplementedException();
        }

        public void Seek(int offset)
        {
            throw new NotImplementedException();
        }
    }

    protected void Init(Wave waveData = null!, PortAudio.SampleFormat sampleFormat = PortAudio.SampleFormat.Float32,
    int sampleRate = 48000, byte[] stream = null!,
    IWaveProvider waveProvider = null!)
    {
        switch (PlaybackBackend)
        {
            case AudioBackend.PortAudio:
                {
                    // First, check if the instance contains something
                    if (WaveData == null)
                    {
                        _isDisposed = false;

                        Pa.Initialize();
                        WaveData = waveData;

                        // Try setting up an output device
                        OParams.Device = Pa.DefaultOutputDevice;
                        if (OParams.Device == Pa.NoDevice)
                        {
                            throw new Exception("No default audio output device is available.");
                        }

                        OParams.Channels = 2;
                        OParams.SampleFormat = sampleFormat;
                        OParams.SuggestedLatency = Pa.GetDeviceInfo(OParams.Device).defaultLowOutputLatency;
                        OParams.HostApiSpecificStreamInfo = IntPtr.Zero;

                        // Set it as the default
                        DefaultOutputParams = OParams;
                    }

                    Stream = new Stream(
                        null,
                        OParams,
                        WaveData!.SampleRate,
                        (uint)SamplesPerBuffer,
                        StreamFlags.NoFlag,
                        PortAudioPlayer.Play,
                        waveData
                    );

                    var hostApiInfo = Pa.GetHostApiInfo(Pa.DefaultHostApi);

                    Stream!.Start();
                    break;
                }
            case AudioBackend.MiniAudio:
                {
                    var engine = new MiniAudioEngine();
                    var format = new AudioFormat
                    {
                        SampleRate = sampleRate,
                        Channels = 2,
                        Format = SoundFlow.Enums.SampleFormat.F32
                    };
                    var defaultDevice = engine.PlaybackDevices.FirstOrDefault(x => x.IsDefault);
                    _playbackDevice = engine.InitializePlaybackDevice(defaultDevice, format);
                    DataProvider = new QueueDataProvider(format);
                    MiniAudioPlayer = new SoundPlayer(engine, format, DataProvider);
                    _playbackDevice.MasterMixer.AddComponent(MiniAudioPlayer);
                    _playbackDevice.Start();
                    MiniAudioPlayer.IsLooping = true;
                    break;
                }
            case AudioBackend.NAudio:
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
                    break;
                }
        }
    }

    internal void UpdateStream(Span<byte> stream, int sampleRate)
    {
        if (MiniAudioPlayer.State != SoundFlow.Enums.PlaybackState.Playing)
        {
            MiniAudioPlayer.Play();
        }
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
        switch (PlaybackBackend)
        {
            case AudioBackend.PortAudio:
                {
                    Vol = Math.Clamp(volume, 0, 1);
                    break;
                }
            case AudioBackend.MiniAudio:
                {
                    _playbackDevice!.MasterMixer.Volume = volume;
                    break;
                }
            case AudioBackend.NAudio:
                {
                    _shouldSendVolUpdateEvent = false;
                    _appVolume!.SimpleAudioVolume.Volume = volume;
                    break;
                }
        }
    }

    #region NAudio Functions
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
            OnVolumeChanged(_appVolume!.SimpleAudioVolume.Volume, _appVolume.SimpleAudioVolume.Mute);
        }
    }
    public void OnSessionDisconnected(AudioSessionDisconnectReason disconnectReason)
    {
        throw new NotImplementedException();
    }
    #endregion

    public void CreateWaveWriter(string fileName)
    {
        switch (PlaybackBackend)
        {
            case AudioBackend.PortAudio:
                {
                    _waveWriterPortAudio = WaveData;
                    _waveWriterPortAudio!.CreateFileStream(fileName);
                    break;
                }
            case AudioBackend.NAudio:
                {
                    _waveWriterNAudio = new WaveFileWriter(fileName, WaveFormat);
                    break;
                }
        }
    }
    public void CloseWaveWriter()
    {
        switch (PlaybackBackend)
        {
            case AudioBackend.PortAudio:
                {
                    _waveWriterPortAudio!.Dispose(true);
                    _waveWriterPortAudio = null;
                    break;
                }
            case AudioBackend.NAudio:
                {
                    _waveWriterNAudio!.Dispose();
                    _waveWriterNAudio = null;
                    break;
                }
        }
    }

    public virtual void Dispose()
    {
        switch (PlaybackBackend)
        {
            case AudioBackend.PortAudio:
                {
                    if (_isDisposed || Stream is null)
                    {
                        return;
                    }

                    IsDisposing = true;
                    Stream!.Stop();

                    Stream!.Dispose();
                    break;
                }
            case AudioBackend.MiniAudio:
                {
                    if (MiniAudioPlayer is not null && _playbackDevice is not null)
                    {
                        MiniAudioPlayer.Stop();
                        _playbackDevice.Stop();
                        _playbackDevice.MasterMixer.RemoveComponent(MiniAudioPlayer);
                    }
                    break;
                }
            case AudioBackend.NAudio:
                {
                    if (_out is not null)
                    {
                        _out!.Stop();
                        _out.Dispose();
                        _appVolume!.Dispose();
                    }
                    break;
                }
        }
        GC.SuppressFinalize(this);

        _isDisposed = true;
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
            var sizeInBytes = sizeToAllocateInBytes;
            int aligned32Bits = sizeInBytes % 4;
            sizeToAllocateInBytes = (aligned32Bits == 0) ? sizeInBytes : (sizeInBytes + 4 - aligned32Bits);
            ByteBuffer = new byte[sizeToAllocateInBytes];
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
