namespace Kermalis.VGMusicStudio.Core.NDS.SDAT;

public sealed class SDATEngine : Engine
{
	public static SDATEngine? SDATInstance { get; private set; }

	public override SDATConfig Config { get; }
	public override SDATMixer? Mixer { get; }
	public override SDATMixer_NAudio? Mixer_NAudio { get; }
	public override SDATPlayer Player { get; }
	public override bool UseNewMixer { get; }

	public SDATEngine(SDAT sdat, bool useNewMixer = false)
	{
		UseNewMixer = useNewMixer;
		Config = new SDATConfig(sdat);
		if (UseNewMixer)
		{
			Mixer = new SDATMixer();
			Player = new SDATPlayer(Config, Mixer);
		}
		else
		{
			Mixer_NAudio = new SDATMixer_NAudio();
			Player = new SDATPlayer(Config, Mixer_NAudio);
		}

		SDATInstance = this;
		Instance = this;
	}

	public override void Dispose()
	{
		base.Dispose();
		SDATInstance = null;
	}
}
