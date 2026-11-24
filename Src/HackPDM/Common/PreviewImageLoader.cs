using System;
#if WINDOWS
using System.IO;
#endif
using System.Threading.Tasks;

#if WINDOWS
using HackPDM.Core.Configuration;
#endif

using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

#if WINDOWS
using Windows.Storage;
using Windows.Storage.FileProperties;
#endif

namespace HackPDM.Common;

[System.ComponentModel.Bindable( true )]
public sealed record EntryPreview( byte[]? ImageBytes, string? LocalFilePath );

public interface IPreviewImageLoader
{
	ValueTask<ImageSource?> FromBytesAsync( byte[]? bytes );
	ValueTask<ImageSource?> FromFileIconAsync( string? filePath, uint size = 512 );
}

public sealed class PreviewImageLoader : IPreviewImageLoader
{
	private const int MaxPreviewDecodePixels = 512;

	public ValueTask<ImageSource?> FromBytesAsync( byte[]? bytes )
	{
		if( bytes is not { Length: > 0 } )
			return ValueTask.FromResult<ImageSource?>( null );
		return LoadFromBytesCoreAsync( bytes );
	}

	private static async ValueTask<ImageSource?> LoadFromBytesCoreAsync( byte[] bytes )
	{
		BitmapImage image = new() { DecodePixelWidth = MaxPreviewDecodePixels };
		await BitmapImageLoader.LoadFromBytesAsync( image, bytes );
		return image;
	}

	public ValueTask<ImageSource?> FromFileIconAsync( string? filePath, uint size = 512 )
	{
#if !WINDOWS
		_ = ( filePath, size );
		return ValueTask.FromResult<ImageSource?>( null );
#else
		if( string.IsNullOrEmpty( filePath ) )
			return ValueTask.FromResult<ImageSource?>( null );
		return LoadFileIconCoreAsync( filePath, size );
#endif
	}

#if WINDOWS
	private static async ValueTask<ImageSource?> LoadFileIconCoreAsync( string filePath, uint size )
	{
		try
		{
			if( !await Task.Run( () => File.Exists( filePath ) ) )
				return null;

			StorageFile file = await StorageFile.GetFileFromPathAsync( filePath );
			using StorageItemThumbnail? thumbnail = await file.GetThumbnailAsync( ThumbnailMode.SingleItem, size, ThumbnailOptions.ResizeThumbnail );
			if( thumbnail is null )
				return null;

			BitmapImage image = new()
			{
				DecodePixelWidth = (int)Math.Clamp( size, 1, MaxPreviewDecodePixels ),
			};
			await image.SetSourceAsync( thumbnail );
			return image;
		}
		catch( Exception ex )
		{
			HackLogger.Debug( nameof( PreviewImageLoader ), $"Failed to get file icon for '{filePath}': {ex.Message}", ex );
			return null;
		}
	}
#endif
}
