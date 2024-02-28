﻿using Kermalis.VGMusicStudio.Core.Formats;
using Kermalis.VGMusicStudio.Core.NDS.SDAT;
using Kermalis.VGMusicStudio.Core.Util;
using System;

namespace Kermalis.VGMusicStudio.Core.NDS.DSE;

public sealed class DSEMixer : Mixer
{
	private const int NUM_CHANNELS = 0x20; // Actual value unknown for now

	private readonly float _samplesReciprocal;
	private readonly int _samplesPerBuffer;
	private bool _isFading;
	private long _fadeMicroFramesLeft;
	private float _fadePos;
	private float _fadeStepPerMicroframe;

	private readonly DSEChannel[] _channels;
	private readonly Wave _buffer;

	public DSEMixer()
	{
		// The sampling frequency of the mixer is 1.04876 MHz with an amplitude resolution of 24 bits, but the sampling frequency after mixing with PWM modulation is 32.768 kHz with an amplitude resolution of 10 bits.
		// - gbatek
		// I'm not using either of those because the samples per buffer leads to an overflow eventually
		const int sampleRate = 65_456;
		_samplesPerBuffer = 341; // TODO
		_samplesReciprocal = 1f / _samplesPerBuffer;

		_channels = new DSEChannel[NUM_CHANNELS];
		for (byte i = 0; i < NUM_CHANNELS; i++)
		{
			_channels[i] = new DSEChannel(i);
		}

		_buffer = new Wave()
		{
			DiscardOnBufferOverflow = true,
			BufferLength = _samplesPerBuffer * 64,
		};
		_buffer.CreateIeeeFloatWave(sampleRate, 2, 16);
		Init(_buffer);
	}

	internal DSEChannel? AllocateChannel()
	{
		static int GetScore(DSEChannel c)
		{
			// Free channels should be used before releasing channels
			return c.Owner is null ? -2 : DSEUtils.IsStateRemovable(c.State) ? -1 : 0;
		}
		DSEChannel? nChan = null;
		for (int i = 0; i < NUM_CHANNELS; i++)
		{
			DSEChannel c = _channels[i];
			if (nChan is null)
			{
				nChan = c;
			}
			else
			{
				int nScore = GetScore(nChan);
				int cScore = GetScore(c);
				if (cScore <= nScore && (cScore < nScore || c.Volume <= nChan.Volume))
				{
					nChan = c;
				}
			}
		}
		return nChan is not null && 0 >= GetScore(nChan) ? nChan : null;
	}

	internal void ChannelTick()
	{
		for (int i = 0; i < NUM_CHANNELS; i++)
		{
			DSEChannel chan = _channels[i];
			if (chan.Owner is null)
			{
				continue;
			}

			chan.Volume = (byte)chan.StepEnvelope();
			if (chan.NoteLength == 0 && !DSEUtils.IsStateRemovable(chan.State))
			{
				chan.SetEnvelopePhase7_2074ED8();
			}
			int vol = SDATUtils.SustainTable[chan.NoteVelocity] + SDATUtils.SustainTable[chan.Volume] + SDATUtils.SustainTable[chan.Owner.Volume] + SDATUtils.SustainTable[chan.Owner.Expression];
			//int pitch = ((chan.Key - chan.BaseKey) << 6) + chan.SweepMain() + chan.Owner.GetPitch(); // "<< 6" is "* 0x40"
			int pitch = (chan.Key - chan.RootKey) << 6; // "<< 6" is "* 0x40"
			if (DSEUtils.IsStateRemovable(chan.State) && vol <= -92544)
			{
				chan.Stop();
			}
			else
			{
				chan.Volume = SDATUtils.GetChannelVolume(vol);
				chan.Panpot = chan.Owner.Panpot;
				chan.Timer = SDATUtils.GetChannelTimer(chan.BaseTimer, pitch);
			}
		}
	}

	internal void BeginFadeIn()
	{
		_fadePos = 0f;
		_fadeMicroFramesLeft = (long)(GlobalConfig.Instance.PlaylistFadeOutMilliseconds / 1000.0 * 192);
		_fadeStepPerMicroframe = 1f / _fadeMicroFramesLeft;
		_isFading = true;
	}
	internal void BeginFadeOut()
	{
		_fadePos = 1f;
		_fadeMicroFramesLeft = (long)(GlobalConfig.Instance.PlaylistFadeOutMilliseconds / 1000.0 * 192);
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

	private readonly byte[] _b = new byte[4];
	internal void Process(bool output, bool recording)
	{
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
		for (int i = 0; i < _samplesPerBuffer; i++)
		{
			int left = 0,
				right = 0;
			for (int j = 0; j < NUM_CHANNELS; j++)
			{
				DSEChannel chan = _channels[j];
				if (chan.Owner is null)
				{
					continue;
				}

				bool muted = Mutes[chan.Owner.Index]; // Get mute first because chan.Process() can call chan.Stop() which sets chan.Owner to null
				chan.Process(out short channelLeft, out short channelRight);
				if (!muted)
				{
					left += channelLeft;
					right += channelRight;
				}
			}
			float f = left * masterLevel;
			if (f < short.MinValue)
			{
				f = short.MinValue;
			}
			else if (f > short.MaxValue)
			{
				f = short.MaxValue;
			}
			left = (int)f;
			_b[0] = (byte)left;
			_b[1] = (byte)(left >> 8);
			f = right * masterLevel;
			if (f < short.MinValue)
			{
				f = short.MinValue;
			}
			else if (f > short.MaxValue)
			{
				f = short.MaxValue;
			}
			right = (int)f;
			_b[2] = (byte)right;
			_b[3] = (byte)(right >> 8);
			masterLevel += masterStep;
			if (output)
			{
				_buffer.AddSamples(_b, 0, 4);
			}
			if (recording)
			{
				_waveWriter!.Write(_b, 0, 4);
			}
		}
	}
}
