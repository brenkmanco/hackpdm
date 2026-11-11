using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;

using HackPDM.Infrastructure.Odoo;
using HackPDM.Shared.GlobalData;
using HackPDM.Mvvm.Views.Common.Imaging;

using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

using Windows.Storage.Streams;

namespace HackPDM.Mvvm.Views.Common.Converters;

/// <summary>
/// Turns icon keys into images for the view. Bundled assets are looked up by key; file types without
/// a bundled icon fall back to the icon stored on the server's hp.type. Must be used on the UI thread.
/// </summary>
public static class EntryIcons
{
	private const string GenericFile = "def_fi";
	// one image per key, shared by every row that shows it
	private static readonly Dictionary<string, ImageSource?> Cache = [];

	// creating the provider registers the bundled asset map
	static EntryIcons() => _ = HackPDM.Mvvm.Views.Common.Controls.UISettings.ImageProvider;

	/// <summary>A bundled asset ("ok", "fo_loc", ...), or null if there is none.</summary>
	public static ImageSource? Asset( string key )
	{
		if( Cache.TryGetValue( key, out var cached ) )
			return cached;
		ImageSource? image = AssetsImageProvider.AssetMap.TryGetValue( key, out var uri )
			? uri.EndsWith( ".svg", StringComparison.OrdinalIgnoreCase ) ? new SvgImageSource( new Uri( uri ) ) : new BitmapImage( new Uri( uri ) )
			: null;
		return Cache[ key ] = image;
	}

	/// <summary>Icon for a file extension (without the dot).</summary>
	public static ImageSource? ForType( string? extension )
	{
		if( string.IsNullOrEmpty( extension ) )
			return Asset( GenericFile );
		return Asset( extension ) ?? ServerTypeIcon( extension ) ?? Asset( GenericFile );
	}

	/// <summary>Icon for a file status; null for statuses without one (no versions, unknown).</summary>
	public static ImageSource? ForStatus( FileStatus status )
		=> Asset( status.ToString().ToLowerInvariant() );

	private static ImageSource? ServerTypeIcon( string extension )
	{
		string key = $"type:{extension}";
		if( Cache.TryGetValue( key, out var cached ) )
			return cached;
		if( !OdooDefaults.Instance.ExtToType.TryGetValue( $".{extension}", out var type ) || type.icon is not { Length: > 0 } bytes )
			return Cache[ key ] = null;

		// returned right away; the bitmap fills in once decoded
		BitmapImage image = new();
		LoadAsync( image, bytes );
		return Cache[ key ] = image;
	}

	private static async void LoadAsync( BitmapImage image, byte[] bytes )
	{
		try
		{
			using InMemoryRandomAccessStream stream = new();
			await stream.WriteAsync( bytes.AsBuffer() );
			stream.Seek( 0 );
			await image.SetSourceAsync( stream );
		}
		catch( Exception e )
		{
			System.Diagnostics.Debug.WriteLine( $"type icon failed to decode: {e.Message}" );
		}
	}
}

/// <summary>File extension → file-type icon.</summary>
public partial class TypeIconConverter : IValueConverter
{
	public object? Convert( object value, Type targetType, object parameter, string language ) => EntryIcons.ForType( value as string );
	public object ConvertBack( object value, Type targetType, object parameter, string language ) => throw new NotSupportedException();
}

/// <summary><see cref="FileStatus"/> → status overlay icon.</summary>
public partial class StatusIconConverter : IValueConverter
{
	public object? Convert( object value, Type targetType, object parameter, string language ) => value is FileStatus status ? EntryIcons.ForStatus( status ) : null;
	public object ConvertBack( object value, Type targetType, object parameter, string language ) => throw new NotSupportedException();
}
