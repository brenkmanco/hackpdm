using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace HackPDM.Mvvm.Models.Common;

/// <summary>ObservableCollection that can swap its contents with a single Reset notification.</summary>
/// <typeparam name="T">The element type.</typeparam>
public class RangeObservableCollection<T> : ObservableCollection<T>
{
	/// <summary>Replaces the whole contents, raising a single Reset notification instead of one per item.</summary>
	/// <param name="items">The new contents, in order.</param>
	/// <exception cref="System.InvalidOperationException">Called from a handler of this collection's change notification.</exception>
	public void ReplaceAll( IEnumerable<T> items )
	{
		CheckReentrancy();
		Items.Clear();
		foreach( var item in items )
			Items.Add( item );
		OnPropertyChanged( new System.ComponentModel.PropertyChangedEventArgs( nameof( Count ) ) );
		OnPropertyChanged( new System.ComponentModel.PropertyChangedEventArgs( "Item[]" ) );
		OnCollectionChanged( new System.Collections.Specialized.NotifyCollectionChangedEventArgs(
			System.Collections.Specialized.NotifyCollectionChangedAction.Reset ) );
	}
}
