using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HackPDM.Common;
using HackPDM.Core.Configuration;
using HackPDM.Core.Odoo;
using HackPDM.Core.Services;

using Microsoft.UI.Xaml.Data;

namespace HackPDM.Settings;

/// <summary>View model for <see cref="OdooFileTypeManagerView"/>: registered file types, ignore filters, debounced search, and cancellable refresh.</summary>
[Bindable]
public sealed partial class OdooFileTypeManagerViewModel : ObservableObject, IDisposable
{
	private const int FilterDebounceMs = 180;

	private List<FileTypeRow> _allTypes = [];
	private List<FileTypeEntryFilterRow> _allFilters = [];
	private CancellationTokenSource? _refreshCts;
	private CancellationTokenSource? _filterDebounceCts;

	public RangeObservableCollection<FileTypeRow> FilteredTypes { get; } = [];
	public RangeObservableCollection<FileTypeEntryFilterRow> FilteredEntryFilters { get; } = [];

	[ObservableProperty] public partial string FilterQuery { get; set; } = string.Empty;
	[ObservableProperty] public partial string PolicySummaryText { get; set; } = string.Empty;
	[ObservableProperty] public partial string TypesTabHeader { get; set; } = "Registered Types";
	[ObservableProperty] public partial string FiltersTabHeader { get; set; } = "Ignore Filters";
	[ObservableProperty] public partial string CountSummaryText { get; set; } = string.Empty;

	[ObservableProperty]
	[NotifyCanExecuteChangedFor( nameof( RefreshCommand ) )]
	public partial bool IsRefreshing { get; set; }

	private bool CanRefresh() => !IsRefreshing;

	partial void OnFilterQueryChanged( string value )
	{
		CancellationToken token = CtsHelper.Replace( ref _filterDebounceCts ).Token;
		_ = DebounceApplyFilterAsync( value, token );
	}

	public void LoadFromCache()
	{
		CtsHelper.CancelAndDispose( ref _filterDebounceCts );
		OdooDefaults defaults = OdooDefaults.Instance;
		Dictionary<int, string> categoryById = [];
		foreach( var c in defaults.HpCategories ?? [] )
		{
			if( c.id is int cid )
				categoryById[ cid ] = c.name ?? string.Empty;
		}

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
		PolicySummaryText = restrictTypes + restrictProps;

		var (types, filters) = FilterRows( _allTypes, _allFilters, FilterQuery );
		ApplyFilteredResults( types, filters );
	}

	[RelayCommand( CanExecute = nameof( CanRefresh ) )]
	private async Task RefreshAsync()
	{
		if( IsRefreshing )
			return;
		if( OdooDefaults.Instance is not OdooDefaults concrete || concrete.OdooId == 0 )
		{
			LoadFromCache();
			return;
		}

		CancellationToken token = CtsHelper.Replace( ref _refreshCts ).Token;
		IsRefreshing = true;
		try
		{
			OdooLookupCache loaded = await Task.Run( () => OdooLookupCache.LoadAsync(), token ).WaitAsync( token );
			if( token.IsCancellationRequested )
				return;
			concrete.Lookups = loaded;
			LoadFromCache();
		}
		catch( OperationCanceledException ) when( token.IsCancellationRequested )
		{
		}
		catch( Exception ex )
		{
			HackLogger.Warn( "FileTypes", $"Refreshing file types failed: {ex.Message}", ex );
			PolicySummaryText = $"Could not refresh server rules: {ex.Message}";
		}
		finally
		{
			IsRefreshing = false;
		}
	}

	public void CancelPendingOperations()
	{
		CtsHelper.CancelAndDispose( ref _refreshCts );
		CtsHelper.CancelAndDispose( ref _filterDebounceCts );
	}

	public void Dispose() => CancelPendingOperations();

	private async Task DebounceApplyFilterAsync( string rawQuery, CancellationToken token )
	{
		try
		{
			await Task.Delay( FilterDebounceMs, token );
			List<FileTypeRow> sourceTypes = _allTypes;
			List<FileTypeEntryFilterRow> sourceFilters = _allFilters;
			var (types, filters) = await Task.Run( () => FilterRows( sourceTypes, sourceFilters, rawQuery ), token );
			if( token.IsCancellationRequested )
				return;
			ApplyFilteredResults( types, filters );
		}
		catch( OperationCanceledException ) when( token.IsCancellationRequested )
		{
		}
	}

	private void ApplyFilteredResults( List<FileTypeRow> types, List<FileTypeEntryFilterRow> filters )
	{
		FilteredTypes.ReplaceAll( types );
		FilteredEntryFilters.ReplaceAll( filters );
		TypesTabHeader = $"Registered Types ({types.Count})";
		FiltersTabHeader = $"Ignore Filters ({filters.Count})";
		CountSummaryText = $"{types.Count} of {_allTypes.Count} file type(s) · {filters.Count} of {_allFilters.Count} ignore filter(s)";
	}

	private static (List<FileTypeRow> Types, List<FileTypeEntryFilterRow> Filters) FilterRows(
		List<FileTypeRow> allTypes,
		List<FileTypeEntryFilterRow> allFilters,
		string rawQuery )
	{
		string query = rawQuery.Trim();
		if( query.Length == 0 )
			return ( allTypes, allFilters );

		static bool Matches( string? text, string q )
			=> !string.IsNullOrEmpty( text ) && text.Contains( q, StringComparison.OrdinalIgnoreCase );

		List<FileTypeRow> types =
		[
			.. allTypes.Where( t => Matches( t.Extension, query )
				|| Matches( t.Category, query )
				|| Matches( t.RegEx, query )
				|| Matches( t.Description, query ) )
		];
		List<FileTypeEntryFilterRow> filters =
		[
			.. allFilters.Where( f => Matches( f.Proto, query )
				|| Matches( f.RegEx, query )
				|| Matches( f.Description, query ) )
		];
		return ( types, filters );
	}
}
