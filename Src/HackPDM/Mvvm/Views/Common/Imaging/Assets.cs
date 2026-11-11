using System;
using System.Collections.Generic;
using System.IO;

using Windows.Storage;

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

	private static StorageFolder Storage => ApplicationData.Current.LocalFolder;

	static Assets()
	{
		AssetMap = [];
		foreach( var (key, path) in BundledImages )
			AssetMap[ key ] = $"{SB.ASSETSPREFIX}{SB.ASSETSFOLDER}/{path}";
		foreach( var (key, path) in ExtraImages )
			AssetMap[ key ] = $"{SB.ASSETSPREFIX}{SB.ASSETSFOLDER}/{path}";
		LoadInLocalDictionary();
	}

	private static async void LoadInLocalDictionary()
	{
		const string msDir = $"{SB.LOCALPREFIX}/{SB.ASSETSFOLDER}/{SB.IMAGEFOLDER}";

		// async void (it runs from the static constructor): any exception here would crash the app
		try
		{
			var assetFolder = await Storage.CreateFolderAsync( SB.ASSETSFOLDER, CreationCollisionOption.OpenIfExists );
			var imageFolder = await assetFolder.CreateFolderAsync( SB.IMAGEFOLDER, CreationCollisionOption.OpenIfExists );
			foreach( StorageFile file in await imageFolder.GetFilesAsync() )
			{
				// keyed like the built-in entries (no extension); a local image replaces a built-in one
				AssetMap[ Path.GetFileNameWithoutExtension( file.Name ) ] = $"{msDir}/{file.Name}";
			}
		}
		catch( Exception e )
		{
			System.Diagnostics.Debug.WriteLine( $"local image assets not loaded: {e.Message}" );
		}
	}
}
