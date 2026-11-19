using HackPDM.Core.Configuration;

namespace HackPDM.Core.Pdm;

/// <summary>Safely writes downloaded file contents into the working folder.</summary>
public static class LocalFileWriter
{
	public static long? Write( HackFile file )
	{
		if( file.FileContents is null || file.DirectoryName is not { } dir || file.Name is not { } name )
		{
			HackLogger.Warn( "LocalFileWriter", $"{file.Name} missing contents, folder, or name" );
			return null;
		}
		try
		{
			Directory.CreateDirectory( dir );
			string target = Path.Combine( dir, name );
			string tempPath = $"{target}.{Guid.NewGuid():N}.tmp";
			try
			{
				File.WriteAllBytes( tempPath, file.FileContents );
				File.Move( tempPath, target, overwrite: true );
			}
			catch
			{
				TryDeleteTemp( tempPath );
				throw;
			}

			file.ApplyModifiedDateToLocal();
			return file.FileSize ?? 0;
		}
		catch( Exception e ) when( e is UnauthorizedAccessException or IOException )
		{
			HackLogger.Error( "LocalFileWriter", $"Failed writing '{file.Name}': {e.Message}", e );
			return null;
		}
	}

	public static Task<bool> WriteAsync( HackFile file )
	{
		if( file.FileContents is null )
		{
			HackLogger.Warn( "LocalFileWriter", $"{file.Name} file contents empty" );
			return Task.FromResult( false );
		}
		byte[] contents = file.FileContents;
		return PlaceAsync( file, target => WriteBytesAtomicAsync( target, contents ) );
	}

	public static Task<bool> WriteFromFileAsync( HackFile file, string sourcePath )
		=> PlaceAsync( file, target =>
		{
			File.Move( sourcePath, target, overwrite: true );
			return Task.FromResult( true );
		} );

	private static async Task<bool> PlaceAsync( HackFile file, Func<string, Task<bool>> write )
	{
		try
		{
			if( LocalTargetPath( file ) is not { } targetPath )
				return false;

			Directory.CreateDirectory( Path.GetDirectoryName( targetPath )! );
			if( File.Exists( targetPath ) && File.GetAttributes( targetPath ).HasFlag( FileAttributes.ReadOnly ) )
				File.SetAttributes( targetPath, File.GetAttributes( targetPath ) & ~FileAttributes.ReadOnly );

			if( !await write( targetPath ) )
				return false;

			file.FullPath = targetPath;
			file.Info = null;
			file.ApplyModifiedDateToLocal();
			file.ApplyNonOwnerReadOnly();
			return true;
		}
		catch( Exception e ) when( e is UnauthorizedAccessException or IOException )
		{
			HackLogger.Error( "LocalFileWriter", $"Failed placing '{file.Name}': {e.Message}", e );
			return false;
		}
	}

	internal static string? LocalTargetPath( HackFile file )
	{
		if( string.IsNullOrEmpty( file.DirectoryName ) )
			return null;

		string relative = file.DirectoryName.Replace( '\\', Path.DirectorySeparatorChar ).TrimStart( Path.DirectorySeparatorChar );
		if( file.Name is { Length: > 0 } name
			&& !string.Equals( Path.GetFileName( relative ), name, StringComparison.OrdinalIgnoreCase ) )
		{
			relative = Path.Combine( relative, name );
		}

		string root = Path.GetFullPath( HackDefaults.Instance?.PwaPathAbsolute ?? "" );
		string target = Path.GetFullPath( Path.Combine( root, relative ) );
		string rootWithSeparator = root.EndsWith( Path.DirectorySeparatorChar ) ? root : root + Path.DirectorySeparatorChar;
		StringComparison cmp = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
		return target.StartsWith( rootWithSeparator, cmp ) ? target : null;
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
				await stream.FlushAsync();
			}
			File.Move( tempPath, path, overwrite: true );
			return true;
		}
		catch( Exception e ) when( e is UnauthorizedAccessException or IOException )
		{
			TryDeleteTemp( tempPath );
			HackLogger.Error( "LocalFileWriter", $"Failed writing '{path}': {e.Message}", e );
			return false;
		}
	}

	private static void TryDeleteTemp( string tempPath )
	{
		try
		{
			if( File.Exists( tempPath ) )
				File.Delete( tempPath );
		}
		catch( Exception ex ) when( ex is IOException or UnauthorizedAccessException )
		{
		}
	}
}
