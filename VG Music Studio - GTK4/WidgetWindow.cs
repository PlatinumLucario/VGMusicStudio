using Adw;

namespace Kermalis.VGMusicStudio.GTK4;

internal class WidgetWindow : Window
{
    internal Gtk.Box WidgetBox;
    internal WidgetWindow(Gtk.Widget widget)
    {
        New();

        Title = MainWindow.GetProgramName();

        var header = HeaderBar.New();
        WidgetBox = Gtk.Box.New(Gtk.Orientation.Vertical, 0);
        WidgetBox.Append(widget);

        var box = Gtk.Box.New(Gtk.Orientation.Vertical, 0);
        box.Append(header);
        box.Append(WidgetBox);
        
        SetContent(box);
    }
}