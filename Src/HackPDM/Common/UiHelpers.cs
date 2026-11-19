using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;

using HackPDM.Core.Configuration;
using HackPDM.Core.Theming;

using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;

namespace HackPDM.Common;

/// <summary>Common contract implemented by dockable/detachable tool panels (Status Log, Search Vault).</summary>
public interface IDockablePanel
{
	Window? HostWindow { get; set; }
	void SetHostMode( bool isDocked );
	void SetMinimalMode( bool minimal );
}

/// <summary>Zero-LINQ typed selection extractors for WinUI list/table controls.</summary>
public static class SelectionExtensions
{
	public static List<T> ExtractSelected<T>( this IList<object> items ) where T : class
	{
		if( items.Count == 0 )
			return [];
		List<T> selected = new( items.Count );
		for( int i = 0; i < items.Count; i++ )
		{
			if( items[ i ] is T match )
				selected.Add( match );
		}
		return selected;
	}

	public static T? FirstSelected<T>( this IList<object> items ) where T : class
	{
		for( int i = 0; i < items.Count; i++ )
		{
			if( items[ i ] is T match )
				return match;
		}
		return null;
	}
}

/// <summary>Converts between platform-neutral <see cref="ArgbColor"/>/packed ARGB and <see cref="Color"/>.</summary>
public static class UiColors
{
	public static Color ToUiColor( this ArgbColor c ) => Color.FromArgb( c.A, c.R, c.G, c.B );

	public static Color ToUiColor( uint argb )
		=> Color.FromArgb( (byte)( argb >> 24 ), (byte)( argb >> 16 ), (byte)( argb >> 8 ), (byte)argb );

	public static ArgbColor ToArgbColor( this Color c ) => new( c.A, c.R, c.G, c.B );
}

/// <summary>Lifecycle helpers for replacing or cancelling a nullable <see cref="CancellationTokenSource"/>.</summary>
public static class CtsHelper
{
	public static CancellationTokenSource Replace( ref CancellationTokenSource? slot )
	{
		CancellationTokenSource next = new();
		CancellationTokenSource? previous = Interlocked.Exchange( ref slot, next );
		CancelAndDisposeInternal( previous );
		return next;
	}

	public static void CancelAndDispose( ref CancellationTokenSource? slot )
		=> CancelAndDisposeInternal( Interlocked.Exchange( ref slot, null ) );

	private static void CancelAndDisposeInternal( CancellationTokenSource? cts )
	{
		if( cts is null )
			return;
		try { cts.Cancel(); }
		catch( ObjectDisposedException ) { }
		finally { cts.Dispose(); }
	}
}

public static class VisibilityExtensions
{
	public static Visibility ToVisibility( this bool visible ) => visible ? Visibility.Visible : Visibility.Collapsed;
}

/// <summary>Typed lookups against <see cref="Application.Resources"/> with optional fallback keys.</summary>
public static class ThemeResources
{
	public static TBrush? TryGetBrush<TBrush>( string key ) where TBrush : Brush
		=> Application.Current.Resources.TryGetValue( key, out object? value ) ? value as TBrush : null;

	public static Brush GetBrush( string primaryKey, Brush fallback, string? secondaryKey = null )
		=> TryGetBrush<Brush>( primaryKey )
			?? ( secondaryKey is not null ? TryGetBrush<Brush>( secondaryKey ) : null )
			?? fallback;
}

public static class ClipboardHelper
{
	public static bool TrySetText( string text, string logCategory = "Clipboard" )
	{
		try
		{
			DataPackage package = new();
			package.SetText( text );
			Clipboard.SetContent( package );
			return true;
		}
		catch( Exception ex )
		{
			HackLogger.Warn( logCategory, $"Failed to copy text to clipboard: {ex.Message}", ex );
			return false;
		}
	}
}

public static class KeyboardHelper
{
	public static bool IsDown( VirtualKey key )
		=> ( InputKeyboardSource.GetKeyStateForCurrentThread( key ) & CoreVirtualKeyStates.Down ) == CoreVirtualKeyStates.Down;
}

public static class BitmapImageLoader
{
	public static async Task LoadFromBytesAsync( BitmapImage image, byte[] bytes )
	{
		using InMemoryRandomAccessStream stream = new();
		await stream.WriteAsync( bytes.AsBuffer() );
		stream.Seek( 0 );
		await image.SetSourceAsync( stream );
	}
}

public static class VisualTreeExtensions
{
	public static T? FindAncestor<T>( this DependencyObject? start, DependencyObject? stopAt = null ) where T : DependencyObject
	{
		for( DependencyObject? current = start; current is not null && !ReferenceEquals( current, stopAt ); current = VisualTreeHelper.GetParent( current ) )
		{
			if( current is T match )
				return match;
		}
		return default;
	}
}

/// <summary>Null-safe, heterogeneous-type-safe comparer shared by grouping and observable sorting.</summary>
public sealed class SafeComparableComparer : IComparer, IComparer<IComparable?>
{
	public static readonly SafeComparableComparer Instance = new();

	public int Compare( object? x, object? y )
	{
		if( ReferenceEquals( x, y ) )
			return 0;
		if( x is null )
			return -1;
		if( y is null )
			return 1;
		if( x.GetType() == y.GetType() && x is IComparable cx )
			return cx.CompareTo( y );
		return string.CompareOrdinal( x.ToString(), y.ToString() );
	}

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
