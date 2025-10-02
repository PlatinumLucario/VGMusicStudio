using System;

namespace Kermalis.VGMusicStudio.Core.GBA.MP2K;

// All of this was written in C++ by ipatix; I just converted it
internal class MP2KReverb
{
    protected readonly float[] _reverbBuffer;
    protected readonly float _intensity;
    protected readonly byte _numBuffers;
    protected readonly int _bufferLen;
    protected int _bufferPos1, _bufferPos2;

    public MP2KReverb(MP2KMixer mixer, byte intensity, byte numBuffers)
    {
        _bufferLen = mixer.SamplesPerBuffer;
        _bufferPos2 = _bufferLen;
        _intensity = intensity / (float)0x80;
        _numBuffers = numBuffers;
        _reverbBuffer = new float[_bufferLen * 2 * numBuffers];
    }

    public void Process(Span<float> buffer, int samplesPerBuffer)
    {
        int index = 0;
        while (samplesPerBuffer > 0)
        {
            int left = Process(buffer, samplesPerBuffer, ref index);
            index += (samplesPerBuffer - left) * 2;
            samplesPerBuffer = left;
        }
    }

    protected virtual void Reset()
    {
        Array.Fill(_reverbBuffer, 0f, 0, _reverbBuffer.Length);
    }

    protected virtual int Process(Span<float> buffer, int samplesPerBuffer, ref int index)
    {
        int rSamplesPerBuffer = _reverbBuffer.Length / 2;

        int count = Math.Min(
            Math.Min(rSamplesPerBuffer - _bufferPos2, rSamplesPerBuffer - _bufferPos1),
            samplesPerBuffer
            );
        bool reset1 = rSamplesPerBuffer - _bufferPos1 == count,
            reset2 = rSamplesPerBuffer - _bufferPos2 == count;
        for (int i = 0; i < count; i++)
        {
            float rev = (_reverbBuffer[_bufferPos1 * 2] * 2
                + _reverbBuffer[_bufferPos1 * 2 + 1] * 2
                + _reverbBuffer[_bufferPos2 * 2] * 2
                + _reverbBuffer[_bufferPos2 * 2 + 1] * 2) * _intensity * (1.0f / 4.0f);

            _reverbBuffer[_bufferPos1 * 2] = buffer[index++] += rev;
            _reverbBuffer[_bufferPos1 * 2 + 1] = buffer[index++] += rev;
            _bufferPos1++; _bufferPos2++;
        }
        if (reset1)
        {
            _bufferPos1 = 0;
        }
        if (reset2)
        {
            _bufferPos2 = 0;
        }
        return samplesPerBuffer - count;
    }
}

internal class MP2KReverbCamelot1 : MP2KReverb
{
    readonly float[] _cBuffer;
    public MP2KReverbCamelot1(MP2KMixer mixer, byte intensity, byte numBuffers) : base(mixer, intensity, numBuffers)
    {
        _bufferPos2 = 0;
        _cBuffer = new float[_bufferLen * 2];
    }

    protected override int Process(Span<float> buffer, int samplesPerBuffer, ref int index)
    {
        int rSamplesPerBuffer = _reverbBuffer.Length / 2;
        int cSamplesPerBuffer = _cBuffer.Length / 2;
        int count = Math.Min(
            Math.Min(rSamplesPerBuffer - _bufferPos1, cSamplesPerBuffer - _bufferPos2),
            samplesPerBuffer
            );
        bool reset1 = count == rSamplesPerBuffer - _bufferPos1,
            resetC = count == cSamplesPerBuffer - _bufferPos2;
        for (int i = 0; i < count; i++)
        {
            float mixL = buffer[index] + _cBuffer[_bufferPos2 * 2];
            float mixR = buffer[index + 1] + _cBuffer[_bufferPos2 * 2 + 1];

            float lA = _reverbBuffer[_bufferPos1 * 2];
            float rA = _reverbBuffer[_bufferPos1 * 2 + 1];

            buffer[index] = _reverbBuffer[_bufferPos1 * 2] = mixL;
            buffer[index + 1] = _reverbBuffer[_bufferPos1 * 2 + 1] = mixR;

            float lRMix = mixL / 4f + rA / 4f;
            float rRMix = mixR / 4f + lA / 4f;

            _cBuffer[_bufferPos2 * 2] = lRMix;
            _cBuffer[_bufferPos2 * 2 + 1] = rRMix;

            index += 2;
            _bufferPos1++; _bufferPos2++;
        }

        if (reset1)
        {
            _bufferPos1 = 0;
        }
        if (resetC)
        {
            _bufferPos2 = 0;
        }
        return samplesPerBuffer - count;
    }
}

internal class MP2KReverbCamelot2 : MP2KReverb
{
    readonly float[] _cBuffer; int _cPos;
    readonly float _primary, _secondary;
    internal MP2KReverbCamelot2(MP2KMixer mixer, byte intensity, byte numBuffers, float primary, float secondary) : base(mixer, intensity, numBuffers)
    {
        _cBuffer = new float[_bufferLen * 2];
        _bufferPos2 = _reverbBuffer.Length / 2 - (_cBuffer.Length / 2 / 3);
        _primary = primary; _secondary = secondary;
    }

    protected override int Process(Span<float> buffer, int samplesPerBuffer, ref int index)
    {
        int rSamplesPerBuffer = _reverbBuffer.Length / 2;
        int count = Math.Min(
                Math.Min(rSamplesPerBuffer - _bufferPos1, rSamplesPerBuffer - _bufferPos2),
                Math.Min(samplesPerBuffer, _cBuffer.Length / 2 - _cPos)
                );
        bool reset = rSamplesPerBuffer - _bufferPos1 == count,
            reset2 = rSamplesPerBuffer - _bufferPos2 == count,
            resetC = _cBuffer.Length / 2 - _cPos == count;

        for (int i = 0; i < count; i++)
        {
            float mixL = buffer[index] + _cBuffer[_cPos * 2];
            float mixR = buffer[index + 1] + _cBuffer[_cPos * 2 + 1];

            float lA = _reverbBuffer[_bufferPos1 * 2];
            float rA = _reverbBuffer[_bufferPos1 * 2 + 1];

            buffer[index] = _reverbBuffer[_bufferPos1 * 2] = mixL;
            buffer[index + 1] = _reverbBuffer[_bufferPos1 * 2 + 1] = mixR;

            float lRMix = lA * _primary + rA * _secondary;
            float rRMix = rA * _primary + lA * _secondary;

            float lB = _reverbBuffer[_bufferPos2 * 2 + 1] / 4f;
            float rB = mixR / 4f;

            _cBuffer[_cPos * 2] = lRMix + lB;
            _cBuffer[_cPos * 2 + 1] = rRMix + rB;

            index += 2;
            _bufferPos1++; _bufferPos2++; _cPos++;
        }
        if (reset)
        {
            _bufferPos1 = 0;
        }
        if (reset2)
        {
            _bufferPos2 = 0;
        }
        if (resetC)
        {
            _cPos = 0;
        }
        return samplesPerBuffer - count;
    }
}