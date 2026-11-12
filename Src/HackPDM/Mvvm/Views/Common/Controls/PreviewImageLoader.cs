using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;

using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;

namespace HackPDM.Mvvm.Views.Common.Controls;

/// <summary>Turns preview bytes or a local file's shell icon into something an Image control can show.</summary>
public interface IPreviewImageLoader
{
	/// <summary>Decodes encoded image bytes (PNG, JPEG, BMP...) into an image source.</summary>
	/// <param name="bytes">The encoded image; null or empty gives null.</param>
	/// <returns>The decoded image; null when there are no bytes.</returns>
	/// <remarks>Call on the UI thread. Undecodable bytes make the returned task fault.</remarks>
	Task<ImageSource?> FromBytesAsync( byte[]? bytes );

	/// <summary>Gets the shell's thumbnail (or icon) of a local file.</summary>
	/// <param name="filePath">The local file; null, empty or missing gives null.</param>
	/// <param name="size">The requested size of the longer side, in pixels.</param>
	/// <returns>The thumbnail; null when the file is missing or the platform has no thumbnail for it (errors go to the debug output).</returns>
	/// <remarks>Call on the UI thread.</remarks>
	Task<ImageSource?> FromFileIconAsync( string? filePath, uint size = 512 );
}

/// <inheritdoc cref="IPreviewImageLoader"/>
public sealed class PreviewImageLoader : IPreviewImageLoader
{
	/// <inheritdoc/>
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

	/// <inheritdoc/>
	public async Task<ImageSource?> FromFileIconAsync( string? filePath, uint size = 512 )
	{
		try
		{
			if( string.IsNullOrEmpty( filePath ) || !File.Exists( filePath ) )
				return null;

			StorageFile file = await StorageFile.GetFileFromPathAsync( filePath );
			using StorageItemThumbnail? thumbnail = await file.GetThumbnailAsync( Windows.Storage.FileProperties.ThumbnailMode.SingleItem, size, Windows.Storage.FileProperties.ThumbnailOptions.ResizeThumbnail );
			if( thumbnail is null )
				return null;

			BitmapImage image = new();
			await image.SetSourceAsync( thumbnail );
			return image;
		}
		catch( Exception ex )
		{
			Debug.WriteLine( $"Failed to get icon: {ex.Message}" );
			return null;
		}
	}
}
