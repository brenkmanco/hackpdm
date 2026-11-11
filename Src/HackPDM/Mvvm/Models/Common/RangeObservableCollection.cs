using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace HackPDM.Mvvm.Models.Common;

/// <summary>ObservableCollection that can swap its contents with a single Reset notification.</summary>
public class RangeObservableCollection<T> : ObservableCollection<T>
{
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
