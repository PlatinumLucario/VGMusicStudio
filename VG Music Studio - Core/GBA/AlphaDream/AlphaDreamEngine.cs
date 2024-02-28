using System.IO;

namespace Kermalis.VGMusicStudio.Core.GBA.AlphaDream;

public sealed class AlphaDreamEngine : Engine
{
	public static AlphaDreamEngine? AlphaDreamInstance { get; private set; }

	public override AlphaDreamConfig Config { get; }
	public override AlphaDreamMixer Mixer { get; }
	public AlphaDreamMixer_NAudio Mixer_NAudio { get; }
	public override AlphaDreamPlayer Player { get; }
	public override bool UseNewMixer { get => false; }

	public AlphaDreamEngine(byte[] rom)
	{
		if (rom.Length > GBAUtils.CARTRIDGE_CAPACITY)
		{
			throw new InvalidDataException($"The ROM is too large. Maximum size is 0x{GBAUtils.CARTRIDGE_CAPACITY:X7} bytes.");
		}

		Config = new AlphaDreamConfig(rom);
		if (Engine.Instance!.UseNewMixer)
		{
			Mixer = new AlphaDreamMixer(Config);
			Player = new AlphaDreamPlayer(Config, Mixer);
		}
		else
		{
			Mixer_NAudio = new AlphaDreamMixer_NAudio(Config);
			Player = new AlphaDreamPlayer(Config, Mixer_NAudio);
		}

		AlphaDreamInstance = this;
		Instance = this;
	}

	public override void Dispose()
	{
		base.Dispose();
		AlphaDreamInstance = null;
	}
}
