using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

using WinUI.TableView;

using HackPDM.Domain.Representation;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Shared.GlobalData;
using HackPDM.Mvvm.Views.Common.Controls;
using HackPDM.UI.Types;
using HackPDM.Mvvm.Models.Hack;

using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media;

using Color = Windows.UI.Color;
using EntryRow = HackPDM.Mvvm.Models.Hack.EntryRow;

namespace HackPDM.Mvvm.Views.Common.Settings;

public class OdooEntryColumns
{
	public double RowHeight { get; set; } = 18;
	public double FontSize { get; set; } = 10;
	public bool GroupingEnabled { get; set; } = true;
	public bool GroupCollapsed { get; set; } = false;
	public GroupByField GroupBy { get; set; } = new()
	{
		FieldName = "Status",
		SortDirection = SortDirection.Ascending
	};
	/// <summary>The entry list's columns by <see cref="EntryRow"/> property; saved, so the user's widths and order survive.</summary>
	public Dictionary<string, OdooEntryColumnID?> Columns { get; set; } = new()
	{
		[ nameof( EntryRow.Name ) ] = Column( nameof( EntryRow.Name ), 0, "Name", 350 ),
		[ nameof( EntryRow.Type ) ] = Column( nameof( EntryRow.Type ), 1, "Type" ),
		[ nameof( EntryRow.Size ) ] = Column( nameof( EntryRow.Size ), 2, "Size" ),
		[ nameof( EntryRow.Status ) ] = Column( nameof( EntryRow.Status ), 3, "Status" ),
		[ nameof( EntryRow.Checkout ) ] = Column( nameof( EntryRow.Checkout ), 4, "Checkout" ),
		[ nameof( EntryRow.LocalDate ) ] = Column( nameof( EntryRow.LocalDate ), 5, "Local Date" ),
		[ nameof( EntryRow.RemoteDate ) ] = Column( nameof( EntryRow.RemoteDate ), 6, "Remote Date" ),
		[ nameof( EntryRow.FullName ) ] = Column( nameof( EntryRow.FullName ), 7, "Full Name", 200, autoSize: true ),
	};

	private static OdooEntryColumnID Column( string path, int index, string header, double width = 100, bool autoSize = false )
		=> new() { BindingPath = path, Column = new() { DisplayIndex = index, Header = header, Width = width, AutoSize = autoSize } };
}
