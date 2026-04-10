using System;

namespace Kermalis.VGMusicStudio.Core.GBA.MP2K;

internal class MP2KSample
{
    public SampleHeader Header;
    public byte[] PCMData;

    public MP2KSample(ReadOnlySpan<byte> src, bool isPCM4 = false)
    {
        if (isPCM4)
        {
            src[..16].CopyTo(PCMData = new byte[16]);
        }
        else
        {
            Header = new SampleHeader(src);
            PCMData = src.Slice(16, (int)(Header.Length - 1)).ToArray();
        }
    }
}
