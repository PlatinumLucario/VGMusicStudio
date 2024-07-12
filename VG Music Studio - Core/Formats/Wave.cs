using Kermalis.EndianBinaryIO;
using NAudio.Utils;
using System;
using System.IO;

namespace Kermalis.VGMusicStudio.Core.Formats;

public class Wave
{
    public string? FileName;
    public ushort Channels;
    public uint SampleRate;
    public ushort BitsPerSample;
    public ushort ExtraSize;
    public ushort BlockAlign;
    public uint AverageBytesPerSecond;
    public bool IsLooped = false;
    public uint LoopStart;
    public uint LoopEnd;

    public bool DiscardOnBufferOverflow;
    public int BufferLength;

    public byte[]? Buffer;
    private int ReadPosition;
    private int WritePosition;
    private int ByteCount;
    private object? LockObject;

    private long DataChunkSize;
    private long DataChunkLength;
    private long DataChunkPosition;
    private readonly Stream? InStream;
    private readonly Stream? OutStream;

    public long Position
    {
        get
        {
            return InStream!.Position - DataChunkPosition;
        }
        set
        {
            lock (LockObject!)
            {
                value = Math.Min(value, DataChunkLength);
                // To keep it in sync
                value -= (value % BlockAlign);
                InStream!.Position = value + DataChunkPosition;
            }
        }
    }

    public int BufferedBytes
    {
        get
        {
            if (this != null)
            {
                return Count;
            }

            return 0;
        }
    }

    public int Count
    {
        get
        {
            lock (LockObject!)
            {
                return ByteCount;
            }
        }
    }

    public Wave()
    {
        InStream = new MemoryStream();
        OutStream = new MemoryStream();
    }
    public Wave(string fileName)
    {
        InStream = new MemoryStream();
        OutStream = new MemoryStream();
        FileName = fileName;
    }

    public Wave CreateFormat(uint sampleRate, ushort channels, ushort blockAlign, uint averageBytesPerSecond, ushort bitsPerSample)
    {
        Channels = channels;
        SampleRate = sampleRate;
        AverageBytesPerSecond = averageBytesPerSecond;
        BlockAlign = blockAlign;
        BitsPerSample = bitsPerSample;
        ExtraSize = 0;
        return new Wave();
    }
    public Wave CreateIeeeFloatWave(uint sampleRate, ushort channels) => CreateFormat(sampleRate, channels, (ushort)(4 * channels), sampleRate * (ushort)(4 * channels), 32);
    public Wave CreateIeeeFloatWave(uint sampleRate, ushort channels, ushort bits) => CreateFormat(sampleRate, channels, (ushort)(4 * channels), sampleRate * (ushort)(4 * channels), bits);

    public void AddSamples(Span<byte> buffer, int offset, int count)
    {
        if (Buffer == null)
        {
            Buffer = new byte[BufferLength];
            LockObject = new object();
        }

        if (WriteBuffer(buffer, offset, count) < count && !DiscardOnBufferOverflow)
        {
            throw new InvalidOperationException("The buffer is full and cannot be written to.");
        }
    }

    public int ReadBuffer(Span<byte> data, int offset, int count)
    {
        lock (LockObject!)
        {
            if (count > ByteCount)
            {
                count = ByteCount;
            }

            int num = 0;
            int num2 = Math.Min(Buffer!.Length - ReadPosition, count);
            Array.Copy(Buffer, ReadPosition, data.ToArray(), offset, num2);
            num += num2;
            ReadPosition += num2;
            ReadPosition %= Buffer.Length;
            if (num < count)
            {
                Array.Copy(Buffer, ReadPosition, data.ToArray(), offset + num, count - num);
                ReadPosition += count - num;
                num = count;
            }

            ByteCount -= num;
            return num;
        }
    }

    public int WriteBuffer(Span<byte> data, int offset, int count)
    {
        lock (LockObject!)
        {
            int num = 0;
            if (count > Buffer!.Length - ByteCount)
            {
                count = Buffer.Length - ByteCount;
            }

            int num2 = Math.Min(Buffer.Length - WritePosition, count);
            Array.Copy(data.ToArray(), offset, Buffer, WritePosition, num2);
            WritePosition += num2;
            WritePosition %= Buffer.Length;
            num += num2;
            if (num < count)
            {
                Array.Copy(data.ToArray(), offset + num, Buffer, WritePosition, count - num);
                WritePosition += count - num;
                num = count;
            }

            ByteCount += num;
            return num;
        }
    }

    public int Read(Span<byte> array, int offset, int count)
    {
        if (count % BlockAlign != 0)
        {
            throw new ArgumentException(
                $"Must read complete blocks: requested {count}, block align is {BlockAlign}");
        }
        lock (LockObject!)
        {
            // sometimes there is more junk at the end of the file past the data chunk
            if (Position + count > DataChunkLength)
            {
                count = (int)(DataChunkLength - Position);
            }
            return InStream!.Read(array.ToArray(), offset, count);
        }
    }

    public void Write(Span<byte> data, int offset, int count)
    {
        if (OutStream!.Length + count > uint.MaxValue)
        {
            throw new ArgumentException("WAV file too large", nameof(count));
        }

        OutStream.Write(data.ToArray(), offset, count);
        DataChunkSize += count;
    }

    public Span<byte> WriteBytes(Span<long> data) => WriteBytes(data, WaveEncoding.Pcm16);

    public Span<byte> WriteBytes(Span<long> data, WaveEncoding encoding)
    {
        var convertedData = new byte[data.Length * 2];
        int index = 0;
        for (int i = 0; i < data.Length; i++)
        {
            convertedData[index++] = (byte)(data[i] & 0xff);
            convertedData[index++] = (byte)(data[i] >> 8);
            convertedData[index++] = (byte)(data[i] >> 16);
            convertedData[index++] = (byte)(data[i] >> 24);
            convertedData[index++] = (byte)(data[i] >> 32);
            convertedData[index++] = (byte)(data[i] >> 40);
            convertedData[index++] = (byte)(data[i] >> 48);
            convertedData[index++] = (byte)(data[i] >> 56);
        }

        return WriteBytes(convertedData, encoding);
    }

    public Span<byte> WriteBytes(Span<int> data) => WriteBytes(data, WaveEncoding.Pcm16);

    public Span<byte> WriteBytes(Span<int> data, WaveEncoding encoding)
    {
        var convertedData = new byte[data.Length * 2];
        int index = 0;
        for (int i = 0; i < data.Length; i++)
        {
            convertedData[index++] = (byte)(data[i] & 0xff);
            convertedData[index++] = (byte)(data[i] >> 8);
            convertedData[index++] = (byte)(data[i] >> 16);
            convertedData[index++] = (byte)(data[i] >> 24);
        }

        return WriteBytes(convertedData, encoding);
    }

    public Span<byte> WriteBytes(Span<short> data) => WriteBytes(data, WaveEncoding.Pcm16);

    public Span<byte> WriteBytes(Span<short> data, WaveEncoding encoding)
    {
        var convertedData = new byte[data.Length * 2];
        int index = 0;
        for (int i = 0; i < data.Length; i++)
        {
            convertedData[index++] = (byte)(data[i] & 0xff);
            convertedData[index++] = (byte)(data[i] >> 8);
        }

        return WriteBytes(convertedData, encoding);
    }

    public Span<byte> WriteBytes(Span<byte> data) => WriteBytes(data, WaveEncoding.Pcm16);

    public Span<byte> WriteBytes(Span<byte> data, WaveEncoding encoding)
    {
        // Creating the RIFF Wave header
        string fileID = "RIFF";
        uint fileSize = (uint)(data.Length + 44); // File size must match the size of the samples and header size
        string waveID = "WAVE";
        string formatID = "fmt ";
        uint formatLength = 16; // Always a length 16
        ushort formatType = (ushort)encoding; // 1 is PCM16, 2 is ADPCM, etc.
                               // Number of channels is already manually defined
        uint sampleRate = SampleRate; // Sample Rate is read directly from the Info context
        ushort bitsPerSample = 16; // bitsPerSample must be written to AFTER numNibbles
        uint numNibbles = sampleRate * bitsPerSample * Channels / 8; // numNibbles must be written BEFORE bitsPerSample is written
        ushort bitRate = (ushort)(bitsPerSample * Channels / 8);
        string dataID = "data";
        uint dataSize = (uint)data.Length;

        byte[] samplerChunk;

        if (IsLooped)
        {
            string samplerID = "smpl";
            uint samplerSize = 0;

            uint manufacturer = 0;
            uint product = 0;
            uint samplePeriod = 0;
            uint midiUnityNote = 0;
            uint midiPitchFraction = 0;
            uint smpteFormat = 0;
            uint smpteOffset = 0;
            uint numSampleLoops = 1;
            uint samplerDataSize = 0;

            samplerSize += 36;

            if (numSampleLoops > 0)
            {
                var loopID = new uint[numSampleLoops];
                var loopType = new uint[numSampleLoops];
                var loopStart = new uint[numSampleLoops];
                var loopEnd = new uint[numSampleLoops];
                var loopFraction = new uint[numSampleLoops];
                var loopNumPlayback = new uint[numSampleLoops];

                var loopHeaderSize = 0;
                for (int i = 0; i < numSampleLoops; i++)
                {
                    loopID[i] = 0;
                    loopType[i] = 0;
                    loopStart[i] = LoopStart;
                    loopEnd[i] = LoopEnd;
                    loopFraction[i] = 0;
                    loopNumPlayback[i] = 0;

                    loopHeaderSize += 24;
                    samplerSize += 24;
                }
                var loopHeader = new byte[loopHeaderSize];
                var lw = new EndianBinaryWriter(new MemoryStream(loopHeader));
                for (int i = 0; i < numSampleLoops; i++)
                {
                    lw.WriteUInt32(loopID[i]);
                    lw.WriteUInt32(loopType[i]);
                    lw.WriteUInt32(loopStart[i]);
                    lw.WriteUInt32(loopEnd[i]);
                    lw.WriteUInt32(loopFraction[i]);
                    lw.WriteUInt32(loopNumPlayback[i]);
                }
                samplerChunk = new byte[samplerSize + 8];

                var sw = new EndianBinaryWriter(new MemoryStream(samplerChunk));
                sw.WriteChars(samplerID);
                sw.WriteUInt32(samplerSize);
                sw.WriteUInt32(manufacturer);
                sw.WriteUInt32(product);
                sw.WriteUInt32(samplePeriod);
                sw.WriteUInt32(midiUnityNote);
                sw.WriteUInt32(midiPitchFraction);
                sw.WriteUInt32(smpteFormat);
                sw.WriteUInt32(smpteOffset);
                sw.WriteUInt32(numSampleLoops);
                sw.WriteUInt32(samplerDataSize);
                sw.WriteBytes(loopHeader);

                fileSize += (uint)samplerChunk.Length;

                var waveData = new byte[fileSize];
                var w = new EndianBinaryWriter(new MemoryStream(waveData));
                w.WriteChars(fileID);
                w.WriteUInt32(fileSize);
                w.WriteChars(waveID);
                w.WriteChars(formatID);
                w.WriteUInt32(formatLength);
                w.WriteUInt16(formatType);
                w.WriteUInt16(Channels);
                w.WriteUInt32(sampleRate);
                w.WriteUInt32(numNibbles);
                w.WriteUInt16(bitRate);
                w.WriteUInt16(bitsPerSample);
                w.WriteChars(dataID);
                w.WriteUInt32(dataSize);
                w.WriteBytes(data);
                w.WriteBytes(samplerChunk);

                return waveData;
            }
            else
            {
                samplerChunk = new byte[samplerSize + 8];

                var sw = new EndianBinaryWriter(new MemoryStream(samplerChunk));
                sw.WriteChars(samplerID);
                sw.WriteUInt32(samplerSize);
                sw.WriteUInt32(manufacturer);
                sw.WriteUInt32(product);
                sw.WriteUInt32(samplePeriod);
                sw.WriteUInt32(midiUnityNote);
                sw.WriteUInt32(midiPitchFraction);
                sw.WriteUInt32(smpteFormat);
                sw.WriteUInt32(smpteOffset);
                sw.WriteUInt32(numSampleLoops);
                sw.WriteUInt32(samplerDataSize);

                fileSize += (uint)samplerChunk.Length;

                var waveData = new byte[fileSize];
                var w = new EndianBinaryWriter(new MemoryStream(waveData));
                w.WriteChars(fileID);
                w.WriteUInt32(fileSize);
                w.WriteChars(waveID);
                w.WriteChars(formatID);
                w.WriteUInt32(formatLength);
                w.WriteUInt16(formatType);
                w.WriteUInt16(Channels);
                w.WriteUInt32(sampleRate);
                w.WriteUInt32(numNibbles);
                w.WriteUInt16(bitRate);
                w.WriteUInt16(bitsPerSample);
                w.WriteChars(dataID);
                w.WriteUInt32(dataSize);
                w.WriteBytes(data);
                w.WriteBytes(samplerChunk);

                return waveData;
            }
        }
        else
        {
            var waveData = new byte[fileSize];
            var w = new EndianBinaryWriter(new MemoryStream(waveData));
            w.WriteChars(fileID);
            w.WriteUInt32(fileSize);
            w.WriteChars(waveID);
            w.WriteChars(formatID);
            w.WriteUInt32(formatLength);
            w.WriteUInt16(formatType);
            w.WriteUInt16(Channels);
            w.WriteUInt32(sampleRate);
            w.WriteUInt32(numNibbles);
            w.WriteUInt16(bitRate);
            w.WriteUInt16(bitsPerSample);
            w.WriteChars(dataID);
            w.WriteUInt32(dataSize);
            w.WriteBytes(data);

            return waveData;
        }
    }
}
