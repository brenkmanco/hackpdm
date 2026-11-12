using System;
using System.Collections.Generic;
using System.IO;

using HackPDM.Shared.GlobalData;

using SB = HackPDM.Shared.GlobalData.StorageBox;

namespace HackPDM.Mvvm.Views.Common.Imaging;

/// <summary>
/// Image key (usually a file extension) → image URI. The bundled images come from
/// <c>BundledImages</c>, generated from the Assets folder at build time (see the GenerateAssetMap
/// target in HackPDM.csproj); images the user drops in the local Assets/Images folder replace them.
/// </summary>
public static partial class Assets
{
	public static readonly Dictionary<string, string> AssetMap;

	/// <summary>Keys that don't match a file name: extensions that share an icon, and extension icons with no Icons/ equivalent.</summary>
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

	/// <summary>
	/// Images the user adds to replace or extend the bundled ones, keyed by file name without extension
	/// (HackPDM/Assets/Images in <see cref="AppDataPaths.Data"/>). A plain folder, not the packaged app's
	/// LocalFolder, so it works unpackaged on Windows and on every other platform.
	/// </summary>
	public static string LocalImagesFolder => Path.Combine( AppDataPaths.Data, SB.ASSETSFOLDER, SB.IMAGEFOLDER );

	static Assets()
	{
		AssetMap = [];
		foreach( var (key, path) in BundledImages )
			AssetMap[ key ] = $"{SB.ASSETSPREFIX}{SB.ASSETSFOLDER}/{path}";
		foreach( var (key, path) in ExtraImages )
			AssetMap[ key ] = $"{SB.ASSETSPREFIX}{SB.ASSETSFOLDER}/{path}";
		LoadInLocalDictionary();
	}

	private static void LoadInLocalDictionary()
	{
		// runs from the static constructor: any exception here would break every icon
		try
		{
			string folder = LocalImagesFolder;
			if( !Directory.Exists( folder ) )
				return;
			foreach( string file in Directory.EnumerateFiles( folder ) )
			{
				// keyed like the built-in entries (no extension); a local image replaces a built-in one
				AssetMap[ Path.GetFileNameWithoutExtension( file ) ] = new Uri( file ).AbsoluteUri;
			}
		}
		catch( Exception e ) when( e is IOException or UnauthorizedAccessException )
		{
			System.Diagnostics.Debug.WriteLine( $"local image assets not loaded: {e.Message}" );
		}
	}
}
