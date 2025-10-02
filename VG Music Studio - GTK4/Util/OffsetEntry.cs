
using System;

namespace Kermalis.VGMusicStudio.GTK4.Util;

internal class OffsetEntry : Gtk.Box
{
    private Gtk.Label _hexLabel;
    internal Gtk.Entry Entry;
    internal OffsetEntry(int offset = 0)
    {
        New(Gtk.Orientation.Horizontal, 3);
        _hexLabel = Gtk.Label.New("0x");
        Entry = Gtk.Entry.New();
        Entry.Text_ = $"{offset:X7}";
        Entry.OnChanged += Offset_OnChanged;
        Append(_hexLabel);
        Append(Entry);
    }

    private void Offset_OnChanged(Gtk.Editable sender, EventArgs args)
    {
        Entry.OnChanged -= Offset_OnChanged;
        var text = sender.GetText();
        for (int i = 0; i < text.Length; i++)
        {
            bool isValidHexValue = false;
            if (char.IsDigit(text[i]))
            {
                isValidHexValue = true;
            }
            else
            {
                switch (text[i])
                {
                    case 'A':
                    case 'a':
                        isValidHexValue = true;
                        break;
                    case 'B':
                    case 'b':
                        isValidHexValue = true;
                        break;
                    case 'C':
                    case 'c':
                        isValidHexValue = true;
                        break;
                    case 'D':
                    case 'd':
                        isValidHexValue = true;
                        break;
                    case 'E':
                    case 'e':
                        isValidHexValue = true;
                        break;
                    case 'F':
                    case 'f':
                        isValidHexValue = true;
                        break;
                }
            }

            if (!isValidHexValue)
            {
                text.Remove(i, 1);
            }

            Entry.Text_ = $"{text:X7}";
        }
        Entry.OnChanged += Offset_OnChanged;
    }

    internal void SetValue(long offset)
    {
        Entry.Text_ = $"{offset:X7}";
    }
}