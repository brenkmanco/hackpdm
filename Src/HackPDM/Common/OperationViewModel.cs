using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;

using HackPDM.Core.Services;

namespace HackPDM.Common;

/// <summary>ObservableCollection that swaps, appends, trims, or sorts its items with a single Reset notification.</summary>
public sealed class RangeObservableCollection<T> : ObservableCollection<T>
{
	private static readonly PropertyChangedEventArgs CountChangedArgs = new( nameof( Count ) );
	private static readonly PropertyChangedEventArgs IndexerChangedArgs = new( "Item[]" );
	private static readonly NotifyCollectionChangedEventArgs ResetChangedArgs = new( NotifyCollectionChangedAction.Reset );

	public void ClearIfNonEmpty()
	{
		if( Items.Count > 0 )
			Clear();
	}

	public void ReplaceAll( IEnumerable<T> items )
	{
		CheckReentrancy();
		if( Items.Count == 0 && items is ICollection<T> { Count: 0 } )
			return;

		Items.Clear();
		if( Items is List<T> list && items is ICollection<T> col && col.Count > 0 )
			list.EnsureCapacity( col.Count );
		foreach( T item in items )
			Items.Add( item );
		NotifyReset();
	}

	public void ReplaceSorted( IEnumerable<T> items, Func<T, IComparable?> keySelector, bool descending )
	{
		List<T> sorted = SortToList( items, keySelector, descending );
		ReplaceAll( sorted );
	}

	public void Sort( Func<T, IComparable?> keySelector, bool descending )
		=> ReplaceAll( SortToList( Items, keySelector, descending ) );

	private static List<T> SortToList( IEnumerable<T> items, Func<T, IComparable?> keySelector, bool descending )
		=> descending
			? [ .. items.OrderByDescending( keySelector, SafeComparableComparer.Instance ) ]
			: [ .. items.OrderBy( keySelector, SafeComparableComparer.Instance ) ];

	private void NotifyReset()
	{
		OnPropertyChanged( CountChangedArgs );
		OnPropertyChanged( IndexerChangedArgs );
		OnCollectionChanged( ResetChangedArgs );
	}
}

/// <summary>Base view model for pages that run PDM operations reporting into the status window.</summary>
public abstract class OperationViewModel( IDialogService dialogs, IOperationReporter reporter ) : ObservableObject
{
	protected IDialogService Dialogs { get; } = dialogs;
	protected IOperationReporter Reporter { get; } = reporter;

	protected async Task RunOperationAsync( Func<Task> operation, string? completedMessage = null )
	{
		Reporter.ShowWindow();
		await operation();
		if( completedMessage is not null )
			await Dialogs.ShowMessageAsync( completedMessage );
	}

	protected Task RunOnIdsAsync( IReadOnlyList<int> ids, Func<IReadOnlyList<int>, Task> operation, string? completedMessage = null )
		=> ids.Count == 0 ? Task.CompletedTask : RunOperationAsync( () => operation( ids ), completedMessage );

	protected static int[] RemoteIds( IEnumerable<int?> ids ) => [ .. ids.OfType<int>().Where( id => id != 0 ) ];
}
