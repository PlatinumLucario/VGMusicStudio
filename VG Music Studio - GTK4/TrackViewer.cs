using System;
using Adw;

namespace Kermalis.VGMusicStudio.GTK4;

internal sealed class TrackViewer : Window
{
    private readonly Gio.ListStore Model = Gio.ListStore.New(GetGType());
    private Gtk.NoSelection? SelectionModel { get; set; }
    internal Gtk.ColumnView? ColumnView { get; set; }

    internal TrackViewer()
    {
        New();

        Title = MainWindow.GetProgramName() + " — " + "Track Viewer";

        var header = HeaderBar.New();
        header.SetShowEndTitleButtons(true);
        header.SetShowStartTitleButtons(true);
        
        var viewport = Gtk.Viewport.New(Gtk.Adjustment.New(0, -1, -1, 1, 1, 1), Gtk.Adjustment.New(0, -1, -1, 1, 1, 1));
        var scrolledWindow = Gtk.ScrolledWindow.New();
        scrolledWindow.SetSizeRequest(700, 200);
        scrolledWindow.SetHexpand(true);

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

        SetVexpand(true);
        SetHexpand(true);

        SetContent(mainBox);
    }
}