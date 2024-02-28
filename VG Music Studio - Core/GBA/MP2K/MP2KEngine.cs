using System.IO;

namespace Kermalis.VGMusicStudio.Core.GBA.MP2K;

public sealed class MP2KEngine : Engine
{
    public static MP2KEngine? MP2KInstance { get; private set; }

    public override MP2KConfig Config { get; }
    public override MP2KMixer Mixer { get; }
    public MP2KMixer_NAudio Mixer_NAudio { get; }
    public override MP2KPlayer Player { get; }
    public override bool UseNewMixer { get => true; }

    public MP2KEngine(byte[] rom)
    {
        if (rom.Length > GBAUtils.CARTRIDGE_CAPACITY)
        {
            throw new InvalidDataException($"The ROM is too large. Maximum size is 0x{GBAUtils.CARTRIDGE_CAPACITY:X7} bytes.");
        }

        Config = new MP2KConfig(rom);
        if (UseNewMixer)
        {
            Mixer = new MP2KMixer(Config);
            Player = new MP2KPlayer(Config, Mixer);
        }
        else
        {
            Mixer_NAudio = new MP2KMixer_NAudio(Config);
            Player = new MP2KPlayer(Config, Mixer_NAudio);
        }
        
        MP2KInstance = this;
        Instance = this;
    }

    public override void Dispose()
    {
        base.Dispose();
        MP2KInstance = null;
    }
}
