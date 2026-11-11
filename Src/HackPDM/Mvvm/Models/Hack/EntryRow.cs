using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

using HackPDM.Core;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Mvvm.Models.Hack;

// No UI-framework types in this file: view models use these directly.

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
public class EntryRow : HackPDM.Domain.Representation.EntryRow
{
	public override FileInfo? LocalFile
	{
		get
		{
			if (field is not null) return field;

			string? path = ReprType switch
			{
				EntryReprType.Both or EntryReprType.Remote => PwaPaths.ToAbsolute( FullName ),
				EntryReprType.Local => FullName,
				_ => null,
			};
			
			FileInfo? file = string.IsNullOrEmpty(path) ? null : new(path);
			field = file?.Exists is true ? file : null;
			return field;
		}
		set;
	}
	public override bool? IsLocal
	{
		get
		{
			field = Id switch
			{
				null => null,
				0 => true,
				_ => LocalFile?.Exists ?? false,
			};
			
			return field;
		}
	}
	public override bool? IsRemote
	{
		get
		{
			field = Id switch
			{
				null => null,
				not 0 => true,
				_ => false,
			};
			return field;
		}
	}
	// need IsLocal to be hit so that LocalFile is evaluated
	public override bool? IsOnlyLocal => IsRemote is false & IsLocal is true;
	public override bool? IsOnlyRemote => IsRemote is true & IsLocal is false;
}
