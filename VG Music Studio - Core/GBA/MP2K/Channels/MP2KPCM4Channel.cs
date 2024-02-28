using System;

namespace Kermalis.VGMusicStudio.Core.GBA.MP2K;

internal sealed class MP2KPCM4Channel : MP2KPSGChannel
{
	private readonly float[] _sample;

	public MP2KPCM4Channel(MP2KMixer mixer)
		: base(mixer)
	{
		_sample = new float[0x20];
	}
	public MP2KPCM4Channel(MP2KMixer_NAudio mixer)
		: base(mixer)
	{
		_sample = new float[0x20];
	}
	public void Init(MP2KTrack owner, NoteInfo note, ADSR env, int instPan, int sampleOffset)
	{
		Init(owner, note, env, instPan);
		if (Engine.Instance!.UseNewMixer)
			MP2KUtils.PCM4ToFloat(_mixer!.Config.ROM.AsSpan(sampleOffset), _sample);
		else
			MP2KUtils.PCM4ToFloat(_mixer_NAudio!.Config.ROM.AsSpan(sampleOffset), _sample);
	}

	public override void SetPitch(int pitch)
	{
		_frequency = 7_040 * MathF.Pow(2, ((Note.Note - 69) / 12f) + (pitch / 768f));
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
			float samp = _sample[_pos];

			buffer[bufPos++] += samp * vol.LeftVol;
			buffer[bufPos++] += samp * vol.RightVol;

			_interPos += interStep;
			int posDelta = (int)_interPos;
			_interPos -= posDelta;
			_pos = (_pos + posDelta) & 0x1F;
		} while (--samplesPerBuffer > 0);
	}
}