using System.Collections;
using System.Collections.Specialized;

using HackPDM.Abstractions.Structures;

namespace HackPDM.Tests.Structures;

public sealed class ObservableRingBufferTests
{
	[Fact]
	public void ImplementsNonGenericIList_ForWinUiItemsControls()
	{
		ObservableRingBuffer<string> buffer = new( 3 );
		IList list = buffer;

		list.Add( "one" );
		list.Add( "two" );

		Assert.Equal( 2, list.Count );
		Assert.Equal( "one", list[ 0 ] );
		Assert.Equal( "two", list[ 1 ] );
		Assert.True( list.Contains( "one" ) );
		Assert.Equal( 1, list.IndexOf( "two" ) );
	}

	[Fact]
	public void Resize_KeepsNewestItemsAndRaisesReset()
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
}
