using System.Diagnostics.CodeAnalysis;

using HackPDM.Core.Configuration;

namespace HackPDM.Core.Pdm;

/// <summary>Shared safe file-system and stream helpers.</summary>
public static class FileOps
{
	public static void TryDelete( string? path )
	{
		if( string.IsNullOrEmpty( path ) )
			return;
		try
		{
			ClearReadOnlyIfPresent( path );
			File.Delete( path );
		}
		catch( Exception e ) when( e is IOException or UnauthorizedAccessException )
		{
		}
	}

	public static void ClearReadOnlyIfPresent( string path )
	{
		if( string.IsNullOrEmpty( path ) || !Path.Exists( path ) )
			return;
		try
		{
			FileAttributes attributes = File.GetAttributes( path );
			if( ( attributes & FileAttributes.ReadOnly ) != 0 )
				File.SetAttributes( path, attributes & ~FileAttributes.ReadOnly );
		}
		catch( Exception e ) when( e is FileNotFoundException or DirectoryNotFoundException or IOException or UnauthorizedAccessException )
		{
		}
	}

	public static void ClearReadOnlyRecursive( string directory )
	{
		try
		{
			if( !Directory.Exists( directory ) )
				return;
			ClearReadOnlyIfPresent( directory );
			EnumerationOptions opts = new() { RecurseSubdirectories = true, IgnoreInaccessible = true };
			foreach( string entry in Directory.EnumerateFileSystemEntries( directory, "*", opts ) )
				ClearReadOnlyIfPresent( entry );
		}
		catch( Exception e ) when( e is IOException or UnauthorizedAccessException )
		{
		}
	}

	public static byte[] ToByteArrayZeroCopy( this MemoryStream stream )
		=> stream.TryGetBuffer( out ArraySegment<byte> seg ) && seg.Offset == 0 && seg.Array is { } arr && seg.Count == arr.Length
			? arr
			: stream.ToArray();
}

/// <summary>Safely writes downloaded file contents into the working folder.</summary>
public static class LocalFileWriter
{
	internal static bool IsSingleSafeFileName( [NotNullWhen( true )] string? name )
		=> !string.IsNullOrWhiteSpace( name )
			&& name is not ( "." or ".." )
			&& name.IndexOfAny( [ '\\', '/' ] ) < 0
			&& !Path.IsPathRooted( name );

	public static long? Write( HackFile file )
		=> WriteInRoot( file, PwaPaths.Vault );

	internal static long? WriteInRoot( HackFile file, string? allowedRoot )
	{
		if( file.FileContents is null || file.DirectoryName is not { Length: > 0 } dir || !IsSingleSafeFileName( file.Name ) )
		{
			HackLogger.Warn( nameof( LocalFileWriter ), $"{file.Name} missing contents, folder, or valid file name" );
			return null;
		}
		try
		{
			string target = Path.GetFullPath( Path.Combine( dir, file.Name ) );
			if( !PwaPaths.IsInsideDirectory( target, allowedRoot ) )
			{
				HackLogger.Warn( nameof( LocalFileWriter ), $"Refused writing '{file.Name}' outside allowed root '{allowedRoot}'" );
				return null;
			}
			Directory.CreateDirectory( Path.GetDirectoryName( target )! );
			FileOps.ClearReadOnlyIfPresent( target );
			string tempPath = $"{target}.{Guid.NewGuid():N}.tmp";
			try
			{
				File.WriteAllBytes( tempPath, file.FileContents );
				File.Move( tempPath, target, overwrite: true );
			}
			catch
			{
				FileOps.TryDelete( tempPath );
				throw;
			}

			file.FullPath = target;
			file.Info = null;
			file.ApplyModifiedDateToLocal();
			file.ApplyNonOwnerReadOnly();
			return file.FileSize ?? file.FileContents.Length;
		}
		catch( Exception e ) when( e is UnauthorizedAccessException or IOException or ArgumentException or NotSupportedException )
		{
			HackLogger.Error( nameof( LocalFileWriter ), $"Failed writing '{file.Name}': {e.Message}", e );
			return null;
		}
	}

	public static Task<bool> WriteAsync( HackFile file )
	{
		if( file.FileContents is null )
		{
			HackLogger.Warn( nameof( LocalFileWriter ), $"{file.Name} file contents empty" );
			return Task.FromResult( false );
		}
		byte[] contents = file.FileContents;
		return PlaceAsync( file, LocalTargetPath( file ), target => WriteBytesAtomicAsync( target, contents ) );
	}

	public static Task<bool> WriteFromFileAsync( HackFile file, string sourcePath )
		=> PlaceAsync( file, LocalTargetPath( file ), target => Task.Run( () =>
		{
			File.Move( sourcePath, target, overwrite: true );
			return true;
		} ) );

	internal static Task<bool> WriteFromFileInRootAsync( HackFile file, string sourcePath, string allowedRoot )
	{
		string? targetPath = null;
		if( file.DirectoryName is { Length: > 0 } dir && IsSingleSafeFileName( file.Name ) )
		{
			try
			{
				string candidate = Path.GetFullPath( Path.Combine( dir, file.Name ) );
				if( PwaPaths.IsInsideDirectory( candidate, allowedRoot ) )
					targetPath = candidate;
			}
			catch
			{
			}
		}
		return PlaceAsync( file, targetPath, target => Task.Run( () =>
		{
			File.Move( sourcePath, target, overwrite: true );
			return true;
		} ) );
	}

	private static async Task<bool> PlaceAsync( HackFile file, string? targetPath, Func<string, Task<bool>> write )
	{
		try
		{
			if( targetPath is null )
				return false;

			Directory.CreateDirectory( Path.GetDirectoryName( targetPath )! );
			FileOps.ClearReadOnlyIfPresent( targetPath );

			if( !await write( targetPath ) )
				return false;

			file.FullPath = targetPath;
			file.Info = null;
			file.ApplyModifiedDateToLocal();
			file.ApplyNonOwnerReadOnly();
			return true;
		}
		catch( Exception e ) when( e is UnauthorizedAccessException or IOException or ArgumentException )
		{
			HackLogger.Error( nameof( LocalFileWriter ), $"Failed placing '{file.Name}': {e.Message}", e );
			return false;
		}
	}

	internal static string? LocalTargetPath( HackFile file )
	{
		if( file.DirectoryName is null || !IsSingleSafeFileName( file.Name ) )
			return null;
		string rawRoot = HackDefaults.Instance?.PwaPathAbsolute ?? "";
		if( string.IsNullOrWhiteSpace( rawRoot ) )
			return null;

		string relative = file.DirectoryName
			.Replace( '\\', Path.DirectorySeparatorChar )
			.Replace( '/', Path.DirectorySeparatorChar )
			.TrimStart( Path.DirectorySeparatorChar );
		if( Path.IsPathRooted( relative ) )
			return null;
		string combined = relative.Length == 0 ? file.Name : Path.Combine( relative, file.Name );
		return PwaPaths.ResolveInsideRoot( rawRoot, combined );
	}

	private static async Task<bool> WriteBytesAtomicAsync( string path, byte[] bytes )
	{
		if( string.IsNullOrEmpty( path ) )
			throw new ArgumentException( "Empty path", nameof( path ) );
		ArgumentNullException.ThrowIfNull( bytes );

		string tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
		try
		{
			await using( FileStream stream = new( tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, useAsync: true ) )
			{
				await stream.WriteAsync( bytes );
			}
			File.Move( tempPath, path, overwrite: true );
			return true;
		}
		catch( Exception e ) when( e is UnauthorizedAccessException or IOException )
		{
			FileOps.TryDelete( tempPath );
			HackLogger.Error( nameof( LocalFileWriter ), $"Failed writing '{path}': {e.Message}", e );
			return false;
		}
	}
}
