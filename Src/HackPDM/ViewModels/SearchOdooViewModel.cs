using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HackPDM.Domain.Representation;
using HackPDM.Services;
using HackPDM.Shared.GlobalData;
using HackPDM.UI.Forms;

namespace HackPDM.ViewModels;

/// <summary>
/// State and commands for the search window. Searching is done by <see cref="IEntrySearchService"/>;
/// showing a result in the file manager goes through the file manager's view model.
/// </summary>
public partial class SearchOdooViewModel : ObservableObject
{
	private const string DefaultMaxResults = "100";

	private readonly IEntrySearchService _search;
	private readonly IPdmWorkflowService _workflow;
	private readonly IEntryFileService _files;
	private readonly IDialogService _dialogs;
	private readonly IOperationReporter _reporter;
	private readonly Func<IReadOnlyList<string>, string?, Task> _locate;

	public SearchOdooViewModel()
		: this(
			new EntrySearchService(),
			new PdmWorkflowService( StatusWindowReporter.Default ),
			new EntryFileService(),
			new DialogService(),
			StatusWindowReporter.Default,
			( segments, fileName ) => InstanceManager.HFM.RequestLocateAsync( segments, fileName ) ) { }

	public SearchOdooViewModel(
		IEntrySearchService search,
		IPdmWorkflowService workflow,
		IEntryFileService files,
		IDialogService dialogs,
		IOperationReporter reporter,
		Func<IReadOnlyList<string>, string?, Task> locate )
	{
		_search = search;
		_workflow = workflow;
		_files = files;
		_dialogs = dialogs;
		_reporter = reporter;
		_locate = locate;
	}

	#region Collections

	public ObservableCollection<SearchRow> SearchResultsList { get; } = [];
	public ObservableCollection<SearchPropertiesRow> PropertiesActive { get; } = [];
	public ObservableCollection<OperatorsRow> OperatorList { get; } = [];
	public ObservableCollection<SearchPropertiesRow> PropertiesSearchable { get; } = [];

	#endregion
	#region Search form

	[ObservableProperty] public partial string FileName { get; set; } = string.Empty;
	[ObservableProperty] public partial string MaxResults { get; set; } = DefaultMaxResults;
	[ObservableProperty] public partial OperatorsRow? SelectedComparer { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor( nameof( IsLocalOnlyEnabled ) )]
	public partial bool CheckedOutMe { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor( nameof( IsLocalOnlyEnabled ) )]
	public partial bool DeletedRemotely { get; set; }

	/// <summary>Search local files that have no remote entry; turns the remote-only filters off.</summary>
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

	/// <summary>Checked-out, deleted, comparer and property filters only apply to remote searches.</summary>
	public bool AreRemoteFiltersEnabled => !LocalOnly;
	/// <summary>"Local only" can't be combined with the remote filters.</summary>
	public bool IsLocalOnlyEnabled => !CheckedOutMe && !DeletedRemotely;

	#endregion
	#region Property filter form

	[ObservableProperty] public partial SearchPropertiesRow? SelectedProperty { get; set; }
	[ObservableProperty] public partial OperatorsRow? SelectedPropertyOperator { get; set; }
	[ObservableProperty] public partial string PropertyValue { get; set; } = string.Empty;

	#endregion
	#region Selection (pushed in by the view)

	public IReadOnlyList<SearchRow> SelectedResults { get; set; } = [];
	public IReadOnlyList<SearchPropertiesRow> SelectedActiveProperties { get; set; } = [];

	#endregion

	/// <summary>Fills the property and operator pickers (once Odoo metadata is available).</summary>
	public void LoadLookups()
	{
		if( PropertiesSearchable.Count == 0 )
		{
			foreach( SearchPropertiesRow property in _search.GetSearchableProperties() )
				PropertiesSearchable.Add( property );
		}
		if( OperatorList.Count == 0 )
		{
			foreach( Operators op in Enum.GetValues<Operators>() )
				OperatorList.Add( new OperatorsRow { Operator = op } );
		}
		SelectedComparer ??= OperatorList.FirstOrDefault( o => o.Operator == Operators.ILike );
		SelectedPropertyOperator ??= OperatorList.FirstOrDefault( o => o.Operator == Operators.Equal );
	}

	#region Commands: searching

	[RelayCommand]
	private async Task SearchAsync()
	{
		if( !int.TryParse( MaxResults, out int maxResults ) )
		{
			await _dialogs.ShowMessageAsync( "Invalid max results value. Please enter a valid number." );
			return;
		}

		EntrySearchQuery query = new(
			FileName,
			SelectedComparer?.OpRepr ?? "=",
			CheckedOutMe,
			DeletedRemotely,
			LocalOnly,
			maxResults,
			LocalOnly ? [] : [ .. PropertiesActive ] );

		List<SearchRow> rows = await _search.SearchAsync( query );
		SearchResultsList.Clear();
		foreach( SearchRow row in rows )
			SearchResultsList.Add( row );

		bool remoteSearch = !LocalOnly && !DeletedRemotely;
		await _dialogs.ShowMessageAsync( remoteSearch && rows.Count == 0 ? "No results found." : "Finished!" );
	}

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
		PropertiesActive.Clear();
		SelectedComparer = null;
	}

	[RelayCommand]
	private async Task AddPropertyAsync()
	{
		if( SelectedProperty is not { } property || SelectedPropertyOperator is not { } op )
		{
			await _dialogs.ShowMessageAsync( "Add Property Name and Comparator" );
			return;
		}

		PropertiesActive.Add( new SearchPropertiesRow
		{
			ID = property.ID,
			Comparer = op.Operator,
			Value = PropertyValue,
			Name = property.Name,
			Text = property.Text,
		} );
	}

	[RelayCommand]
	private void DeleteProperties()
	{
		foreach( SearchPropertiesRow row in SelectedActiveProperties.ToList() )
			PropertiesActive.Remove( row );
	}

	[RelayCommand]
	private void ResetProperties() => PropertiesActive.Clear();

	#endregion
	#region Commands: acting on results

	private ArrayList SelectedRemoteIds()
		=> [ .. SelectedResults.Where( r => r.Id is not null ).Select( r => (object)r.Id!.Value ) ];

	[RelayCommand]
	private Task CheckoutAsync() => SetCheckoutAsync( checkout: true );

	[RelayCommand]
	private Task UndoCheckoutAsync() => SetCheckoutAsync( checkout: false );

	[RelayCommand]
	private async Task CheckoutAndOpenAsync()
	{
		if( await SetCheckoutAsync( checkout: true ) )
			await OpenAsync();
	}

	/// <returns>False when nothing was done.</returns>
	private async Task<bool> SetCheckoutAsync( bool checkout )
	{
		if( LocalOnly )
		{
			await _dialogs.ShowMessageAsync( checkout ? "Can't checkout local only entries" : "Can't uncheckout local only entries" );
			return false;
		}

		ArrayList ids = SelectedRemoteIds();
		if( ids.Count == 0 )
			return false;

		_reporter.ShowWindow();
		if( !checkout )
		{
			await _workflow.UnCheckoutAsync( ids );
			return true;
		}
		// make sure the local copies are current before checking them out
		await _workflow.GetLatestAsync( ids );
		await _workflow.CheckoutAsync( ids );
		return true;
	}

	/// <summary>Local results open their folder; remote results download the latest version to temp and open it.</summary>
	[RelayCommand]
	private async Task OpenAsync()
	{
		foreach( SearchRow row in SelectedResults )
		{
			if( row.Id is int id )
				await _files.OpenLatestRemoteAsync( id );
			else if( row.Directory is { } directory )
				_files.OpenFolder( directory );
		}
	}

	/// <summary>Shows the result in the file manager: selects its folder in the tree and the file in the list.</summary>
	[RelayCommand]
	private Task LocateAsync( SearchRow? row )
		=> row is null ? Task.CompletedTask : _locate( _search.TreeSegmentsBelowRoot( row ), row.Name );

	#endregion
}
