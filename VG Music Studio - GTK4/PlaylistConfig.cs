using System;
using System.Collections.Generic;
using Gtk;
using Kermalis.VGMusicStudio.Core;
using Kermalis.VGMusicStudio.Core.Properties;

namespace Kermalis.VGMusicStudio.GTK4;

internal class PlaylistConfig : Box
{
    public DropDown PlaylistDropDown { get; set; }
    public DropDown PlaylistSongDropDown { get; set; }
    public Button ButtonPrevPlistSong, ButtonNextPlistSong;
    public uint SelectedPlaylist, SelectedSong = 0;
    private List<Config.Playlist>? Playlists { get; set; }
    internal List<Config.Song>? Songs { get; set; }
    private Box PlaylistBox, PlaylistSongBox, PlaylistSongBoxDropDown;
    // private Gio.ListStore PlistModel = Gio.ListStore.New(GetGType());
    // private StringList StringList { get; set; }
    private static StringList? PlaylistStrings { get; set; }
    private static StringList? PlaylistSongStrings { get; set; }

    // internal class ImageStringRow : ColumnView
    // {
    //     Image? Icon { get; set; }
    //     string Label { get; set; }

    //     ColumnViewColumn IconColumn { get; set; }
    //     ColumnViewColumn LabelColumn { get; set; }
    //     internal enum IconType
    //     {
    //         Playlist,
    //         Song
    //     }
    //     internal ImageStringRow(string label, IconType iconType)
    //     {
    //         New(Model);

    //         var factory = SignalListItemFactory.New();
    //         IconColumn = ColumnViewColumn.New("", factory);
    //         AppendColumn(IconColumn);
    //     }
    // }

    internal PlaylistConfig()
    {
        SetOrientation(Orientation.Vertical);
        Spacing = 1;
        Halign = Align.Center;
        PlaylistStrings = StringList.New(null);
        PlaylistSongStrings = StringList.New(null);

        // var playlistIcon = Image.New();
        // playlistIcon.SetFromIconName("vgms-playlist-symbolic");
        // playlistIcon.SetPixelSize(16);
        // var songIcon = Image.New();
        // songIcon.SetFromIconName("vgms-song-symbolic");
        // songIcon.SetPixelSize(16);

        ButtonPrevPlistSong = new Button() { Sensitive = false, TooltipText = Strings.PlayerPreviousSong, IconName = "media-skip-backward-symbolic" };
        ButtonNextPlistSong = new Button() { Sensitive = false, TooltipText = Strings.PlayerNextSong, IconName = "media-skip-forward-symbolic" };

        PlaylistDropDown = new DropDown();
        PlaylistDropDown.WidthRequest = 300;
        PlaylistDropDown.Sensitive = false;
        PlaylistDropDown.SetModel(PlaylistStrings);
        PlaylistSongDropDown = new DropDown();
        PlaylistSongDropDown.WidthRequest = 300;
        PlaylistSongDropDown.Sensitive = false;
        PlaylistSongDropDown.SetModel(PlaylistSongStrings);

        PlaylistBox = New(Orientation.Horizontal, 1);
        PlaylistBox.Halign = Align.Center;
        PlaylistSongBox = New(Orientation.Horizontal, 4);
        PlaylistSongBox.Halign = Align.Center;

        // PlaylistBox.Append(playlistIcon);
        PlaylistBox.Append(PlaylistDropDown);
        PlaylistSongBoxDropDown = New(Orientation.Horizontal, 1);
        PlaylistSongBoxDropDown.Halign = Align.Center;
        // PlaylistSongBoxDropDown.Append(songIcon);
        PlaylistSongBoxDropDown.Append(PlaylistSongDropDown);
        PlaylistSongBox.MarginStart = 40;
        PlaylistSongBox.MarginEnd = 40;
        PlaylistSongBox.Append(ButtonPrevPlistSong);
        PlaylistSongBox.Append(PlaylistSongBoxDropDown);
        PlaylistSongBox.Append(ButtonNextPlistSong);

        Append(PlaylistBox);
        Append(PlaylistSongBox);
        // SetModel(StringList);
    }

    internal void PlaylistStringSelect()
    {
        if (PlaylistSongStrings!.GetNItems() is not 0)
        {
            var numItems = PlaylistSongStrings.GetNItems();
            PlaylistSongStrings.Splice(0, numItems, null);
        }
        Songs = Playlists![(int)PlaylistDropDown.Selected].Songs;
        for (int i = 0; i < Songs.Count; i++)
        {
            PlaylistSongStrings.Append(Songs[i].Name);
        }
        SelectedSong = 0;
        SelectedPlaylist = PlaylistDropDown.Selected;
    }

    internal uint GetPlaylistSongIndex(int index)
    {
        var numItems = PlaylistSongStrings!.GetNItems();
        var newIndex = PlaylistDropDown.Selected;
        for (int i = 0; i < numItems; i++)
        {
            if (Songs![i].Index.Equals(index))
                newIndex = (uint)i;
        }
        return newIndex;
    }

    internal int GetSongIndex(uint index)
    {
        var strObj = (StringObject)PlaylistSongDropDown.SelectedItem!;
        var selectedItemName = strObj.String;
        var newIndex = (int)index;
        foreach (var song in Songs!)
        {
            if (song.Name.Equals(selectedItemName))
            {
                newIndex = song.Index;
            }
        }
        return newIndex;
    }

    internal int GetNumSongs()
    {
        return (int)PlaylistSongStrings!.NItems;
    }

    internal void AddEntries(List<Config.Playlist> playlists)
    {
        Playlists = playlists;
        if (PlaylistStrings!.GetNItems() is not 0)
        {
            var numPlistItems = PlaylistStrings!.GetNItems();
            PlaylistStrings.Splice(0, numPlistItems, null);
        }
        foreach (Config.Playlist plist in Playlists)
        {
            PlaylistStrings.Append(plist.Name);
        }
    }
}