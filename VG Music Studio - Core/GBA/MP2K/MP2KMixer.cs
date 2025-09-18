using System;
using System.IO;
using System.Linq;
using Kermalis.VGMusicStudio.Core.Formats;
using Kermalis.VGMusicStudio.Core.Util;
using NAudio.Wave;
using SoundFlow.Enums;
using SoundFlow.Structs;

namespace Kermalis.VGMusicStudio.Core.GBA.MP2K;

public sealed class MP2KMixer : Mixer
{
	internal readonly int SampleRate;
	internal override int SamplesPerBuffer { get; }
	internal readonly float SampleRateReciprocal;
	private readonly float _samplesReciprocal;
	internal readonly float PCM8MasterVolume;
	private bool _isFading;
	private long _fadeMicroFramesLeft;
	private float _fadePos;
	private float _fadeStepPerMicroframe;

	internal readonly MP2KConfig Config;
	private readonly AudioBackend MP2KPlaybackBackend;

	#region PortAudio Fields
	// PortAudio Fields
	private readonly Audio? _audioPortAudio;
	private readonly Wave? _bufferPortAudio;
	#endregion

	#region MiniAudio Fields
	// MiniAudio Fields
	private readonly float[]? _bufferMiniAudio;
	private readonly AudioFormat _formatSoundFlow;
    protected override AudioFormat SoundFlowFormat => _formatSoundFlow;
	#endregion

	#region NAudio Fields
	// NAudio Fields
	private readonly WaveBuffer? _audioNAudio;
	private readonly BufferedWaveProvider? _bufferNAudio;

	protected override WaveFormat WaveFormat => _bufferNAudio!.WaveFormat;
	#endregion

	private readonly float[][] _trackBuffers;
	private readonly MP2KPCM8Channel[] _pcm8Channels;
	private readonly MP2KSquareChannel _sq1;
	private readonly MP2KSquareChannel _sq2;
	private readonly MP2KPCM4Channel _pcm4;
	private readonly MP2KNoiseChannel _noise;
	private readonly MP2KPSGChannel[] _psgChannels;

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
		_trackBuffers = new float[0x10][];
		for (int i = 0; i < _trackBuffers.Length; i++)
		{
			_trackBuffers[i] = new float[amt];
		}
		MP2KPlaybackBackend = PlaybackBackend;
		switch (PlaybackBackend)
		{
			case AudioBackend.PortAudio:
				{
					_audioPortAudio = new Audio(amt * sizeof(float)) { Float32BufferCount = amt };
					_bufferPortAudio = new Wave()
					{
						DiscardOnBufferOverflow = true,
						BufferLength = SamplesPerBuffer * 64,
					};
					_bufferPortAudio.CreateIeeeFloatWave((uint)SampleRate, 2);

					Init(waveData: _bufferPortAudio);
					break;
				}
			case AudioBackend.MiniAudio:
				{
					_bufferMiniAudio = new float[amt];
					_formatSoundFlow = new AudioFormat
					{
						Channels = 2,
						SampleRate = SampleRate,
						Format = SampleFormat.F32
					};
					Init();
					break;
				}
			case AudioBackend.NAudio:
				{
					_audioNAudio = new WaveBuffer(amt * sizeof(float)) { FloatBufferCount = amt };
					_bufferNAudio = new BufferedWaveProvider(WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, 2))
					{
						DiscardOnBufferOverflow = true,
						BufferLength = SamplesPerBuffer * 64,
					};
					Init(waveProvider: _bufferNAudio);
					break;
				}
		}
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
			Span<float> buf = _trackBuffers[i];
			buf.Clear();
		}
		switch (MP2KPlaybackBackend)
		{
			case AudioBackend.PortAudio:
				{
					_audioPortAudio!.Clear();
					break;
				}
			case AudioBackend.MiniAudio:
				{
					Array.Clear(_bufferMiniAudio!);
					break;
				}
			case AudioBackend.NAudio:
				{
					_audioNAudio!.Clear();
					break;
				}
		}

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
			Span<float> buf = _trackBuffers[i];
			for (int j = 0; j < SamplesPerBuffer; j++)
			{
				switch (MP2KPlaybackBackend)
				{
					case AudioBackend.PortAudio:
						{
							_audioPortAudio!.Float32Buffer![j * 2] += buf[j * 2] * level;
							_audioPortAudio.Float32Buffer[(j * 2) + 1] += buf[(j * 2) + 1] * level;
							break;
						}
					case AudioBackend.MiniAudio:
						{
							_bufferMiniAudio![j * 2] += buf[j * 2] * level;
							_bufferMiniAudio[(j * 2) + 1] += buf[(j * 2) + 1] * level;
							break;
						}
					case AudioBackend.NAudio:
						{
							_audioNAudio!.FloatBuffer![j * 2] += buf[j * 2] * level;
							_audioNAudio.FloatBuffer[(j * 2) + 1] += buf[(j * 2) + 1] * level;
							break;
						}
				}
				level += masterStep;
			}
		}
		if (output)
		{
			switch (MP2KPlaybackBackend)
			{
				case AudioBackend.PortAudio:
					{
						_bufferPortAudio!.AddSamples(_audioPortAudio!.ByteBuffer, 0, _audioPortAudio.ByteBufferCount);
						break;
					}
				case AudioBackend.MiniAudio:
					{
						DataProvider!.AddSamples(_bufferMiniAudio); // Thank you LSXPrime for pointing out that it just needs AddSamples and nothing else in here
						break;
					}
				case AudioBackend.NAudio:
					{
						_bufferNAudio!.AddSamples(_audioNAudio!.ByteBuffer, 0, _audioNAudio.ByteBufferCount);
						break;
					}
			}
		}
		if (recording)
		{
			switch (MP2KPlaybackBackend)
			{
				case AudioBackend.PortAudio:
					{
						_waveWriterPortAudio!.Write(_audioPortAudio!.ByteBuffer, 0, _audioPortAudio.ByteBufferCount);
						break;
					}
				case AudioBackend.MiniAudio:
					{
						_soundFlowEncoder!.Encode(_bufferMiniAudio); // Again, thank you LSXPrime for showing how to encode
						break;
					}
				case AudioBackend.NAudio:
					{
						_waveWriterNAudio!.Write(_audioNAudio!.ByteBuffer, 0, _audioNAudio.ByteBufferCount);
						break;
					}
			}
		}
	}
}
