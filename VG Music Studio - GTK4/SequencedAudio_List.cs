using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Gtk;
using Kermalis.EndianBinaryIO;
using Kermalis.VGMusicStudio.Core;
using static Gtk.SignalListItemFactory;

namespace Kermalis.VGMusicStudio.GTK4;

internal class SequencedAudio_List : Viewport
{
	private GObject.Value? Id { get; set; }
	private GObject.Value? InternalName { get; set; }
	private GObject.Value? PlaylistName { get; set; }
	private GObject.Value? SongTableOffset { get; set; }
	private GObject.Value? SequenceOffset { get; set; }

	private bool IsSongTable = false;
	internal bool IsInitialized = false;
	public bool HasSelectedRow = false;

	private EndianBinaryReader? Reader { get; set; }

	private readonly Gio.ListStore Model = Gio.ListStore.New(GetGType());
	private SignalListItemFactory? SeqListItemFactory { get; set; }
	private SingleSelection? SelectionModel { get; set; }
	// private SortListModel? SortModel { get; set; }
	private ColumnViewSorter? ColumnSorter { get; set; }
	internal ColumnView? ColumnView { get; set; }

	ColumnViewColumn PlistColumn = null!;
	ColumnViewColumn SongTableOffsetColumn = null!;
	ColumnViewColumn SequenceOffsetColumn = null!;

	public SequencedAudio_List[]? SoundData { get; set; }

	public SequencedAudio_List(int id, string name, string plistname, string songTableOffset, string seqOffset)
		: base()
	{
		Id = new GObject.Value(id);
		InternalName = new GObject.Value(name);
		PlaylistName = new GObject.Value(plistname);
		if (songTableOffset is not null)
		{
			SongTableOffset = new GObject.Value(songTableOffset);
			SequenceOffset = new GObject.Value(seqOffset);
		}
	}

	public void AddEntries(long numSongs, Config config)
	{
		if (Model.GetNItems() is not 0)
		{
			Model.RemoveAll();
		}

		SoundData = new SequencedAudio_List[numSongs];
		var sNames = new string[numSongs];
		for (int i = 0; i < sNames.Length; i++)
		{
			sNames[i] = "";
		}
		if (config.InternalSongNames is not null)
		{
			foreach (Config.InternalSongName sf in config.InternalSongNames)
			{
				foreach (Config.Song s in sf.Songs)
				{
					sNames[s.Index] = s.Name;
				}
			}
		}

		var plistNames = new string[numSongs];
		for (int i = 0; i < plistNames.Length; i++)
		{
			plistNames[i] = "";
		}
		if (config.Playlists is not null)
		{
			foreach (Config.Playlist p in config.Playlists)
			{
				foreach (Config.Song s in p.Songs)
				{
					plistNames[s.Index] = s.Name;
				}
			}
		}

		var sTEntryOffsetString = new string[numSongs];
		var seqOffsetString = new string[numSongs];
		if (config.SongTableOffset is not null)
		{
			IsSongTable = true;
			Reader ??= new EndianBinaryReader(new MemoryStream(config.ROM!));
			for (int i = 0, s = 0; i < SoundData.Length; i++)
			{
				sTEntryOffsetString[i] = string.Format("0x{0:X}", config.SongTableOffset[s] + (i * 8));
				Reader.Stream.Position = config.SongTableOffset[s] + (i * 8);
				var seqOffset = (Reader.ReadUInt32() << 8) >> 8; // To remove the "08" modifier value from the offset (which that value is only for memory usage anyways)
				seqOffsetString[i] = string.Format("0x{0:X}", seqOffset);
				if (s < config.SongTableOffset.Length - 1)
				{
					s++;
				}
			}
		}
		else
		{
			IsSongTable = false;
		}
		for (int i = 0; i < SoundData!.Length; i++)
		{
			SoundData[i] = new SequencedAudio_List(i, sNames[i], plistNames[i], sTEntryOffsetString[i], seqOffsetString[i]);
		}

		foreach (var data in SoundData!)
		{
			Model.Append(data);
		}
	}

	internal SequencedAudio_List()
	{
		var scrolledWindow = ScrolledWindow.New();
		scrolledWindow.SetSizeRequest(600, 200);
		scrolledWindow.SetHexpand(true);

		SelectionModel = SingleSelection.New(Model);

		ColumnView = ColumnView.New(SelectionModel);
		ColumnView.AddCssClass("data-table");
		ColumnView.SetShowColumnSeparators(true);
		ColumnView.SetShowRowSeparators(true);
		ColumnView.SetReorderable(false);
		ColumnView.SetHexpand(true);

		// ColumnSorter = (ColumnViewSorter)ColumnView.GetSorter()!;
		// ColumnSorter.GetPrimarySortColumn();
		// SortModel = SortListModel.New(Model, ColumnSorter);

		scrolledWindow.SetChild(ColumnView);

		Child = scrolledWindow;

		SetVexpand(true);
		SetHexpand(true);
	}

	internal void Init()
	{
		IsInitialized = false;

		// ID Column
		SeqListItemFactory = SignalListItemFactory.New();
		SeqListItemFactory.OnSetup += OnSetupIDLabel;
		SeqListItemFactory.OnBind += OnBindIDText;

		var idColumn = ColumnViewColumn.New("#", SeqListItemFactory);
		idColumn.SetResizable(true);
		// NewWithProperties(GetGType(), ["id", "internalName", "playlistName", "offset"], [Id, InternalName, PlaylistName, Offset]);
		// var idExpression = Gtk.Internal.PropertyExpression.New(GetGType(), nint.Zero, GLib.Internal.NonNullableUtf8StringOwnedHandle.Create("Id"));
		// var idSorter = NumericSorter.New(new PropertyExpression(idExpression));
		// idColumn.SetSorter(idSorter);
		ColumnView!.AppendColumn(idColumn);

		// Internal Name Column
		SeqListItemFactory = SignalListItemFactory.New();
		SeqListItemFactory.OnSetup += OnSetupNameLabel;
		SeqListItemFactory.OnBind += OnBindNameText;

		var nameColumn = ColumnViewColumn.New("Internal Name", SeqListItemFactory);
		nameColumn.SetFixedWidth(160);
		nameColumn.SetExpand(true);
		nameColumn.SetResizable(true);
		// nameColumn.SetSorter(ColumnSorter);
		ColumnView.AppendColumn(nameColumn);

		IsInitialized = true;
	}

	internal void ChangeColumns(bool isSongTable = false)
	{
		IsSongTable = isSongTable;
		if (IsSongTable)
		{
			// Playlist Name Column
			SeqListItemFactory = SignalListItemFactory.New();
			SeqListItemFactory.OnSetup += OnSetupPlistLabel;
			SeqListItemFactory.OnBind += OnBindPlistText;

			PlistColumn = ColumnViewColumn.New("Playlist Name", SeqListItemFactory);
			PlistColumn.SetFixedWidth(160);
			PlistColumn.SetExpand(true);
			PlistColumn.SetResizable(true);
			// plistColumn.SetSorter(ColumnSorter);
			ColumnView!.AppendColumn(PlistColumn);

			// Song Table Offset Column
			SeqListItemFactory = SignalListItemFactory.New();
			SeqListItemFactory.OnSetup += OnSetupSongTableOffsetLabel;
			SeqListItemFactory.OnBind += OnBindSongTableOffsetText;

			SongTableOffsetColumn = ColumnViewColumn.New("Song Table Offset", SeqListItemFactory);
			SongTableOffsetColumn.SetFixedWidth(80);
			SongTableOffsetColumn.SetExpand(true);
			SongTableOffsetColumn.SetResizable(true);
			// offsetColumn.SetSorter(ColumnSorter);
			ColumnView.AppendColumn(SongTableOffsetColumn);

			// Sequence Offset Column
			SeqListItemFactory = SignalListItemFactory.New();
			SeqListItemFactory.OnSetup += OnSetupSeqOffsetLabel;
			SeqListItemFactory.OnBind += OnBindSeqOffsetText;

			SequenceOffsetColumn = ColumnViewColumn.New("Sequence Offset", SeqListItemFactory);
			SequenceOffsetColumn.SetFixedWidth(80);
			SequenceOffsetColumn.SetExpand(true);
			SequenceOffsetColumn.SetResizable(true);
			// offsetColumn.SetSorter(ColumnSorter);
			ColumnView.AppendColumn(SequenceOffsetColumn);
		}
		else
		{
			if (PlistColumn is not null)
			{
				ColumnView!.RemoveColumn(PlistColumn);
			}
			if (SongTableOffsetColumn is not null)
			{
				ColumnView!.RemoveColumn(SongTableOffsetColumn);
			}
			if (SequenceOffsetColumn is not null)
			{
				ColumnView!.RemoveColumn(SequenceOffsetColumn);
			}
		}
		ConfigureTimer();
	}
	internal void SelectRow(int index)
	{
		HasSelectedRow = true;
		SelectionModel?.SelectItem((uint)index, true);
		HasSelectedRow = false;
	}

	private void ConfigureTimer()
	{
		var timer = GLib.Timer.New(); // Creates a new timer variable
		var context = GLib.MainContext.GetThreadDefault(); // Reads the main context default thread
		var source = GLib.Functions.TimeoutSourceNew(50); // Creates and configures the timeout interval at 50 microseconds, so it updates upon selection
		source.SetCallback(ListCallback); // Sets the callback for the timer interval to be used on
		var microsec = new CULong(source.Attach(context)); // Configures the microseconds based on attaching the GLib MainContext thread
		// timer.Elapsed(ref microsec); // Adds the pointer to the configured microseconds source
		GLib.Internal.Timer.Elapsed(timer.Handle, ref microsec); // GLib.Timer.Elapsed was removed in GirCore 0.6.3, so we're using this workaround instead
		timer.Start(); // Starts the timer
	}

	private bool ListCallback()
	{
		if (SelectionModel?.GetSelectedItem() is SequencedAudio_List list)
		{
			if (list.Id is not null)
			{
				if (IsInitialized)
				{
					MainWindow.ChangeIndex(list.Id.GetInt());
				}
			}
		}
		return true;
	}

	private static void OnSetupIDLabel(SignalListItemFactory sender, SetupSignalArgs args)
	{
		if (args.Object is not ListItem listItem)
		{
			return;
		}

		var label = Label.New(null);
		label.SetEllipsize(Pango.EllipsizeMode.End);
		label.Halign = Align.Center;
		listItem.Child = label;
	}

	private static void OnSetupNameLabel(SignalListItemFactory sender, SetupSignalArgs args)
	{
		if (args.Object is not ListItem listItem)
		{
			return;
		}

		var label = Label.New(null);
		label.SetEllipsize(Pango.EllipsizeMode.End);
		label.Halign = Align.Start;
		listItem.Child = label;
	}

	private static void OnSetupPlistLabel(SignalListItemFactory sender, SetupSignalArgs args)
	{
		if (args.Object is not ListItem listItem)
		{
			return;
		}

		var label = Label.New(null);
		label.SetEllipsize(Pango.EllipsizeMode.End);
		label.Halign = Align.Start;
		listItem.Child = label;
	}

	private static void OnSetupSongTableOffsetLabel(SignalListItemFactory sender, SetupSignalArgs args)
	{
		if (args.Object is not ListItem listItem)
		{
			return;
		}

		var label = Label.New(null);
		label.SetEllipsize(Pango.EllipsizeMode.End);
		label.Halign = Align.Start;
		listItem.Child = label;
	}

	private static void OnSetupSeqOffsetLabel(SignalListItemFactory sender, SetupSignalArgs args)
	{
		if (args.Object is not ListItem listItem)
		{
			return;
		}

		var label = Label.New(null);
		label.SetEllipsize(Pango.EllipsizeMode.End);
		label.Halign = Align.Start;
		listItem.Child = label;
	}

	private void OnBindIDText(SignalListItemFactory sender, BindSignalArgs args)
	{
		if (args.Object is not ListItem listItem)
		{
			return;
		}

		if (listItem.Child is not Label label)
		{
			return;
		}

		if (listItem.Item is not SequencedAudio_List userData)
		{
			return;
		}

		if (userData.Id is not null)
		{
			label.SetText(userData.Id.GetInt().ToString());
		}
	}

	private void OnBindNameText(SignalListItemFactory sender, BindSignalArgs args)
	{
		if (args.Object is not ListItem listItem)
		{
			return;
		}

		if (listItem.Child is not Label label)
		{
			return;
		}

		if (listItem.Item is not SequencedAudio_List userData)
		{
			return;
		}

		if (userData.InternalName is not null && userData.InternalName.GetString != null)
		{
			label.SetText(userData.InternalName.GetString()!);
		}
	}

	private void OnBindPlistText(SignalListItemFactory sender, BindSignalArgs args)
	{
		if (args.Object is not ListItem listItem)
		{
			return;
		}

		if (listItem.Child is not Label label)
		{
			return;
		}

		if (listItem.Item is not SequencedAudio_List userData)
		{
			return;
		}

		if (userData.PlaylistName is not null && userData.PlaylistName.GetString != null)
		{
			label.SetText(userData.PlaylistName.GetString()!);
		}
	}

	private void OnBindSongTableOffsetText(SignalListItemFactory sender, BindSignalArgs args)
	{
		if (args.Object is not ListItem listItem)
		{
			return;
		}

		if (listItem.Child is not Label label)
		{
			return;
		}

		if (listItem.Item is not SequencedAudio_List userData)
		{
			return;
		}

		if (userData.SongTableOffset is not null && userData.SongTableOffset.GetString != null)
		{
			label.SetText(userData.SongTableOffset.GetString()!);
		}
	}

	private void OnBindSeqOffsetText(SignalListItemFactory sender, BindSignalArgs args)
	{
		if (args.Object is not ListItem listItem)
		{
			return;
		}

		if (listItem.Child is not Label label)
		{
			return;
		}

		if (listItem.Item is not SequencedAudio_List userData)
		{
			return;
		}

		if (userData.SequenceOffset is not null && userData.SequenceOffset.GetString != null)
		{
			label.SetText(userData.SequenceOffset.GetString()!);
		}
	}
}
