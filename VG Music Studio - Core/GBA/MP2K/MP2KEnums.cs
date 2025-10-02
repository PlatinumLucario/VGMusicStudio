using System;

namespace Kermalis.VGMusicStudio.Core.GBA.MP2K;

internal enum EnvelopeState : byte
{
	Initializing,
	Rising,
	Decaying,
	Playing,
	PseudoEcho,
	Releasing,
	Dying,
	Dead,
}
internal enum ReverbType : byte
{
	None,
	Normal,
	Camelot1,
	Camelot2,
	MGAT,
}

internal enum GoldenSunPSGType : byte
{
	Square,
	Saw,
	Triangle,
}
internal enum LFOType : byte
{
	Pitch,
	Volume,
	Panpot,
}
internal enum SquarePattern : byte
{
	D12,
	D25,
	D50,
	D75,
}
internal enum NoisePattern : byte
{
	Fine,
	Rough,
}
/// <summary>
/// These flags determines the voice type used, whenever it be either PCM8 data or a PSG format
/// </summary>
internal enum VoiceType : byte
{
	/// <summary>Raw PCM8 data</summary>
	PCM8,
	/// <summary>PSG Square Wave Type 1</summary>
	Square1,
	/// <summary>PSG Square Wave Type 2</summary>
	Square2,
	/// <summary>PSG PCM4 Wave Data</summary>
	PCM4,
	/// <summary>PSG Noise Wave</summary>
	Noise,
	/// <summary>Unused</summary>
	Invalid5,
	/// <summary>Unused</summary>
	Invalid6,
	/// <summary>Unused</summary>
	Invalid7,
}
/// <summary>
/// These are flags that apply to the voice types
/// </summary>
[Flags]
internal enum VoiceFlags : byte
{
	/// <summary>PCM8 only</summary>
	Fixed = 0x08,
	/// <summary>Only used with PSG types: Square1, Square2, PCM4, Noise</summary>
	OffWithNoise = 0x09,
	/// <summary>PCM8 only</summary>
	Reversed = 0x10,
	/// <summary>PCM8 (Only in Pokémon main series games)</summary>
	Compressed = 0x20,

	// These are flags that cancel out every other bit after them if set so they should only be checked with equality
	KeySplit = 0x40,
	Drum = 0x80,
}
internal enum LibraryCommandTypes : byte
{
    xIECV = 8,
    xIECL = 9,
}
internal enum MemoryOperatorType : byte
{
	mem_set = 0,
}
