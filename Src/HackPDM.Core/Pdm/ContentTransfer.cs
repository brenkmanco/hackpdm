using System.Security.Cryptography;

using HackPDM.Core.Odoo;
using HackPDM.Core.Odoo.Models;

namespace HackPDM.Core.Pdm;

/// <summary>Chunked upload (hp.upload) and download (hp.version.read_contents) for large files.</summary>
public static class ContentTransfer
{
	private const string UploadModel = "hp.upload";

	public static long InlineLimitBytes { get; set; } = 4L * 1024 * 1024;
	public static int ChunkBytes { get; set; } = 4 * 1024 * 1024;
	public static int MaxAttempts { get; set; } = 3;

	public static bool IsLarge( long? size ) => size > InlineLimitBytes;
	public static int TimeoutMs( long bytes, int attempt = 1 ) => DownloadOperations.ContentsTimeoutMs( bytes, attempt );

	public static async Task<Result<int>> UploadFileAsync( string path, CancellationToken token = default, Action<long>? onBytes = null )
	{
		try
		{
			await using FileStream stream = new( path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, useAsync: true );
			return await UploadAsync( stream, token, onBytes );
		}
		catch( Exception e ) when( e is IOException or UnauthorizedAccessException )
		{
			return Result<int>.Fail( $"could not read {path}: {e.Message}" );
		}
	}

	public static async Task<Result<int>> UploadAsync( Stream stream, CancellationToken token = default, Action<long>? onBytes = null )
	{
		long size = stream.Length;
		stream.Position = 0;
		string sha1 = Convert.ToHexStringLower( await SHA1.HashDataAsync( stream, token ) );

		Result<OdooRow> begun = await OdooRpc.ExecuteAsync<OdooRow>( UploadModel, "upload_begin", [ size, sha1 ], OdooRpc.LongTimeout, token );
		if( !begun.Ok )
			return Result<int>.Fail( $"upload refused: {begun.Error}" );
		int id = begun.Value?.Int( "id" ) ?? 0;
		if( id == 0 )
			return Result<int>.Fail( "upload refused: the server returned no upload id" );
		int chunk = Math.Max( 1, Math.Min( ChunkBytes, begun.Value?.Int( "chunk_size" ) ?? ChunkBytes ) );

		for( int round = 1; round <= 2; round++ )
		{
			if( await SendChunksAsync( id, stream, size, chunk, token, onBytes ) is { Ok: false } sent )
				return Result<int>.Fail( sent.Error );
			Result<bool> finished = await OdooRpc.ExecuteAsync<bool>( UploadModel, "upload_finish", [ id ], TimeoutMs( size ), token );
			if( !finished.Ok )
				return Result<int>.Fail( $"upload not completed: {finished.Error}" );
			if( finished.Value )
				return Result<int>.Success( id );
		}
		return Result<int>.Fail( "the file arrived damaged twice (did it change while it was being sent?)" );
	}

	private static async Task<Result<bool>> SendChunksAsync(
		int id,
		Stream stream,
		long size,
		int chunk,
		CancellationToken token,
		Action<long>? onBytes )
	{
		byte[] buffer = new byte[ (int)Math.Min( chunk, Math.Max( size, 1 ) ) ];
		long offset = 0;
		int failures = 0;
		string? lastError = null;

		while( offset < size )
		{
			token.ThrowIfCancellationRequested();
			if( failures >= MaxAttempts )
				return Result<bool>.Fail( $"upload failed at byte {offset} of {size}: {lastError}" );

			stream.Position = offset;
			int wanted = (int)Math.Min( buffer.Length, size - offset );
			int read = await stream.ReadAtLeastAsync( buffer.AsMemory( 0, wanted ), wanted, throwOnEndOfStream: false, token );
			if( read < wanted )
				return Result<bool>.Fail( "the file got shorter while it was being sent" );

			Result<int> reply = await OdooRpc.ExecuteAsync<int>(
				UploadModel,
				"upload_chunk",
				[ id, offset, Convert.ToBase64String( buffer, 0, read ) ],
				TimeoutMs( read, failures + 1 ),
				token );
			if( !reply.Ok )
			{
				failures++;
				lastError = reply.Error;
				continue;
			}
			if( reply.Value > offset )
				failures = 0;
			else
			{
				failures++;
				lastError = $"the server is at byte {reply.Value}";
			}
			long next = Math.Clamp( reply.Value, 0, size );
			if( next > offset )
				onBytes?.Invoke( next - offset );
			offset = next;
		}
		return Result<bool>.Success( true );
	}

	public static async Task<Result<string>> DownloadAsync(
		int versionId,
		long size,
		Stream destination,
		CancellationToken token = default,
		Action<long>? onBytes = null )
	{
		using IncrementalHash sha1 = IncrementalHash.CreateHash( HashAlgorithmName.SHA1 );
		long offset = 0;
		int failures = 0;

		while( offset < size )
		{
			token.ThrowIfCancellationRequested();
			int wanted = (int)Math.Min( ChunkBytes, size - offset );
			Result<OdooRow> reply = await OdooRpc.ExecuteAsync<OdooRow>(
				HpVersion.ModelName,
				"read_contents",
				[ versionId, offset, wanted ],
				TimeoutMs( wanted, failures + 1 ),
				token );
			if( !reply.Ok )
			{
				if( ++failures >= MaxAttempts )
					return Result<string>.Fail( $"download failed at byte {offset} of {size}: {reply.Error}" );
				continue;
			}

			byte[] piece;
			try
			{
				piece = Convert.FromBase64String( reply.Value?.Text( "data" ) ?? "" );
			}
			catch( FormatException )
			{
				return Result<string>.Fail( "the server sent data that is not base64" );
			}
			if( piece.Length == 0 )
				return Result<string>.Fail( $"the server's file ended at byte {offset} of {size}" );

			await destination.WriteAsync( piece, token );
			sha1.AppendData( piece );
			offset += piece.Length;
			failures = 0;
			onBytes?.Invoke( piece.Length );
		}
		return Result<string>.Success( Convert.ToHexStringLower( sha1.GetHashAndReset() ) );
	}

	public static async Task<Result<byte[]>> DownloadBytesAsync( HpVersion version, CancellationToken token = default, Action<long>? onBytes = null )
	{
		using MemoryStream memory = new( (int)Math.Min( version.file_size ?? 0, int.MaxValue ) );
		Result<string> got = await DownloadAsync( version.id ?? 0, version.file_size ?? 0, memory, token, onBytes );
		if( !got.Ok )
			return Result<byte[]>.Fail( got.Error );
		return Mismatch( version, got.Value ) is { } error
			? Result<byte[]>.Fail( error )
			: Result<byte[]>.Success( memory.ToArray() );
	}

	public static async Task<Result<string>> DownloadToTempFileAsync( HpVersion version, CancellationToken token = default, Action<long>? onBytes = null )
	{
		string temp = Path.Combine( Path.GetTempPath(), $"hackpdm-{Guid.NewGuid():N}.part" );
		Result<string> got;
		try
		{
			await using FileStream file = new( temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1 << 16, useAsync: true );
			got = await DownloadAsync( version.id ?? 0, version.file_size ?? 0, file, token, onBytes );
		}
		catch( Exception e ) when( e is IOException or UnauthorizedAccessException )
		{
			got = Result<string>.Fail( $"could not write a temporary file: {e.Message}" );
		}
		catch
		{
			TryDelete( temp );
			throw;
		}

		string? error = got.Ok ? Mismatch( version, got.Value ) : got.Error;
		if( error is null )
			return Result<string>.Success( temp );
		TryDelete( temp );
		return Result<string>.Fail( error );
	}

	private static string? Mismatch( HpVersion version, string? actual )
		=> version.checksum is { Length: > 0 } expected && !string.Equals( expected, actual, StringComparison.OrdinalIgnoreCase )
			? "the downloaded file does not match the vault's checksum"
			: null;

	private static void TryDelete( string path )
	{
		try { File.Delete( path ); }
		catch( Exception e ) when( e is IOException or UnauthorizedAccessException ) { }
	}
}
