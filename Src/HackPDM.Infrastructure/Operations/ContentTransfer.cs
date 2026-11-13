using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

using HackPDM.Infrastructure.Odoo;
using HackPDM.Infrastructure.Odoo.Models;

namespace HackPDM.Infrastructure.Operations;

/// <summary>
/// Moves large file contents in pieces, so no single JSON-RPC request carries a whole file: uploads
/// through the server's <c>hp.upload</c> (begin, chunks, finish with a SHA-1 check) and downloads
/// through <c>hp.version.read_contents</c>. Every piece has a timeout scaled to its size and is retried;
/// an upload resumes from the bytes the server reports as received.
/// </summary>
/// <remarks>
/// Files up to <see cref="InlineLimitBytes"/> still travel inline (base64 in the staged record or the
/// <c>read</c> of <c>file_contents</c>): one call instead of three is faster for the many small files of
/// a typical commit.
/// </remarks>
public static class ContentTransfer
{
	private const string UploadModel = "hp.upload";

	/// <summary>Largest file sent or read inline, in bytes; larger files go in pieces.</summary>
	public static long InlineLimitBytes { get; set; } = 4L * 1024 * 1024;

	/// <summary>Bytes per piece; the server's own limit (from <c>upload_begin</c>) wins when smaller.</summary>
	public static int ChunkBytes { get; set; } = 4 * 1024 * 1024;

	/// <summary>Failed calls in a row, for one piece, before a transfer gives up.</summary>
	public static int MaxAttempts { get; set; } = 3;

	/// <summary>Whether a file of <paramref name="size"/> bytes goes in pieces.</summary>
	public static bool IsLarge( long? size ) => size > InlineLimitBytes;

	/// <summary>The timeout for moving <paramref name="bytes"/> in one call (see <see cref="PdmDownloadOperations.ContentsTimeoutMs"/>).</summary>
	public static int TimeoutMs( long bytes, int attempt = 1 ) => PdmDownloadOperations.ContentsTimeoutMs( bytes, attempt );

	// ------------------------------------------------------------------ upload

	/// <summary>Uploads the file at <paramref name="path"/> in pieces.</summary>
	/// <param name="path">The local file.</param>
	/// <param name="token">Cancels the upload.</param>
	/// <returns>The verified <c>hp.upload</c> id, to stage as a version's <c>upload_id</c>; or why it failed.</returns>
	/// <exception cref="OperationCanceledException"><paramref name="token"/> was cancelled.</exception>
	public static async Task<Result<int>> UploadFileAsync( string path, CancellationToken token = default )
	{
		try
		{
			await using FileStream stream = new( path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, useAsync: true );
			return await UploadAsync( stream, token );
		}
		catch( Exception e ) when( e is IOException or UnauthorizedAccessException )
		{
			return Result<int>.Fail( $"could not read {path}: {e.Message}" );
		}
	}

	/// <summary>Uploads the contents of <paramref name="stream"/> in pieces.</summary>
	/// <param name="stream">A readable, seekable stream; read from its start.</param>
	/// <param name="token">Cancels the upload.</param>
	/// <returns>The verified <c>hp.upload</c> id; or why it failed.</returns>
	/// <remarks>When the server finds the file damaged (its SHA-1 does not match), it is sent once more.</remarks>
	/// <exception cref="OperationCanceledException"><paramref name="token"/> was cancelled.</exception>
	public static async Task<Result<int>> UploadAsync( Stream stream, CancellationToken token = default )
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
			if( await SendChunksAsync( id, stream, size, chunk, token ) is { Ok: false } sent )
				return Result<int>.Fail( sent.Error );
			// the server reads the whole file back to check it
			Result<bool> finished = await OdooRpc.ExecuteAsync<bool>( UploadModel, "upload_finish", [ id ], TimeoutMs( size ), token );
			if( !finished.Ok )
				return Result<int>.Fail( $"upload not completed: {finished.Error}" );
			if( finished.Value )
				return Result<int>.Success( id );
		}
		return Result<int>.Fail( "the file arrived damaged twice (did it change while it was being sent?)" );
	}

	private static async Task<Result<bool>> SendChunksAsync( int id, Stream stream, long size, int chunk, CancellationToken token )
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

			Result<int> reply = await OdooRpc.ExecuteAsync<int>( UploadModel, "upload_chunk",
				[ id, offset, Convert.ToBase64String( buffer, 0, read ) ], TimeoutMs( read, failures + 1 ), token );
			if( !reply.Ok )
			{
				// the piece may still have arrived: the next call at the same offset finds out
				failures++;
				lastError = reply.Error;
				continue;
			}
			// the server says where to continue: past this piece, or back where a piece went missing
			if( reply.Value > offset )
				failures = 0;
			else
			{
				failures++;
				lastError = $"the server is at byte {reply.Value}";
			}
			offset = Math.Clamp( reply.Value, 0, size );
		}
		return Result<bool>.Success( true );
	}

	// ------------------------------------------------------------------ download

	/// <summary>Downloads a version's file in pieces into <paramref name="destination"/>.</summary>
	/// <param name="versionId">The hp.version id.</param>
	/// <param name="size">The file's size in bytes (its <c>file_size</c>).</param>
	/// <param name="destination">Where the bytes are written, from its current position.</param>
	/// <param name="token">Cancels the download.</param>
	/// <returns>The SHA-1 (lower-case hex) of the bytes written; or why the download failed.</returns>
	/// <exception cref="OperationCanceledException"><paramref name="token"/> was cancelled.</exception>
	public static async Task<Result<string>> DownloadAsync( int versionId, long size, Stream destination, CancellationToken token = default )
	{
		using IncrementalHash sha1 = IncrementalHash.CreateHash( HashAlgorithmName.SHA1 );
		long offset = 0;
		int failures = 0;
		while( offset < size )
		{
			token.ThrowIfCancellationRequested();
			int wanted = (int)Math.Min( ChunkBytes, size - offset );
			Result<OdooRow> reply = await OdooRpc.ExecuteAsync<OdooRow>( HpVersion.GetHpModel()!, "read_contents",
				[ versionId, offset, wanted ], TimeoutMs( wanted, failures + 1 ), token );
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
		}
		return Result<string>.Success( Convert.ToHexStringLower( sha1.GetHashAndReset() ) );
	}

	/// <summary>Downloads a version's file in pieces into memory, checking it against the version's <c>checksum</c> when known.</summary>
	/// <param name="version">The version; needs <c>id</c> and <c>file_size</c>.</param>
	/// <param name="token">Cancels the download.</param>
	/// <returns>The file's bytes; or why the download failed.</returns>
	public static async Task<Result<byte[]>> DownloadBytesAsync( HpVersion version, CancellationToken token = default )
	{
		using MemoryStream memory = new( (int)Math.Min( version.file_size ?? 0, int.MaxValue ) );
		Result<string> got = await DownloadAsync( version.id ?? 0, version.file_size ?? 0, memory, token );
		if( !got.Ok )
			return Result<byte[]>.Fail( got.Error );
		return Mismatch( version, got.Value ) is { } error ? Result<byte[]>.Fail( error ) : Result<byte[]>.Success( memory.ToArray() );
	}

	/// <summary>Downloads a version's file in pieces into a new temporary file, checking it against the version's <c>checksum</c> when known.</summary>
	/// <param name="version">The version; needs <c>id</c> and <c>file_size</c>.</param>
	/// <param name="token">Cancels the download.</param>
	/// <returns>The temporary file's path (the caller moves or deletes it); or why the download failed, with nothing left behind.</returns>
	public static async Task<Result<string>> DownloadToTempFileAsync( HpVersion version, CancellationToken token = default )
	{
		string temp = Path.Combine( Path.GetTempPath(), $"hackpdm-{Guid.NewGuid():N}.part" );
		Result<string> got;
		try
		{
			await using( FileStream file = new( temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1 << 16, useAsync: true ) )
				got = await DownloadAsync( version.id ?? 0, version.file_size ?? 0, file, token );
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
		try
		{
			File.Delete( path );
		}
		catch( Exception e ) when( e is IOException or UnauthorizedAccessException )
		{
		}
	}
}
