using Gtk;
using Kermalis.VGMusicStudio.Core;

namespace Kermalis.VGMusicStudio.GTK4;

public class SequencedAudio_TrackInfo : Box
{
    public readonly bool[] NumTracks;
    public readonly SongState Info;
    
    public SequencedAudio_TrackInfo()
    {
        NumTracks = new bool[SongState.MAX_TRACKS];
        for (int i = 0; i < SongState.MAX_TRACKS; i++)
            NumTracks[i] = true;

        Info = new SongState();
    }
}