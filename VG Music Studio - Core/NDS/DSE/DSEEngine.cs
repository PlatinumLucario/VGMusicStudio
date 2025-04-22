using System;

namespace Kermalis.VGMusicStudio.Core.NDS.DSE;

public sealed class DSEEngine : Engine
{
	public static DSEEngine? DSEInstance { get; private set; }

	public override DSEConfig Config { get; }
	public override DSEMixer? Mixer { get; }
	public override DSEMixer_NAudio? Mixer_NAudio { get; }
	public override DSEPlayer Player { get; }
	public override bool UseNewMixer { get; }

	public DSEEngine(string SWDFile, string bgmPath, bool useNewMixer = false, bool useNewUI = false)
	{
		Config = new DSEConfig(bgmPath, useNewUI);
		if (useNewMixer)
		{
			UseNewMixer = useNewMixer;
			Mixer = new DSEMixer();
			Player = new DSEPlayer(SWDFile, Config, Mixer);
		}
		else
		{
			UseNewMixer = useNewMixer;
			Mixer_NAudio = new DSEMixer_NAudio();
			Player = new DSEPlayer(SWDFile, Config, Mixer_NAudio);
		}

		DSEInstance = this;
		Instance = this;
	}

	public override void Dispose()
	{
		base.Dispose();
		DSEInstance = null;
	}
}
