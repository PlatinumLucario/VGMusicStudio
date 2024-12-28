﻿using Kermalis.VGMusicStudio.Core.Formats;
using Kermalis.VGMusicStudio.Core.Util;
using System;
using System.Linq;

namespace Kermalis.VGMusicStudio.Core.GBA.MP2K;

public sealed class MP2KMixer : Mixer
{
    internal readonly int SampleRate;
    internal readonly int SamplesPerBuffer;
    internal readonly float SampleRateReciprocal;
    private readonly float _samplesReciprocal;
    internal readonly float PCM8MasterVolume;
    private bool _isFading;
    private long _fadeMicroFramesLeft;
    private float _fadePos;
    private float _fadeStepPerMicroframe;

    internal readonly MP2KConfig Config;
    private readonly Audio _audio;
    private readonly float[][] _trackBuffers;
    private readonly MP2KPCM8Channel[] _pcm8Channels;
    private readonly MP2KSquareChannel _sq1;
    private readonly MP2KSquareChannel _sq2;
    private readonly MP2KPCM4Channel _pcm4;
    private readonly MP2KNoiseChannel _noise;
    private readonly MP2KPSGChannel[] _psgChannels;
    private readonly Wave _buffer;

    internal MP2KMixer() { }

    internal MP2KMixer(MP2KConfig config)
    {
        Config = config;
        (SampleRate, SamplesPerBuffer) = MP2KUtils.FrequencyTable[config.SampleRate];
        SampleRateReciprocal = 1f / SampleRate;
        _samplesReciprocal = 1f / SamplesPerBuffer;
        PCM8MasterVolume = config.Volume / 15f;

        _pcm8Channels = new MP2KPCM8Channel[24];
        for (int i = 0; i < _pcm8Channels.Length; i++)
        {
            _pcm8Channels[i] = new MP2KPCM8Channel(this);
        }
        _psgChannels = [_sq1 = new MP2KSquareChannel(this), _sq2 = new MP2KSquareChannel(this), _pcm4 = new MP2KPCM4Channel(this), _noise = new MP2KNoiseChannel(this)];

        int amt = SamplesPerBuffer * 2;
        Instance = this;
        _audio = new Audio(amt * sizeof(float)) { Float32BufferCount = amt };
        _trackBuffers = new float[0x10][];
        for (int i = 0; i < _trackBuffers.Length; i++)
        {
            _trackBuffers[i] = new float[amt];
        }
        _buffer = new Wave()
        {
            DiscardOnBufferOverflow = true,
            BufferLength = SamplesPerBuffer * 64,
        };
        _buffer.CreateIeeeFloatWave((uint)SampleRate, 2);

        Init(_buffer, _audio);
    }

    internal MP2KPCM8Channel? AllocPCM8Channel(MP2KTrack owner, ADSR env, NoteInfo note, byte vol, sbyte pan, int instPan, int pitch, bool bFixed, bool bCompressed, int sampleOffset)
    {
        MP2KPCM8Channel? nChn = null;
        IOrderedEnumerable<MP2KPCM8Channel> byOwner = _pcm8Channels.OrderByDescending(c => c.Owner is null ? 0xFF : c.Owner.Index);
        foreach (MP2KPCM8Channel i in byOwner) // Find free
        {
            if (i.State == EnvelopeState.Dead || i.Owner is null)
            {
                nChn = i;
                break;
            }
        }
        if (nChn is null) // Find releasing
        {
            foreach (MP2KPCM8Channel i in byOwner)
            {
                if (i.State == EnvelopeState.Releasing)
                {
                    nChn = i;
                    break;
                }
            }
        }
        if (nChn is null) // Find prioritized
        {
            foreach (MP2KPCM8Channel i in byOwner)
            {
                if (owner.Priority > i.Owner!.Priority)
                {
                    nChn = i;
                    break;
                }
            }
        }
        if (nChn is null) // None available
        {
            MP2KPCM8Channel lowest = byOwner.First(); // Kill lowest track's instrument if the track is lower than this one
            if (lowest.Owner!.Index >= owner.Index)
            {
                nChn = lowest;
            }
        }
        if (nChn is not null) // Could still be null from the above if
        {
            nChn.Init(owner, note, env, sampleOffset, vol, pan, instPan, pitch, bFixed, bCompressed);
        }
        return nChn;
    }
    internal MP2KPSGChannel? AllocPSGChannel(MP2KTrack owner, ADSR env, NoteInfo note, byte vol, sbyte pan, int instPan, int pitch, VoiceType type, object arg)
    {
        MP2KPSGChannel nChn;
        switch (type)
        {
            case VoiceType.Square1:
                {
                    nChn = _sq1;
                    if (nChn.State < EnvelopeState.Releasing && nChn.Owner!.Index < owner.Index)
                    {
                        return null;
                    }
                    _sq1.Init(owner, note, env, instPan, (SquarePattern)arg);
                    break;
                }
            case VoiceType.Square2:
                {
                    nChn = _sq2;
                    if (nChn.State < EnvelopeState.Releasing && nChn.Owner!.Index < owner.Index)
                    {
                        return null;
                    }
                    _sq2.Init(owner, note, env, instPan, (SquarePattern)arg);
                    break;
                }
            case VoiceType.PCM4:
                {
                    nChn = _pcm4;
                    if (nChn.State < EnvelopeState.Releasing && nChn.Owner!.Index < owner.Index)
                    {
                        return null;
                    }
                    _pcm4.Init(owner, note, env, instPan, (int)arg);
                    break;
                }
            case VoiceType.Noise:
                {
                    nChn = _noise;
                    if (nChn.State < EnvelopeState.Releasing && nChn.Owner!.Index < owner.Index)
                    {
                        return null;
                    }
                    _noise.Init(owner, note, env, instPan, (NoisePattern)arg);
                    break;
                }
            default: return null;
        }
        nChn.SetVolume(vol, pan);
        nChn.SetPitch(pitch);
        return nChn;
    }

    internal void BeginFadeIn()
    {
        _fadePos = 0f;
        _fadeMicroFramesLeft = (long)(GlobalConfig.Instance.PlaylistFadeOutMilliseconds / 1_000.0 * GBAUtils.AGB_FPS);
        _fadeStepPerMicroframe = 1f / _fadeMicroFramesLeft;
        _isFading = true;
    }
    internal void BeginFadeOut()
    {
        _fadePos = 1f;
        _fadeMicroFramesLeft = (long)(GlobalConfig.Instance.PlaylistFadeOutMilliseconds / 1_000.0 * GBAUtils.AGB_FPS);
        _fadeStepPerMicroframe = -1f / _fadeMicroFramesLeft;
        _isFading = true;
    }
    internal bool IsFading()
    {
        return _isFading;
    }
    internal bool IsFadeDone()
    {
        return _isFading && _fadeMicroFramesLeft == 0;
    }
    internal void ResetFade()
    {
        _isFading = false;
        _fadeMicroFramesLeft = 0;
    }

    internal void Process(bool output, bool recording)
    {
        for (int i = 0; i < _trackBuffers.Length; i++)
        {
            float[] buf = _trackBuffers[i];
            Array.Clear(buf, 0, buf.Length);
        }
        _audio.Clear();

        for (int i = 0; i < _pcm8Channels.Length; i++)
        {
            MP2KPCM8Channel c = _pcm8Channels[i];
            if (c.Owner is not null)
            {
                c.Process(_trackBuffers[c.Owner.Index]);
            }
        }

        for (int i = 0; i < _psgChannels.Length; i++)
        {
            MP2KPSGChannel c = _psgChannels[i];
            if (c.Owner is not null)
            {
                c.Process(_trackBuffers[c.Owner.Index]);
            }
        }

        float masterStep;
        float masterLevel;
        if (_isFading && _fadeMicroFramesLeft == 0)
        {
            masterStep = 0;
            masterLevel = 0;
        }
        else
        {
            float fromMaster = 1f;
            float toMaster = 1f;
            if (_fadeMicroFramesLeft > 0)
            {
                const float scale = 10f / 6f;
                fromMaster *= (_fadePos < 0f) ? 0f : MathF.Pow(_fadePos, scale);
                _fadePos += _fadeStepPerMicroframe;
                toMaster *= (_fadePos < 0f) ? 0f : MathF.Pow(_fadePos, scale);
                _fadeMicroFramesLeft--;
            }
            masterStep = (toMaster - fromMaster) * _samplesReciprocal;
            masterLevel = fromMaster;
        }
        for (int i = 0; i < _trackBuffers.Length; i++)
        {
            if (Mutes[i])
            {
                continue;
            }

            float level = masterLevel;
            float[] buf = _trackBuffers[i];
            for (int j = 0; j < SamplesPerBuffer; j++)
            {
                _audio.Float32Buffer![j * 2] += buf[j * 2] * level;
                _audio.Float32Buffer[(j * 2) + 1] += buf[(j * 2) + 1] * level;
                level += masterStep;
            }
        }
        if (output)
        {
            _buffer.AddSamples(_audio.ByteBuffer, 0, _audio.ByteBufferCount);
        }
        if (recording)
        {
            //_waveWriter!.Write(_audio.ByteBuffer, 0, _audio.ByteBufferCount);
        }
    }
}
