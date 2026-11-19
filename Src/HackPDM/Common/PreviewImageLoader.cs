using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;

using HackPDM.Core.Configuration;

using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;

namespace HackPDM.Common;

public sealed record EntryPreview( byte[]? ImageBytes, string? LocalFilePath );

public interface IPreviewImageLoader
{
	Task<ImageSource?> FromBytesAsync( byte[]? bytes );
	Task<ImageSource?> FromFileIconAsync( string? filePath, uint size = 512 );
}

public sealed class PreviewImageLoader : IPreviewImageLoader
{
	public async Task<ImageSource?> FromBytesAsync( byte[]? bytes )
	{
		if( bytes is not { Length: > 0 } )
			return null;

		using InMemoryRandomAccessStream stream = new();
		await stream.WriteAsync( bytes.AsBuffer() );
		stream.Seek( 0 );

		BitmapImage image = new();
		await image.SetSourceAsync( stream );
		return image;
	}

	public async Task<ImageSource?> FromFileIconAsync( string? filePath, uint size = 512 )
	{
#if !WINDOWS
		await Task.CompletedTask;
		return null;
#else
		try
		{
			if( string.IsNullOrEmpty( filePath ) || !File.Exists( filePath ) )
				return null;

			StorageFile file = await StorageFile.GetFileFromPathAsync( filePath );
			using StorageItemThumbnail? thumbnail = await file.GetThumbnailAsync( ThumbnailMode.SingleItem, size, ThumbnailOptions.ResizeThumbnail );
			if( thumbnail is null )
				return null;

			BitmapImage image = new();
			await image.SetSourceAsync( thumbnail );
			return image;
		}
		catch( Exception ex )
		{
			HackLogger.Debug( "PreviewImageLoader", $"Failed to get file icon for '{filePath}': {ex.Message}", ex );
			return null;
		}
#endif
	}
}
