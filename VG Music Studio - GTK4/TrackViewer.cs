using System;
using Adw;
using Kermalis.VGMusicStudio.Core;
using Kermalis.VGMusicStudio.Core.Properties;

namespace Kermalis.VGMusicStudio.GTK4;

internal sealed class TrackViewer : Window
{
    private readonly string? EventType;
    private readonly string? Arguments;
    private readonly long Offset;
    private readonly long[] Ticks;

    private Gtk.StringList TrackList;
    private Gtk.DropDown TrackDropDown;
    private readonly Gio.ListStore Model = Gio.ListStore.New(GetGType());
    private Gtk.NoSelection? SelectionModel { get; set; }
    internal Gtk.ColumnView? ColumnView { get; set; }
    internal TrackViewer[]? TrackData { get; set; }

    private TrackViewer(string eventType, string arguments, long offset, Span<long> ticks)
    : base()
    {
        EventType = eventType;
        Arguments = arguments;
        Offset = offset;
        Ticks = ticks.ToArray();
    }
    internal TrackViewer()
    {
        New();

        Title = $"{MainWindow.GetProgramName()} — {Strings.TrackViewerTitle}";

        var header = HeaderBar.New();
        header.SetShowEndTitleButtons(true);
        header.SetShowStartTitleButtons(true);

        TrackList = Gtk.StringList.New(null);
        TrackDropDown = new Gtk.DropDown
        {
            WidthRequest = 100
        };
        TrackDropDown.SetModel(TrackList);
        TrackDropDown.OnNotify += TrackSelected;

        var viewport = Gtk.Viewport.New(Gtk.Adjustment.New(0, -1, -1, 1, 1, 1), Gtk.Adjustment.New(0, -1, -1, 1, 1, 1));
        var scrolledWindow = Gtk.ScrolledWindow.New();
        scrolledWindow.SetSizeRequest(700, 500);
        scrolledWindow.SetHexpand(true);
        scrolledWindow.SetVexpand(true);

        SelectionModel = Gtk.NoSelection.New(Model);

        ColumnView = Gtk.ColumnView.New(SelectionModel);
        ColumnView.AddCssClass("data-table");
        ColumnView.SetShowColumnSeparators(true);
        ColumnView.SetShowRowSeparators(true);
        ColumnView.SetReorderable(false);
        ColumnView.SetHexpand(true);

        scrolledWindow.SetChild(ColumnView);

        viewport.Child = scrolledWindow;

        var mainBox = Gtk.Box.New(Gtk.Orientation.Vertical, 6);
        mainBox.Append(header);
        mainBox.Append(TrackDropDown);
        mainBox.Append(viewport);

        SetVexpand(true);
        SetHexpand(true);

        SetContent(mainBox);
    }

    internal void Init()
    {
        // Event Column
        var listItemFactory = Gtk.SignalListItemFactory.New();
        listItemFactory.OnSetup += (_, args) => OnSetupLabel(args, Gtk.Align.Center);
        listItemFactory.OnBind += (_, args) => OnBindText(args, (ud) => ud.EventType!);

        var eventColumn = Gtk.ColumnViewColumn.New("Event", listItemFactory);
        eventColumn.SetFixedWidth(100);
        eventColumn.SetExpand(true);
        eventColumn.SetResizable(true);
        ColumnView!.AppendColumn(eventColumn);

        // Arguments Column
        listItemFactory = Gtk.SignalListItemFactory.New();
        listItemFactory.OnSetup += (_, args) => OnSetupLabel(args, Gtk.Align.Center);
        listItemFactory.OnBind += (_, args) => OnBindText(args, (ud) => ud.Arguments!);

        var argumentsColumn = Gtk.ColumnViewColumn.New("Arguments", listItemFactory);
        argumentsColumn.SetFixedWidth(100);
        argumentsColumn.SetExpand(true);
        argumentsColumn.SetResizable(true);
        ColumnView.AppendColumn(argumentsColumn);

        // Offset Column
        listItemFactory = Gtk.SignalListItemFactory.New();
        listItemFactory.OnSetup += (_, args) => OnSetupLabel(args, Gtk.Align.Center);
        listItemFactory.OnBind += (_, args) => OnBindOffsetText(args, (ud) => ud.Offset);

        var offsetColumn = Gtk.ColumnViewColumn.New("Offset", listItemFactory);
        offsetColumn.SetFixedWidth(100);
        offsetColumn.SetExpand(true);
        offsetColumn.SetResizable(true);
        ColumnView!.AppendColumn(offsetColumn);

        // Ticks Column
        listItemFactory = Gtk.SignalListItemFactory.New();
        listItemFactory.OnSetup += (_, args) => OnSetupLabel(args, Gtk.Align.Center);
        listItemFactory.OnBind += (_, args) => OnBindTicksText(args, (ud) => ud.Ticks);

        var ticksColumn = Gtk.ColumnViewColumn.New("Ticks", listItemFactory);
        ticksColumn.SetFixedWidth(100);
        ticksColumn.SetExpand(true);
        ticksColumn.SetResizable(true);
        ColumnView.AppendColumn(ticksColumn);
    }

    public void ReloadDropDownEntries()
    {
        if (TrackList.NItems is not 0)
        {
            TrackList.Splice(0, TrackList.NItems, null);
        }

        for (int i = 0; i < Engine.Instance?.Player.LoadedSong?.Events.Length; i++)
        {
            TrackList.Append($"Track {i}");
        }
    }

    public void ReloadColumnEntries()
    {
        if (Model.GetNItems() is not 0)
            Model.RemoveAll();

        if (Engine.Instance is null) return;
        if (Engine.Instance.Player.LoadedSong is null) return;

        TrackData = new TrackViewer[Engine.Instance.Player.LoadedSong.Events[TrackDropDown.Selected]!.Count];
        int i = 0;
        foreach (var trackEvent in Engine.Instance.Player.LoadedSong.Events[TrackDropDown.Selected]!)
        {
            var numTicks = new long[trackEvent.Ticks.Count];
            int t = 0;
            foreach (var ticks in trackEvent.Ticks)
            {
                numTicks[t++] = ticks;
            }
            TrackData[i++] = new TrackViewer(trackEvent.Command.Label, trackEvent.Command.Arguments, trackEvent.Offset, numTicks);
        }

        foreach (var data in TrackData!)
        {
            Model.Append(data);
        }
    }
    
    private void TrackSelected(GObject.Object sender, NotifySignalArgs args)
    {
        if (TrackDropDown.SelectedItem is not null)
        {
            ReloadColumnEntries();
        }
    }

    private static void OnSetupLabel(Gtk.SignalListItemFactory.SetupSignalArgs args, Gtk.Align align)
    {
        if (args.Object is not Gtk.ListItem listItem)
        {
            return;
        }

        var label = Gtk.Label.New(null);
        label.Halign = align;
        listItem.Child = label;
    }

    private static void OnBindText(Gtk.SignalListItemFactory.BindSignalArgs args, Func<TrackViewer, string> getText)
    {
        if (args.Object is not Gtk.ListItem listItem)
        {
            return;
        }

        if (listItem.Child is not Gtk.Label label) return;
        if (listItem.Item is not TrackViewer userData) return;

        label.SetText(getText(userData));
    }
    private static void OnBindOffsetText(Gtk.SignalListItemFactory.BindSignalArgs args, Func<TrackViewer, long> getValue)
    {
        if (args.Object is not Gtk.ListItem listItem)
        {
            return;
        }

        if (listItem.Child is not Gtk.Label label) return;
        if (listItem.Item is not TrackViewer userData) return;

        label.SetText(string.Format("0x{0:X}", getValue(userData)));
    }
    private static void OnBindTicksText(Gtk.SignalListItemFactory.BindSignalArgs args, Func<TrackViewer, long[]> getValue)
    {
        if (args.Object is not Gtk.ListItem listItem)
        {
            return;
        }

        if (listItem.Child is not Gtk.Label label) return;
        if (listItem.Item is not TrackViewer userData) return;
        
        var array = getValue(userData);
        var str = "";
        foreach (var val in array)
        {
            str = String.Join(", ", val);
        }

        label.SetText(str);
    }
}