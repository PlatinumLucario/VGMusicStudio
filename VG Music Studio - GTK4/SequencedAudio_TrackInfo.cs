using System;
using System.Collections.Generic;
using Cairo;
using Gtk;
using Kermalis.VGMusicStudio.Core;
using Kermalis.VGMusicStudio.Core.Properties;
using Kermalis.VGMusicStudio.Core.Util;

namespace Kermalis.VGMusicStudio.GTK4;

internal class SequencedAudio_TrackInfo : Box
{
    private Label? TempoLabel { get; set; }
    private SpinButton TempoSpinButton { get; set; }

    private CheckButton? TrackToggleCheckButtonHeader { get; set; }
    private Label? VelocityHeader { get; set; }

    private CheckButton[]? TrackToggleCheckButton { get; set; }
    private Label[]? PositionLabel { get; set; }
    private Label[]? RestLabel { get; set; }
    private Label[]? VoiceLabel { get; set; }
    private Label[]? NotesLabel { get; set; }
    private Label[]? PanpotLabel { get; set; }
    private Label[]? VolumeLabel { get; set; }
    private Label[]? LFOLabel { get; set; }
    private Label[]? PitchBendLabel { get; set; }
    private Label[]? ExtraLabel { get; set; }
    private VelocityBar[]? Velocity { get; set; }
    private Label[]? TypeLabel { get; set; }

    private ListBox? ListBox { get; set; }
    private static readonly List<string> KeysCache = new(128);

    public readonly bool[]? NumTracks;
    public readonly SongState? Info;
    internal static int NumTracksToDraw;

    public SequencedAudio_TrackInfo[]? TrackInfo { get; set; }

    internal SequencedAudio_TrackInfo()
    {
        NumTracks = new bool[SongState.MAX_TRACKS];
        for (int i = 0; i < SongState.MAX_TRACKS; i++)
            NumTracks[i] = true;

        Info = new SongState();
        
        TempoLabel = Label.New(Strings.PlayerTempo);
        TempoSpinButton = SpinButton.New(Adjustment.New(Info.Tempo, 0, ushort.MaxValue, 1, 10, 0), 0, 0);
        TempoSpinButton.SetNumeric(true);
        TempoSpinButton.OnValueChanged += ChangeTempo;
        TempoSpinButton.OnChangeValue += ChangeTempo;
        var tempoBox = New(Orientation.Horizontal, 4);
        tempoBox.Append(TempoLabel);
        tempoBox.Append(TempoSpinButton);
        tempoBox.SetHalign(Align.Center);
        tempoBox.MarginStart = 100;
        tempoBox.MarginEnd = 100;
        var listHeader = CreateListHeader();
        var viewport = Viewport.New(Adjustment.New(0, -1, -1, 1, 1, 1), Adjustment.New(0, -1, -1, 1, 1, 1));
        var scrolledWindow = ScrolledWindow.New();
        scrolledWindow.SetSizeRequest(700, 150);
        scrolledWindow.SetHexpand(true);
        scrolledWindow.SetVexpand(true);

        ListBox = ListBox.New();
        ListBox.SetHexpand(true);
        ListBox.SetSelectionMode(SelectionMode.None);

        scrolledWindow.SetChild(ListBox);

        viewport.Child = scrolledWindow;

        SetOrientation(Orientation.Vertical);
        Append(tempoBox);
        Append(listHeader);
        Append(viewport);
        SetVexpand(true);
        SetHexpand(true);
        SetNumTracks(0);
        ConfigureTimer();
    }

    private void ChangeTempo(SpinButton sender, EventArgs args)
    {
        if (Engine.Instance is not null)
        {
            Engine.Instance.Player.Tempo = (ushort)TempoSpinButton.Value;
        }
    }

    internal static void SetNumTracks(int num) => NumTracksToDraw = num;

    private void ConfigureTimer()
    {
        var timer = GLib.Timer.New(); // Creates a new timer variable
        var context = GLib.MainContext.GetThreadDefault(); // Reads the main context default thread
        var source = GLib.Functions.TimeoutSourceNew(1); // Creates and configures the timeout interval at 1 microsecond, so it updates in real time
        source.SetCallback(TrackTimerCallback); // Sets the callback for the timer interval to be used on
        var microsec = (ulong)source.Attach(context); // Configures the microseconds based on attaching the GLib MainContext thread
        timer.Elapsed(ref microsec); // Adds the pointer to the configured microseconds source
        timer.Start(); // Starts the timer
    }

    private bool TrackTimerCallback()
    {
        if (TrackToggleCheckButton is not null &&
            PositionLabel is not null &&
            RestLabel is not null &&
            VoiceLabel is not null &&
            NotesLabel is not null &&
            PanpotLabel is not null &&
            VolumeLabel is not null &&
            LFOLabel is not null &&
            PitchBendLabel is not null &&
            ExtraLabel is not null &&
            Velocity is not null &&
            TypeLabel is not null)
        {
            if (PositionLabel.Length == 0) return true;
            for (int i = 0; i < NumTracksToDraw; i++)
            {
                if (TrackToggleCheckButton[i] is not null &&
                    PositionLabel[i] is not null &&
                    RestLabel[i] is not null &&
                    VoiceLabel[i] is not null &&
                    NotesLabel[i] is not null &&
                    PanpotLabel[i] is not null &&
                    VolumeLabel[i] is not null &&
                    LFOLabel[i] is not null &&
                    PitchBendLabel[i] is not null &&
                    ExtraLabel[i] is not null &&
                    Velocity[i] is not null &&
                    TypeLabel[i] is not null)
                {
                    if (Engine.Instance!.Player.State is not PlayerState.Stopped)
                    {
                        ToggleTrack(i, TrackToggleCheckButton[i].Active);
                        PositionLabel[i].SetText(string.Format("0x{0:X}", Info.Tracks[i].Position));
                        RestLabel[i].SetText(Info.Tracks[i].Rest.ToString());
                        VoiceLabel[i].SetText(Info.Tracks[i].Voice.ToString());
                        NotesLabel[i].SetText(GetNote(Info.Tracks[i]));
                        PanpotLabel[i].SetText(Info.Tracks[i].Panpot.ToString());
                        VolumeLabel[i].SetText(Info.Tracks[i].Volume.ToString());
                        LFOLabel[i].SetText(Info.Tracks[i].LFO.ToString());
                        PitchBendLabel[i].SetText(Info.Tracks[i].PitchBend.ToString());
                        ExtraLabel[i].SetText(Info.Tracks[i].Extra.ToString());
                        VelocityHeader!.WidthRequest = GetWidth() / 4;
                        Velocity[i].WidthRequest = GetWidth() / 4;
                        Velocity[i].UpdateColor(Info.Tracks[i]);
                        Velocity[i].QueueDraw();
                        // Visualizer[i].Update(Info.Tracks[i]);
                        if (Info.Tracks[i].Type is not null)
                            TypeLabel[i].SetText(Info.Tracks[i].Type);
                    }
                    else
                    {
                        ToggleTrack(i, TrackToggleCheckButton[i].Active);
                        PositionLabel[i].SetText(string.Format("0x{0:X}", 0));
                        RestLabel[i].SetText(0.ToString());
                        VoiceLabel[i].SetText(0.ToString());
                        NotesLabel[i].SetText("");
                        PanpotLabel[i].SetText(0.ToString());
                        VolumeLabel[i].SetText(0.ToString());
                        LFOLabel[i].SetText(0.ToString());
                        PitchBendLabel[i].SetText(0.ToString());
                        ExtraLabel[i].SetText(Info.Tracks[i].Extra.ToString());
                        VelocityHeader!.WidthRequest = GetWidth() / 4;
                        Velocity[i].WidthRequest = GetWidth() / 4;
                        Velocity[i].UpdateColor(Info.Tracks[i]);
                        Velocity[i].QueueDraw();
                        if (Info.Tracks[i].Type is not null)
                            TypeLabel[i].SetText("");
                    }
                }
            }
        }
        return true;
    }

    private class VelocityBar : DrawingArea
    {
        private SongState.Track? Track;
        private HSLColor Color;
        internal VelocityBar()
        {
            Color = new HSLColor();
            SetHexpand(true);
            SetVexpand(true);
            SetDrawFunc(DrawVisualizerBar);
        }

        internal void UpdateColor(SongState.Track track)
        {
            Track = track;
            if (GlobalConfig.Instance is not null) // Nullability check
                Color = new HSLColor(GlobalConfig.Instance.Colors[track.Voice]);
        }

        private void DrawVisualizerBar(DrawingArea drawingArea, Context cr, int width, int height)
        {
            // cr.LineWidth = 3;

            DrawLineL(cr, width, height);
            DrawLineR(cr, width, height);

            cr.Save();

            cr.SetSourceRgb(0.5, 0.5, 0.5);

            cr.Rectangle(width / 2.0, 0, 5 * (width / 599f), height);
            cr.Fill();
            cr.Restore();
            // DrawRounded(cr, (width / 2.0) - 20, 150);

            cr.Restore();
        }

        private void DrawLineL(Context cr, int width, int height)
        {
            DrawTrough(cr, height, width / 2, -(width / 3));
            if (Track is not null)
            {
                DrawVolumeLine(cr, height, width / 2, -(Track.LeftVolume * ((width / 3) + (width / 9))));
            }
            DrawText(cr, height, (width / 2) - (width / 3) - 8, (width / 2) - (width / 3) - (width / 9) - 7.5, "-1.0", "L");
            DrawOverampLine(cr, height, (width / 2) - (width / 3), -(width / 9));
        }

        private void DrawLineR(Context cr, int width, int height)
        {
            DrawTrough(cr, height, (width / 2) + (5 * (width / 599f)), width / 3);
            if (Track is not null)
            {
                DrawVolumeLine(cr, height, (width / 2) + (5 * (width / 599f)), Track.LeftVolume * ((width / 3) + (width / 9)));
            }
            DrawText(cr, height, (width / 2) + (5 * (width / 599f)) + (width / 3) - 8, width / 1.045, "+1.0", "R");
            DrawOverampLine(cr, height, (width / 2) + (5 * (width / 599f)) + (width / 3), width / 9);
        }

        private void DrawTrough(Context cr, int height, float pos, double length)
        {
            cr.Save();
            cr.SetSourceRgba(0.5, 0.5, 0.5, 0.5);
            cr.LineWidth = 5;
            cr.MoveTo(pos, height / 2);
            cr.LineTo(pos + length, height / 2);
            cr.Stroke();
            cr.Restore();
        }

        private void DrawVolumeLine(Context cr, int height, float pos, float length)
        {
            cr.Save();
            cr.SetSourceRgb(Color.R, Color.G, Color.B);
            cr.LineWidth = 5;
            cr.MoveTo(pos, height / 2);
            cr.LineTo(pos + length, height / 2);
            cr.Stroke();
            cr.Restore();
        }

        private void DrawText(Context cr, double height, double posVolLabel, double posChLabel, string volumeLabel, string channelLabel)
        {
            cr.Save();
            cr.SetSourceRgb(0.5, 0.5, 0.5);
            cr.SelectFontFace("Sans", FontSlant.Normal, FontWeight.Normal);
            cr.SetFontSize(7.0);
            cr.MoveTo(posVolLabel, height);
            cr.ShowText(volumeLabel);
            cr.Restore();

            cr.Save();
            cr.SetSourceRgb(0.5, 0.5, 0.5);
            cr.SelectFontFace("Sans", FontSlant.Normal, FontWeight.Normal);
            cr.SetFontSize(8.0);
            cr.MoveTo(posChLabel, height / 1.5);
            cr.ShowText(channelLabel);
            cr.Restore();
        }

        private void DrawOverampLine(Context cr, int height, float pos, int length)
        {
            cr.Save();
            cr.SetSourceRgba(1.0, 0.0, 0.0, 0.5);
            cr.LineWidth = 5;
            cr.MoveTo(pos, height / 2);
            cr.LineTo(pos + length, height / 2);
            cr.Stroke();
            cr.ClosePath();
            cr.Restore();
        }
    }

    private static string GetNote(SongState.Track track)
    {
        string key = "";
        if (track.Keys[0] == byte.MaxValue)
        {
            if (track.PreviousKeysTime != 0)
            {
                track.PreviousKeysTime--;
                key = track.PreviousKeys;
            }
            else
            {
                key = string.Empty;
            }
        }
        else // Keys are held down
        {
            KeysCache.Clear();
            string noteName = "";
            for (int nk = 0; nk < SongState.MAX_KEYS; nk++)
            {
                byte k = track.Keys[nk];
                if (k == byte.MaxValue)
                {
                    break;
                }

                noteName = ConfigUtils.GetKeyName(k);
                if (nk != 0)
                {
                    KeysCache.Add(' ' + noteName);
                }
                else
                {
                    KeysCache.Add(noteName);
                }
            }
            foreach (var k in KeysCache)
            {
                if (k == noteName)
                {
                    key = k;
                }
            }

            track.PreviousKeysTime = GlobalConfig.Instance.RefreshRate << 2;
            track.PreviousKeys = key;
        }
        return key;
    }
    private Box CreateListHeader()
    {
        var columns = New(Orientation.Horizontal, 4);
        columns.SetHexpand(true);

        TrackToggleCheckButtonHeader = CheckButton.New();
        TrackToggleCheckButtonHeader.Active = true;
        TrackToggleCheckButtonHeader.OnToggled += ToggleAllTracks;
        TrackToggleCheckButtonHeader.SetMarginStart(2); // So that the starting margin is aligned with the check buttons in the list box
        TrackToggleCheckButtonHeader.SetHalign(Align.Start);
        columns.Append(TrackToggleCheckButtonHeader);

        var positionLabelHeader = Label.New(Strings.PlayerPosition);
        positionLabelHeader.SetMaxWidthChars(1);
        positionLabelHeader.SetEllipsize(Pango.EllipsizeMode.End);
        positionLabelHeader.WidthRequest = 80;
        positionLabelHeader.SetHexpand(true);
        columns.Append(positionLabelHeader);

        var restLabelHeader = Label.New(Strings.PlayerRest);
        restLabelHeader.SetMaxWidthChars(1);
        restLabelHeader.SetEllipsize(Pango.EllipsizeMode.End);
        restLabelHeader.WidthRequest = 30;
        restLabelHeader.SetHexpand(true);
        columns.Append(restLabelHeader);

        var voiceLabelHeader = Label.New("Voice");
        voiceLabelHeader.SetMaxWidthChars(1);
        voiceLabelHeader.SetEllipsize(Pango.EllipsizeMode.End);
        voiceLabelHeader.WidthRequest = 30;
        voiceLabelHeader.SetHexpand(true);
        columns.Append(voiceLabelHeader);

        var notesLabelHeader = Label.New(Strings.PlayerNotes);
        notesLabelHeader.SetMaxWidthChars(1);
        notesLabelHeader.SetEllipsize(Pango.EllipsizeMode.End);
        notesLabelHeader.WidthRequest = 30;
        notesLabelHeader.SetHexpand(true);
        columns.Append(notesLabelHeader);

        var panpotLabelHeader = Label.New("Panpot");
        panpotLabelHeader.SetMaxWidthChars(1);
        panpotLabelHeader.SetEllipsize(Pango.EllipsizeMode.End);
        panpotLabelHeader.WidthRequest = 30;
        panpotLabelHeader.SetHexpand(true);
        columns.Append(panpotLabelHeader);

        var volumeLabelHeader = Label.New("Volume");
        volumeLabelHeader.SetMaxWidthChars(1);
        volumeLabelHeader.SetEllipsize(Pango.EllipsizeMode.End);
        volumeLabelHeader.WidthRequest = 30;
        volumeLabelHeader.SetHexpand(true);
        columns.Append(volumeLabelHeader);

        var lfoLabelHeader = Label.New("LFO");
        lfoLabelHeader.SetMaxWidthChars(1);
        lfoLabelHeader.SetEllipsize(Pango.EllipsizeMode.End);
        lfoLabelHeader.WidthRequest = 30;
        lfoLabelHeader.SetHexpand(true);
        columns.Append(lfoLabelHeader);

        var pitchBendLabelHeader = Label.New("Pitch Bend");
        pitchBendLabelHeader.SetMaxWidthChars(1);
        pitchBendLabelHeader.SetEllipsize(Pango.EllipsizeMode.End);
        pitchBendLabelHeader.WidthRequest = 30;
        pitchBendLabelHeader.SetHexpand(true);
        columns.Append(pitchBendLabelHeader);

        var extraLabelHeader = Label.New("Extra");
        extraLabelHeader.SetMaxWidthChars(1);
        extraLabelHeader.SetEllipsize(Pango.EllipsizeMode.End);
        extraLabelHeader.WidthRequest = 30;
        extraLabelHeader.SetHexpand(true);
        columns.Append(extraLabelHeader);

        VelocityHeader = Label.New("");
        VelocityHeader.SetMaxWidthChars(1);
        VelocityHeader.WidthRequest = GetWidth() / 4;
        VelocityHeader.SetHexpand(true);
        columns.Append(VelocityHeader);

        var typeLabelHeader = Label.New(Strings.PlayerType);
        typeLabelHeader.SetMaxWidthChars(1);
        typeLabelHeader.SetHalign(Align.End);
        typeLabelHeader.WidthRequest = 60;
        typeLabelHeader.SetHexpand(true);
        columns.Append(typeLabelHeader);
        return columns;
    }

    private void ToggleTrack(int index, bool active)
    {
        if (active)
        {
            Engine.Instance!.Mixer.Mutes[index] = false;
            NumTracks![index] = true;
        }
        else
        {
            Engine.Instance!.Mixer.Mutes[index] = true;
            NumTracks![index] = false;
        }

        var numActive = 0;
        for (int i = 0; i < NumTracksToDraw; i++)
        {
            if (NumTracks[i])
                numActive++;
        }
        if (numActive == NumTracksToDraw)
        {
            TrackToggleCheckButtonHeader!.Inconsistent = false;
            TrackToggleCheckButtonHeader.Active = true;
        }
        else if (numActive < NumTracksToDraw && numActive is not 0)
        {
            TrackToggleCheckButtonHeader!.Inconsistent = true;
        }
        else
        {
            TrackToggleCheckButtonHeader!.Inconsistent = false;
            TrackToggleCheckButtonHeader.Active = false;
        }
    }

    private void ToggleAllTracks(CheckButton sender, EventArgs args)
    {
        if (sender.Active)
        {
            for (int i = 0; i < NumTracksToDraw; i++)
            {
                Engine.Instance!.Mixer.Mutes[i] = false;
                NumTracks![i] = TrackToggleCheckButton![i].Active = true;
            }
        }
        else
        {
            for (int i = 0; i < NumTracksToDraw; i++)
            {
                Engine.Instance!.Mixer.Mutes[i] = true;
                NumTracks![i] = TrackToggleCheckButton![i].Active = false;
            }
        }
    }

    public void AddTrackInfo()
    {
        ListBox!.RemoveAll();
        for (int i = 0; i < NumTracks!.Length; i++)
        {
            if (i < NumTracksToDraw)
                NumTracks[i] = true;
            else
                NumTracks[i] = false;
        }

        TempoSpinButton.Value = Engine.Instance!.Player.Tempo;

        TrackToggleCheckButton = new CheckButton[NumTracksToDraw];
        PositionLabel = new Label[NumTracksToDraw];
        RestLabel = new Label[NumTracksToDraw];
        VoiceLabel = new Label[NumTracksToDraw];
        NotesLabel = new Label[NumTracksToDraw];
        PanpotLabel = new Label[NumTracksToDraw];
        VolumeLabel = new Label[NumTracksToDraw];
        LFOLabel = new Label[NumTracksToDraw];
        PitchBendLabel = new Label[NumTracksToDraw];
        ExtraLabel = new Label[NumTracksToDraw];
        Velocity = new VelocityBar[NumTracksToDraw];
        TypeLabel = new Label[NumTracksToDraw];
        for (int i = 0; i < NumTracksToDraw; i++)
        {
            var columns = New(Orientation.Horizontal, 4);
            columns.SetHexpand(true);

            TrackToggleCheckButton[i] = CheckButton.New();
            TrackToggleCheckButton[i].Active = true;
            TrackToggleCheckButton[i].SetHalign(Align.Start);
            columns.Append(TrackToggleCheckButton[i]);

            PositionLabel[i] = Label.New(string.Format("0x{0:X}", Info!.Tracks[i].Position));
            PositionLabel[i].SetEllipsize(Pango.EllipsizeMode.End);
            PositionLabel[i].WidthRequest = 80;
            PositionLabel[i].SetHexpand(true);
            columns.Append(PositionLabel[i]);

            RestLabel[i] = Label.New(Info.Tracks[i].Rest.ToString());
            RestLabel[i].SetEllipsize(Pango.EllipsizeMode.End);
            RestLabel[i].WidthRequest = 30;
            RestLabel[i].SetHexpand(true);
            columns.Append(RestLabel[i]);

            VoiceLabel[i] = Label.New(Info.Tracks[i].Voice.ToString());
            VoiceLabel[i].SetEllipsize(Pango.EllipsizeMode.End);
            VoiceLabel[i].WidthRequest = 30;
            VoiceLabel[i].SetHexpand(true);
            columns.Append(VoiceLabel[i]);

            NotesLabel[i] = Label.New("");
            NotesLabel[i].SetEllipsize(Pango.EllipsizeMode.End);
            NotesLabel[i].WidthRequest = 30;
            NotesLabel[i].SetHexpand(true);
            columns.Append(NotesLabel[i]);

            PanpotLabel[i] = Label.New(Info.Tracks[i].Panpot.ToString());
            PanpotLabel[i].SetEllipsize(Pango.EllipsizeMode.End);
            PanpotLabel[i].WidthRequest = 30;
            PanpotLabel[i].SetHexpand(true);
            columns.Append(PanpotLabel[i]);

            VolumeLabel[i] = Label.New(Info.Tracks[i].Volume.ToString());
            VolumeLabel[i].SetEllipsize(Pango.EllipsizeMode.End);
            VolumeLabel[i].WidthRequest = 30;
            VolumeLabel[i].SetHexpand(true);
            columns.Append(VolumeLabel[i]);

            LFOLabel[i] = Label.New(Info.Tracks[i].LFO.ToString());
            LFOLabel[i].SetEllipsize(Pango.EllipsizeMode.End);
            LFOLabel[i].WidthRequest = 30;
            LFOLabel[i].SetHexpand(true);
            columns.Append(LFOLabel[i]);

            PitchBendLabel[i] = Label.New(Info.Tracks[i].PitchBend.ToString());
            PitchBendLabel[i].SetEllipsize(Pango.EllipsizeMode.End);
            PitchBendLabel[i].WidthRequest = 30;
            PitchBendLabel[i].SetHexpand(true);
            columns.Append(PitchBendLabel[i]);

            ExtraLabel[i] = Label.New(Info.Tracks[i].Extra.ToString());
            ExtraLabel[i].SetEllipsize(Pango.EllipsizeMode.End);
            ExtraLabel[i].WidthRequest = 30;
            ExtraLabel[i].SetHexpand(true);
            columns.Append(ExtraLabel[i]);

            Velocity[i] = new VelocityBar();
            Velocity[i].SetHalign(Align.Center);
            Velocity[i].WidthRequest = GetWidth() / 4;
            Velocity[i].SetHexpand(true);
            columns.Append(Velocity[i]);

            TypeLabel[i] = Label.New("");
            TypeLabel[i].SetHalign(Align.End);
            TypeLabel[i].WidthRequest = 60;
            TypeLabel[i].SetHexpand(true);
            columns.Append(TypeLabel[i]);

            ListBox.Append(columns);
        }
    }

    internal void ResetMutes()
    {
        for (int i = 0; i < SongState.MAX_TRACKS; i++)
        {
            NumTracks![i] = true;
            if (TrackToggleCheckButton is not null && i < TrackToggleCheckButton.Length)
                TrackToggleCheckButton[i].Active = true;
        }
    }
}