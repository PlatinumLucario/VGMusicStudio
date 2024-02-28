using System;

namespace Kermalis.VGMusicStudio.Core.GBA.MP2K;

internal sealed class MP2KSquareChannel : MP2KPSGChannel
{
	private float[] _pat;

	public MP2KSquareChannel(MP2KMixer mixer)
		: base(mixer)
	{
		_pat = null!;
	}
	public MP2KSquareChannel(MP2KMixer_NAudio mixer)
		: base(mixer)
	{
		_pat = null!;
	}
	public void Init(MP2KTrack owner, NoteInfo note, ADSR env, int instPan, SquarePattern pattern)
	{
		Init(owner, note, env, instPan);
		_pat = pattern switch
		{
			SquarePattern.D12 => MP2KUtils.SquareD12,
			SquarePattern.D25 => MP2KUtils.SquareD25,
			SquarePattern.D50 => MP2KUtils.SquareD50,
			_ => MP2KUtils.SquareD75,
		};
	}

	public override void SetPitch(int pitch)
	{
		_frequency = 3_520 * MathF.Pow(2, ((Note.Note - 69) / 12f) + (pitch / 768f));
	}

	public override void Process(float[] buffer)
	{
		StepEnvelope();
		if (State == EnvelopeState.Dead)
		{
			return;
		}

		ChannelVolume vol = GetVolume();
		float interStep;

		int bufPos = 0;
		int samplesPerBuffer;
		if (Engine.Instance!.UseNewMixer)
		{
			interStep = _frequency * _mixer!.SampleRateReciprocal;
			samplesPerBuffer = _mixer!.SamplesPerBuffer;
		}
		else
		{
			interStep = _frequency * _mixer_NAudio!.SampleRateReciprocal;
			samplesPerBuffer = _mixer_NAudio!.SamplesPerBuffer;
		}
		do
		{
			float samp = _pat[_pos];

			buffer[bufPos++] += samp * vol.LeftVol;
			buffer[bufPos++] += samp * vol.RightVol;

			_interPos += interStep;
			int posDelta = (int)_interPos;
			_interPos -= posDelta;
			_pos = (_pos + posDelta) & 0x7;
		} while (--samplesPerBuffer > 0);
	}
}