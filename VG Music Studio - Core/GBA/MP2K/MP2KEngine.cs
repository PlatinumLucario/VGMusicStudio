using System.IO;

namespace Kermalis.VGMusicStudio.Core.GBA.MP2K;

public sealed class MP2KEngine : Engine
{
    public static MP2KEngine? MP2KInstance { get; private set; }

    public override MP2KConfig Config { get; }
    public override MP2KMixer Mixer { get; }
    public override MP2KMixer_NAudio Mixer_NAudio { get; }
    public override MP2KPlayer Player { get; }
    public override bool UseNewMixer { get; }

    public MP2KEngine(byte[] rom) => new MP2KEngine(rom, true, true);
    public MP2KEngine(byte[] rom, bool useNewMixer) => new MP2KEngine(rom, useNewMixer, true);
    public MP2KEngine(byte[] rom, bool useNewMixer, bool mainPlaylistFirst)
    {
        UseNewMixer = useNewMixer;
        if (rom.Length > GBAUtils.CARTRIDGE_CAPACITY)
        {
            throw new InvalidDataException($"The ROM is too large. Maximum size is 0x{GBAUtils.CARTRIDGE_CAPACITY:X7} bytes.");
        }

        Config = new MP2KConfig(rom, mainPlaylistFirst);
        if (UseNewMixer)
        {
            Mixer = new MP2KMixer(Config);
            Player = new MP2KPlayer(Config, Mixer);
        }
        else
        {
            Mixer = new MP2KMixer();
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
