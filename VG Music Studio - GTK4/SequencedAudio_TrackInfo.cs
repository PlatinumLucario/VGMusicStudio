using System;
using System.Collections.Generic;
using Cairo;
using Gtk;
using Kermalis.VGMusicStudio.Core;
using Kermalis.VGMusicStudio.Core.Properties;
using Kermalis.VGMusicStudio.Core.Util;
using Kermalis.VGMusicStudio.GTK4.Util;

namespace Kermalis.VGMusicStudio.GTK4;

public class SequencedAudio_TrackInfo : Box
{
    private Label? TempoLabel { get; set; }
    public Label[]? PositionLabel { get; set; }
    public Label[]? RestLabel { get; set; }
    public Label[]? VoiceLabel { get; set; }
    public Label[]? NotesLabel { get; set; }
    public Label[]? PanpotLabel { get; set; }
    public Label[]? VolumeLabel { get; set; }
    public Label[]? LFOLabel { get; set; }
    public Label[]? PitchBendLabel { get; set; }
    public Label[]? ExtraLabel { get; set; }
    private VisualizerBar[]? Visualizer { get; set; }
    public Label[]? TypeLabel { get; set; }

    private ListBox? ListBox { get; set; }
    private static readonly List<string> _keysCache = new(128);

    public readonly bool[]? NumTracks;
    public readonly SongState? Info;
    public SequencedAudio_TrackInfo[]? TrackInfo { get; set; }

    internal SequencedAudio_TrackInfo()
    {
        NumTracks = new bool[SongState.MAX_TRACKS];
        for (int i = 0; i < SongState.MAX_TRACKS; i++)
            NumTracks[i] = true;

        Info = new SongState();

        TempoLabel = Label.New(string.Format("{0} - {1}", Strings.PlayerTempo, Info.Tempo));
        var listHeader = CreateListHeader();
        var viewport = Viewport.New(Adjustment.New(0, -1, -1, 1, 1, 1), Adjustment.New(0, -1, -1, 1, 1, 1));
        var scrolledWindow = ScrolledWindow.New();
        scrolledWindow.SetSizeRequest(900, 150);
        scrolledWindow.SetHexpand(true);
        scrolledWindow.SetVexpand(true);

        ListBox = ListBox.New();
        ListBox.SetHexpand(true);
        ListBox.SetSelectionMode(SelectionMode.None);

        scrolledWindow.SetChild(ListBox);

        viewport.Child = scrolledWindow;

        SetOrientation(Orientation.Vertical);
        Append(TempoLabel);
        Append(listHeader);
        Append(viewport);
        SetVexpand(true);
        SetHexpand(true);
        ConfigureTimer();
        Hide();
    }

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
        TempoLabel!.SetText(string.Format("{0} - {1}", Strings.PlayerTempo, Info!.Tempo));
        if (PositionLabel is not null &&
            RestLabel is not null &&
            VoiceLabel is not null &&
            NotesLabel is not null &&
            PanpotLabel is not null &&
            VolumeLabel is not null &&
            LFOLabel is not null &&
            PitchBendLabel is not null &&
            ExtraLabel is not null &&
            Visualizer is not null &&
            TypeLabel is not null)
        {
            for (int i = 0; i < Info!.Tracks.Length; i++)
            {
                if (PositionLabel[i] is not null &&
                RestLabel[i] is not null &&
                VoiceLabel[i] is not null &&
                NotesLabel[i] is not null &&
                PanpotLabel[i] is not null &&
                VolumeLabel[i] is not null &&
                LFOLabel[i] is not null &&
                PitchBendLabel[i] is not null &&
                ExtraLabel[i] is not null &&
                Visualizer[i] is not null &&
                TypeLabel[i] is not null &&
                Info.Tracks[i].Type is not null)
                {
                    PositionLabel[i].SetText(string.Format("0x{0:X}", Info.Tracks[i].Position));
                    RestLabel[i].SetText(Info.Tracks[i].Rest.ToString());
                    VoiceLabel[i].SetText(Info.Tracks[i].Voice.ToString());
                    NotesLabel[i].SetText(GetNote(Info.Tracks[i]));
                    PanpotLabel[i].SetText(Info.Tracks[i].Panpot.ToString());
                    VolumeLabel[i].SetText(Info.Tracks[i].Volume.ToString());
                    LFOLabel[i].SetText(Info.Tracks[i].LFO.ToString());
                    PitchBendLabel[i].SetText(Info.Tracks[i].PitchBend.ToString());
                    ExtraLabel[i].SetText(Info.Tracks[i].Extra.ToString());
                    Visualizer[i].Update(Info.Tracks[i]);
                    TypeLabel[i].SetText(Info.Tracks[i].Type);
                }
            }
        }
        return true;
    }

    private class VisualizerBar2 : DrawingArea
    {
        private HSLColor Color;
        internal VisualizerBar2()
        {
            Color = new HSLColor();
            WidthRequest = 150;
            SetHexpand(true);
            SetVexpand(true);
            SetDrawFunc(DrawVisualizerBar);
        }

        private void DrawVisualizerBar(DrawingArea drawingArea, Context cr, int width, int height)
        {
            throw new NotImplementedException();
        }
    }
    private class VisualizerBar : Box
    {
        private ProgressBar? VisualizerBarL { get; set; }
        private ProgressBar? VisualizerBarR { get; set; }
        internal VisualizerBar()
        {
            New(Orientation.Horizontal, 0);
            VisualizerBarL = ProgressBar.New();
            VisualizerBarL.SetDirection(TextDirection.Rtl);
            VisualizerBarR = ProgressBar.New();
            VisualizerBarR.SetDirection(TextDirection.Ltr);
            Append(VisualizerBarL);
            Append(VisualizerBarR);
        }

        internal void Update(SongState.Track track)
        {
            VisualizerBarL!.SetFraction(track.LeftVolume);
            VisualizerBarR!.SetFraction(track.RightVolume);
        }
        internal void UpdateCss(int index, SongState.Track track)
        {
            if (VisualizerBarL!.GetName() != "visualizer" + index.ToString())
            {
                VisualizerBarL!.SetName("visualizer" + index.ToString());
                VisualizerBarR!.SetName("visualizer" + index.ToString());
            }
            var display = GetDisplay();
            var provider = CssProvider.New();
            provider.LoadFromString(
                "#visualizer" + index.ToString() + " progress { "
                + "background-image: none; "
                + "background-color: rgb("
                + GlobalConfig.Instance.Colors[track.Voice].R.ToString() + ","
                + GlobalConfig.Instance.Colors[track.Voice].G.ToString() + ","
                + GlobalConfig.Instance.Colors[track.Voice].B.ToString() + "); }");

            StyleContext.AddProviderForDisplay(display, provider, Constants.STYLE_PROVIDER_PRIORITY_APPLICATION);
            // VisualizerBarL!.AddCssClass(
            //     "progressbar > trough > progress { "
            //     + "background-image: none; "
            //     + "background-color: rgb("
            //     + GlobalConfig.Instance.Colors[track.Voice].R.ToString() + ","
            //     + GlobalConfig.Instance.Colors[track.Voice].G.ToString() + ","
            //     + GlobalConfig.Instance.Colors[track.Voice].B.ToString() + "); }");
            // VisualizerBarR!.AddCssClass(
            //     "progressbar > trough > progress { "
            //     + "background-image: none; "
            //     + "background-color: rgb("
            //     + GlobalConfig.Instance.Colors[track.Voice].R.ToString() + ","
            //     + GlobalConfig.Instance.Colors[track.Voice].G.ToString() + ","
            //     + GlobalConfig.Instance.Colors[track.Voice].B.ToString() + "); }");
            var cssL = VisualizerBarL!.GetCssClasses();
            var cssR = VisualizerBarR!.GetCssClasses();
            var cssNameL = VisualizerBarL!.GetCssName();
            var cssNameR = VisualizerBarR!.GetCssName();
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
            _keysCache.Clear();
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
                    _keysCache.Add(' ' + noteName);
                }
                else
                {
                    _keysCache.Add(noteName);
                }
            }
            foreach (var k in _keysCache)
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

        var positionLabelHeader = Label.New(Strings.PlayerPosition);
        positionLabelHeader.SetMaxWidthChars(1);
        positionLabelHeader.SetEllipsize(Pango.EllipsizeMode.End);
        positionLabelHeader.WidthRequest = 100;
        positionLabelHeader.SetHexpand(true);
        columns.Append(positionLabelHeader);

        var restLabelHeader = Label.New(Strings.PlayerRest);
        restLabelHeader.SetMaxWidthChars(1);
        restLabelHeader.SetEllipsize(Pango.EllipsizeMode.End);
        restLabelHeader.WidthRequest = 40;
        restLabelHeader.SetHexpand(true);
        columns.Append(restLabelHeader);

        var voiceLabelHeader = Label.New("Voice");
        voiceLabelHeader.SetMaxWidthChars(1);
        voiceLabelHeader.SetEllipsize(Pango.EllipsizeMode.End);
        voiceLabelHeader.WidthRequest = 40;
        voiceLabelHeader.SetHexpand(true);
        columns.Append(voiceLabelHeader);

        var notesLabelHeader = Label.New(Strings.PlayerNotes);
        notesLabelHeader.SetMaxWidthChars(1);
        notesLabelHeader.SetEllipsize(Pango.EllipsizeMode.End);
        notesLabelHeader.WidthRequest = 40;
        notesLabelHeader.SetHexpand(true);
        columns.Append(notesLabelHeader);

        var panpotLabelHeader = Label.New("Panpot");
        panpotLabelHeader.SetMaxWidthChars(1);
        panpotLabelHeader.SetEllipsize(Pango.EllipsizeMode.End);
        panpotLabelHeader.WidthRequest = 40;
        panpotLabelHeader.SetHexpand(true);
        columns.Append(panpotLabelHeader);

        var volumeLabelHeader = Label.New("Volume");
        volumeLabelHeader.SetMaxWidthChars(1);
        volumeLabelHeader.SetEllipsize(Pango.EllipsizeMode.End);
        volumeLabelHeader.WidthRequest = 40;
        volumeLabelHeader.SetHexpand(true);
        columns.Append(volumeLabelHeader);

        var lfoLabelHeader = Label.New("LFO");
        lfoLabelHeader.SetMaxWidthChars(1);
        lfoLabelHeader.SetEllipsize(Pango.EllipsizeMode.End);
        lfoLabelHeader.WidthRequest = 40;
        lfoLabelHeader.SetHexpand(true);
        columns.Append(lfoLabelHeader);

        var pitchBendLabelHeader = Label.New("Pitch Bend");
        pitchBendLabelHeader.SetMaxWidthChars(1);
        pitchBendLabelHeader.SetEllipsize(Pango.EllipsizeMode.End);
        pitchBendLabelHeader.WidthRequest = 40;
        pitchBendLabelHeader.SetHexpand(true);
        columns.Append(pitchBendLabelHeader);

        var extraLabelHeader = Label.New("Extra");
        extraLabelHeader.SetMaxWidthChars(1);
        extraLabelHeader.SetEllipsize(Pango.EllipsizeMode.End);
        extraLabelHeader.WidthRequest = 40;
        extraLabelHeader.SetHexpand(true);
        columns.Append(extraLabelHeader);

        var visualizerBoxHeader = Label.New("Visualizer");
        visualizerBoxHeader.SetMaxWidthChars(1);
        visualizerBoxHeader.WidthRequest = 300;
        visualizerBoxHeader.SetHexpand(true);
        columns.Append(visualizerBoxHeader);

        var typeLabelHeader = Label.New(Strings.PlayerType);
        typeLabelHeader.SetMaxWidthChars(1);
        typeLabelHeader.SetHalign(Align.End);
        typeLabelHeader.WidthRequest = 100;
        typeLabelHeader.SetHexpand(true);
        columns.Append(typeLabelHeader);
        return columns;
    }

    public void AddTrackInfo()
    {
        ListBox!.RemoveAll();
        PositionLabel = new Label[Info!.Tracks.Length];
        RestLabel = new Label[Info!.Tracks.Length];
        VoiceLabel = new Label[Info!.Tracks.Length];
        NotesLabel = new Label[Info!.Tracks.Length];
        PanpotLabel = new Label[Info!.Tracks.Length];
        VolumeLabel = new Label[Info!.Tracks.Length];
        LFOLabel = new Label[Info!.Tracks.Length];
        PitchBendLabel = new Label[Info!.Tracks.Length];
        ExtraLabel = new Label[Info!.Tracks.Length];
        Visualizer = new VisualizerBar[Info!.Tracks.Length];
        TypeLabel = new Label[Info!.Tracks.Length];
        for (int i = 0; i < Info!.Tracks.Length; i++)
        {
            var columns = New(Orientation.Horizontal, 4);
            columns.SetHexpand(true);
            PositionLabel[i] = Label.New(string.Format("0x{0:X}", Info.Tracks[i].Position));
            PositionLabel[i].SetEllipsize(Pango.EllipsizeMode.End);
            PositionLabel[i].WidthRequest = 100;
            PositionLabel[i].SetHexpand(true);
            columns.Append(PositionLabel[i]);
            RestLabel[i] = Label.New(Info.Tracks[i].Rest.ToString());
            RestLabel[i].SetEllipsize(Pango.EllipsizeMode.End);
            RestLabel[i].WidthRequest = 40;
            RestLabel[i].SetHexpand(true);
            columns.Append(RestLabel[i]);
            VoiceLabel[i] = Label.New(Info.Tracks[i].Voice.ToString());
            VoiceLabel[i].SetEllipsize(Pango.EllipsizeMode.End);
            VoiceLabel[i].WidthRequest = 40;
            VoiceLabel[i].SetHexpand(true);
            columns.Append(VoiceLabel[i]);
            NotesLabel[i] = Label.New("");
            NotesLabel[i].SetEllipsize(Pango.EllipsizeMode.End);
            NotesLabel[i].WidthRequest = 40;
            NotesLabel[i].SetHexpand(true);
            columns.Append(NotesLabel[i]);
            PanpotLabel[i] = Label.New(Info.Tracks[i].Panpot.ToString());
            PanpotLabel[i].SetEllipsize(Pango.EllipsizeMode.End);
            PanpotLabel[i].WidthRequest = 40;
            PanpotLabel[i].SetHexpand(true);
            columns.Append(PanpotLabel[i]);
            VolumeLabel[i] = Label.New(Info.Tracks[i].Volume.ToString());
            VolumeLabel[i].SetEllipsize(Pango.EllipsizeMode.End);
            VolumeLabel[i].WidthRequest = 40;
            VolumeLabel[i].SetHexpand(true);
            columns.Append(VolumeLabel[i]);
            LFOLabel[i] = Label.New(Info.Tracks[i].LFO.ToString());
            LFOLabel[i].SetEllipsize(Pango.EllipsizeMode.End);
            LFOLabel[i].WidthRequest = 40;
            LFOLabel[i].SetHexpand(true);
            columns.Append(LFOLabel[i]);
            PitchBendLabel[i] = Label.New(Info.Tracks[i].PitchBend.ToString());
            PitchBendLabel[i].SetEllipsize(Pango.EllipsizeMode.End);
            PitchBendLabel[i].WidthRequest = 40;
            PitchBendLabel[i].SetHexpand(true);
            columns.Append(PitchBendLabel[i]);
            ExtraLabel[i] = Label.New(Info.Tracks[i].Extra.ToString());
            ExtraLabel[i].SetEllipsize(Pango.EllipsizeMode.End);
            ExtraLabel[i].WidthRequest = 40;
            ExtraLabel[i].SetHexpand(true);
            columns.Append(ExtraLabel[i]);
            Visualizer[i] = new VisualizerBar();
            Visualizer[i].SetHalign(Align.Center);
            Visualizer[i].WidthRequest = 300;
            Visualizer[i].SetHexpand(true);
            columns.Append(Visualizer[i]);
            TypeLabel[i] = Label.New("");
            TypeLabel[i].SetHalign(Align.End);
            TypeLabel[i].WidthRequest = 100;
            TypeLabel[i].SetHexpand(true);
            columns.Append(TypeLabel[i]);
            ListBox.Append(columns);
        }
    }
}