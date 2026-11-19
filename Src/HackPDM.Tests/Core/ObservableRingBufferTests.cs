using System.Collections;
using System.Collections.Specialized;

using HackPDM.Core.Services;

namespace HackPDM.Tests.Core;

public sealed class ObservableRingBufferTests
{
	[Fact]
	public void ImplementsNonGenericIList_ForWinUiItemsControls()
	{
		ObservableRingBuffer<string> buffer = new( 3 );
		IList list = buffer;

		Assert.Equal( 0, list.Add( "one" ) );
		Assert.Equal( 1, list.Add( "two" ) );

		Assert.Equal( 2, list.Count );
		Assert.Equal( "one", list[ 0 ] );
		Assert.Equal( "two", list[ 1 ] );
		Assert.True( list.Contains( "one" ) );
		Assert.Equal( 1, list.IndexOf( "two" ) );
	}

	[Fact]
	public void NewestFirst_PrependsAtZeroAndDropsOldestAtEndWithConsistentCount()
	{
		ObservableRingBuffer<string> buffer = new( 3, newestFirst: true );
		List<(NotifyCollectionChangedAction Action, int Index, int CountDuringEvent, string? Item)> events = [];
		buffer.CollectionChanged += ( _, e ) =>
		{
			int idx = e.Action == NotifyCollectionChangedAction.Remove ? e.OldStartingIndex : e.NewStartingIndex;
			string? item = e.Action == NotifyCollectionChangedAction.Remove
				? e.OldItems?[ 0 ] as string
				: e.NewItems?[ 0 ] as string;
			events.Add( ( e.Action, idx, buffer.Count, item ) );
		};

		buffer.Add( "first" );
		buffer.Add( "second" );
		buffer.Add( "third" );
		Assert.Equal( [ "third", "second", "first" ], buffer );

		buffer.Add( "fourth" );
		Assert.Equal( [ "fourth", "third", "second" ], buffer );
		Assert.Equal( 3, buffer.Count );

		// When evicting, Remove fires at last index while Count == Capacity - 1, then Add at 0 with Count == Capacity.
		Assert.Equal( ( NotifyCollectionChangedAction.Remove, 2, 2, "first" ), events[ ^2 ] );
		Assert.Equal( ( NotifyCollectionChangedAction.Add, 0, 3, "fourth" ), events[ ^1 ] );
	}

	[Fact]
	public void NewestFirst_AddRange_SmallAndLargeBatchesKeepNewestAtZero()
	{
		ObservableRingBuffer<int> buffer = new( 5, newestFirst: true );
		int resetCount = 0;
		buffer.CollectionChanged += ( _, e ) =>
		{
			if( e.Action == NotifyCollectionChangedAction.Reset )
				resetCount++;
		};

		// Small batch (<= 4) notifies incrementally (no Reset).
		buffer.AddRange( [ 1, 2, 3 ] );
		Assert.Equal( [ 3, 2, 1 ], buffer );
		Assert.Equal( 0, resetCount );

		// Larger batch (> 4, < Capacity) wraps and raises a single Reset.
		ObservableRingBuffer<int> larger = new( 8, newestFirst: true );
		int largerResets = 0;
		larger.CollectionChanged += ( _, e ) => { if( e.Action == NotifyCollectionChangedAction.Reset ) largerResets++; };
		larger.AddRange( [ 1, 2 ] );
		larger.AddRange( [ 3, 4, 5, 6, 7, 8, 9 ] );
		Assert.Equal( [ 9, 8, 7, 6, 5, 4, 3, 2 ], larger );
		Assert.Equal( 1, largerResets );

		// Batch >= Capacity retains only the last Capacity elements in newest-first order.
		buffer.AddRange( [ 10, 11, 12, 13, 14, 15, 16 ] );
		Assert.Equal( [ 16, 15, 14, 13, 12 ], buffer );
		Assert.Equal( 1, resetCount );
	}

	[Fact]
	public void OldestFirst_Resize_KeepsNewestItemsAndRaisesReset()
	{
		ObservableRingBuffer<int> buffer = new( 5 );
		for( int i = 1; i <= 5; i++ )
			buffer.Add( i );

		NotifyCollectionChangedAction? action = null;
		buffer.CollectionChanged += ( _, e ) => action = e.Action;

		buffer.Resize( 3 );

		Assert.Equal( 3, buffer.Capacity );
		Assert.Equal( [ 3, 4, 5 ], buffer );
		Assert.Equal( NotifyCollectionChangedAction.Reset, action );

		buffer.Add( 6 );
		Assert.Equal( [ 4, 5, 6 ], buffer );
	}

	[Fact]
	public void NewestFirst_Resize_KeepsNewestItemsAndPreservesReverseOrder()
	{
		ObservableRingBuffer<int> buffer = new( 5, newestFirst: true );
		for( int i = 1; i <= 5; i++ )
			buffer.Add( i );

		NotifyCollectionChangedAction? action = null;
		buffer.CollectionChanged += ( _, e ) => action = e.Action;

		buffer.Resize( 3 );

		Assert.Equal( 3, buffer.Capacity );
		Assert.Equal( [ 5, 4, 3 ], buffer );
		Assert.Equal( NotifyCollectionChangedAction.Reset, action );

		buffer.Add( 6 );
		Assert.Equal( [ 6, 5, 4 ], buffer );
	}

	[Fact]
	public void CopyTo_CopiesWrappedNewestFirstItemsInLogicalOrder()
	{
		ObservableRingBuffer<int> buffer = new( 4, newestFirst: true );
		for( int i = 1; i <= 6; i++ )
			buffer.Add( i );

		int[] dest = new int[ 6 ];
		buffer.CopyTo( dest, 1 );

		Assert.Equal( [ 0, 6, 5, 4, 3, 0 ], dest );
	}

	[Fact]
	public void CapacityOne_NewestFirstAndOldestFirst_ReplaceSingleSlotCleanly()
	{
		ObservableRingBuffer<string> rev = new( 1, newestFirst: true );
		rev.Add( "a" );
		rev.Add( "b" );
		Assert.Equal( [ "b" ], rev );

		ObservableRingBuffer<string> fwd = new( 1, newestFirst: false );
		fwd.Add( "a" );
		fwd.Add( "b" );
		Assert.Equal( [ "b" ], fwd );
	}
}
