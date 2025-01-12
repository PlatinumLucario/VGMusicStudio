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
    private PlayingPlaylist? _playlist;
    private int _curSong = -1;

    private bool _songEnded = false;
    private bool _stopUI = false;
    private bool _playlistChanged = false;
    private bool _autoplay = false;
    private ManuallyChanged _manuallyChanged = 0;

    public static MainWindow? Instance { get; private set; }

    #region Widgets

    // The Windows
    private WidgetWindow _playlistWindow, _seqAudioPianoWindow, _sequencedAudioTrackInfoWindow, _sequencedAudioListWindow;
    private TrackViewer _trackViewer;

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
    private readonly Gio.Menu _mainMenu, _fileMenu, _dataMenu, _playlistMenu,
        _widgetMenu, _playlistWidgetMenu, _seqAudioPianoWidgetMenu, _seqAudioTrackInfoWidgetMenu, _seqAudioListWidgetMenu;

    // Menu Labels
    private readonly Label _fileLabel, _dataLabel, _playlistLabel, _widgetLabel;

    // Menu Items
    private readonly Gio.MenuItem
        _fileItem,
        _openDSEItem, _openAlphaDreamItem, _openMP2KItem, _openSDATItem,
        _dataItem,
        _trackViewerItem, _exportDLSItem, _exportSF2Item, _exportMIDIItem, _exportWAVItem,
        _playlistItem,
        _endPlaylistItem,
        _widgetItem,
        _playlistWidgetTiledItem, _playlistWidgetWindowedItem, _playlistWidgetHideItem,
        _seqAudioPianoWidgetTiledItem, _seqAudioPianoWidgetWindowedItem, _seqAudioPianoWidgetHideItem,
        _seqAudioTrackInfoWidgetTiledItem, _seqAudioTrackInfoWidgetWindowedItem, _seqAudioTrackInfoWidgetHideItem,
        _seqAudioListWidgetTiledItem, _seqAudioListWidgetWindowedItem, _seqAudioListWidgetHideItem;

    // Menu Actions
    private Gio.SimpleAction
        _openDSEAction, _openAlphaDreamAction, _openMP2KAction, _openSDATAction,
        _trackViewerAction, _exportDLSAction, _exportSF2Action, _exportMIDIAction, _exportWAVAction,
        _endPlaylistAction,
        _playlistWidgetTiledAction, _playlistWidgetWindowedAction, _playlistWidgetHideAction,
        _seqAudioPianoWidgetTiledAction, _seqAudioPianoWidgetWindowedAction, _seqAudioPianoWidgetHideAction,
        _seqAudioTrackInfoWidgetTiledAction, _seqAudioTrackInfoWidgetWindowedAction, _seqAudioTrackInfoWidgetHideAction,
        _seqAudioListWidgetTiledAction, _seqAudioListWidgetWindowedAction, _seqAudioListWidgetHideAction;

    // Boxes
    private Box _mainBox, _configButtonBox, _configPlayerButtonBox, _configSpinButtonBox, _configBarBox,
        _playlistBox, _pianoBox, _sequencedAudioTrackInfoBox, _sequencedAudioListBox;

    // One Scale controling volume and one Scale for the sequenced track
    private Scale _volumeBar, _positionBar;

    // Mouse Click and Drag Gestures
    private GestureClick _positionGestureClick;
    private GestureDrag _positionGestureDrag;

    // Playlist
    private PlaylistConfig _configPlaylist;

    // Sequenced Audio Piano
    private SequencedAudio_Piano _piano;

    // Sequenced Audio List
    private SequencedAudio_List _sequencedAudioList;

    // Sequenced Audio Track Info
    private SequencedAudio_TrackInfo _sequencedAudioTrackInfo;

    // Error Handle
    private GLib.Internal.ErrorOwnedHandle ErrorHandle = new GLib.Internal.ErrorOwnedHandle(IntPtr.Zero);

    // Callback
    private Gio.Internal.AsyncReadyCallback _saveCallback { get; set; }
    private Gio.Internal.AsyncReadyCallback _openCallback { get; set; }
    private Gio.Internal.AsyncReadyCallback _selectFolderCallback { get; set; }
    private Gio.Internal.AsyncReadyCallback _exceptionCallback { get; set; }

    #endregion

    private enum ManuallyChanged
    {
        None = 0,
        Initialized = 1,
        SpinButton = 2,
        PlaylistDropDown = 3,
        List = 4
    }

    public MainWindow(Application app)
    {
        // Main Window
        SetDefaultSize(100, 100); // Sets the default size of the Window
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

        _trackViewerItem = Gio.MenuItem.New(Strings.TrackViewerTitle, "app.trackViewer");
        _trackViewerAction = Gio.SimpleAction.New("trackViewer", null);
        _app.AddAction(_trackViewerAction);
        _trackViewerAction.OnActivate += OpenTrackViewer;
        _dataMenu.AppendItem(_trackViewerItem);
        _trackViewerItem.Unref();

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

        // Widget Menu
        _widgetMenu = Gio.Menu.New();

        _widgetLabel = Label.NewWithMnemonic("Widgets");
        _widgetLabel.GetMnemonicKeyval();
        _widgetLabel.SetUseUnderline(true);
        _widgetItem = Gio.MenuItem.New(_widgetLabel.GetLabel(), null);
        _popoverMenuBar.AddMnemonicLabel(_widgetLabel);
        _widgetItem.SetSubmenu(_widgetMenu);

        _playlistWidgetMenu = Gio.Menu.New();

        _playlistWidgetTiledItem = Gio.MenuItem.New("Tiled", "app.playlistWidgetTiled");
        _playlistWidgetTiledAction = Gio.SimpleAction.NewStateful("playlistWidgetTiled", null, GLib.Variant.NewBoolean(false));
        _app.AddAction(_playlistWidgetTiledAction);
        _playlistWidgetTiledAction.Enabled = false;
        _playlistWidgetTiledAction.OnActivate += Playlist_GetTiled;
        _playlistWidgetMenu.AppendItem(_playlistWidgetTiledItem);
        _playlistWidgetTiledItem.Unref();

        _playlistWidgetWindowedItem = Gio.MenuItem.New("Windowed", "app.playlistWidgetWindowed");
        _playlistWidgetWindowedAction = Gio.SimpleAction.NewStateful("playlistWidgetWindowed", null, GLib.Variant.NewBoolean(false));
        _app.AddAction(_playlistWidgetWindowedAction);
        _playlistWidgetWindowedAction.Enabled = false;
        _playlistWidgetWindowedAction.OnActivate += Playlist_GetWindowed;
        _playlistWidgetMenu.AppendItem(_playlistWidgetWindowedItem);
        _playlistWidgetWindowedItem.Unref();

        _playlistWidgetHideItem = Gio.MenuItem.New("Hide", "app.playlistWidgetHide");
        _playlistWidgetHideAction = Gio.SimpleAction.NewStateful("playlistWidgetHide", null, GLib.Variant.NewBoolean(true));
        _app.AddAction(_playlistWidgetHideAction);
        _playlistWidgetHideAction.Enabled = false;
        _playlistWidgetHideAction.OnActivate += Playlist_Hide;
        _playlistWidgetMenu.AppendItem(_playlistWidgetHideItem);
        _playlistWidgetHideItem.Unref();

        _widgetMenu.AppendSubmenu("Playlist", _playlistWidgetMenu);

        _seqAudioPianoWidgetMenu = Gio.Menu.New();

        _seqAudioPianoWidgetTiledItem = Gio.MenuItem.New("Tiled", "app.seqAudioPianoWidgetTiled");
        _seqAudioPianoWidgetTiledAction = Gio.SimpleAction.NewStateful("seqAudioPianoWidgetTiled", null, GLib.Variant.NewBoolean(false));
        _app.AddAction(_seqAudioPianoWidgetTiledAction);
        _seqAudioPianoWidgetTiledAction.Enabled = false;
        _seqAudioPianoWidgetTiledAction.OnActivate += SeqAudioPiano_GetTiled;
        _seqAudioPianoWidgetMenu.AppendItem(_seqAudioPianoWidgetTiledItem);
        _seqAudioPianoWidgetTiledItem.Unref();

        _seqAudioPianoWidgetWindowedItem = Gio.MenuItem.New("Windowed", "app.seqAudioPianoWidgetWindowed");
        _seqAudioPianoWidgetWindowedAction = Gio.SimpleAction.NewStateful("seqAudioPianoWidgetWindowed", null, GLib.Variant.NewBoolean(false));
        _app.AddAction(_seqAudioPianoWidgetWindowedAction);
        _seqAudioPianoWidgetWindowedAction.Enabled = false;
        _seqAudioPianoWidgetWindowedAction.OnActivate += SeqAudioPiano_GetWindowed;
        _seqAudioPianoWidgetMenu.AppendItem(_seqAudioPianoWidgetWindowedItem);
        _seqAudioPianoWidgetWindowedItem.Unref();

        _seqAudioPianoWidgetHideItem = Gio.MenuItem.New("Hide", "app.seqAudioPianoWidgetHide");
        _seqAudioPianoWidgetHideAction = Gio.SimpleAction.NewStateful("seqAudioPianoWidgetHide", null, GLib.Variant.NewBoolean(true));
        _app.AddAction(_seqAudioPianoWidgetHideAction);
        _seqAudioPianoWidgetHideAction.Enabled = false;
        _seqAudioPianoWidgetHideAction.OnActivate += SeqAudioPiano_Hide;
        _seqAudioPianoWidgetMenu.AppendItem(_seqAudioPianoWidgetHideItem);
        _seqAudioPianoWidgetHideItem.Unref();

        _widgetMenu.AppendSubmenu("Piano", _seqAudioPianoWidgetMenu);

        _seqAudioTrackInfoWidgetMenu = Gio.Menu.New();

        _seqAudioTrackInfoWidgetTiledItem = Gio.MenuItem.New("Tiled", "app.seqAudioTrackInfoWidgetTiled");
        _seqAudioTrackInfoWidgetTiledAction = Gio.SimpleAction.NewStateful("seqAudioTrackInfoWidgetTiled", null, GLib.Variant.NewBoolean(false));
        _app.AddAction(_seqAudioTrackInfoWidgetTiledAction);
        _seqAudioTrackInfoWidgetTiledAction.Enabled = false;
        _seqAudioTrackInfoWidgetTiledAction.OnActivate += SeqAudioTrackInfo_GetTiled;
        _seqAudioTrackInfoWidgetMenu.AppendItem(_seqAudioTrackInfoWidgetTiledItem);
        _seqAudioTrackInfoWidgetTiledItem.Unref();

        _seqAudioTrackInfoWidgetWindowedItem = Gio.MenuItem.New("Windowed", "app.seqAudioTrackInfoWidgetWindowed");
        _seqAudioTrackInfoWidgetWindowedAction = Gio.SimpleAction.NewStateful("seqAudioTrackInfoWidgetWindowed", null, GLib.Variant.NewBoolean(false));
        _app.AddAction(_seqAudioTrackInfoWidgetWindowedAction);
        _seqAudioTrackInfoWidgetWindowedAction.Enabled = false;
        _seqAudioTrackInfoWidgetWindowedAction.OnActivate += SeqAudioTrackInfo_GetWindowed;
        _seqAudioTrackInfoWidgetMenu.AppendItem(_seqAudioTrackInfoWidgetWindowedItem);
        _seqAudioTrackInfoWidgetWindowedItem.Unref();

        _seqAudioTrackInfoWidgetHideItem = Gio.MenuItem.New("Hide", "app.seqAudioTrackInfoWidgetHide");
        _seqAudioTrackInfoWidgetHideAction = Gio.SimpleAction.NewStateful("seqAudioTrackInfoWidgetHide", null, GLib.Variant.NewBoolean(true));
        _app.AddAction(_seqAudioTrackInfoWidgetHideAction);
        _seqAudioTrackInfoWidgetHideAction.Enabled = false;
        _seqAudioTrackInfoWidgetHideAction.OnActivate += SeqAudioTrackInfo_Hide;
        _seqAudioTrackInfoWidgetMenu.AppendItem(_seqAudioTrackInfoWidgetHideItem);
        _seqAudioTrackInfoWidgetHideItem.Unref();

        _widgetMenu.AppendSubmenu("Sequenced Audio Track Info", _seqAudioTrackInfoWidgetMenu);

        _seqAudioListWidgetMenu = Gio.Menu.New();

        _seqAudioListWidgetTiledItem = Gio.MenuItem.New("Tiled", "app.seqAudioListWidgetTiled");
        _seqAudioListWidgetTiledAction = Gio.SimpleAction.NewStateful("seqAudioListWidgetTiled", null, GLib.Variant.NewBoolean(true));
        _app.AddAction(_seqAudioListWidgetTiledAction);
        _seqAudioListWidgetTiledAction.OnActivate += SeqAudioList_GetTiled;
        _seqAudioListWidgetMenu.AppendItem(_seqAudioListWidgetTiledItem);
        _seqAudioListWidgetTiledItem.Unref();

        _seqAudioListWidgetWindowedItem = Gio.MenuItem.New("Windowed", "app.seqAudioListWidgetWindowed");
        _seqAudioListWidgetWindowedAction = Gio.SimpleAction.NewStateful("seqAudioListWidgetWindowed", null, GLib.Variant.NewBoolean(false));
        _app.AddAction(_seqAudioListWidgetWindowedAction);
        _seqAudioListWidgetWindowedAction.OnActivate += SeqAudioList_GetWindowed;
        _seqAudioListWidgetMenu.AppendItem(_seqAudioListWidgetWindowedItem);
        _seqAudioListWidgetWindowedItem.Unref();

        _seqAudioListWidgetHideItem = Gio.MenuItem.New("Hide", "app.seqAudioListWidgetHide");
        _seqAudioListWidgetHideAction = Gio.SimpleAction.NewStateful("seqAudioListWidgetHide", null, GLib.Variant.NewBoolean(false));
        _app.AddAction(_seqAudioListWidgetHideAction);
        _seqAudioListWidgetHideAction.OnActivate += SeqAudioList_Hide;
        _seqAudioListWidgetMenu.AppendItem(_seqAudioListWidgetHideItem);
        _seqAudioListWidgetHideItem.Unref();

        _widgetMenu.AppendSubmenu("Sequenced Audio List", _seqAudioListWidgetMenu);

        _mainMenu.AppendItem(_widgetItem);
        _widgetItem.Unref();

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
        _sequenceNumberSpinButton = SpinButton.New(Adjustment.New(0, 0, -1, 1, 1, 1), 1, 0);
        _sequenceNumberSpinButton.Sensitive = false;
        _sequenceNumberSpinButton.Value = 0;
        _sequenceNumberSpinButton.OnValueChanged += SequenceNumberSpinButton_ValueChanged;
        _sequenceNumberSpinButton.OnChangeValue += SequenceNumberSpinButton_ChangeValue;

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
        _configPlaylist = new PlaylistConfig();
        _configPlaylist.ButtonPrevPlistSong.OnClicked += (o, e) => PlayPreviousSong();
        _configPlaylist.ButtonNextPlistSong.OnClicked += PlayNextSong;
        _playlistBox = Box.New(Orientation.Vertical, 0);
        _playlistBox.SetVexpand(true);

        // Sequenced Audio Piano
        _piano = new();
        _pianoBox = Box.New(Orientation.Vertical, 0);
        _pianoBox.SetVexpand(false);

        // Sequenced Audio Track Info
        _sequencedAudioTrackInfo = new();
        _sequencedAudioTrackInfoBox = Box.New(Orientation.Vertical, 0);
        _sequencedAudioTrackInfoBox.SetVexpand(true);

        // Sequenced Audio List
        _sequencedAudioList = new();
        _sequencedAudioListBox = Box.New(Orientation.Vertical, 0);
        _sequencedAudioListBox.SetVexpand(true);
        _sequencedAudioListBox.Append(_sequencedAudioList);

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
        _mainBox.Append(_playlistBox);
        _mainBox.Append(_pianoBox);
        _mainBox.Append(_sequencedAudioTrackInfoBox);
        _mainBox.Append(_sequencedAudioListBox);

        SetContent(_mainBox);

        Instance = this;

        // Ensures the entire application gets closed when the main window is closed
        OnCloseRequest += (sender, args) =>
        {
            DisposeEngine(); // Engine must be disposed first, otherwise the window will softlock when closing
            Dispose();
            return false;
        };
    }

#region Widget Display Toggle Methods
    private void Playlist_GetTiled(object sender, EventArgs args)
    {
        if (_playlistWidgetTiledAction.GetState()!.GetBoolean() == true)
            return;

        if (_playlistWindow is not null)
        {
            if (_playlistWindow.WidgetBox.GetFirstChild() is not null)
            {
                _playlistWindow.WidgetBox.Remove(_configPlaylist);
            }
            _playlistWindow.OnCloseRequest -= PlaylistWindow_CloseRequest;
            _playlistWindow.Dispose();
            _playlistWindow.Close();
            if (_playlistWindow is not null)
            {
                _playlistWindow = null!;
            }
        }

        _playlistBox.Append(_configPlaylist);

        _playlistWidgetTiledAction.SetState(GLib.Variant.NewBoolean(true));
        _playlistWidgetWindowedAction.SetState(GLib.Variant.NewBoolean(false));
        _playlistWidgetHideAction.SetState(GLib.Variant.NewBoolean(false));
    }

    private void Playlist_GetWindowed(object sender, EventArgs args)
    {
        if (_playlistWidgetWindowedAction.GetState()!.GetBoolean() == true)
            return;

        if (_playlistBox.GetFirstChild() is not null)
        {
            _playlistBox.Remove(_configPlaylist);
        }
        _playlistWindow ??= new WidgetWindow(_configPlaylist);
        _playlistWindow.OnCloseRequest += PlaylistWindow_CloseRequest;
        _playlistWindow.Present();

        _playlistWidgetTiledAction.SetState(GLib.Variant.NewBoolean(false));
        _playlistWidgetWindowedAction.SetState(GLib.Variant.NewBoolean(true));
        _playlistWidgetHideAction.SetState(GLib.Variant.NewBoolean(false));
    }

    private void Playlist_Hide(object sender, EventArgs args)
    {
        if (_playlistWidgetHideAction.GetState()!.GetBoolean() == true)
            return;

        if (_playlistWindow is not null)
        {
            if (_playlistWindow.WidgetBox.GetFirstChild() is not null)
            {
                _playlistWindow.WidgetBox.Remove(_configPlaylist);
            }
            _playlistWindow.OnCloseRequest -= PlaylistWindow_CloseRequest;
            _playlistWindow.Dispose();
            _playlistWindow.Close();
            if (_playlistWindow is not null)
            {
                _playlistWindow = null!;
            }
        }
        if (_playlistBox.GetFirstChild() is not null)
        {
            _playlistBox.Remove(_configPlaylist);
        }

        _playlistWidgetTiledAction.SetState(GLib.Variant.NewBoolean(false));
        _playlistWidgetWindowedAction.SetState(GLib.Variant.NewBoolean(false));
        _playlistWidgetHideAction.SetState(GLib.Variant.NewBoolean(true));
    }

    private void SeqAudioPiano_GetTiled(object sender, EventArgs args)
    {
        if (_seqAudioPianoWidgetTiledAction.GetState()!.GetBoolean() == true)
            return;

        if (_seqAudioPianoWindow is not null)
        {
            if (_seqAudioPianoWindow.WidgetBox.GetFirstChild() is not null)
            {
                _seqAudioPianoWindow.WidgetBox.Remove(_piano);
            }
            _seqAudioPianoWindow.OnCloseRequest -= SeqAudioPianoWindow_CloseRequest;
            _seqAudioPianoWindow.Dispose();
            _seqAudioPianoWindow.Close();
            if (_seqAudioPianoWindow is not null)
            {
                _seqAudioPianoWindow = null!;
            }
        }

        _pianoBox.Append(_piano);

        _seqAudioPianoWidgetTiledAction.SetState(GLib.Variant.NewBoolean(true));
        _seqAudioPianoWidgetWindowedAction.SetState(GLib.Variant.NewBoolean(false));
        _seqAudioPianoWidgetHideAction.SetState(GLib.Variant.NewBoolean(false));
    }

    private void SeqAudioPiano_GetWindowed(object sender, EventArgs args)
    {
        if (_seqAudioPianoWidgetWindowedAction.GetState()!.GetBoolean() == true)
            return;

        if (_pianoBox.GetFirstChild() is not null)
        {
            _pianoBox.Remove(_piano);
        }
        _seqAudioPianoWindow ??= new WidgetWindow(_piano);
        _seqAudioPianoWindow.OnCloseRequest += SeqAudioPianoWindow_CloseRequest;
        _seqAudioPianoWindow.Present();

        _seqAudioPianoWidgetTiledAction.SetState(GLib.Variant.NewBoolean(false));
        _seqAudioPianoWidgetWindowedAction.SetState(GLib.Variant.NewBoolean(true));
        _seqAudioPianoWidgetHideAction.SetState(GLib.Variant.NewBoolean(false));
    }

    private void SeqAudioPiano_Hide(object sender, EventArgs args)
    {
        if (_seqAudioPianoWidgetHideAction.GetState()!.GetBoolean() == true)
            return;

        if (_seqAudioPianoWindow is not null)
        {
            if (_seqAudioPianoWindow.WidgetBox.GetFirstChild() is not null)
            {
                _seqAudioPianoWindow.WidgetBox.Remove(_piano);
            }
            _seqAudioPianoWindow.OnCloseRequest -= SeqAudioPianoWindow_CloseRequest;
            _seqAudioPianoWindow.Dispose();
            _seqAudioPianoWindow.Close();
            if (_seqAudioPianoWindow is not null)
            {
                _seqAudioPianoWindow = null!;
            }
        }
        if (_pianoBox.GetFirstChild() is not null)
        {
            _pianoBox.Remove(_piano);
        }

        _seqAudioPianoWidgetTiledAction.SetState(GLib.Variant.NewBoolean(false));
        _seqAudioPianoWidgetWindowedAction.SetState(GLib.Variant.NewBoolean(false));
        _seqAudioPianoWidgetHideAction.SetState(GLib.Variant.NewBoolean(true));
    }

    private void SeqAudioTrackInfo_GetTiled(object sender, EventArgs args)
    {
        if (_seqAudioTrackInfoWidgetTiledAction.GetState()!.GetBoolean() == true)
            return;

        if (_sequencedAudioTrackInfoWindow is not null)
        {
            if (_sequencedAudioTrackInfoWindow.WidgetBox.GetFirstChild() is not null)
            {
                _sequencedAudioTrackInfoWindow.WidgetBox.Remove(_sequencedAudioTrackInfo);
            }
            _sequencedAudioTrackInfoWindow.OnCloseRequest -= SeqAudioTrackInfoWindow_CloseRequest;
            _sequencedAudioTrackInfoWindow.Dispose();
            _sequencedAudioTrackInfoWindow.Close();
            if (_sequencedAudioTrackInfoWindow is not null)
            {
                _sequencedAudioTrackInfoWindow = null!;
            }
        }

        _sequencedAudioTrackInfoBox.Append(_sequencedAudioTrackInfo);

        _seqAudioTrackInfoWidgetTiledAction.SetState(GLib.Variant.NewBoolean(true));
        _seqAudioTrackInfoWidgetWindowedAction.SetState(GLib.Variant.NewBoolean(false));
        _seqAudioTrackInfoWidgetHideAction.SetState(GLib.Variant.NewBoolean(false));
    }

    private void SeqAudioTrackInfo_GetWindowed(object sender, EventArgs args)
    {
        if (_seqAudioTrackInfoWidgetWindowedAction.GetState()!.GetBoolean() == true)
            return;

        if (_sequencedAudioTrackInfoBox.GetFirstChild() is not null)
        {
            _sequencedAudioTrackInfoBox.Remove(_sequencedAudioTrackInfo);
        }
        _sequencedAudioTrackInfoWindow ??= new WidgetWindow(_sequencedAudioTrackInfo);
        _sequencedAudioTrackInfoWindow.OnCloseRequest += SeqAudioTrackInfoWindow_CloseRequest;
        _sequencedAudioTrackInfoWindow.Present();

        _seqAudioTrackInfoWidgetTiledAction.SetState(GLib.Variant.NewBoolean(false));
        _seqAudioTrackInfoWidgetWindowedAction.SetState(GLib.Variant.NewBoolean(true));
        _seqAudioTrackInfoWidgetHideAction.SetState(GLib.Variant.NewBoolean(false));
    }

    private void SeqAudioTrackInfo_Hide(object sender, EventArgs args)
    {
        if (_seqAudioTrackInfoWidgetHideAction.GetState()!.GetBoolean() == true)
            return;

        if (_sequencedAudioTrackInfoWindow is not null)
        {
            if (_sequencedAudioTrackInfoWindow.WidgetBox.GetFirstChild() is not null)
            {
                _sequencedAudioTrackInfoWindow.WidgetBox.Remove(_sequencedAudioTrackInfo);
            }
            _sequencedAudioTrackInfoWindow.OnCloseRequest -= SeqAudioTrackInfoWindow_CloseRequest;
            _sequencedAudioTrackInfoWindow.Dispose();
            _sequencedAudioTrackInfoWindow.Close();
            if (_sequencedAudioTrackInfoWindow is not null)
            {
                _sequencedAudioTrackInfoWindow = null!;
            }
        }
        if (_sequencedAudioTrackInfoBox.GetFirstChild() is not null)
        {
            _sequencedAudioTrackInfoBox.Remove(_sequencedAudioTrackInfo);
        }

        _seqAudioTrackInfoWidgetTiledAction.SetState(GLib.Variant.NewBoolean(false));
        _seqAudioTrackInfoWidgetWindowedAction.SetState(GLib.Variant.NewBoolean(false));
        _seqAudioTrackInfoWidgetHideAction.SetState(GLib.Variant.NewBoolean(true));
    }

    private void SeqAudioList_GetTiled(object sender, EventArgs args)
    {
        if (_seqAudioListWidgetTiledAction.GetState()!.GetBoolean() == true)
            return;

        if (_sequencedAudioListWindow is not null)
        {
            if (_sequencedAudioListWindow.WidgetBox.GetFirstChild() is not null)
            {
                _sequencedAudioListWindow.WidgetBox.Remove(_sequencedAudioList);
            }
            _sequencedAudioListWindow.OnCloseRequest -= SeqAudioListWindow_CloseRequest;
            _sequencedAudioListWindow.Dispose();
            _sequencedAudioListWindow.Close();
            if (_sequencedAudioListWindow is not null)
            {
                _sequencedAudioListWindow = null!;
            }
        }

        _sequencedAudioListBox.Append(_sequencedAudioList);

        _seqAudioListWidgetTiledAction.SetState(GLib.Variant.NewBoolean(true));
        _seqAudioListWidgetWindowedAction.SetState(GLib.Variant.NewBoolean(false));
        _seqAudioListWidgetHideAction.SetState(GLib.Variant.NewBoolean(false));
    }

    private void SeqAudioList_GetWindowed(object sender, EventArgs args)
    {
        if (_seqAudioListWidgetWindowedAction.GetState()!.GetBoolean() == true)
            return;

        if (_sequencedAudioListBox.GetFirstChild() is not null)
        {
            _sequencedAudioListBox.Remove(_sequencedAudioList);
        }
        _sequencedAudioListWindow ??= new WidgetWindow(_sequencedAudioList);
        _sequencedAudioListWindow.OnCloseRequest += SeqAudioListWindow_CloseRequest;
        _sequencedAudioListWindow.Present();

        _seqAudioListWidgetTiledAction.SetState(GLib.Variant.NewBoolean(false));
        _seqAudioListWidgetWindowedAction.SetState(GLib.Variant.NewBoolean(true));
        _seqAudioListWidgetHideAction.SetState(GLib.Variant.NewBoolean(false));
    }

    private void SeqAudioList_Hide(object sender, EventArgs args)
    {
        if (_seqAudioListWidgetHideAction.GetState()!.GetBoolean() == true)
            return;

        if (_sequencedAudioListWindow is not null)
        {
            if (_sequencedAudioListWindow.WidgetBox.GetFirstChild() is not null)
            {
                _sequencedAudioListWindow.WidgetBox.Remove(_sequencedAudioList);
            }
            _sequencedAudioListWindow.OnCloseRequest -= SeqAudioListWindow_CloseRequest;
            _sequencedAudioListWindow.Dispose();
            _sequencedAudioListWindow.Close();
            if (_sequencedAudioListWindow is not null)
            {
                _sequencedAudioListWindow = null!;
            }
        }
        if (_sequencedAudioListBox.GetFirstChild() is not null)
        {
            _sequencedAudioListBox.Remove(_sequencedAudioList);
        }

        _seqAudioListWidgetTiledAction.SetState(GLib.Variant.NewBoolean(false));
        _seqAudioListWidgetWindowedAction.SetState(GLib.Variant.NewBoolean(false));
        _seqAudioListWidgetHideAction.SetState(GLib.Variant.NewBoolean(true));
    }

    #region Widget Window Closure Methods

    private bool PlaylistWindow_CloseRequest(Gtk.Window sender, EventArgs args)
    {
        Playlist_GetTiled(sender, args);
        return false;
    }

    private bool SeqAudioPianoWindow_CloseRequest(Gtk.Window sender, EventArgs args)
    {
        SeqAudioPiano_GetTiled(sender, args);
        return false;
    }

    private bool SeqAudioTrackInfoWindow_CloseRequest(Gtk.Window sender, EventArgs args)
    {
        SeqAudioTrackInfo_GetTiled(sender, args);
        return false;
    }

    private bool SeqAudioListWindow_CloseRequest(Gtk.Window sender, EventArgs args)
    {
        SeqAudioList_GetTiled(sender, args);
        return false;
    }
    #endregion

#region Widget Menu Enable and Disable Methods
    private void PlaylistWidgetAction_IsEnabled(bool enabled)
    {

        _playlistWidgetTiledAction.Enabled = enabled;
        _playlistWidgetWindowedAction.Enabled = enabled;
        _playlistWidgetHideAction.Enabled = enabled;
    }

    private void PianoWidgetAction_IsEnabled(bool enabled)
    {

        _seqAudioPianoWidgetTiledAction.Enabled = enabled;
        _seqAudioPianoWidgetWindowedAction.Enabled = enabled;
        _seqAudioPianoWidgetHideAction.Enabled = enabled;
    }

    private void SeqAudioTrackInfoWidgetAction_IsEnabled(bool enabled)
    {

        _seqAudioTrackInfoWidgetTiledAction.Enabled = enabled;
        _seqAudioTrackInfoWidgetWindowedAction.Enabled = enabled;
        _seqAudioTrackInfoWidgetHideAction.Enabled = enabled;
    }

    private void SeqAudioListWidgetAction_IsEnabled(bool enabled)
    {

        _seqAudioListWidgetTiledAction.Enabled = enabled;
        _seqAudioListWidgetWindowedAction.Enabled = enabled;
        _seqAudioListWidgetHideAction.Enabled = enabled;
    }
    #endregion
    #endregion

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
        if (Engine.Instance is not null)
        {
            if (_manuallyChanged is ManuallyChanged.None)
            {
                _manuallyChanged = ManuallyChanged.SpinButton;
                _autoplay = true;
                CheckIfChangedManually((int)_sequenceNumberSpinButton.Adjustment!.Value);
                _autoplay = false;
                _manuallyChanged = ManuallyChanged.None;
            }
        }
    }

    private void SequenceNumberSpinButton_ChangeValue(SpinButton sender, SpinButton.ChangeValueSignalArgs args)
    {
        int index = (int)_sequenceNumberSpinButton.Adjustment!.Value;
        if (_manuallyChanged is ManuallyChanged.None)
        {
            _manuallyChanged = ManuallyChanged.SpinButton;
            _autoplay = true;
            CheckIfChangedManually(index);
            _autoplay = false;
            _manuallyChanged = ManuallyChanged.None;
        }
    }

    private void CheckIfChangedManually(int index)
    {
        switch (_manuallyChanged)
        {
            case ManuallyChanged.Initialized:
                {
                    if (Engine.Instance!.Config.Playlists is not null)
                    {
                        PlaylistSongStringChanged(index);
                    }
                    _sequencedAudioList.SelectRow(index);
                    // _sequencedAudioList.ColumnView!.ScrollTo((uint)index, null, ListScrollFlags.None, ScrollInfo.New());
                    _sequenceNumberSpinButton.Value = index;
                    SetAndLoadSong(index);
                    // _manuallyChanged = ManuallyChanged.None;
                    break;
                }
            case ManuallyChanged.SpinButton:
                {
                    _sequencedAudioList.SelectRow(index);
                    _sequencedAudioList.ColumnView!.ScrollTo((uint)index, null, ListScrollFlags.Select, ScrollInfo.New());
                    if (Engine.Instance!.Config.Playlists is not null)
                    {
                        PlaylistSongStringChanged(index);
                    }
                    SetAndLoadSong(index);
                    break;
                }
            case ManuallyChanged.PlaylistDropDown:
                {
                    _sequencedAudioList.SelectRow(index);
                    if (!_playlistChanged)
                        _sequencedAudioList.ColumnView!.ScrollTo((uint)index, null, ListScrollFlags.Select, ScrollInfo.New());
                    _sequenceNumberSpinButton.Value = index;
                    SetAndLoadSong(index);
                    break;
                }
            case ManuallyChanged.List:
                {
                    _sequenceNumberSpinButton.Value = index;
                    if (Engine.Instance!.Config.Playlists is not null)
                    {
                        PlaylistSongStringChanged(index);
                    }
                    SetAndLoadSong(index);
                    break;
                }
            default:
                {
                    break;
                }
        }
    }

    internal static string GetProgramName() => ConfigUtils.PROGRAM_NAME;
    private void SetSongToProgramTitle(List<Config.Song> songs, int songIndex)
    {
        Title = $"{GetProgramName()} - {songs[songIndex].Name}";
    }

    internal static void ChangeIndex(int index)
    {
        // First, check if the index is identical to current song index
        // to prevent it from unexpectedly stopping
        if (index != Instance!._curSong)
        {
            if (!Instance._sequencedAudioList.HasSelectedRow)
            {
                if (Instance._manuallyChanged is ManuallyChanged.None)
                {
                    Instance._manuallyChanged = ManuallyChanged.List;
                    Instance!.CheckIfChangedManually(index);  // This will check to see if it was changed manually
                    Instance._manuallyChanged = ManuallyChanged.None;
                }
            }
            Instance._sequencedAudioList.HasSelectedRow = false;
        }
    }

    //private void SequencesListView_SelectionGet(object sender, EventArgs e)
    //{
    //	var item = _sequencedAudioList.SelectedItem;
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
        if (_configPlaylist.PlaylistDropDown!.SelectedItem is not null)
        {
            _autoplay = false;  // Must be set to false first
            CheckPlaylistItem();  // Check the playlist item, to set the dropdown to it's first song in the playlist
            _configPlaylist.PlaylistStringSelect();  // Selects the playlist item
            _playlistChanged = true;  // We set this, so that the autoplay doesn't get set while changing playlists
            _manuallyChanged = ManuallyChanged.PlaylistDropDown;
            CheckIfChangedManually(_configPlaylist.GetSongIndex(_configPlaylist.PlaylistDropDown.Selected));  // This will set and load the song
            _manuallyChanged = ManuallyChanged.None;
            _playlistChanged = false;  // Now we can set it back to false
        }
    }

    private void OnPlaylistSongStringSelected(GObject.Object sender, NotifySignalArgs args)
    {
        if (_configPlaylist.PlaylistSongDropDown.SelectedItem is not null)
        {
            if (_configPlaylist.PlaylistDropDown.Selected != _configPlaylist.SelectedPlaylist)
                Stop();
            if (_configPlaylist.PlaylistSongDropDown.Selected != _configPlaylist.SelectedSong)
            {
                CheckPlaylistItem();
                var selectedItem = (StringObject)_configPlaylist.PlaylistSongDropDown.SelectedItem;
                var selectedItemName = selectedItem.String;
                foreach (var song in _configPlaylist.Songs!)
                {
                    if (song.Name.Equals(selectedItemName))
                    {
                        _configPlaylist.SelectedSong = _configPlaylist.PlaylistSongDropDown.Selected;
                        if (_manuallyChanged is ManuallyChanged.None)
                        {
                            _manuallyChanged = ManuallyChanged.PlaylistDropDown;
                            if (!_playlistChanged)
                                _autoplay = true;
                            CheckIfChangedManually(song.Index);
                            _autoplay = false;
                            _manuallyChanged = ManuallyChanged.None;
                        }
                    }
                }
            }
        }
    }
    private void PlaylistSongStringChanged(int index)
    {
        if (_configPlaylist.PlaylistSongDropDown.SelectedItem is not null)
        {
            foreach (var song in _configPlaylist.Songs!)
            {
                if (song.Index.Equals(index))
                {
                    _configPlaylist.PlaylistSongDropDown.SetSelected(_configPlaylist.GetPlaylistSongIndex(index));
                }
            }
        }
    }
    public void SetAndLoadSong(int index)
    {
        _curSong = index;

        Stop();
        Title = GetProgramName();
        //_sequencesListView.Margin = 0;
        //_sequencedAudioTrackInfo.Reset();
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
            if (Engine.Instance.Config.Playlists is not null)
            {
                List<Config.Song> songs = cfg.Playlists![^1].Songs; // Complete "All Songs" playlist is present in all configs at the last index value
                int songIndex = songs.FindIndex(s => s.Index == index);
                if (songIndex != -1)
                {
                    SetSongToProgramTitle(songs, songIndex); // Done! It's now a func
                    PlaylistSongStringChanged(index);
                    CheckPlaylistItem();
                }
            }
            else
            {
                List<Config.Song> songs = cfg.InternalSongNames![0].Songs;
                int songIndex = songs.FindIndex(s => s.Index == index);
                if (songIndex != -1)
                {
                    SetSongToProgramTitle(songs, songIndex);
                }
            }
            _positionBar.Adjustment!.Upper = loadedSong!.MaxTicks;
            _positionBar.SetRange(0, loadedSong.MaxTicks);
            _sequencedAudioTrackInfo.SetNumTracks(loadedSong.Events.Length);
            _sequencedAudioTrackInfo.AddTrackInfo();
            if (_autoplay)
            {
                Play();
            }
        }
        else
        {
            _sequencedAudioTrackInfo.SetNumTracks(0);
        }
        _positionBar.Sensitive = _exportWAVAction.Enabled = success;
        _exportMIDIAction.Enabled = success && MP2KEngine.MP2KInstance is not null;
        _exportDLSAction.Enabled = _exportSF2Action.Enabled = success && AlphaDreamEngine.AlphaDreamInstance is not null;
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
        _sequenceNumberSpinButton.Sensitive = /* _sequencedAudioListBox.Sensitive = */ spinButtonAndListBoxEnabled;
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
            _configPlaylist.PlaylistDropDown!.Sensitive =
            _configPlaylist.ButtonPrevPlistSong.Sensitive =
            _configPlaylist.PlaylistSongDropDown!.Sensitive =
            _configPlaylist.ButtonNextPlistSong.Sensitive = true;
        _exportDLSAction.Enabled = false;
        _exportMIDIAction.Enabled = true;
        _exportSF2Action.Enabled = false;
        PlaylistWidgetAction_IsEnabled(true);
        PianoWidgetAction_IsEnabled(true);
        SeqAudioTrackInfoWidgetAction_IsEnabled(true);
        SeqAudioListWidgetAction_IsEnabled(true);
        // _sequencedAudioTrackInfo.Init();
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
        // _sequencedAudioTrackInfo.AddEntries();

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
        source.SetCallback(TimerCallback); // Sets the callback for the timer interval to be used on
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
            _configPlaylist.PlaylistSongDropDown.Selected -= 1;
            _manuallyChanged = ManuallyChanged.PlaylistDropDown;
            _autoplay = true;
            CheckIfChangedManually(_configPlaylist.Songs![(int)_configPlaylist.PlaylistSongDropDown.Selected].Index);
            _autoplay = false;
            _manuallyChanged = ManuallyChanged.None;
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
            _configPlaylist.PlaylistSongDropDown.Selected += 1;
            _manuallyChanged = ManuallyChanged.PlaylistDropDown;
            _autoplay = true;
            CheckIfChangedManually(_configPlaylist.Songs![(int)_configPlaylist.PlaylistSongDropDown.Selected].Index);
            _autoplay = false;
            _manuallyChanged = ManuallyChanged.None;
            CheckPlaylistItem();
        }
    }

    private void CheckPlaylistItem()
    {
        // For the Previous Song button
        if (_configPlaylist.PlaylistSongDropDown.Selected is 0)
            _configPlaylist.ButtonPrevPlistSong.Sensitive = false;
        else
            _configPlaylist.ButtonPrevPlistSong.Sensitive = true;

        // For the Next Song button
        if (_configPlaylist.PlaylistSongDropDown.Selected == _configPlaylist.GetNumSongs() - 1)
            _configPlaylist.ButtonNextPlistSong.Sensitive = false;
        else
            _configPlaylist.ButtonNextPlistSong.Sensitive = true;
    }

    private void FinishLoading(long numSongs)
    {
        Engine.Instance!.Player.SongEnded += SongEnded;
        _sequencedAudioList.Show();
        var config = Engine.Instance.Config;
        _sequencedAudioList.AddEntries(numSongs, config.InternalSongNames, config.Playlists, config.SongTableOffset!);
        _sequencedAudioList.Init();
        if (config.Playlists is not null)
        {
            _configPlaylist.AddEntries(config.Playlists);
            _configPlaylist.PlaylistDropDown.OnNotify += OnPlaylistStringSelected;
            _configPlaylist.PlaylistSongDropDown.OnNotify += OnPlaylistSongStringSelected;
        }
        //foreach (Config.Playlist playlist in Engine.Instance.Config.Playlists)
        //{
        //	_sequencedAudioListBox.Insert(Label.New(playlist.Name), playlist.Songs.Count);
        //	_sequencedAudioList.Add(new SoundSequenceListItem(playlist));
        //	_sequencedAudioList.AddRange(playlist.Songs.Select(s => new SoundSequenceListItem(s)).ToArray());
        //}
        _sequenceNumberSpinButton.Adjustment!.Upper = numSongs;
#if DEBUG
        // [Debug methods specific to this GUI will go in here]
#endif
        _autoplay = false;
        _manuallyChanged = ManuallyChanged.Initialized;
        CheckIfChangedManually(Engine.Instance.Config.Playlists[^1].Songs.Count == 0 ? 0 : Engine.Instance.Config.Playlists[^1].Songs[0].Index);
        _manuallyChanged = ManuallyChanged.None;
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
        _sequencedAudioTrackInfo.SetNumTracks(0);
        _sequencedAudioTrackInfo.ResetMutes();
        ResetPlaylistStuff(false);
        UpdatePositionIndicators(0L);
        //_signal.Connect(_sequencesListFactory, SequencesListView_SelectionGet, false, null);
        // _sequenceNumberSpinButton.OnValueChanged -= SequenceNumberSpinButton_ValueChanged;
        _sequenceNumberSpinButton.Visible = false;
        _sequenceNumberSpinButton.Value = _sequenceNumberSpinButton.Adjustment!.Upper = 0;
        //_sequencesListView.Selection.SelectFunction = null;
        //_sequencesColumnView.Unref();
        //_signal.Connect(_sequencesListFactory, SequencesListView_SelectionGet, true, null);
        // _sequenceNumberSpinButton.OnValueChanged += SequenceNumberSpinButton_ValueChanged;
    }

    private bool TimerCallback()
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
                Player player = Engine.Instance!.Player;
                SongState info = _sequencedAudioTrackInfo.Info!;
                player.UpdateSongState(info);
                _piano.UpdateKeys(info.Tracks, _sequencedAudioTrackInfo.NumTracks!);
                UpdatePositionIndicators(player.ElapsedTicks);
            }
        }
        return true;
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

    private void OpenTrackViewer(Gio.SimpleAction sender, Gio.SimpleAction.ActivateSignalArgs args)
    {
        if (_trackViewer is not null)
        {
            _trackViewer.FocusVisible = true;
        }

        _trackViewer = new TrackViewer();
        _trackViewer.Present();

        _trackViewer.OnCloseRequest += TrackViewer_WindowClosed;
    }

    private bool TrackViewer_WindowClosed(Gtk.Window sender, EventArgs args)
    {
        _trackViewer.Dispose();
        _trackViewer = null!;
        return false;
    }
}
