using System;
using System.Collections.Generic;
using Gtk;
using Kermalis.VGMusicStudio.Core;
using Kermalis.VGMusicStudio.Core.Properties;

namespace Kermalis.VGMusicStudio.GTK4;

internal class PlaylistSelector : Box
{
    public DropDown? PlaylistDropDown { get; set; }
    public DropDown? PlaylistSongDropDown { get; set; }
    public GestureClick? PlaylistClick { get; set; }
    public GestureClick? PlaylistSongClick { get; set; }
    public ToggleButton? ButtonPlayPlist;
    public Button? ButtonPlistStyle;
    public Button? ButtonPrevPlistSong, ButtonNextPlistSong;
    public uint SelectedPlaylistIndex, SelectedSongIndex = 0;
    public static int PrevSelectedPlaylistIndex, PrevSelectedSongIndex = -1;
    private readonly SignalListItemFactory? PlaylistFactory;
    private readonly SignalListItemFactory? SongFactory;
    private readonly static Gio.ListStore PlaylistModel = Gio.ListStore.New(GetGType());
    private readonly static Gio.ListStore PlaylistSongModel = Gio.ListStore.New(GetGType());
    private List<Config.Playlist>? Playlists { get; set; }
    internal List<Config.Song>? Songs { get; set; }
    private readonly Box? PlaylistBox, PlaylistSongBoxDropDown;
    internal readonly Box? PlaylistSongBox;
    private int Index { get; set; }
    private string? Title { get; set; }
    private string? ImageName { get; set; }
    private PlaylistSelector? SelectedPlaylist { get; set; }
    private PlaylistSelector? SelectedSong { get; set; }
    private static Contents[]? ContentsPlaylist { get; set; }
    private static Contents[]? ContentsSong { get; set; }
    internal bool PlaylistIsSelected = false;
    internal bool SongIsSelected = false;

    protected PlaylistSelector(string title, string image)
        : base()
    {
        Title = title;
        ImageName = image;
    }
    internal PlaylistSelector()
    {
        SetOrientation(Orientation.Vertical);
        Spacing = 1;
        Halign = Align.Center;

        IconTheme.GetForDisplay(Gdk.Display.GetDefault()!).AddResourcePath("/org/Kermalis/VGMusicStudio/GTK4/icons/scalable/actions");

        ButtonPlayPlist = new ToggleButton() { Sensitive = false, TooltipText = "Play Playlist", IconName = "vgms-play-playlist-symbolic" };
        ButtonPlistStyle = new Button() { Sensitive = false, TooltipText = "Play Style" };

        ButtonPrevPlistSong = new Button() { Sensitive = false, TooltipText = Strings.PlayerPreviousSong, IconName = "media-skip-backward-symbolic" };
        ButtonNextPlistSong = new Button() { Sensitive = false, TooltipText = Strings.PlayerNextSong, IconName = "media-skip-forward-symbolic" };

        PlaylistDropDown = new DropDown
        {
            WidthRequest = 300,
            Sensitive = false
        };
        PlaylistDropDown.SetModel(PlaylistModel);
        PlaylistSongDropDown = new DropDown
        {
            WidthRequest = 300,
            Sensitive = false
        };
        PlaylistSongDropDown.SetModel(PlaylistSongModel);

        PlaylistFactory = SignalListItemFactory.New();
        PlaylistFactory.OnSetup += PlaylistFactory_SetupPlaylists;
        PlaylistFactory.OnBind += PlaylistFactory_BindPlaylists;

        PlaylistDropDown!.SetFactory(PlaylistFactory);

        SongFactory = SignalListItemFactory.New();
        SongFactory.OnSetup += SongFactory_SetupSongs;
        SongFactory.OnBind += SongFactory_BindSongs;

        PlaylistSongDropDown!.SetFactory(SongFactory);

        PlaylistBox = New(Orientation.Horizontal, 4);
        PlaylistBox.Halign = Align.Center;
        PlaylistSongBox = New(Orientation.Horizontal, 4);
        PlaylistSongBox.Halign = Align.Center;

        PlaylistBox.Append(ButtonPlayPlist);
        PlaylistBox.Append(PlaylistDropDown);
        PlaylistBox.Append(ButtonPlistStyle);
        PlaylistSongBoxDropDown = New(Orientation.Horizontal, 1);
        PlaylistSongBoxDropDown.Halign = Align.Center;
        PlaylistSongBoxDropDown.Append(PlaylistSongDropDown);
        PlaylistSongBox.MarginStart = 40;
        PlaylistSongBox.MarginEnd = 40;
        PlaylistSongBox.Append(ButtonPrevPlistSong);
        PlaylistSongBox.Append(PlaylistSongBoxDropDown);
        PlaylistSongBox.Append(ButtonNextPlistSong);

        Append(PlaylistBox);
        Append(PlaylistSongBox);
    }

    protected struct Contents()
    {
        internal Label Title = Label.New("");
        internal Image Image = Image.New();
        internal Image Checkmark = Image.NewFromIconName("object-select-symbolic");
    }

    private void PlaylistFactory_SetupPlaylists(SignalListItemFactory sender, SignalListItemFactory.SetupSignalArgs args)
    {
        if (args.Object is not ListItem item)
        {
            return;
        }
        if (Index >= ContentsPlaylist!.Length - 1)
        {
            Index = 0;
            ContentsPlaylist[^1] = new();
            ContentsPlaylist[^1].Title.SetXalign(0);
            ContentsPlaylist[^1].Title.SetMaxWidthChars(20);
            ContentsPlaylist[^1].Title.SetEllipsize(Pango.EllipsizeMode.End);

            Box box = New(Orientation.Horizontal, 10);

            box.Append(ContentsPlaylist[^1].Image);
            box.Append(ContentsPlaylist[^1].Title);
            box.Append(ContentsPlaylist[^1].Checkmark);

            item.SetChild(box);
        }
        else
        {
            ContentsPlaylist[Index] = new();
            ContentsPlaylist[Index].Title.SetXalign(0);
            ContentsPlaylist[Index].Title.SetMaxWidthChars(50);
            ContentsPlaylist[Index].Title.SetEllipsize(Pango.EllipsizeMode.End);

            Box box = New(Orientation.Horizontal, 10);

            box.Append(ContentsPlaylist[Index].Image);
            box.Append(ContentsPlaylist[Index].Title);
            box.Append(ContentsPlaylist[Index].Checkmark);

            item.SetChild(box);
            Index++;
        }
    }

    private void SongFactory_SetupSongs(SignalListItemFactory sender, SignalListItemFactory.SetupSignalArgs args)
    {
        if (args.Object is not ListItem item)
        {
            return;
        }
        if (Index >= ContentsSong!.Length - 1)
        {
            Index = 0;
            ContentsSong[^1] = new();
            ContentsSong[^1].Title.SetXalign(0);
            ContentsSong[^1].Title.SetMaxWidthChars(20);
            ContentsSong[^1].Title.SetEllipsize(Pango.EllipsizeMode.End);

            Box box = New(Orientation.Horizontal, 10);

            box.Append(ContentsSong[^1].Image);
            box.Append(ContentsSong[^1].Title);
            box.Append(ContentsSong[^1].Checkmark);

            item.SetChild(box);
        }
        else
        {
            ContentsSong[Index] = new();
            ContentsSong[Index].Title.SetXalign(0);
            ContentsSong[Index].Title.SetMaxWidthChars(50);
            ContentsSong[Index].Title.SetEllipsize(Pango.EllipsizeMode.End);

            Box box = New(Orientation.Horizontal, 10);

            box.Append(ContentsSong[Index].Image);
            box.Append(ContentsSong[Index].Title);
            box.Append(ContentsSong[Index].Checkmark);

            item.SetChild(box);
            Index++;
        }
    }

    private void PlaylistFactory_BindPlaylists(SignalListItemFactory sender, SignalListItemFactory.BindSignalArgs args)
    {
        if (args.Object is not ListItem item)
        {
            return;
        }
        if (item.Item is not PlaylistSelector holder)
        {
            return;
        }
        if (item.Child is not Box box)
        {
            return;
        }
        BindItems(ContentsPlaylist![(int)item.Position], holder, box);
    }

    private void SongFactory_BindSongs(SignalListItemFactory sender, SignalListItemFactory.BindSignalArgs args)
    {
        if (args.Object is not ListItem item)
        {
            return;
        }
        if (item.Item is not PlaylistSelector holder)
        {
            return;
        }
        if (item.Child is not Box box)
        {
            return;
        }
        BindItems(ContentsSong![(int)item.Position], holder, box);
    }

    private void BindItems(Contents contents, PlaylistSelector holder, Box box)
    {
        contents.Title = (Label)box.GetFirstChild()!.GetNextSibling()!;
        contents.Title.SetLabel(holder.Title!);
        contents.Image = (Image)box.GetFirstChild()!;
        contents.Image.SetFromIconName(holder.ImageName);
        contents.Checkmark = (Image)box.GetLastChild()!;
        Popover popup = (Popover)box.GetAncestor(Popover.GetGType())!;
        if (popup is not null)
        {
            if (popup.IsAncestor(PlaylistDropDown!))
            {
                DropDown dropdown = (DropDown)popup.GetAncestor(DropDown.GetGType())!;
                PlaylistSelector selectedItem = (PlaylistSelector)dropdown.SelectedItem!;
                if (selectedItem.Title == contents.Title.Label_)
                {
                    SelectedPlaylist = selectedItem;
                    contents.Checkmark.SetVisible(true);
                    dropdown.OnNotify += Playlist_Notify;
                }
                else
                {
                    contents.Checkmark.SetVisible(false);
                }
            }
            else if (popup.IsAncestor(PlaylistSongDropDown!))
            {
                DropDown dropdown = (DropDown)popup.GetAncestor(DropDown.GetGType())!;
                PlaylistSelector selectedItem = (PlaylistSelector)dropdown.SelectedItem!;
                if (selectedItem.Title == contents.Title.Label_)
                {
                    SelectedSong = selectedItem;
                    contents.Checkmark.SetVisible(true);
                    dropdown.OnNotify += Song_Notify;
                }
                else
                {
                    contents.Checkmark.SetVisible(false);
                }
            }
        }
        else
        {
            contents.Checkmark.SetVisible(false);
        }
    }

    private void Playlist_Notify(GObject.Object sender, NotifySignalArgs args)
    {
        var name = args.Pspec.GetName();
        if (args.Pspec.GetName() == "selected")
        {
            var dropdown = (DropDown)sender;

            PlaylistIsSelected = true;

            if (ContentsPlaylist is not null)
            {
                if (dropdown.Selected >= ContentsPlaylist.Length)
                {
                    return;
                }
            }

            if (ContentsPlaylist![PrevSelectedPlaylistIndex].Checkmark is null)
            {
                ContentsPlaylist![PrevSelectedPlaylistIndex] = new();
            }
            if (ContentsPlaylist[dropdown.Selected].Checkmark is null)
            {
                ContentsPlaylist[dropdown.Selected] = new();
            }

            ContentsPlaylist![PrevSelectedPlaylistIndex].Checkmark.SetVisible(false);
            ContentsPlaylist[dropdown.Selected].Checkmark.SetVisible(true);

            PrevSelectedPlaylistIndex = (int)dropdown.Selected;
        }
    }

    private void Song_Notify(GObject.Object sender, NotifySignalArgs args)
    {
        var name = args.Pspec.GetName();
        if (args.Pspec.GetName() == "selected")
        {
            var dropdown = (DropDown)sender;

            SongIsSelected = true;

            if (ContentsSong is not null)
            {
                if (dropdown.Selected >= ContentsSong.Length)
                {
                    return;
                }
            }

            if (ContentsSong![PrevSelectedSongIndex].Checkmark is null)
            {
                ContentsSong![PrevSelectedSongIndex] = new();
            }
            if (ContentsSong[dropdown.Selected].Checkmark is null)
            {
                ContentsSong[dropdown.Selected] = new();
            }

            ContentsSong![PrevSelectedSongIndex].Checkmark.SetVisible(false);
            ContentsSong[dropdown.Selected].Checkmark.SetVisible(true);

            PrevSelectedSongIndex = (int)dropdown.Selected;
        }
    }

    internal void PlaylistStringSelect()
    {
        AddSongEntries();
    }

    internal Config.Playlist GetPlaylist()
    {
        Config.Playlist playlist = null!;
        foreach (Config.Playlist plist in Playlists!)
        {
            PlaylistSelector selectedItem = (PlaylistSelector)PlaylistDropDown!.GetSelectedItem()!;
            var selectedItemName = selectedItem.Title;
            if (plist.Name == selectedItemName)
            {
                playlist = plist;
            }
        }
        return playlist;
    }

    internal string GetTitle()
    {
        return Title!;
    }

    internal uint GetPlaylistSongIndex(int index)
    {
        var numItems = PlaylistSongModel!.GetNItems();
        var newIndex = PlaylistDropDown!.Selected;
        for (int i = 0; i < numItems; i++)
        {
            if (Songs![i].Index.Equals(index))
                newIndex = (uint)i;
        }
        return newIndex;
    }

    internal int GetSongIndex(uint index)
    {
        var strObj = (PlaylistSelector)PlaylistSongDropDown!.SelectedItem!;
        var selectedItemName = strObj.Title;
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

    internal static int GetNumSongs()
    {
        return (int)PlaylistSongModel!.NItems;
    }

    internal void AddPlaylistEntries(List<Config.Playlist> playlists)
    {
        Playlists = playlists;
        if (PlaylistModel!.GetNItems() is not 0)
        {
            PlaylistModel.RemoveAll();
        }
        ContentsPlaylist = new Contents[Playlists.Count + 1];
        int i = 0;
        foreach (Config.Playlist plist in Playlists)
        {
            var data = new PlaylistSelector(plist.Name, "vgms-playlist-symbolic")
            {
                Index = i++
            };
            PlaylistModel.Append(data);
        }
        PrevSelectedPlaylistIndex = (int)(PlaylistDropDown!.Selected = SelectedPlaylistIndex = PlaylistDropDown.Selected = PlaylistModel.NItems - 1); // So that "All Songs" main playlist is selected
        Index = 0;
    }
    internal void AddSongEntries()
    {
        if (PlaylistSongModel!.GetNItems() is not 0)
        {
            PlaylistSongModel.RemoveAll();
        }
        Songs = Playlists![(int)PlaylistDropDown!.Selected].Songs;
        ContentsSong = new Contents[Songs.Count + 1];
        for (int i = 0; i < Songs.Count; i++)
        {
            var data = new PlaylistSelector(Songs[i].Name, "vgms-song-symbolic")
            {
                Index = i
            };
            PlaylistSongModel.Append(data);
        }
        PrevSelectedSongIndex = (int)(SelectedSongIndex = PlaylistSongDropDown!.Selected);
        Index = 0;
    }
}