using System;

namespace Kermalis.VGMusicStudio.Core;

public abstract class Engine : IDisposable
{
	public static Engine? Instance { get; protected set; }

	public abstract Config Config { get; }
	public abstract Mixer? Mixer { get; }
	public abstract Mixer_NAudio? Mixer_NAudio { get; }
	public abstract Player Player { get; }
	public abstract bool UseNewMixer { get; }

	public virtual void Dispose()
	{
		Config.Dispose();
		if (UseNewMixer)
			Mixer!.Dispose();
		else
			Mixer_NAudio!.Dispose();
		Player.Dispose();
		Instance = null;
		GC.SuppressFinalize(this);
	}
}
