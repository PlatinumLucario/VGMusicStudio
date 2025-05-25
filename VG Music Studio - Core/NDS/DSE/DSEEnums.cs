namespace Kermalis.VGMusicStudio.Core.NDS.DSE;

internal enum EnvelopeState : byte
{
	Initialize = 0,
	PlayNote = 1,
	Attack = 2,
	Hold = 3,
	Decay = 4,
	Fade = 5,
	Sustain = 6,
	End = 7,
	Release = 8,
}

internal enum SampleFormat : ushort
{
	PCM8 = 0,
	PCM16 = 1,
	ADPCM = 2,
	PSG = 3
}
