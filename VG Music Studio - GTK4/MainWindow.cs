using Kermalis.VGMusicStudio.Core;
using Kermalis.VGMusicStudio.Core.GBA.AlphaDream;
using Kermalis.VGMusicStudio.Core.GBA.MP2K;
using Kermalis.VGMusicStudio.Core.NDS.DSE;
using Kermalis.VGMusicStudio.Core.NDS.SDAT;
using Kermalis.VGMusicStudio.Core.Properties;
using Kermalis.VGMusicStudio.Core.Util;
using Kermalis.VGMusicStudio.GTK4.Util;
using GObject;
using Adw;
using Gtk;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Timers;
using System.Runtime.InteropServices;
using System.Diagnostics;

using Application = Adw.Application;
using Window = Adw.Window;

namespace Kermalis.VGMusicStudio.GTK4;

internal sealed class MainWindow : Window
{
    private int _duration = 0;
    private int _position = 0;

    private PlayingPlaylist? _playlist;
    private int _curSong = -1;

    private bool _songEnded = false;
    private bool _stopUI = false;
    private bool _playlistChanged = false;
    private bool _autoplay = false;

    public static MainWindow? Instance { get; private set; }

    #region Widgets

    // Buttons
    private Button _buttonPlay, _buttonStop, _buttonRecord;
    private ToggleButton _buttonPause;

    // Spin Button for the numbered tracks
    private readonly SpinButton _sequenceNumberSpinButton;

    // Timer
    private readonly GLib.Timer _timer;

    // Popover Menu Bar
    private readonly PopoverMenuBar _popoverMenuBar;

    // LibAdwaita Header Bar
    private readonly Adw.HeaderBar _headerBar;

    // LibAdwaita Application
    private readonly Adw.Application _app;

    // Menu Model
    //private readonly Gio.MenuModel _mainMenu;

    // Menus
    private readonly Gio.Menu _mainMenu, _fileMenu, _dataMenu, _playlistMenu;

    // Menu Labels
    private readonly Label _fileLabel, _dataLabel, _playlistLabel;

    // Menu Items
    private readonly Gio.MenuItem _fileItem, _openDSEItem, _openAlphaDreamItem, _openMP2KItem, _openSDATItem,
        _dataItem, _exportDLSItem, _exportSF2Item, _exportMIDIItem, _exportWAVItem, _playlistItem, _endPlaylistItem;

    // Menu Actions
    private Gio.SimpleAction _openDSEAction, _openAlphaDreamAction, _openMP2KAction, _openSDATAction,
        _exportDLSAction, _exportSF2Action, _exportMIDIAction, _exportWAVAction, _endPlaylistAction;

    // Boxes
    private Box _mainBox, _configButtonBox, _configPlayerButtonBox, _configSpinButtonBox, _configBarBox;

    // One Scale controling volume and one Scale for the sequenced track
    private Scale _volumeBar, _positionBar;

    // Mouse Click and Drag Gestures
    private GestureClick _positionGestureClick;
    private GestureDrag _positionGestureDrag;

    // Adjustments are for indicating the numbers and the position of the scale
    private readonly Adjustment _sequenceNumberAdjustment;

    // Playlist
    private PlaylistConfig _configPlaylistBox;

    // Sound Sequence List
    private SoundSequenceList _soundSequenceList;

    // Error Handle
    private GLib.Internal.ErrorOwnedHandle ErrorHandle = new GLib.Internal.ErrorOwnedHandle(IntPtr.Zero);

    // Signal
    //private Signal<ListItemFactory> _signal;

    // Callback
    private Gio.Internal.AsyncReadyCallback _saveCallback { get; set; }
    private Gio.Internal.AsyncReadyCallback _openCallback { get; set; }
    private Gio.Internal.AsyncReadyCallback _selectFolderCallback { get; set; }
    private Gio.Internal.AsyncReadyCallback _exceptionCallback { get; set; }

    #endregion

    public MainWindow(Application app)
    {
        // Main Window
        SetDefaultSize(700, 500); // Sets the default size of the Window
        Title = GetProgramName(); // Sets the title to the name of the program, which is "VG Music Studio"
        _app = app;

        // LibAdwaita Header Bar
        _headerBar = Adw.HeaderBar.New();
        _headerBar.SetShowEndTitleButtons(true);

        // Main Menu
        _mainMenu = Gio.Menu.New();

        // Popover Menu Bar
        _popoverMenuBar = PopoverMenuBar.NewFromModel(_mainMenu); // This will ensure that the menu model is used inside of the PopoverMenuBar widget
        _popoverMenuBar.MenuModel = _mainMenu;
        _popoverMenuBar.MnemonicActivate(true);

        // File Menu
        _fileMenu = Gio.Menu.New();

        _fileLabel = Label.NewWithMnemonic(Strings.MenuFile);
        _fileLabel.GetMnemonicKeyval();
        _fileLabel.SetUseUnderline(true);
        _fileItem = Gio.MenuItem.New(_fileLabel.GetLabel(), null);
        _fileLabel.SetMnemonicWidget(_popoverMenuBar);
        _popoverMenuBar.AddMnemonicLabel(_fileLabel);
        _fileItem.SetSubmenu(_fileMenu);

        _openDSEItem = Gio.MenuItem.New(Strings.MenuOpenDSE, "app.openDSE");
        _openDSEAction = Gio.SimpleAction.New("openDSE", null);
        _openDSEItem.SetActionAndTargetValue("app.openDSE", null);
        _app.AddAction(_openDSEAction);
        _openDSEAction.OnActivate += OpenDSE;
        _fileMenu.AppendItem(_openDSEItem);
        _openDSEItem.Unref();

        _openSDATItem = Gio.MenuItem.New(Strings.MenuOpenSDAT, "app.openSDAT");
        _openSDATAction = Gio.SimpleAction.New("openSDAT", null);
        _openSDATItem.SetActionAndTargetValue("app.openSDAT", null);
        _app.AddAction(_openSDATAction);
        _openSDATAction.OnActivate += OpenSDAT;
        _fileMenu.AppendItem(_openSDATItem);
        _openSDATItem.Unref();

        _openAlphaDreamItem = Gio.MenuItem.New(Strings.MenuOpenAlphaDream, "app.openAlphaDream");
        _openAlphaDreamAction = Gio.SimpleAction.New("openAlphaDream", null);
        _app.AddAction(_openAlphaDreamAction);
        _openAlphaDreamAction.OnActivate += OpenAlphaDream;
        _fileMenu.AppendItem(_openAlphaDreamItem);
        _openAlphaDreamItem.Unref();

        _openMP2KItem = Gio.MenuItem.New(Strings.MenuOpenMP2K, "app.openMP2K");
        _openMP2KAction = Gio.SimpleAction.New("openMP2K", null);
        _app.AddAction(_openMP2KAction);
        _openMP2KAction.OnActivate += OpenMP2K;
        _fileMenu.AppendItem(_openMP2KItem);
        _openMP2KItem.Unref();

        _mainMenu.AppendItem(_fileItem); // Note: It must append the menu item variable (_fileItem), not the file menu variable (_fileMenu) itself
        _fileItem.Unref();

        // Data Menu
        _dataMenu = Gio.Menu.New();

        _dataLabel = Label.NewWithMnemonic(Strings.MenuData);
        _dataLabel.GetMnemonicKeyval();
        _dataLabel.SetUseUnderline(true);
        _dataItem = Gio.MenuItem.New(_dataLabel.GetLabel(), null);
        _popoverMenuBar.AddMnemonicLabel(_dataLabel);
        _dataItem.SetSubmenu(_dataMenu);

        _exportDLSItem = Gio.MenuItem.New(Strings.MenuSaveDLS, "app.exportDLS");
        _exportDLSAction = Gio.SimpleAction.New("exportDLS", null);
        _app.AddAction(_exportDLSAction);
        _exportDLSAction.Enabled = false;
        _exportDLSAction.OnActivate += ExportDLS;
        _dataMenu.AppendItem(_exportDLSItem);
        _exportDLSItem.Unref();

        _exportSF2Item = Gio.MenuItem.New(Strings.MenuSaveSF2, "app.exportSF2");
        _exportSF2Action = Gio.SimpleAction.New("exportSF2", null);
        _app.AddAction(_exportSF2Action);
        _exportSF2Action.Enabled = false;
        _exportSF2Action.OnActivate += ExportSF2;
        _dataMenu.AppendItem(_exportSF2Item);
        _exportSF2Item.Unref();

        _exportMIDIItem = Gio.MenuItem.New(Strings.MenuSaveMIDI, "app.exportMIDI");
        _exportMIDIAction = Gio.SimpleAction.New("exportMIDI", null);
        _app.AddAction(_exportMIDIAction);
        _exportMIDIAction.Enabled = false;
        _exportMIDIAction.OnActivate += ExportMIDI;
        _dataMenu.AppendItem(_exportMIDIItem);
        _exportMIDIItem.Unref();

        _exportWAVItem = Gio.MenuItem.New(Strings.MenuSaveWAV, "app.exportWAV");
        _exportWAVAction = Gio.SimpleAction.New("exportWAV", null);
        _app.AddAction(_exportWAVAction);
        _exportWAVAction.Enabled = false;
        _exportWAVAction.OnActivate += ExportWAV;
        _dataMenu.AppendItem(_exportWAVItem);
        _exportWAVItem.Unref();

        _mainMenu.AppendItem(_dataItem);
        _dataItem.Unref();

        // Playlist Menu
        _playlistMenu = Gio.Menu.New();

        _playlistLabel = Label.NewWithMnemonic(Strings.MenuPlaylist);
        _playlistLabel.GetMnemonicKeyval();
        _playlistLabel.SetUseUnderline(true);
        _playlistItem = Gio.MenuItem.New(_playlistLabel.GetLabel(), null);
        _popoverMenuBar.AddMnemonicLabel(_playlistLabel);
        _playlistItem.SetSubmenu(_playlistMenu);

        _endPlaylistItem = Gio.MenuItem.New(Strings.MenuEndPlaylist, "app.endPlaylist");
        _endPlaylistAction = Gio.SimpleAction.New("endPlaylist", null);
        _app.AddAction(_endPlaylistAction);
        _endPlaylistAction.Enabled = false;
        _endPlaylistAction.OnActivate += EndCurrentPlaylist;
        _playlistMenu.AppendItem(_endPlaylistItem);
        _endPlaylistItem.Unref();

        _mainMenu.AppendItem(_playlistItem);
        _playlistItem.Unref();

        // Buttons
        _buttonPlay = new Button() { Sensitive = false, TooltipText = Strings.PlayerPlay, IconName = "media-playback-start-symbolic" };
        _buttonPlay.OnClicked += (o, e) => Play();
        _buttonPause = new ToggleButton() { Sensitive = false, TooltipText = Strings.PlayerPause, IconName = "media-playback-pause-symbolic" };
        _buttonPause.OnClicked += (o, e) => Pause();
        _buttonStop = new Button() { Sensitive = false, TooltipText = Strings.PlayerStop, IconName = "media-playback-stop-symbolic" };
        _buttonStop.OnClicked += (o, e) => Stop();

        _buttonRecord = new Button() { Sensitive = false, TooltipText = Strings.PlayerRecord, IconName = "media-record-symbolic" };
        _buttonRecord.OnClicked += ExportWAV;

        // Spin Button
        _sequenceNumberAdjustment = Adjustment.New(0, 0, -1, 1, 1, 1);
        _sequenceNumberSpinButton = SpinButton.New(_sequenceNumberAdjustment, 1, 0);
        _sequenceNumberSpinButton.Sensitive = false;
        _sequenceNumberSpinButton.Value = 0;
        //_sequenceNumberSpinButton.Visible = false;
        _sequenceNumberSpinButton.OnValueChanged += SequenceNumberSpinButton_ValueChanged;

        // // Timer
        _timer = GLib.Timer.New();

        // Volume Bar
        _volumeBar = Scale.New(Orientation.Horizontal, Gtk.Adjustment.New(0, 0, 100, 1, 10, 0));
        _volumeBar.OnValueChanged += VolumeBar_ValueChanged;
        _volumeBar.Sensitive = false;
        _volumeBar.ShowFillLevel = true;
        _volumeBar.DrawValue = false;
        _volumeBar.WidthRequest = 250;

        // Position Bar
        _positionBar = Scale.New(Orientation.Horizontal, Gtk.Adjustment.New(0, 0, 100, 1, 10, 0)); // The Upper value property must contain a value of 1 or higher for the widget to show upon startup
        _positionGestureClick = GestureClick.New();
        _positionGestureDrag = GestureDrag.New();
        _positionBar.AddController(_positionGestureClick);
        _positionBar.AddController(_positionGestureDrag);
        _positionBar.Sensitive = false;
        _positionBar.Focusable = true;
        _positionBar.ShowFillLevel = true;
        _positionBar.DrawValue = false;
        _positionBar.WidthRequest = 250;
        _positionBar.RestrictToFillLevel = false;
        _positionBar.OnChangeValue += PositionBar_ChangeValue;
        _positionBar.OnMoveSlider += PositionBar_MoveSlider;
        _positionBar.OnValueChanged += PositionBar_ValueChanged;
        _positionGestureClick.OnStopped += PositionBar_MouseButtonRelease;
        _positionGestureClick.OnCancel += PositionBar_MouseButtonRelease;
        _positionGestureClick.OnPressed += PositionBar_MouseButtonPress;
        _positionGestureClick.OnReleased += PositionBar_MouseButtonRelease;
        _positionGestureClick.OnUnpairedRelease += PositionBar_MouseButtonRelease;
        _positionGestureClick.OnBegin += PositionBar_MouseButtonOnBegin;
        _positionGestureClick.OnEnd += PositionBar_MouseButtonOnEnd;
        // _positionGestureDrag.OnDragBegin += PositionBar_MouseButtonOnBegin;
        // _positionGestureDrag.OnDragEnd += PositionBar_MouseButtonOnEnd;

        // Playlist
        _configPlaylistBox = new PlaylistConfig();
        _configPlaylistBox.ButtonPrevPlistSong.OnClicked += (o, e) => PlayPreviousSong();
        _configPlaylistBox.ButtonNextPlistSong.OnClicked += PlayNextSong;

        // Sound Sequence List
        _soundSequenceList = new();

        // Main display
        _mainBox = Box.New(Orientation.Vertical, 4);

        _configButtonBox = Box.New(Orientation.Horizontal, 2);
        _configButtonBox.Halign = Align.Center;
        _configPlayerButtonBox = Box.New(Orientation.Horizontal, 3);
        _configPlayerButtonBox.Halign = Align.Center;
        _configSpinButtonBox = Box.New(Orientation.Horizontal, 1);
        _configSpinButtonBox.Halign = Align.Center;
        _configSpinButtonBox.WidthRequest = 100;
        _configBarBox = Box.New(Orientation.Horizontal, 2);
        _configBarBox.Halign = Align.Center;

        _configPlayerButtonBox.MarginStart = 40;
        _configPlayerButtonBox.MarginEnd = 40;
        _configButtonBox.Append(_configPlayerButtonBox);
        _configSpinButtonBox.MarginStart = 100;
        _configSpinButtonBox.MarginEnd = 100;
        _configButtonBox.Append(_configSpinButtonBox);

        _configPlayerButtonBox.Append(_buttonPlay);
        _configPlayerButtonBox.Append(_buttonPause);
        _configPlayerButtonBox.Append(_buttonStop);
        _configPlayerButtonBox.Append(_buttonRecord);

        if (_configSpinButtonBox.GetFirstChild() == null)
        {
            _sequenceNumberSpinButton.Hide();
            _configSpinButtonBox.Append(_sequenceNumberSpinButton);
        }

        _volumeBar.MarginStart = 20;
        _volumeBar.MarginEnd = 20;
        _configBarBox.Append(_volumeBar);
        _positionBar.MarginStart = 20;
        _positionBar.MarginEnd = 20;
        _configBarBox.Append(_positionBar);

        _mainBox.Append(_headerBar);
        _mainBox.Append(_popoverMenuBar);
        _mainBox.Append(_configButtonBox);
        _mainBox.Append(_configBarBox);
        _mainBox.Append(_configPlaylistBox);
        _mainBox.Append(_soundSequenceList);

        SetContent(_mainBox);

        Instance = this;

        // Ensures the entire application gets closed when the main window is closed
        OnCloseRequest += (sender, args) =>
        {
            DisposeEngine(); // Engine must be disposed first, otherwise the window will softlock when closing
            _app.Quit();
            return true;
        };
    }

    // When the value is changed on the volume scale
    private void VolumeBar_ValueChanged(object sender, EventArgs e)
    {
        Engine.Instance!.Mixer.SetVolume((float)(_volumeBar.Adjustment!.Value / _volumeBar.Adjustment.Upper));
    }

    // Sets the volume scale to the specified position
    public void SetVolumeBar(float volume)
    {
        _volumeBar.Adjustment!.Value = (int)(volume * _volumeBar.Adjustment.Upper);
        _volumeBar.OnValueChanged += VolumeBar_ValueChanged;
    }

    private bool _positionBarFree = true;
    private bool _positionBarDebug = false;
    private void PositionBar_MouseButtonPress(object sender, EventArgs args)
    {
        if (_positionBarDebug)
        {
            Debug.WriteLine(sender.ToString() + " | " + args.ToString() + " | _positionBarFree: " + _positionBarFree.ToString());
        }
        _positionBarFree = false;
    }
    private void PositionBar_MouseButtonRelease(object sender, EventArgs args)
    {
        // if (args == EventArgs.Empty)
        // {
        //     return;
        // }

        if (_positionBarDebug)
        {
            Debug.WriteLine(sender.ToString() + " | " + args.ToString() + " | _positionBarFree: " + _positionBarFree.ToString());
        }

        if (!_positionBarFree)
        {
            Engine.Instance!.Player.SetSongPosition((long)_positionBar.Adjustment!.Value); // Sets the value based on the position when mouse button is released
            _positionBarFree = true; // Sets _positionBarFree to true when mouse button is released
            if (Engine.Instance!.Player.State is PlayerState.Playing)
            {
                LetUIKnowPlayerIsPlaying(); // This method will run the void that tells the UI that the player is playing a track
            }
            else
            {
                return;
            }
        }
        else
        {
            return;
        }

    }
    private void PositionBar_MouseButtonOnBegin(object sender, EventArgs args)
    {
        if (_positionBarDebug)
        {
            Debug.WriteLine(sender.ToString() + " | " + args.ToString() + " | _positionBarFree: " + _positionBarFree.ToString());
        }
        _positionBarFree = false;
    }
    private void PositionBar_MouseButtonOnEnd(object sender, EventArgs args)
    {
        if (_positionBarDebug)
        {
            Debug.WriteLine(sender.ToString() + " | " + args.ToString() + " | _positionBarFree: " + _positionBarFree.ToString());
        }
        _positionBarFree = true;
    }
    private bool PositionBar_ChangeValue(object sender, EventArgs args)
    {
        if (_positionBarDebug)
        {
            Debug.WriteLine(sender.ToString() + " | " + args.ToString() + " | _positionBarFree: " + _positionBarFree.ToString());
        }
        return _positionBarFree = false;
    }
    private void PositionBar_MoveSlider(object sender, EventArgs args)
    {
        if (_positionBarDebug)
        {
            Debug.WriteLine(sender.ToString() + " | " + args.ToString() + " | _positionBarFree: " + _positionBarFree.ToString());
        }
        UpdatePositionIndicators(Engine.Instance!.Player.ElapsedTicks);
        _positionBarFree = false;
    }
    private void PositionBar_ValueChanged(object sender, EventArgs args)
    {
        if (Engine.Instance is not null)
            UpdatePositionIndicators(Engine.Instance!.Player.ElapsedTicks); // Sets the value based on the position when mouse button is released

    }

    private void SequenceNumberSpinButton_ValueChanged(object sender, EventArgs e)
    {
        //_sequencesGestureClick.OnBegin -= SequencesListView_SelectionGet;
        //_signal.Connect(_sequencesListFactory, SequencesListView_SelectionGet, false, null);

        int index = (int)_sequenceNumberAdjustment.Value;
        _soundSequenceList.SelectRow(index);
        Stop();
        Title = GetProgramName();
        //_sequencesListView.Margin = 0;
        //_songInfo.Reset();
        bool success;
        if (Engine.Instance == null)
        {
            return; // Prevents referencing a null Engine.Instance when the engine is being disposed, especially while main window is being closed
        }
        Player player = Engine.Instance!.Player;
        Config cfg = Engine.Instance.Config;
        try
        {
            player.LoadSong(index);
            success = Engine.Instance.Player.LoadedSong is not null; // TODO: Make sure loadedsong is null when there are no tracks (for each engine, only mp2k guarantees it rn)
        }
        catch (Exception ex)
        {
            FlexibleMessageBox.Show(ex, string.Format(Strings.ErrorLoadSong, Engine.Instance!.Config.GetSongName(index)));
            success = false;
        }

        //_trackViewer?.UpdateTracks();
        ILoadedSong? loadedSong = player.LoadedSong; // LoadedSong is still null when there are no tracks
        if (success)
        {
            List<Config.Song> songs = cfg.Playlists[^1].Songs; // Complete "All Songs" playlist is present in all configs at the last index value
            int songIndex = songs.FindIndex(s => s.Index == index);
            if (songIndex != -1)
            {
                SetSongToProgramTitle(songs, songIndex); // Done! It's now a func
                CheckPlaylistItem();
            }
            _positionBar.Adjustment!.Upper = loadedSong!.MaxTicks;
            _positionBar.SetRange(0, loadedSong.MaxTicks);
            //_songInfo.SetNumTracks(Engine.Instance.Player.LoadedSong.Events.Length);
            if (_autoplay)
            {
                Play();
            }
        }
        else
        {
            //_songInfo.SetNumTracks(0);
        }
        _positionBar.Sensitive = _exportWAVAction.Enabled = success;
        _exportMIDIAction.Enabled = success && MP2KEngine.MP2KInstance is not null;
        _exportDLSAction.Enabled = _exportSF2Action.Enabled = success && AlphaDreamEngine.AlphaDreamInstance is not null;

        if (!_playlistChanged)
            _autoplay = true;
        //_sequencesGestureClick.OnEnd += SequencesListView_SelectionGet;
        //_signal.Connect(_sequencesListFactory, SequencesListView_SelectionGet, true, null);
    }

    private static string GetProgramName() => ConfigUtils.PROGRAM_NAME;
    private void SetSongToProgramTitle(List<Config.Song> songs, int songIndex)
    {
        Title = $"{GetProgramName()} - {songs[songIndex].Name}";
    }

    internal static void ChangeIndex(int index)
    {
        Instance!._autoplay = false;  // Set to false, so anyone selecting from the sequences list doesn't have the song automatically play
        Instance!.SetAndLoadSong(index);  // This will set and load the song for all widgets
    }

    //private void SequencesListView_SelectionGet(object sender, EventArgs e)
    //{
    //	var item = _soundSequenceList.SelectedItem;
    //	if (item is Config.Song song)
    //	{
    //		SetAndLoadSong(song.Index);
    //	}
    //	else if (item is Config.Playlist playlist)
    //	{
    //		if (playlist.Songs.Count > 0
    //		&& FlexibleMessageBox.Show(string.Format(Strings.PlayPlaylistBody, Environment.NewLine + playlist), Strings.MenuPlaylist, ButtonsType.YesNo) == ResponseType.Yes)
    //		{
    //			ResetPlaylistStuff(false);
    //			_curPlaylist = playlist;
    //			Engine.Instance.Player.ShouldFadeOut = _playlistPlaying = true;
    //			Engine.Instance.Player.NumLoops = GlobalConfig.Instance.PlaylistSongLoops;
    //			_endPlaylistAction.Enabled = true;
    //			SetAndLoadNextPlaylistSong();
    //		}
    //	}
    //}

    private void OnPlaylistStringSelected(GObject.Object sender, NotifySignalArgs args)
    {
        if (_configPlaylistBox.PlaylistDropDown!.SelectedItem is not null)
        {
            _autoplay = false;  // Must be set to false first
            CheckPlaylistItem();  // Check the playlist item, to set the dropdown to it's first song in the playlist
            _configPlaylistBox.PlaylistStringSelect();  // Selects the playlist item
            _playlistChanged = true;  // We set this, so that the autoplay doesn't get set to true until after the song is loaded
            SetAndLoadSong(_configPlaylistBox.GetSongIndex(_configPlaylistBox.PlaylistDropDown.Selected));  // This will set and load the song
            _playlistChanged = false;  // Now we can set it back to false
            _autoplay = true;  // And set this to true, so that when a playlist song is selected, it'll autoplay
        }
    }

    private void OnPlaylistSongStringSelected(GObject.Object sender, NotifySignalArgs args)
    {
        if (_configPlaylistBox.PlaylistSongDropDown.SelectedItem is not null)
        {
            if (_configPlaylistBox.PlaylistDropDown.Selected != _configPlaylistBox.SelectedPlaylist)
                Stop();
            if (_configPlaylistBox.PlaylistSongDropDown.Selected != _configPlaylistBox.SelectedSong)
            {
                CheckPlaylistItem();
                var selectedItem = (StringObject)_configPlaylistBox.PlaylistSongDropDown.SelectedItem;
                var selectedItemName = selectedItem.String;
                foreach (var song in _configPlaylistBox.Songs!)
                {
                    if (song.Name.Equals(selectedItemName))
                    {
                        SetAndLoadSong(song.Index);
                        _configPlaylistBox.SelectedSong = _configPlaylistBox.PlaylistSongDropDown.Selected;
                    }
                }
            }
        }
    }
    private void PlaylistSongStringChanged(int index)
    {
        if (_configPlaylistBox.PlaylistSongDropDown.SelectedItem is not null)
        {
            foreach (var song in _configPlaylistBox.Songs!)
            {
                if (song.Index.Equals(index))
                {
                    _configPlaylistBox.PlaylistSongDropDown.SetSelected(_configPlaylistBox.GetPlaylistSongIndex(index));
                }
            }
        }
    }
    public void SetAndLoadSong(int index)
    {
        _curSong = index;
        if (_sequenceNumberSpinButton.Value == index)
        {
            _soundSequenceList.SelectRow(index);
            SequenceNumberSpinButton_ValueChanged(this, EventArgs.Empty);
            PlaylistSongStringChanged(index);
        }
        else
        {
            _sequenceNumberSpinButton.Value = index;
        }
    }

    //private void SetAndLoadNextPlaylistSong()
    //{
    //	if (_remainingSequences.Count == 0)
    //	{
    //		_remainingSequences.AddRange(_curPlaylist.Songs.Select(s => s.Index));
    //		if (GlobalConfig.Instance.PlaylistMode == PlaylistMode.Random)
    //		{
    //			_remainingSequences.Any();
    //		}
    //	}
    //	long nextSequence = _remainingSequences[0];
    //	_remainingSequences.RemoveAt(0);
    //	SetAndLoadSong(nextSequence);
    //}
    private void ResetPlaylistStuff(bool spinButtonAndListBoxEnabled)
    {
        if (Engine.Instance != null)
        {
            Engine.Instance.Player.ShouldFadeOut = false;
        }
        _curSong = -1;
        _endPlaylistAction.Enabled = false;
        _sequenceNumberSpinButton.Sensitive = /* _soundSequenceListBox.Sensitive = */ spinButtonAndListBoxEnabled;
    }
    private void EndCurrentPlaylist(object sender, EventArgs e)
    {
        if (FlexibleMessageBox.Show(Strings.EndPlaylistBody, Strings.MenuPlaylist, ButtonsType.YesNo) == ResponseType.Yes)
        {
            ResetPlaylistStuff(true);
        }
    }

    private void OpenDSE(Gio.SimpleAction sender, EventArgs e)
    {
        if (Gtk.Functions.GetMinorVersion() <= 8) // There's a bug in Gtk 4.09 and later that has broken FileChooserNative functionality, causing icons and thumbnails to appear broken
        {
            // To allow the dialog to display in native windowing format, FileChooserNative is used instead of FileChooserDialog
            var d = FileChooserNative.New(
                Strings.MenuOpenDSE, // The title shown in the folder select dialog window
                this, // The parent of the dialog window, is the MainWindow itself
                FileChooserAction.SelectFolder, // To ensure it becomes a folder select dialog window, SelectFolder is used as the FileChooserAction
                "Select Folder", // Followed by the accept
                "Cancel");       // and cancel button names.

            d.SetModal(true);

            // Note: Blocking APIs were removed in GTK4, which means the code will proceed to run and return to the main loop, even when a dialog is displayed.
            // Instead, it's handled by the OnResponse event function when it re-enters upon selection.
            d.OnResponse += (sender, e) =>
            {
                if (e.ResponseId != (int)ResponseType.Accept) // In GTK4, the 'Gtk.FileChooserNative.Action' property is used for determining the button selection on the dialog. The 'Gtk.Dialog.Run' method was removed in GTK4, due to it being a non-GUI function and going against GTK's main objectives.
                {
                    d.Unref();
                    return;
                }
                var path = d.GetCurrentFolder()!.GetPath() ?? "";
                d.GetData(path);
                OpenDSEFinish(path);
                d.Unref(); // Ensures disposal of the dialog when closed
                return;
            };
            d.Show();
        }
        else
        {
            var d = FileDialog.New();
            d.SetTitle(Strings.MenuOpenDSE);

            _selectFolderCallback = (source, res, data) =>
            {
                var folderHandle = Gtk.Internal.FileDialog.SelectFolderFinish(d.Handle, res, out ErrorHandle);
                if (folderHandle != IntPtr.Zero)
                {
                    var path = Marshal.PtrToStringUTF8(Gio.Internal.File.GetPath(folderHandle).DangerousGetHandle());
                    OpenDSEFinish(path!);
                    d.Unref();
                }
                d.Unref();
            };
            Gtk.Internal.FileDialog.SelectFolder(d.Handle, Handle, IntPtr.Zero, _selectFolderCallback, IntPtr.Zero); // SelectFolder, Open and Save methods are currently missing from GirCore, but are available in the Gtk.Internal namespace, so we're using this until GirCore updates with the method bindings. See here: https://github.com/gircore/gir.core/issues/900
            //d.SelectFolder(Handle, IntPtr.Zero, _selectFolderCallback, IntPtr.Zero);
        }
    }
    private void OpenDSEFinish(string path)
    {
        DisposeEngine();
        try
        {
            _ = new DSEEngine(path);
        }
        catch (Exception ex)
        {
            FlexibleMessageBox.Show(ex, Strings.ErrorOpenDSE);
            return;
        }
        DSEConfig config = DSEEngine.DSEInstance!.Config;
        FinishLoading(config.BGMFiles.Length);
        _sequenceNumberSpinButton.Visible = false;
        _sequenceNumberSpinButton.Hide();
        _mainMenu.AppendItem(_playlistItem);
        _exportDLSAction.Enabled = false;
        _exportMIDIAction.Enabled = false;
        _exportSF2Action.Enabled = false;
    }
    private void OpenSDAT(Gio.SimpleAction sender, EventArgs e)
    {
        var filterSDAT = FileFilter.New();
        filterSDAT.SetName(Strings.FilterOpenSDAT);
        filterSDAT.AddPattern("*.sdat");
        var allFiles = FileFilter.New();
        allFiles.SetName(Strings.FilterAllFiles);
        allFiles.AddPattern("*.*");

        if (Gtk.Functions.GetMinorVersion() <= 8)
        {
            var d = FileChooserNative.New(
                Strings.MenuOpenSDAT,
                this,
                FileChooserAction.Open,
                "Open",
                "Cancel");

            d.SetModal(true);

            d.AddFilter(filterSDAT);
            d.AddFilter(allFiles);

            d.OnResponse += (sender, e) =>
            {
                if (e.ResponseId != (int)ResponseType.Accept)
                {
                    d.Unref();
                    return;
                }

                var path = d.GetFile()!.GetPath() ?? "";
                d.GetData(path);
                OpenSDATFinish(path);
                d.Unref();
            };
            d.Show();
        }
        else
        {
            var d = FileDialog.New();
            d.SetTitle(Strings.MenuOpenSDAT);
            var filters = Gio.ListStore.New(FileFilter.GetGType());
            filters.Append(filterSDAT);
            filters.Append(allFiles);
            d.SetFilters(filters);
            _openCallback = (source, res, data) =>
            {
                var fileHandle = Gtk.Internal.FileDialog.OpenFinish(d.Handle, res, out ErrorHandle);
                if (fileHandle != IntPtr.Zero)
                {
                    var path = Marshal.PtrToStringUTF8(Gio.Internal.File.GetPath(fileHandle).DangerousGetHandle());
                    OpenSDATFinish(path!);
                    d.Unref();
                }
                d.Unref();
            };
            Gtk.Internal.FileDialog.Open(d.Handle, Handle, IntPtr.Zero, _openCallback, IntPtr.Zero);
            //d.Open(Handle, IntPtr.Zero, _openCallback, IntPtr.Zero);
        }
    }
    private void OpenSDATFinish(string path)
    {
        DisposeEngine();
        try
        {
            using (FileStream stream = File.OpenRead(path))
            {
                _ = new SDATEngine(new SDAT(stream));
            }
        }
        catch (Exception ex)
        {
            FlexibleMessageBox.Show(ex, Strings.ErrorOpenSDAT);
            return;
        }

        SDATConfig config = SDATEngine.SDATInstance!.Config;
        FinishLoading(config.SDAT.INFOBlock.SequenceInfos.NumEntries);
        _sequenceNumberSpinButton.Visible = true;
        _sequenceNumberSpinButton.Show();
        _exportDLSAction.Enabled = false;
        _exportMIDIAction.Enabled = false;
        _exportSF2Action.Enabled = false;
    }
    private void OpenAlphaDream(Gio.SimpleAction sender, EventArgs e)
    {
        var filterGBA = FileFilter.New();
        filterGBA.SetName(Strings.FilterOpenGBA);
        filterGBA.AddPattern("*.gba");
        filterGBA.AddPattern("*.srl");
        var allFiles = FileFilter.New();
        allFiles.SetName(Name = Strings.FilterAllFiles);
        allFiles.AddPattern("*.*");

        if (Gtk.Functions.GetMinorVersion() <= 8)
        {
            var d = FileChooserNative.New(
                Strings.MenuOpenAlphaDream,
                this,
                FileChooserAction.Open,
                "Open",
                "Cancel");
            d.SetModal(true);

            d.AddFilter(filterGBA);
            d.AddFilter(allFiles);

            d.OnResponse += (sender, e) =>
            {
                if (e.ResponseId != (int)ResponseType.Accept)
                {
                    d.Unref();
                    return;
                }
                var path = d.GetFile()!.GetPath() ?? "";
                d.GetData(path);
                OpenAlphaDreamFinish(path);
                d.Unref();
            };
            d.Show();
        }
        else
        {
            var d = FileDialog.New();
            d.SetTitle(Strings.MenuOpenAlphaDream);
            var filters = Gio.ListStore.New(FileFilter.GetGType());
            filters.Append(filterGBA);
            filters.Append(allFiles);
            d.SetFilters(filters);
            _openCallback = (source, res, data) =>
            {
                var fileHandle = Gtk.Internal.FileDialog.OpenFinish(d.Handle, res, out ErrorHandle);
                if (fileHandle != IntPtr.Zero)
                {
                    var path = Marshal.PtrToStringUTF8(Gio.Internal.File.GetPath(fileHandle).DangerousGetHandle());
                    OpenAlphaDreamFinish(path!);
                    d.Unref();
                }
                d.Unref();
            };
            Gtk.Internal.FileDialog.Open(d.Handle, Handle, IntPtr.Zero, _openCallback, IntPtr.Zero);
            //d.Open(Handle, IntPtr.Zero, _openCallback, IntPtr.Zero);
        }
    }
    private void OpenAlphaDreamFinish(string path)
    {
        DisposeEngine();
        try
        {
            _ = new AlphaDreamEngine(File.ReadAllBytes(path));
        }
        catch (Exception ex)
        {
            FlexibleMessageBox.Show(ex, Strings.ErrorOpenAlphaDream);
            return;
        }

        AlphaDreamConfig config = AlphaDreamEngine.AlphaDreamInstance!.Config;
        FinishLoading(config.SongTableSizes[0]);
        _sequenceNumberSpinButton.Visible = true;
        _sequenceNumberSpinButton.Show();
        _mainMenu.AppendItem(_dataItem);
        _mainMenu.AppendItem(_playlistItem);
        _exportDLSAction.Enabled = true;
        _exportMIDIAction.Enabled = false;
        _exportSF2Action.Enabled = true;
    }
    private void OpenMP2K(Gio.SimpleAction sender, EventArgs e)
    {
        //var inFile = GTK4Utils.CreateLoadDialog(["*.gba", "*.srl"], Strings.MenuOpenMP2K, Strings.FilterOpenGBA);
        //if (inFile is not null)
        //{
        //    OpenMP2KFinish(inFile);
        //}


        FileFilter filterGBA = FileFilter.New();
        filterGBA.SetName(Strings.FilterOpenGBA);
        filterGBA.AddPattern("*.gba");
        filterGBA.AddPattern("*.srl");
        FileFilter allFiles = FileFilter.New();
        allFiles.SetName(Strings.FilterAllFiles);
        allFiles.AddPattern("*.*");

        if (Gtk.Functions.GetMinorVersion() <= 8)
        {
            var d = FileChooserNative.New(
                Strings.MenuOpenMP2K,
                this,
                FileChooserAction.Open,
                "Open",
                "Cancel");


            d.AddFilter(filterGBA);
            d.AddFilter(allFiles);

            d.OnResponse += (sender, e) =>
            {
                if (e.ResponseId != (int)ResponseType.Accept)
                {
                    d.Unref();
                    return;
                }
                var path = d.GetFile()!.GetPath() ?? "";
                OpenMP2KFinish(path);
                d.Unref();
            };
            d.Show();
        }
        else
        {
            var d = FileDialog.New();
            d.SetTitle(Strings.MenuOpenMP2K);
            var filters = Gio.ListStore.New(FileFilter.GetGType());
            filters.Append(filterGBA);
            filters.Append(allFiles);
            d.SetFilters(filters);
            _openCallback = (source, res, data) =>
            {
                var fileHandle = Gtk.Internal.FileDialog.OpenFinish(d.Handle, res, out ErrorHandle);
                if (fileHandle != IntPtr.Zero)
                {
                    var path = Marshal.PtrToStringUTF8(Gio.Internal.File.GetPath(fileHandle).DangerousGetHandle());
                    OpenMP2KFinish(path!);
                    filterGBA.Unref();
                    allFiles.Unref();
                    filters.Unref();
                    GObject.Internal.Object.Unref(fileHandle);
                    d.Unref();
                    return;
                }
                d.Unref();
            };
            Gtk.Internal.FileDialog.Open(d.Handle, Handle, IntPtr.Zero, _openCallback, IntPtr.Zero);
            //d.Open(Handle, IntPtr.Zero, _openCallback, IntPtr.Zero);
        }
    }
    private void OpenMP2KFinish(string path)
    {
        if (Engine.Instance is not null)
        {
            DisposeEngine();
        }

        try
        {
            _ = new MP2KEngine(File.ReadAllBytes(path));
        }
        catch (Exception ex)
        {
            //_dialog = Adw.MessageDialog.New(this, Strings.ErrorOpenMP2K, ex.ToString());
            //FlexibleMessageBox.Show(ex, Strings.ErrorOpenMP2K);
            DisposeEngine();
            ExceptionDialog(ex, Strings.ErrorOpenMP2K);
            return;
        }

        MP2KConfig config = MP2KEngine.MP2KInstance!.Config;
        FinishLoading(config.SongTableSizes[0]);
        _sequenceNumberSpinButton.Visible = true;
        _sequenceNumberSpinButton.Show();
        _buttonRecord.Sensitive =
            _configPlaylistBox.PlaylistDropDown!.Sensitive =
            _configPlaylistBox.ButtonPrevPlistSong.Sensitive =
            _configPlaylistBox.PlaylistSongDropDown!.Sensitive =
            _configPlaylistBox.ButtonNextPlistSong.Sensitive = true;
        _exportDLSAction.Enabled = false;
        _exportMIDIAction.Enabled = true;
        _exportSF2Action.Enabled = false;
    }
    private void ExportDLS(Gio.SimpleAction sender, EventArgs e)
    {
        AlphaDreamConfig cfg = AlphaDreamEngine.AlphaDreamInstance!.Config;

        FileFilter ff = FileFilter.New();
        ff.SetName(Strings.FilterSaveDLS);
        ff.AddPattern("*.dls");

        if (Gtk.Functions.GetMinorVersion() <= 8)
        {
            var d = FileChooserNative.New(
                Strings.MenuSaveDLS,
                this,
                FileChooserAction.Save,
                "Save",
                "Cancel");
            d.SetCurrentName(cfg.GetGameName());
            d.AddFilter(ff);

            d.OnResponse += (sender, e) =>
            {
                if (e.ResponseId != (int)ResponseType.Accept)
                {
                    d.Unref();
                    return;
                }

                var path = d.GetFile()!.GetPath() ?? "";
                ExportDLSFinish(cfg, path);
                d.Unref();
            };
            d.Show();
        }
        else
        {
            var d = FileDialog.New();
            d.SetTitle(Strings.MenuSaveDLS);
            var filters = Gio.ListStore.New(FileFilter.GetGType());
            filters.Append(ff);
            d.SetFilters(filters);
            _saveCallback = (source, res, data) =>
            {
                var fileHandle = Gtk.Internal.FileDialog.SaveFinish(d.Handle, res, out ErrorHandle);
                if (fileHandle != IntPtr.Zero)
                {
                    var path = Marshal.PtrToStringUTF8(Gio.Internal.File.GetPath(fileHandle).DangerousGetHandle());
                    ExportDLSFinish(cfg, path!);
                    d.Unref();
                }
                d.Unref();
            };
            Gtk.Internal.FileDialog.Save(d.Handle, Handle, IntPtr.Zero, _saveCallback, IntPtr.Zero);
            //d.Save(Handle, IntPtr.Zero, _saveCallback, IntPtr.Zero);
        }
    }
    private void ExportDLSFinish(AlphaDreamConfig config, string path)
    {
        try
        {
            AlphaDreamSoundFontSaver_DLS.Save(config, path);
            FlexibleMessageBox.Show(string.Format(Strings.SuccessSaveDLS, path), Strings.SuccessSaveDLS);
        }
        catch (Exception ex)
        {
            FlexibleMessageBox.Show(ex, Strings.ErrorSaveDLS);
        }
    }
    private void ExportMIDI(Gio.SimpleAction sender, EventArgs e)
    {
        FileFilter ff = FileFilter.New();
        ff.SetName(Strings.FilterSaveMIDI);
        ff.AddPattern("*.mid");
        ff.AddPattern("*.midi");

        if (Gtk.Functions.GetMinorVersion() <= 8)
        {
            var d = FileChooserNative.New(
                Strings.MenuSaveMIDI,
                this,
                FileChooserAction.Save,
                "Save",
                "Cancel");
            d.SetCurrentName(Engine.Instance!.Config.GetSongName((int)_sequenceNumberSpinButton.Value));
            d.AddFilter(ff);

            d.OnResponse += (sender, e) =>
            {
                if (e.ResponseId != (int)ResponseType.Accept)
                {
                    d.Unref();
                    return;
                }

                var path = d.GetFile()!.GetPath() ?? "";
                ExportMIDIFinish(path);
                d.Unref();
            };
            d.Show();
        }
        else
        {
            var d = FileDialog.New();
            d.SetTitle(Strings.MenuSaveMIDI);
            var filters = Gio.ListStore.New(FileFilter.GetGType());
            filters.Append(ff);
            d.SetFilters(filters);
            _saveCallback = (source, res, data) =>
            {
                var fileHandle = Gtk.Internal.FileDialog.SaveFinish(d.Handle, res, out ErrorHandle);
                if (fileHandle != IntPtr.Zero)
                {
                    var path = Marshal.PtrToStringUTF8(Gio.Internal.File.GetPath(fileHandle).DangerousGetHandle());
                    ExportMIDIFinish(path!);
                    d.Unref();
                }
                d.Unref();
            };
            Gtk.Internal.FileDialog.Save(d.Handle, Handle, IntPtr.Zero, _saveCallback, IntPtr.Zero);
            //d.Save(Handle, IntPtr.Zero, _saveCallback, IntPtr.Zero);
        }
    }
    private void ExportMIDIFinish(string path)
    {
        MP2KPlayer p = MP2KEngine.MP2KInstance!.Player;
        var args = new MIDISaveArgs(true, false, new (int AbsoluteTick, (byte Numerator, byte Denominator))[]
        {
            (0, (4, 4)),
        });

        try
        {
            p.SaveAsMIDI(path, args);
            FlexibleMessageBox.Show(string.Format(Strings.SuccessSaveMIDI, path), Strings.SuccessSaveMIDI);
        }
        catch (Exception ex)
        {
            FlexibleMessageBox.Show(ex, Strings.ErrorSaveMIDI);
        }
    }
    private void ExportSF2(Gio.SimpleAction sender, EventArgs e)
    {
        AlphaDreamConfig cfg = AlphaDreamEngine.AlphaDreamInstance!.Config;

        FileFilter ff = FileFilter.New();
        ff.SetName(Strings.FilterSaveSF2);
        ff.AddPattern("*.sf2");

        if (Gtk.Functions.GetMinorVersion() <= 8)
        {
            var d = FileChooserNative.New(
                Strings.MenuSaveSF2,
                this,
                FileChooserAction.Save,
                "Save",
                "Cancel");

            d.SetCurrentName(cfg.GetGameName());
            d.AddFilter(ff);

            d.OnResponse += (sender, e) =>
            {
                if (e.ResponseId != (int)ResponseType.Accept)
                {
                    d.Unref();
                    return;
                }

                var path = d.GetFile()!.GetPath() ?? "";
                ExportSF2Finish(path, cfg);
                d.Unref();
            };
            d.Show();
        }
        else
        {
            var d = FileDialog.New();
            d.SetTitle(Strings.MenuSaveSF2);
            var filters = Gio.ListStore.New(FileFilter.GetGType());
            filters.Append(ff);
            d.SetFilters(filters);
            _saveCallback = (source, res, data) =>
            {
                var fileHandle = Gtk.Internal.FileDialog.SaveFinish(d.Handle, res, out ErrorHandle);
                if (fileHandle != IntPtr.Zero)
                {
                    var path = Marshal.PtrToStringUTF8(Gio.Internal.File.GetPath(fileHandle).DangerousGetHandle());
                    ExportSF2Finish(path!, cfg);
                    d.Unref();
                }
                d.Unref();
            };
            Gtk.Internal.FileDialog.Save(d.Handle, Handle, IntPtr.Zero, _saveCallback, IntPtr.Zero);
            //d.Save(Handle, IntPtr.Zero, _saveCallback, IntPtr.Zero);
        }
    }
    private void ExportSF2Finish(string path, AlphaDreamConfig config)
    {
        try
        {
            AlphaDreamSoundFontSaver_SF2.Save(path, config);
            FlexibleMessageBox.Show(string.Format(Strings.SuccessSaveSF2, path), Strings.SuccessSaveSF2);
        }
        catch (Exception ex)
        {
            FlexibleMessageBox.Show(ex, Strings.ErrorSaveSF2);
        }
    }
    private void ExportWAV(object sender, EventArgs e)
    {
        FileFilter ff = FileFilter.New();
        ff.SetName(Strings.FilterSaveWAV);
        ff.AddPattern("*.wav");

        if (Gtk.Functions.GetMinorVersion() <= 8)
        {
            var d = FileChooserNative.New(
            Strings.MenuSaveWAV,
            this,
            FileChooserAction.Save,
            "Save",
            "Cancel");

            d.SetCurrentName(Engine.Instance!.Config.GetSongName((int)_sequenceNumberSpinButton.Value));
            d.AddFilter(ff);

            d.OnResponse += (sender, e) =>
            {
                if (e.ResponseId != (int)ResponseType.Accept)
                {
                    d.Unref();
                    return;
                }

                var path = d.GetFile()!.GetPath() ?? "";
                ExportWAVFinish(path);
                d.Unref();
            };
            d.Show();
        }
        else
        {
            var d = FileDialog.New();
            d.SetTitle(Strings.MenuSaveWAV);
            var filters = Gio.ListStore.New(FileFilter.GetGType());
            filters.Append(ff);
            d.SetFilters(filters);
            _saveCallback = (source, res, data) =>
            {
                var fileHandle = Gtk.Internal.FileDialog.SaveFinish(d.Handle, res, out ErrorHandle);
                if (fileHandle != IntPtr.Zero)
                {
                    var path = Marshal.PtrToStringUTF8(Gio.Internal.File.GetPath(fileHandle).DangerousGetHandle());
                    ExportWAVFinish(path!);
                    d.Unref();
                }
                d.Unref();
            };
            Gtk.Internal.FileDialog.Save(d.Handle, Handle, IntPtr.Zero, _saveCallback, IntPtr.Zero);
            //d.Save(Handle, IntPtr.Zero, _saveCallback, IntPtr.Zero);
        }
    }
    private void ExportWAVFinish(string path)
    {
        Stop();

        Player player = Engine.Instance!.Player;
        bool oldFade = player.ShouldFadeOut;
        long oldLoops = player.NumLoops;
        player.ShouldFadeOut = true;
        player.NumLoops = GlobalConfig.Instance.PlaylistSongLoops;

        try
        {
            player.Record(path);
            FlexibleMessageBox.Show(string.Format(Strings.SuccessSaveWAV, path), Strings.SuccessSaveWAV);
        }
        catch (Exception ex)
        {
            FlexibleMessageBox.Show(ex, Strings.ErrorSaveWAV);
        }

        player.ShouldFadeOut = oldFade;
        player.NumLoops = oldLoops;
        _stopUI = false;
    }

    public void ExceptionDialog(Exception error, string heading)
    {
        Debug.WriteLine(error.Message);
        var md = Adw.MessageDialog.New(this, heading, error.Message);
        md.SetModal(true);
        md.AddResponse("ok", ("_OK"));
        md.SetResponseAppearance("ok", ResponseAppearance.Default);
        md.SetDefaultResponse("ok");
        md.SetCloseResponse("ok");
        _exceptionCallback = (source, res, data) =>
        {
            md.Destroy();
        };
        md.Activate();
        md.Show();
    }

    public void LetUIKnowPlayerIsPlaying()
    {
        // Prevents method from being used if timer is already active
        if (_timer.IsActive())
        {
            return;
        }

        // Ensures a GlobalConfig Instance is created if one doesn't exist
        if (GlobalConfig.Instance == null)
        {
            GlobalConfig.Init(); // A new instance needs to be initialized before it can do anything
        }

        // Configures the buttons when player is playing a sequenced track
        _buttonPause.Sensitive = _buttonStop.Sensitive = true; // Setting the 'Sensitive' property to 'true' enables the buttons, allowing you to click on them
        _buttonPause.TooltipText = Strings.PlayerPause;

        ConfigureTimer();
    }

    // Configures the timer, which triggers the CheckPlayback method at every interval depending on the GlobalConfig RefreshRate
    private void ConfigureTimer()
    {
        var context = GLib.MainContext.GetThreadDefault(); // Grabs the default GLib MainContext thread
        var source = GLib.Functions.TimeoutSourceNew((uint)(1_000.0 / GlobalConfig.Instance!.RefreshRate)); // Creates and configures the timeout interval
        source.SetCallback(CheckPlayback); // Sets the callback for the timer interval to be used on
        var microsec = (ulong)source.Attach(context); // Configures the microseconds based on attaching the GLib MainContext thread
        _timer.Elapsed(ref microsec); // Adds the pointer to the configured microseconds source
        _timer.Start(); // Starts the timer
    }

    private void Play()
    {
        Engine.Instance!.Player.IsPauseToggled = false;
        Engine.Instance.Player.Play();
        LetUIKnowPlayerIsPlaying();
    }
    private void Pause()
    {
        Engine.Instance!.Player.TogglePlaying();
        if (Engine.Instance.Player.State == PlayerState.Paused)
        {
            _buttonPause.Active = true;
            _buttonPause.TooltipText = Strings.PlayerUnpause;
            Engine.Instance.Player.IsPauseToggled = true;
            _timer.Stop();
        }
        else
        {
            _buttonPause.Active = false;
            _buttonPause.TooltipText = Strings.PlayerPause;
            Engine.Instance.Player.IsPauseToggled = false;
            _timer.Start();
        }
    }
    private void Stop()
    {
        if (Engine.Instance == null)
        {
            return; // This is here to ensure that it returns if the Engine.Instance is null while closing the main window
        }
        Engine.Instance!.Player.Stop();
        _buttonPause.Active = false;
        _buttonPause.Sensitive = _buttonStop.Sensitive = false;
        _buttonPause.TooltipText = Strings.PlayerPause;
        _timer.Stop();
        UpdatePositionIndicators(0L);
    }
    private void TogglePlayback()
    {
        switch (Engine.Instance!.Player.State)
        {
            case PlayerState.Stopped: Play(); break;
            case PlayerState.Paused:
            case PlayerState.Playing: Pause(); break;
        }
    }
    private void PlayPreviousSong()
    {

        if (_playlist is not null)
        {
            _playlist.UndoThenSetAndLoadPrevSong(this, _curSong);
        }
        else
        {
            _configPlaylistBox.PlaylistSongDropDown.Selected -= 1;
            SetAndLoadSong(_configPlaylistBox.Songs![(int)_configPlaylistBox.PlaylistSongDropDown.Selected].Index);
            CheckPlaylistItem();
        }
    }
    private void PlayNextSong(object? sender, EventArgs? e)
    {
        if (_playlist is not null)
        {
            _playlist.AdvanceThenSetAndLoadNextSong(this, _curSong);
        }
        else
        {
            _configPlaylistBox.PlaylistSongDropDown.Selected += 1;
            SetAndLoadSong(_configPlaylistBox.Songs![(int)_configPlaylistBox.PlaylistSongDropDown.Selected].Index);
            CheckPlaylistItem();
        }
    }

    private void CheckPlaylistItem()
    {
        // For the Previous Song button
        if (_configPlaylistBox.PlaylistSongDropDown.Selected is 0)
            _configPlaylistBox.ButtonPrevPlistSong.Sensitive = false;
        else
            _configPlaylistBox.ButtonPrevPlistSong.Sensitive = true;

        // For the Next Song button
        if (_configPlaylistBox.PlaylistSongDropDown.Selected == _configPlaylistBox.GetNumSongs() - 1)
            _configPlaylistBox.ButtonNextPlistSong.Sensitive = false;
        else
            _configPlaylistBox.ButtonNextPlistSong.Sensitive = true;
    }

    private void FinishLoading(long numSongs)
    {
        Engine.Instance!.Player.SongEnded += SongEnded;
        _soundSequenceList.Show();
        var config = Engine.Instance.Config;
        _soundSequenceList.AddEntries(numSongs, config.InternalSongNames, config.Playlists, config.SongTableOffset!);
        _soundSequenceList.Init();
        if (config.Playlists is not null)
        {
            _configPlaylistBox.AddEntries(config.Playlists);
            _configPlaylistBox.PlaylistDropDown.OnNotify += OnPlaylistStringSelected;
            _configPlaylistBox.PlaylistSongDropDown.OnNotify += OnPlaylistSongStringSelected;
        }
        //foreach (Config.Playlist playlist in Engine.Instance.Config.Playlists)
        //{
        //	_soundSequenceListBox.Insert(Label.New(playlist.Name), playlist.Songs.Count);
        //	_soundSequenceList.Add(new SoundSequenceListItem(playlist));
        //	_soundSequenceList.AddRange(playlist.Songs.Select(s => new SoundSequenceListItem(s)).ToArray());
        //}
        _sequenceNumberAdjustment.Upper = numSongs;
#if DEBUG
        // [Debug methods specific to this GUI will go in here]
#endif
        _autoplay = false;
        SetAndLoadSong(Engine.Instance.Config.Playlists[0].Songs.Count == 0 ? 0 : Engine.Instance.Config.Playlists[0].Songs[0].Index);
        _sequenceNumberSpinButton.Sensitive = _buttonPlay.Sensitive = _volumeBar.Sensitive = true;
        _volumeBar.SetValue(100);
    }
    private void DisposeEngine()
    {
        if (Engine.Instance is not null)
        {
            Stop();
            Engine.Instance.Dispose();
        }

        //_trackViewer?.UpdateTracks();
        Name = GetProgramName();
        //_songInfo.SetNumTracks(0);
        //_songInfo.ResetMutes();
        ResetPlaylistStuff(false);
        UpdatePositionIndicators(0L);
        //_signal.Connect(_sequencesListFactory, SequencesListView_SelectionGet, false, null);
        _sequenceNumberAdjustment.OnValueChanged -= SequenceNumberSpinButton_ValueChanged;
        _sequenceNumberSpinButton.Visible = false;
        _sequenceNumberSpinButton.Value = _sequenceNumberAdjustment.Upper = 0;
        //_sequencesListView.Selection.SelectFunction = null;
        //_sequencesColumnView.Unref();
        //_signal.Connect(_sequencesListFactory, SequencesListView_SelectionGet, true, null);
        _sequenceNumberSpinButton.OnValueChanged += SequenceNumberSpinButton_ValueChanged;
    }

    private bool CheckPlayback()
    {
        if (_songEnded)
        {
            _songEnded = false;
            if (_playlist is not null)
            {
                _playlist.AdvanceThenSetAndLoadNextSong(this, _curSong);
            }
            else
            {
                Stop();
            }
        }
        if (Engine.Instance is not null)
        {
            if (_positionBarFree)
            {
                UpdatePositionIndicators(Engine.Instance!.Player.ElapsedTicks);
            }
        }
        return true;
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (_songEnded)
        {
            _songEnded = false;
            if (_playlist is not null)
            {
                _playlist.AdvanceThenSetAndLoadNextSong(this, _curSong);
            }
            else
            {
                Stop();
            }
        }
        else
        {
            if (Engine.Instance is not null)
            {
                Player player = Engine.Instance!.Player;
                UpdatePositionIndicators(player.ElapsedTicks);
            }
        }
    }
    private void SongEnded()
    {
        _songEnded = true;
        _stopUI = true;
    }

    // This updates _positionBar to the value specified
    private void UpdatePositionIndicators(long ticks)
    {
        if (_positionBarFree)
        {
            _positionBar.Adjustment!.SetValue(ticks);
        }
    }
}
