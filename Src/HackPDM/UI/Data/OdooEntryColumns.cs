using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

using CommunityToolkit.WinUI.UI.Controls;

using HackPDM.Domain.Representation;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Shared.GlobalData;
using HackPDM.UI.Controls;
using HackPDM.UI.Forms.Hack;
using HackPDM.UI.Forms.Helper;
using HackPDM.UI.Forms.Odoo;
using HackPDM.UI.Forms.Settings;
using HackPDM.UI.Types;

using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media;

using Color = Windows.UI.Color;
using EntryRow = HackPDM.UI.Types.EntryRow;

namespace HackPDM.UI.Data;

public class OdooEntryColumns
{
	public double RowHeight { get; set; } = 18;
	public double FontSize { get; set; } = 10;
	public bool GroupingEnabled { get; set; } = true;
	public bool GroupCollapsed { get; set; } = false;
	public GroupByField GroupBy { get; set; } = new()
	{
		FieldName = "Status",
		SortDirection = DataGridSortDirection.Ascending
	};
	public bool InactivesEnabled { get; set; } = false;
	public bool IgnoreFilters { get; set; } = false;
	[JsonConverter( typeof( LazyConverterFactory ) )]
	public Lazy<Dictionary<string, OdooEntryColumnID?>> Columns { get; set; } = new( () => new()
	{
		{ nameof(EntryRow.Name),		new OdooEntryColumnID()
			{ BindingPath = nameof(EntryRow.Name), Column = new Lazy<OdooEntryColumn>( ()
				=> new OdooEntryColumn() { DisplayIndex = 0, Header = "Name", Width = 350, AutoSize = false } ) }},

		{ nameof(EntryRow.Type),		new OdooEntryColumnID()
			{ BindingPath = nameof(EntryRow.Type), Column = new Lazy<OdooEntryColumn>( ()
				=> new OdooEntryColumn() { DisplayIndex = 1, Header = "Type", Width = 100, AutoSize = false } ) }},

		{ nameof(EntryRow.Size),		new OdooEntryColumnID()
			{ BindingPath = nameof(EntryRow.Size), Column = new Lazy<OdooEntryColumn>( ()
				=> new OdooEntryColumn() { DisplayIndex = 2, Header = "Size", Width = 100, AutoSize = false } ) }},

		{ nameof(EntryRow.Status),		new OdooEntryColumnID()
			{ BindingPath = nameof(EntryRow.Status)	, Column = new Lazy<OdooEntryColumn>( ()
				=> new OdooEntryColumn() { DisplayIndex = 3, Header = "Status", Width = 100, AutoSize = false } ) }},

		{ nameof(EntryRow.Checkout),	new OdooEntryColumnID()
			{ BindingPath = nameof(EntryRow.Checkout), Column = new Lazy<OdooEntryColumn>( ()
				=> new OdooEntryColumn() { DisplayIndex = 4, Header = "Checkout", Width = 100, AutoSize = false } ) }},

		{ nameof(EntryRow.LocalDate),	new OdooEntryColumnID()
			{ BindingPath = nameof(EntryRow.LocalDate), Column = new Lazy<OdooEntryColumn>( ()
				=> new OdooEntryColumn() { DisplayIndex = 5, Header = "Local Date", Width = 100, AutoSize = false } ) }},

		{ nameof(EntryRow.RemoteDate),	new OdooEntryColumnID()
			{ BindingPath = nameof(EntryRow.RemoteDate), Column = new Lazy<OdooEntryColumn>( ()
				=> new OdooEntryColumn() { DisplayIndex = 6, Header = "Remote Date", Width = 100, AutoSize = false } ) }},

		{ nameof(EntryRow.FullName),	new OdooEntryColumnID()
			{ BindingPath = nameof(EntryRow.FullName), Column = new Lazy<OdooEntryColumn>( ()
				=> new OdooEntryColumn() { DisplayIndex = 7, Header = "Full Name", Width = 200, AutoSize = true } ) }},

	} );
}
