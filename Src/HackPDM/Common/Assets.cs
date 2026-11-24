using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using HackPDM.Core.Configuration;
using HackPDM.Core.Odoo;
using HackPDM.Core.Odoo.Models;
using HackPDM.Core.Pdm;

using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

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

	private static readonly Dictionary<string, string> BundledAssetMap;
	private static Dictionary<string, string>? _localOverrides;
	private static int _localOverridesStarted;

	private static string LocalImagesFolder => Path.Combine( AppDataPaths.Data, "Assets", "Images" );

	static Assets()
	{
		BundledAssetMap = new( BundledImages.Length + ExtraImages.Length, StringComparer.OrdinalIgnoreCase );
		foreach( var (key, path) in BundledImages )
			BundledAssetMap[ key ] = $"{Prefix}{path}";
		foreach( var (key, path) in ExtraImages )
			BundledAssetMap[ key ] = $"{Prefix}{path}";
	}

	internal static bool TryGetUri( string key, [NotNullWhen( true )] out string? uri )
	{
		EnsureLocalOverridesStarted();
		if( Volatile.Read( ref _localOverrides ) is { } overrides && overrides.TryGetValue( key, out uri ) )
			return true;
		return BundledAssetMap.TryGetValue( key, out uri );
	}

	private static void EnsureLocalOverridesStarted()
	{
		if( Interlocked.CompareExchange( ref _localOverridesStarted, 1, 0 ) != 0 )
			return;
		_ = Task.Run( LoadLocalOverridesCore );
	}

	private static void LoadLocalOverridesCore()
	{
		try
		{
			string folder = LocalImagesFolder;
			if( !Directory.Exists( folder ) )
				return;

			Dictionary<string, string>? overrides = null;
			foreach( string file in Directory.EnumerateFiles( folder ) )
			{
				overrides ??= new Dictionary<string, string>( StringComparer.OrdinalIgnoreCase );
				overrides[ Path.GetFileNameWithoutExtension( file ) ] = new Uri( file ).AbsoluteUri;
			}

			if( overrides is { Count: > 0 } )
			{
				Volatile.Write( ref _localOverrides, overrides );
				UiDispatcher.RunOrPost( EntryIcons.InvalidateAllCache );
			}
		}
		catch( Exception e ) when( e is IOException or UnauthorizedAccessException )
		{
			HackLogger.Warn( nameof( Assets ), $"Local image assets not loaded: {e.Message}", e );
		}
	}
}

/// <summary>Caches and resolves file-type and status icons for the UI thread.</summary>
public static class EntryIcons
{
	private const string GenericFile = "def_fi";
	private const int StatusCount = (int)FileStatus.Unknown + 1;
	private const int IconDecodePixels = 48;

	private static readonly Dictionary<string, ImageSource?> Cache = new( StringComparer.OrdinalIgnoreCase );
	private static readonly Dictionary<string, ImageSource?> TypeCache = new( StringComparer.OrdinalIgnoreCase );
	private static readonly ImageSource?[] StatusCache = new ImageSource?[ StatusCount ];
	private static readonly bool[] StatusCached = new bool[ StatusCount ];

	public static ImageSource? Asset( string key )
	{
		if( Cache.TryGetValue( key, out ImageSource? cached ) )
			return cached;
		ImageSource? image = Assets.TryGetUri( key, out string? uri )
			? uri.EndsWith( ".svg", StringComparison.OrdinalIgnoreCase )
				? new SvgImageSource( new Uri( uri ) )
				{
					RasterizePixelWidth = IconDecodePixels,
					RasterizePixelHeight = IconDecodePixels,
				}
				: new BitmapImage( new Uri( uri ) )
				{
					DecodePixelWidth = IconDecodePixels,
				}
			: null;
		return Cache[ key ] = image;
	}

	public static ImageSource? ForType( string? extension )
	{
		if( string.IsNullOrWhiteSpace( extension ) )
			return Asset( GenericFile );
		if( TypeCache.TryGetValue( extension, out ImageSource? cached ) )
			return cached;

		string normalized = extension.Trim().TrimStart( '.' );
		if( normalized.Length == 0 )
			return TypeCache[ extension ] = Asset( GenericFile );

		ImageSource? resolved = Asset( normalized ) ?? ServerTypeIcon( normalized );
		if( resolved is not null )
			return TypeCache[ extension ] = resolved;

		ImageSource? fallback = Asset( GenericFile );
		if( OdooDefaults.Instance.ExtToType.Count > 0 )
			TypeCache[ extension ] = fallback;
		return fallback;
	}

	public static ImageSource? ForStatus( FileStatus status )
	{
		int idx = (int)status;
		if( (uint)idx < StatusCount && StatusCached[ idx ] )
			return StatusCache[ idx ];

		string key = status switch
		{
			FileStatus.Lo => "lo",
			FileStatus.Ro => "ro",
			FileStatus.Ok => "ok",
			FileStatus.Nv => "nv",
			FileStatus.Lm => "lm",
			FileStatus.Dt => "dt",
			FileStatus.Ds => "ds",
			FileStatus.If => "if",
			FileStatus.Ft => "ft",
			FileStatus.Cm => "cm",
			FileStatus.Co => "co",
			FileStatus.NoVersions => "noversions",
			FileStatus.Unknown => "unknown",
			_ => status.ToString().ToLowerInvariant(),
		};
		ImageSource? icon = Asset( key );
		if( (uint)idx < StatusCount )
		{
			StatusCache[ idx ] = icon;
			StatusCached[ idx ] = true;
		}
		return icon;
	}

	public static void ClearServerCache()
	{
		TypeCache.Clear();
		List<string>? serverKeys = null;
		foreach( string key in Cache.Keys )
		{
			if( key.StartsWith( "type:", StringComparison.Ordinal ) )
				( serverKeys ??= [] ).Add( key );
		}
		if( serverKeys is not null )
		{
			for( int i = 0; i < serverKeys.Count; i++ )
				Cache.Remove( serverKeys[ i ] );
		}
	}

	internal static void InvalidateAllCache()
	{
		Cache.Clear();
		TypeCache.Clear();
		Array.Clear( StatusCache );
		Array.Clear( StatusCached );
	}

	private static ImageSource? ServerTypeIcon( string extension )
	{
		string key = $"type:{extension}";
		if( Cache.TryGetValue( key, out ImageSource? cached ) )
			return cached;
		if( OdooDefaults.Instance.ExtToType.Count == 0 )
			return null;
		if( !OdooDefaults.Instance.ExtToType.TryGetValue( $".{extension}", out HpType? type ) || type.icon is not { Length: > 0 } bytes )
			return Cache[ key ] = null;

		BitmapImage image = new() { DecodePixelWidth = IconDecodePixels };
		_ = LoadBitmapBytesAsync( image, bytes );
		return Cache[ key ] = image;
	}

	private static async Task LoadBitmapBytesAsync( BitmapImage image, byte[] bytes )
	{
		try
		{
			await BitmapImageLoader.LoadFromBytesAsync( image, bytes );
		}
		catch( Exception e )
		{
			HackLogger.Debug( nameof( EntryIcons ), $"Type icon failed to decode: {e.Message}", e );
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
