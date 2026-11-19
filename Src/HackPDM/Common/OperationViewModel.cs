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

/// <summary>ObservableCollection that swaps or sorts its items with a single Reset notification.</summary>
public class RangeObservableCollection<T> : ObservableCollection<T>
{
	public void ReplaceAll( IEnumerable<T> items )
	{
		CheckReentrancy();
		Items.Clear();
		foreach( T item in items )
			Items.Add( item );
		OnPropertyChanged( new PropertyChangedEventArgs( nameof( Count ) ) );
		OnPropertyChanged( new PropertyChangedEventArgs( "Item[]" ) );
		OnCollectionChanged( new NotifyCollectionChangedEventArgs( NotifyCollectionChangedAction.Reset ) );
	}

	public void Sort( Func<T, IComparable?> keySelector, bool descending )
	{
		List<T> sorted = descending
			? [ .. Items.OrderByDescending( keySelector, ComparableComparer.Instance ) ]
			: [ .. Items.OrderBy( keySelector, ComparableComparer.Instance ) ];
		ReplaceAll( sorted );
	}

	private sealed class ComparableComparer : IComparer<IComparable?>
	{
		public static readonly ComparableComparer Instance = new();

		public int Compare( IComparable? x, IComparable? y )
		{
			if( ReferenceEquals( x, y ) )
				return 0;
			if( x is null )
				return -1;
			if( y is null )
				return 1;
			return x.GetType() == y.GetType()
				? x.CompareTo( y )
				: string.CompareOrdinal( x.ToString(), y.ToString() );
		}
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
