using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HackPDM.Common;
using HackPDM.Core.Configuration;
using HackPDM.Core.Odoo;
using HackPDM.Core.Pdm;
using HackPDM.Core.Services;
using HackPDM.Status;

using Microsoft.UI.Xaml.Data;

namespace HackPDM.Search;

[Bindable]
public sealed partial class SearchOdooViewModel(
	IEntrySearchService search,
	IPdmWorkflowService workflow,
	IEntryFileService files,
	IDialogService dialogs,
	IOperationReporter reporter,
	Func<IReadOnlyList<string>, string?, Task> locate ) : OperationViewModel( dialogs, reporter )
{
	private const string DefaultMaxResults = "100";
	private OdooLookupCache? _loadedLookups;
	private string? _loadedAccountKey;

	public RangeObservableCollection<SearchRow> SearchResultsList { get; } = [];
	public ObservableCollection<SearchPropertiesRow> PropertiesActive { get; } = [];
	public ObservableCollection<OperatorsRow> OperatorList { get; } = [];
	public ObservableCollection<SearchPropertiesRow> PropertiesSearchable { get; } = [];

	[ObservableProperty] public partial string FileName { get; set; } = string.Empty;
	[ObservableProperty] public partial string MaxResults { get; set; } = DefaultMaxResults;
	[ObservableProperty] public partial OperatorsRow? SelectedComparer { get; set; }
	[ObservableProperty] public partial bool IsSearching { get; set; }
	[ObservableProperty] public partial string SearchStatusText { get; set; } = string.Empty;

	[ObservableProperty]
	[NotifyPropertyChangedFor( nameof( IsLocalOnlyEnabled ) )]
	public partial bool CheckedOutMe { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor( nameof( IsLocalOnlyEnabled ) )]
	public partial bool DeletedRemotely { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor( nameof( AreRemoteFiltersEnabled ) )]
	public partial bool LocalOnly { get; set; }

	partial void OnLocalOnlyChanged( bool value )
	{
		if( !value )
			return;
		CheckedOutMe = false;
		DeletedRemotely = false;
		SelectedComparer = OperatorList.FirstOrDefault();
	}

	public bool AreRemoteFiltersEnabled => !LocalOnly;
	public bool IsLocalOnlyEnabled => !CheckedOutMe && !DeletedRemotely;

	[ObservableProperty] public partial SearchPropertiesRow? SelectedProperty { get; set; }
	[ObservableProperty] public partial OperatorsRow? SelectedPropertyOperator { get; set; }
	[ObservableProperty] public partial string PropertyValue { get; set; } = string.Empty;

	public IReadOnlyList<SearchRow> SelectedResults { get; set; } = [];
	public IReadOnlyList<SearchPropertiesRow> SelectedActiveProperties { get; set; } = [];

	public void LoadLookups()
	{
		OdooLookupCache currentLookups = OdooDefaults.Instance.Lookups;
		string accountKey = CurrentAccountKey();
		bool accountChanged = _loadedAccountKey is not null
			&& !string.Equals( _loadedAccountKey, accountKey, StringComparison.OrdinalIgnoreCase );
		bool lookupsChanged = !ReferenceEquals( _loadedLookups, currentLookups );

		if( accountChanged || lookupsChanged )
		{
			_loadedLookups = currentLookups;
			_loadedAccountKey = accountKey;
			UiDispatcher.RunOrPost( () =>
			{
				PropertiesSearchable.Clear();
				foreach( SearchPropertiesRow property in search.GetSearchableProperties() )
					PropertiesSearchable.Add( property );
				SelectedProperty = null;
				PropertiesActive.Clear();
				SelectedActiveProperties = [];
				if( accountChanged || ReferenceEquals( currentLookups, OdooLookupCache.Empty ) )
				{
					SearchResultsList.Clear();
					SelectedResults = [];
					SearchStatusText = string.Empty;
					OnPropertyChanged( nameof( QuickResultsPlaceholder ) );
				}
			} );
		}
		if( OperatorList.Count == 0 )
		{
			foreach( Operators op in Enum.GetValues<Operators>() )
				OperatorList.Add( new OperatorsRow { Operator = op } );
		}
		SelectedComparer ??= OperatorList.FirstOrDefault( o => o.Operator == Operators.ILike );
		SelectedPropertyOperator ??= OperatorList.FirstOrDefault( o => o.Operator == Operators.Equal );
	}

	public void ResetForSessionChange()
	{
		_loadedLookups = null;
		_loadedAccountKey = null;
		UiDispatcher.RunOrPost( () =>
		{
			SearchResultsList.Clear();
			SelectedResults = [];
			PropertiesActive.Clear();
			SelectedActiveProperties = [];
			PropertiesSearchable.Clear();
			SelectedProperty = null;
			SearchStatusText = string.Empty;
			OnPropertyChanged( nameof( QuickResultsPlaceholder ) );
		} );
	}

	private static string CurrentAccountKey()
	{
		OdooDefaults odoo = OdooDefaults.Instance;
		return $"{odoo.OdooUrl}|{odoo.OdooDb}|{odoo.OdooUser}|{HackDefaults.Instance.PwaPathAbsolute}";
	}

	[RelayCommand]
	private async Task SearchAsync()
	{
		if( !int.TryParse( MaxResults, out int maxResults ) || maxResults <= 0 )
		{
			await Dialogs.ShowMessageAsync( "Max Results must be a positive whole number.", "Invalid Limit", MessageBoxButtons.OK, MessageBoxIcon.Warning );
			return;
		}

		IsSearching = true;
		SearchStatusText = "Searching…";
		try
		{
			EntrySearchQuery query = new(
				FileName,
				SelectedComparer?.OpRepr ?? "=",
				CheckedOutMe,
				DeletedRemotely,
				LocalOnly,
				maxResults,
				LocalOnly ? Array.Empty<SearchPropertiesRow>() : new List<SearchPropertiesRow>( PropertiesActive ) );

			List<SearchRow> rows = await search.SearchAsync( query );
			SearchResultsList.ReplaceAll( rows );
			SearchStatusText = rows.Count switch
			{
				0 => "No matching files found.",
				1 => "1 file found",
				_ => $"{rows.Count} files found",
			};
			OnPropertyChanged( nameof( QuickResultsPlaceholder ) );
			SearchCompleted?.Invoke( rows.Count );
		}
		catch( Exception ex )
		{
			HackLogger.Error( "Search", $"Search failed: {ex.Message}", ex );
			SearchStatusText = $"Search failed: {ex.Message}";
		}
		finally
		{
			IsSearching = false;
		}
	}

	public event Action<int>? SearchCompleted;

	public string QuickResultsPlaceholder => SearchResultsList.Count == 0
		? "Jump to result…"
		: $"Jump to result ({SearchResultsList.Count})…";

	[RelayCommand]
	private void Reset()
	{
		FileName = string.Empty;
		SelectedProperty = null;
		PropertyValue = string.Empty;
		CheckedOutMe = false;
		DeletedRemotely = false;
		LocalOnly = false;
		MaxResults = DefaultMaxResults;
		SearchResultsList.Clear();
		OnPropertyChanged( nameof( QuickResultsPlaceholder ) );
		PropertiesActive.Clear();
		SearchStatusText = string.Empty;
		SelectedComparer = OperatorList.FirstOrDefault( o => o.Operator == Operators.ILike );
		SelectedPropertyOperator = OperatorList.FirstOrDefault( o => o.Operator == Operators.Equal );
	}

	[RelayCommand]
	private async Task AddPropertyAsync()
	{
		if( SelectedProperty is not { } property || SelectedPropertyOperator is not { } op )
		{
			await Dialogs.ShowMessageAsync( "Select both a Property and a Comparer before adding a filter.", "Missing Filter Field", MessageBoxButtons.OK, MessageBoxIcon.Information );
			return;
		}

		PropertiesActive.Add( new SearchPropertiesRow
		{
			ID = property.ID,
			Comparer = op.Operator,
			Value = PropertyValue,
			Name = property.Name,
			Text = property.Text,
			IsTextOrDate = property.IsTextOrDate,
		} );
		PropertyValue = string.Empty;
	}

	[RelayCommand]
	private void DeleteProperties()
	{
		foreach( SearchPropertiesRow row in SelectedActiveProperties.ToList() )
			PropertiesActive.Remove( row );
	}

	[RelayCommand]
	private void ResetProperties() => PropertiesActive.Clear();

	[RelayCommand] private Task CheckoutAsync() => SetCheckoutAsync( checkout: true );
	[RelayCommand] private Task UndoCheckoutAsync() => SetCheckoutAsync( checkout: false );

	[RelayCommand]
	private async Task CheckoutAndOpenAsync()
	{
		if( await SetCheckoutAsync( checkout: true ) )
			await OpenAsync();
	}

	private async Task<bool> SetCheckoutAsync( bool checkout )
	{
		if( SelectedResults.Count == 0 )
		{
			await Dialogs.ShowMessageAsync( "Select at least one search result first." );
			return false;
		}
		int[] ids = RemoteIds( SelectedResults.Select( r => r.Id ) );
		if( ids.Length == 0 )
		{
			await Dialogs.ShowMessageAsync( checkout ? "Can't checkout local only entries" : "Can't uncheckout local only entries" );
			return false;
		}
		if( Reporter is StatusWindowReporter statusReporter )
		{
			using StatusWindowReporter.OperationScope scope = statusReporter.BeginTrackingScope();
			await RunOnIdsAsync( ids, checkout ? workflow.CheckoutLatestAsync : workflow.UnCheckoutAsync );
			return scope.CompletedWithoutErrors;
		}
		await RunOnIdsAsync( ids, checkout ? workflow.CheckoutLatestAsync : workflow.UnCheckoutAsync );
		return true;
	}

	[RelayCommand]
	private async Task OpenAsync()
	{
		foreach( SearchRow row in SelectedResults )
		{
			if( row.Id is int id and not 0 )
				await files.OpenLatestRemoteAsync( id );
			else if( row.Directory is { Length: > 0 } directory )
			{
				if( row.Name is { Length: > 0 } name )
					files.OpenFile( Path.Combine( directory, name ) );
				else
					files.OpenFolder( directory );
			}
		}
	}

	[RelayCommand]
	private Task LocateAsync( SearchRow? row )
		=> row is null ? Task.CompletedTask : locate( search.TreeSegmentsBelowRoot( row ), row.Name );
}
