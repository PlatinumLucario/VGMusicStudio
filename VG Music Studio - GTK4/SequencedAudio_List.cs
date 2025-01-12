using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Gtk;
using Kermalis.VGMusicStudio.Core;
using static Gtk.SignalListItemFactory;

namespace Kermalis.VGMusicStudio.GTK4;

internal class SequencedAudio_List : Viewport
{
	private GObject.Value? Id { get; set; }
	private GObject.Value? InternalName { get; set; }
	private GObject.Value? PlaylistName { get; set; }
	private GObject.Value? Offset { get; set; }

	private bool IsSongTable = false;
	internal bool IsInitialized = false;
	public bool HasSelectedRow = false;

	private readonly Gio.ListStore Model = Gio.ListStore.New(GetGType());
	private SingleSelection? SelectionModel { get; set; }
	private SortListModel? SortModel { get; set; }
	private ColumnViewSorter? ColumnSorter { get; set; }
	internal ColumnView? ColumnView { get; set; }

	public SequencedAudio_List[]? SoundData { get; set; }

	public SequencedAudio_List(int id, string name, string plistname, string offset)
		: base()
	{
		Id = new GObject.Value(id);
		InternalName = new GObject.Value(name);
		PlaylistName = new GObject.Value(plistname);
		if (offset is not null)
		{
			Offset = new GObject.Value(offset);
		}
	}

	public void AddEntries(long numSongs, List<Config.InternalSongName> internalSongNames, List<Config.Playlist> playlists, int[] songTableOffsets)
	{
		if (Model.GetNItems() is not 0)
			Model.RemoveAll();

		SoundData = new SequencedAudio_List[numSongs];
		var sNames = new string[numSongs];
		for (int i = 0; i < sNames.Length; i++)
		{
			sNames[i] = "";
		}
		if (internalSongNames is not null)
		{
			foreach (Config.InternalSongName sf in internalSongNames)
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
		if (playlists is not null)
		{
			foreach (Config.Playlist p in playlists)
			{
				foreach (Config.Song s in p.Songs)
				{
					plistNames[s.Index] = s.Name;
				}
			}
		}

		var offset = new string[numSongs];
		if (songTableOffsets is not null)
		{
			IsSongTable = true;
			for (int i = 0, s = 0; i < SoundData.Length; i++)
			{
				offset[i] = string.Format("0x{0:X}", songTableOffsets[s] + (i * 8));
				if (s < songTableOffsets.Length - 1)
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
			SoundData[i] = new SequencedAudio_List(i, sNames[i], plistNames[i], offset[i]);
		}

		foreach (var data in SoundData!)
		{
			Model.Append(data);
		}
	}

	internal SequencedAudio_List()
	{
		var scrolledWindow = ScrolledWindow.New();
		scrolledWindow.SetSizeRequest(700, 200);
		scrolledWindow.SetHexpand(true);

		SelectionModel = SingleSelection.New(Model);

		ColumnView = ColumnView.New(SelectionModel);
		ColumnView.AddCssClass("data-table");
		ColumnView.SetShowColumnSeparators(true);
		ColumnView.SetShowRowSeparators(true);
		ColumnView.SetReorderable(false);
		ColumnView.SetHexpand(true);

		ColumnSorter = (ColumnViewSorter)ColumnView.GetSorter()!;
		ColumnSorter.GetPrimarySortColumn();
		SortModel = SortListModel.New(Model, ColumnSorter);

		scrolledWindow.SetChild(ColumnView);

		Child = scrolledWindow;

		SetVexpand(true);
		SetHexpand(true);
	}

	internal void Init()
	{
		IsInitialized = false;

		// ID Column
		var listItemFactory = SignalListItemFactory.New();
		listItemFactory.OnSetup += (_, args) => OnSetupLabel(args, Align.Center);
		listItemFactory.OnBind += (_, args) => OnBindText(args, (ud) => ud.Id!.GetInt().ToString());

		var idColumn = ColumnViewColumn.New("#", listItemFactory);
		idColumn.SetResizable(true);
		// NewWithProperties(GetGType(), ["id", "internalName", "playlistName", "offset"], [Id, InternalName, PlaylistName, Offset]);
		// var idExpression = Gtk.Internal.PropertyExpression.New(GetGType(), nint.Zero, GLib.Internal.NonNullableUtf8StringOwnedHandle.Create("Id"));
		// var idSorter = NumericSorter.New(new PropertyExpression(idExpression));
		// idColumn.SetSorter(idSorter);
		ColumnView!.AppendColumn(idColumn);

		// Internal Name Column
		listItemFactory = SignalListItemFactory.New();
		listItemFactory.OnSetup += (_, args) => OnSetupLabel(args, Align.Start);
		listItemFactory.OnBind += (_, args) => OnBindText(args, (ud) => ud.InternalName!.GetString()!);

		var nameColumn = ColumnViewColumn.New("Internal Name", listItemFactory);
		nameColumn.SetFixedWidth(200);
		nameColumn.SetExpand(true);
		nameColumn.SetResizable(true);
		// nameColumn.SetSorter(ColumnSorter);
		ColumnView.AppendColumn(nameColumn);


		ColumnViewColumn offsetColumn = null!;
		ColumnViewColumn plistColumn = null!;
		if (IsSongTable)
		{
			// Playlist Name Column
			listItemFactory = SignalListItemFactory.New();
			listItemFactory.OnSetup += (_, args) => OnSetupLabel(args, Align.Start);
			listItemFactory.OnBind += (_, args) => OnBindText(args, (ud) => ud.PlaylistName!.GetString()!);

			plistColumn = ColumnViewColumn.New("Playlist Name", listItemFactory);
			plistColumn.SetFixedWidth(320);
			plistColumn.SetExpand(true);
			plistColumn.SetResizable(true);
			// plistColumn.SetSorter(ColumnSorter);
			ColumnView.AppendColumn(plistColumn);

			// Offset Column
			listItemFactory = SignalListItemFactory.New();
			listItemFactory.OnSetup += (_, args) => OnSetupLabel(args, Align.Start);
			listItemFactory.OnBind += (_, args) => OnBindText(args, (ud) => ud.Offset!.GetString()!);

			offsetColumn = ColumnViewColumn.New(nameof(Offset), listItemFactory);
			offsetColumn.SetFixedWidth(100);
			offsetColumn.SetExpand(true);
			offsetColumn.SetResizable(true);
			// offsetColumn.SetSorter(ColumnSorter);
			ColumnView.AppendColumn(offsetColumn);
		}
		else
		{
			if (plistColumn is not null)
			{
				ColumnView.RemoveColumn(plistColumn);
			}
			if (offsetColumn is not null)
			{
				ColumnView.RemoveColumn(offsetColumn);
			}
		}

		IsInitialized = true;
	}

	internal void SelectRow(int index)
	{
		HasSelectedRow = true;
		SelectionModel?.SelectItem((uint)index, true);
		// var selectedItem = "";
		// for (uint i = 0; i < Model.NItems; i++)
		// {
		// 	if (ColumnView!.GetModel()!.IsSelected(i))
		// 		selectedItem = ColumnView!.GetModel()!.GetSelection().ToString();
		// }
	}
	private void OnSetupLabel(SetupSignalArgs args, Align align)
	{
		if (args.Object is not ListItem listItem)
		{
			return;
		}

		var label = Label.New(null);
		label.Halign = align;
		listItem.Child = label;
	}

	private void OnBindText(BindSignalArgs args, Func<SequencedAudio_List, string> getText)
	{
		if (args.Object is not ListItem listItem)
		{
			return;
		}

		if (listItem.Child is not Label label) return;
		if (listItem.Item is not SequencedAudio_List userData) return;

		label.SetText(getText(userData));

		if (listItem is not ColumnViewCell cell) return;

		if (cell.Selected == true)
		{
			if (userData.Id is not null)
			{
				if (IsInitialized)
					MainWindow.ChangeIndex(userData.Id.GetInt());
			}
		}
	}
}
