using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;

using HackPDM.Core.Configuration;
using HackPDM.Core.Odoo;
using HackPDM.Core.Odoo.Models;
using HackPDM.Core.Pdm;

using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

using Windows.Storage.Streams;

namespace HackPDM.Common;

/// <summary>Maps bundled and user-overridden image keys to ms-appx/file URIs.</summary>
public static partial class Assets
{
	private const string Prefix = "ms-appx:///Assets/";

	private static readonly (string Key, string Path)[] ExtraImages =
	[
		( "jpg", "Icons/gen.svg" ),
		( "jpeg", "Icons/gen.svg" ),
		( "bmp", "Icons/gen.svg" ),
		( "png", "Icons/gen.svg" ),
		( "3mf", "ExtensionIcons/3mf.png" ),
		( "asmdot", "ExtensionIcons/asmdot.png" ),
		( "asmprp", "ExtensionIcons/asmprp.png" ),
		( "settings", "ExtensionIcons/settings.png" ),
	];

	public static readonly Dictionary<string, string> AssetMap;
	public static string LocalImagesFolder => Path.Combine( AppDataPaths.Data, "Assets", "Images" );

	static Assets()
	{
		AssetMap = new( StringComparer.OrdinalIgnoreCase );
		foreach( var (key, path) in BundledImages )
			AssetMap[ key ] = $"{Prefix}{path}";
		foreach( var (key, path) in ExtraImages )
			AssetMap[ key ] = $"{Prefix}{path}";
		LoadLocalOverrides();
	}

	private static void LoadLocalOverrides()
	{
		try
		{
			string folder = LocalImagesFolder;
			if( !Directory.Exists( folder ) )
				return;
			foreach( string file in Directory.EnumerateFiles( folder ) )
				AssetMap[ Path.GetFileNameWithoutExtension( file ) ] = new Uri( file ).AbsoluteUri;
		}
		catch( Exception e ) when( e is IOException or UnauthorizedAccessException )
		{
			HackLogger.Warn( "Assets", $"Local image assets not loaded: {e.Message}", e );
		}
	}
}

/// <summary>Caches and resolves file-type and status icons for the UI thread.</summary>
public static class EntryIcons
{
	private const string GenericFile = "def_fi";
	private static readonly Dictionary<string, ImageSource?> Cache = new( StringComparer.OrdinalIgnoreCase );

	public static ImageSource? Asset( string key )
	{
		if( Cache.TryGetValue( key, out ImageSource? cached ) )
			return cached;
		ImageSource? image = Assets.AssetMap.TryGetValue( key, out string? uri )
			? uri.EndsWith( ".svg", StringComparison.OrdinalIgnoreCase )
				? new SvgImageSource( new Uri( uri ) )
				: new BitmapImage( new Uri( uri ) )
			: null;
		return Cache[ key ] = image;
	}

	public static ImageSource? ForType( string? extension )
	{
		if( string.IsNullOrWhiteSpace( extension ) )
			return Asset( GenericFile );
		string normalized = extension.Trim().TrimStart( '.' );
		return normalized.Length == 0
			? Asset( GenericFile )
			: Asset( normalized ) ?? ServerTypeIcon( normalized ) ?? Asset( GenericFile );
	}

	public static ImageSource? ForStatus( FileStatus status )
		=> Asset( status.ToString().ToLowerInvariant() );

	private static ImageSource? ServerTypeIcon( string extension )
	{
		string key = $"type:{extension}";
		if( Cache.TryGetValue( key, out ImageSource? cached ) )
			return cached;
		if( !OdooDefaults.Instance.ExtToType.TryGetValue( $".{extension}", out HpType? type ) || type.icon is not { Length: > 0 } bytes )
			return Cache[ key ] = null;

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
			HackLogger.Debug( "EntryIcons", $"Type icon failed to decode: {e.Message}", e );
		}
	}
}

public sealed partial class TypeIconConverter : IValueConverter
{
	public object? Convert( object value, Type targetType, object parameter, string language ) => EntryIcons.ForType( value as string );
	public object ConvertBack( object value, Type targetType, object parameter, string language ) => throw new NotSupportedException();
}

public sealed partial class StatusIconConverter : IValueConverter
{
	public object? Convert( object value, Type targetType, object parameter, string language )
		=> value is FileStatus status ? EntryIcons.ForStatus( status ) : null;
	public object ConvertBack( object value, Type targetType, object parameter, string language ) => throw new NotSupportedException();
}

public sealed partial class FileStatusDescriptionConverter : IValueConverter
{
	public static string Describe( FileStatus status ) => status switch
	{
		FileStatus.Lo => "Lo — Local Only",
		FileStatus.Ro => "Ro — Remote Only",
		FileStatus.Ok => "Ok — Latest (In Sync)",
		FileStatus.Nv => "Nv — Newer on Server",
		FileStatus.Lm => "Lm — Modified Locally",
		FileStatus.Dt => "Dt — Deleted on Server",
		FileStatus.Ds => "Ds — Destroyed",
		FileStatus.If => "If — Ignored by Filter",
		FileStatus.Ft => "Ft — Unregistered Type",
		FileStatus.Cm => "Cm — Checked Out (Me)",
		FileStatus.Co => "Co — Checked Out (Other)",
		FileStatus.NoVersions => "No Versions",
		_ => status.ToString(),
	};

	public object Convert( object? value, Type targetType, object? parameter, string language )
		=> value is FileStatus status ? Describe( status ) : string.Empty;
	public object ConvertBack( object? value, Type targetType, object? parameter, string language ) => throw new NotSupportedException();
}
