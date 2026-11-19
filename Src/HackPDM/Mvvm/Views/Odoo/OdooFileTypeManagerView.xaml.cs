using System;
using System.Collections.Generic;
using System.Linq;

using HackPDM.Domain.Representation;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Mvvm.Models.Common;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace HackPDM.Mvvm.Views.Odoo;

/// <summary>
/// Displays the Odoo server's registered <c>hp.type</c> extensions/categories and <c>hp.entry.name.filter</c> ignore rules.
/// </summary>
public sealed partial class OdooFileTypeManagerView : Page
{
	private Window? _parentWindow;
	private List<FileTypeRow> _allTypes = [];
	private List<FileTypeEntryFilterRow> _allFilters = [];

	public RangeObservableCollection<FileTypeRow> FilteredTypes { get; } = [];
	public RangeObservableCollection<FileTypeEntryFilterRow> FilteredEntryFilters { get; } = [];

	public OdooFileTypeManagerView()
	{
		InitializeComponent();
		KeyDown += OnPageKeyDown;
		Loaded += ( _, _ ) => LoadFromCache();
	}

	public void StoreWindowInstance( Window window ) => _parentWindow = window;

	private void LoadFromCache()
	{
		var defaults = OdooDefaults.Instance;
		var categoryById = ( defaults.HpCategories ?? [] )
			.Where( c => c.id is int )
			.ToDictionary( c => c.id!.Value, c => c.name ?? string.Empty );

		_allTypes =
		[
			.. ( defaults.HpTypes ?? [] )
				.OrderBy( t => t.file_ext, StringComparer.OrdinalIgnoreCase )
				.Select( t => new FileTypeRow
				{
					Extension = string.IsNullOrWhiteSpace( t.file_ext ) ? "" : $".{t.file_ext.TrimStart( '.' ).ToLowerInvariant()}",
					Category = t.cat_id?.id is int cid && categoryById.TryGetValue( cid, out string? cname ) && cname.Length > 0
						? cname
						: t.cat_id?.name ?? "",
					RegEx = t.type_regex ?? "",
					Description = t.description ?? "",
				} )
		];

		_allFilters =
		[
			.. ( defaults.HpEntryNameFilters ?? [] )
				.OrderBy( f => f.name_proto, StringComparer.OrdinalIgnoreCase )
				.Select( f => new FileTypeEntryFilterRow
				{
					Id = f.id ?? 0,
					Proto = f.name_proto ?? "",
					RegEx = f.name_regex ?? "",
					Description = f.description ?? "",
				} )
		];

		string restrictTypes = defaults.RestrictTypes switch
		{
			true => "Unknown extensions: Restricted to registered types",
			false => "Unknown extensions: Allowed",
			null => "Server policy not loaded (sign in to view live server rules)",
		};
		string restrictProps = defaults.RestrictProperties switch
		{
			true => " · Custom properties: Restricted",
			false => " · Custom properties: Unrestricted",
			null => "",
		};
		PolicySummaryText.Text = restrictTypes + restrictProps;

		ApplyFilter();
	}

	private void ApplyFilter()
	{
		string query = FilterBox.Text?.Trim() ?? string.Empty;
		static bool Matches( string? text, string q )
			=> !string.IsNullOrEmpty( text ) && text.Contains( q, StringComparison.OrdinalIgnoreCase );

		List<FileTypeRow> types = query.Length == 0
			? _allTypes
			: [ .. _allTypes.Where( t => Matches( t.Extension, query ) || Matches( t.Category, query ) || Matches( t.RegEx, query ) || Matches( t.Description, query ) ) ];

		List<FileTypeEntryFilterRow> filters = query.Length == 0
			? _allFilters
			: [ .. _allFilters.Where( f => Matches( f.Proto, query ) || Matches( f.RegEx, query ) || Matches( f.Description, query ) ) ];

		FilteredTypes.ReplaceAll( types );
		FilteredEntryFilters.ReplaceAll( filters );

		TypesTab.Header = $"Registered Types ({types.Count})";
		FiltersTab.Header = $"Ignore Filters ({filters.Count})";
		CountSummaryText.Text = $"{types.Count} of {_allTypes.Count} file type(s) · {filters.Count} of {_allFilters.Count} ignore filter(s)";
	}

	private void FilterBox_TextChanged( object sender, TextChangedEventArgs e ) => ApplyFilter();

	private async void RefreshButton_Click( object sender, RoutedEventArgs e )
	{
		if( OdooDefaults.Instance is not OdooDefaults concrete || concrete.OdooId == 0 )
		{
			LoadFromCache();
			return;
		}
		RefreshButton.IsEnabled = false;
		BusyRing.IsActive = true;
		try
		{
			concrete.Lookups = await OdooLookupCache.LoadAsync();
			LoadFromCache();
		}
		finally
		{
			BusyRing.IsActive = false;
			RefreshButton.IsEnabled = true;
		}
	}

	private void CloseButton_Click( object sender, RoutedEventArgs e ) => _parentWindow?.Close();

	private void OnPageKeyDown( object sender, KeyRoutedEventArgs e )
	{
		if( e.Key == Windows.System.VirtualKey.Escape )
		{
			e.Handled = true;
			_parentWindow?.Close();
		}
	}
}
