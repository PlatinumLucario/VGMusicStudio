using System;
using System.Collections.Generic;
using Kermalis.VGMusicStudio.Core;

namespace Kermalis.VGMusicStudio.UI
{
    internal interface IPlayer : IDisposable
    {
        void GetSongState(SongInfoControl.SongInfo info);
    }
}
