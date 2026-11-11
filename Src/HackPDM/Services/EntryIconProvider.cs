using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

using HackPDM.Infrastructure.Odoo;
using HackPDM.UI.Compatibility;

using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

using Windows.Storage.Streams;

using static HackPDM.UI.Controls.UISettings;

namespace HackPDM.Services;

/// <summary>Icons for files, statuses and folders: bundled assets first, then the icon stored on the hp.type.</summary>
public interface IEntryIconProvider
{
	/// <summary>A bundled asset by key (status codes like "ok", folder keys like "fo_loc").</summary>
	Task<ImageSource?> GetAssetAsync( string key );
	/// <summary>Icon for a file extension (without the dot); falls back to the server's type icon, then a generic file.</summary>
	Task<ImageSource?> GetTypeIconAsync( string? extension );
}

public sealed class EntryIconProvider : IEntryIconProvider
{
	// decoded hp.type icons, one per extension
	private static readonly ConcurrentDictionary<string, ImageSource> RemoteIcons = new();

	private static AssetsImageProvider? Assets => ImageProvider;

	public async Task<ImageSource?> GetAssetAsync( string key )
		=> Assets is { } assets ? await assets.GetImageAsync( key ) : null;

	public async Task<ImageSource?> GetTypeIconAsync( string? extension )
		=> await GetAssetAsync( extension ?? "lo" )
			?? await GetRemoteTypeIconAsync( extension )
			?? await GetAssetAsync( "def_fi" );

	private static async Task<ImageSource?> GetRemoteTypeIconAsync( string? extension )
	{
		if( string.IsNullOrEmpty( extension ) )
			return null;
		if( RemoteIcons.TryGetValue( extension, out var cached ) )
			return cached;
		if( !OdooDefaults.Instance.ExtToType.TryGetValue( $".{extension}", out var type ) || type.icon is not { Length: > 0 } bytes )
			return null;

		try
		{
			using InMemoryRandomAccessStream stream = new();
			using DataWriter writer = new( stream );
			writer.WriteBytes( bytes );
			await writer.StoreAsync();
			await writer.FlushAsync();
			stream.Seek( 0 );

			BitmapImage image = new();
			await image.SetSourceAsync( stream );
			return RemoteIcons.GetOrAdd( extension, image );
		}
		catch
		{
			return null;
		}
	}
}
