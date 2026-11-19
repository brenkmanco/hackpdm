using HackPDM.Core.Configuration;
using HackPDM.Core.Odoo;
using HackPDM.Core.Odoo.Models;
using HackPDM.Core.SolidWorks;

namespace HackPDM.Core.Pdm;

/// <summary>Builds and stages entry, version, dependency relationship, and SolidWorks property records.</summary>
public static class CommitStaging
{
	internal const string UploadSource = "__upload_source";

	public static HpEntry? NewEntry( HackFile hack, int dirId, HpPDMCommit? commit = null )
	{
		HpType? type = TypeOf( hack, out bool restricted );
		if( restricted )
			return null;

		HpEntry entry = new()
		{
			commit_id = new Many2One { id = commit?.id },
			name = hack.Name,
			deleted = false,
			dir_id = dirId,
			LocalFile = hack,
			returnType = EntryReturnType.Staged,
		};
		if( type is not null )
		{
			entry.cat_id = type.cat_id?.id ?? 0;
			entry.type_id = type.id;
		}
		return entry;
	}

	public static async Task<HpEntry?> FindRemoteEntryAsync( HpEntry entry )
	{
		HpEntry? found = ( await HpEntry.GetRecordsBySearchAsync(
			OdooDomain.Where( "name", entry.name ).And( "dir_id", entry.dir_id?.id ).And( "deleted", false ) ) )?.FirstOrDefault();
		if( found is { id: not 0 } )
			found.returnType = EntryReturnType.GotExisting;
		return found;
	}

	public static async Task<HpRecordStaged?> StageEntryAsync( HpEntry entry )
	{
		HpRecordStaged? staged = await entry.StageRecAsync( false );
		entry.returnType = staged?.id is null or 0 ? EntryReturnType.Failed : EntryReturnType.Staged;
		return staged;
	}

	internal static HpVersion? PrepareVersion( HackFile hack, HpEntry entry, int commitId )
		=> NewVersion( hack, entry.id, entry.dir_id, entry.windows_complete_name, (Many2One?)commitId );

	internal static HpVersion? PrepareVersion( HackFile hack, HpRecordStaged stagedEntry )
		=> NewVersion(
			hack,
			stagedEntry.id,
			stagedEntry.payload?.GetValueOrDefault( "dir_id" ) as int?,
			stagedEntry.payload?.GetValueOrDefault( "windows_complete_name" ) as string,
			stagedEntry.commit_id );

	public static Task<HpRecordStaged?> StageVersionAsync(
		HackFile hack,
		HpEntry entry,
		int commitId,
		Action<string>? reportError = null,
		Action<long>? onBytesUploaded = null )
		=> StageAsync( PrepareVersion( hack, entry, commitId ), existingEntry: true, reportError, onBytesUploaded );

	public static Task<HpRecordStaged?> StageVersionAsync(
		HackFile hack,
		HpRecordStaged stagedEntry,
		Action<string>? reportError = null,
		Action<long>? onBytesUploaded = null )
		=> StageAsync( PrepareVersion( hack, stagedEntry ), existingEntry: false, reportError, onBytesUploaded );

	private static async Task<HpRecordStaged?> StageAsync(
		HpVersion? version,
		bool existingEntry,
		Action<string>? reportError,
		Action<long>? onBytesUploaded )
	{
		if( version is null )
			return null;
		if( existingEntry )
			version.HashedValues[ "__internal__existing" ] = true;

		int? uploadId = null;
		if( version.HashedValues.GetValueOrDefault( UploadSource ) is string path )
		{
			Result<int> uploaded = await ContentTransfer.UploadFileAsync( path, onBytes: onBytesUploaded );
			if( !uploaded.Ok )
			{
				version.LastError = uploaded.Error;
				reportError?.Invoke( $"upload of {Path.GetFileName( path )} failed: {uploaded.Error}" );
				return null;
			}
			uploadId = uploaded.Value;
		}

		HpRecordStaged? staged = version.PreStageRec();
		if( staged is null )
			return null;
		if( uploadId is { } id )
			staged.payload![ "upload_id" ] = id;

		int inlineBytes = version.file_contents?.Length ?? 0;
		staged.CreateTimeoutMs = ContentTransfer.TimeoutMs( inlineBytes );
		await staged.CreateAsync();
		version.LastError = staged.LastError;
		if( staged.id is not null and not 0 )
		{
			if( uploadId is null && inlineBytes > 0 )
				onBytesUploaded?.Invoke( inlineBytes );
			return staged;
		}
		reportError?.Invoke( $"the server refused the version: {staged.LastError}" );
		return null;
	}

	private static HpVersion? NewVersion( HackFile hack, Many2One? entryId, Many2One? dirId, string? serverName, Many2One? commit )
	{
		TypeOf( hack, out bool restricted );
		if( restricted )
			return null;

		string? uploadFrom = hack.FileContents is null && LargeFileOnDisk( hack ) ? hack.FullPath : null;
		byte[]? contents = uploadFrom is null ? hack.FileContents ?? ReadContents( hack ) : null;
		if( contents is null && uploadFrom is null )
			return null;

		HpVersion version = new()
		{
			name = hack.Name,
			dir_id = dirId,
			entry_id = entryId,
			file_ext = hack.TypeExt?[ 1.. ].ToLower(),
			WinPathway = serverName,
			node_id = OdooDefaults.Instance.MyNode?.id,
			file_contents = contents,
			preview_image = PreviewOf( hack ),
			commit_id = commit,
			HashedValues = { [ "windows_complete_name" ] = serverName },
		};
		if( uploadFrom is not null )
			version.HashedValues[ UploadSource ] = uploadFrom;
		return version;
	}

	private static bool LargeFileOnDisk( HackFile hack )
	{
		try
		{
			return hack.FullPath is { Length: > 0 } path
				&& File.Exists( path )
				&& ContentTransfer.IsLarge( new FileInfo( path ).Length );
		}
		catch( Exception ex ) when( ex is IOException or UnauthorizedAccessException )
		{
			return false;
		}
	}

	private static byte[]? ReadContents( HackFile hack )
	{
		try
		{
			return File.ReadAllBytes( hack.FullPath ?? "" );
		}
		catch( Exception ex )
		{
			HackLogger.Error( "CommitStaging", $"Failed reading '{hack.FullPath}': {ex.Message}", ex );
			return null;
		}
	}

	private static byte[]? PreviewOf( HackFile hack )
	{
		if( hack.FullPath is not { } path || !SwDocumentTypes.IsSolidWorks( path ) )
			return null;
		try
		{
			return SolidWorksUtil.DocMgr?.GetPreviewPng( path );
		}
		catch( Exception ex )
		{
			HackLogger.Debug( "CommitStaging", $"No preview extracted for '{path}': {ex.Message}", ex );
			return null;
		}
	}

	private static HpType? TypeOf( HackFile hack, out bool restricted )
	{
		bool known = OdooDefaults.Instance.ExtToType.TryGetValue( hack.TypeExt?.ToLower() ?? "", out HpType? type );
		restricted = OdooDefaults.Instance.RestrictTypes is true && !known;
		return type;
	}

	private static string? SolidWorksFileOf( HpRecordStaged? version )
	{
		if( version is null || !SwDocumentTypes.IsSolidWorksExtension( version.payload?.GetValueOrDefault( "file_ext" )?.ToString() ) )
			return null;
		string serverName = version.payload?.GetValueOrDefault( "WinPathway" )?.ToString()
			?? version.HashedValues.GetValueOrDefault( "windows_complete_name" ) as string
			?? throw new ArgumentException( $"staged version {version.id} has no path" );
		return PwaPaths.ToAbsolute( serverName );
	}

	public static async Task<bool> StageRelationshipsAsync( params HpRecordStaged[] versions )
	{
		try
		{
			SwReferenceResolver resolver = new( HackDefaults.Instance?.PwaPathAbsolute );
			List<HpRecordStaged> staged = [];
			foreach( HpRecordStaged version in versions )
			{
				if( SolidWorksFileOf( version ) is not { } file )
					continue;
				foreach( string path in SwDependencies.Of( file, resolver ).Select( r => r.Local ?? r.Stored ) )
				{
					staged.Add( new HpRecordStaged
					{
						commit_id = version.commit_id,
						committing_id = version.commit_id,
						target_model = HpVersionRelationship.ModelName,
						payload = new Dictionary<string, object?>
						{
							[ "path" ] = path,
							[ "windows_complete_name" ] = PwaPaths.ToServerName( path ),
							[ "version" ] = new Dictionary<string, object?>
							{
								[ "name" ] = version.payload?.GetValueOrDefault( "name" ),
								[ "entry_id" ] = version.payload?.GetValueOrDefault( "entry_id" ),
								[ "version_staged_id" ] = version.id,
							},
						},
					} );
				}
			}
			if( staged.Count > 0 && await HpRecordStaged.MultiCreateAsync( staged ) is { Ok: false } failed )
			{
				HackLogger.Error( "CommitStaging", $"Unable to stage relationships: {failed.Error}" );
				return false;
			}
			return true;
		}
		catch( Exception e )
		{
			HackLogger.Error( "CommitStaging", $"Unable to create relationships: {e.Message}", e );
			return false;
		}
	}

	public static async Task<bool> StagePropertiesAsync( params HpRecordStaged[] versions )
	{
		List<HpVersionProperty> staged = [];
		foreach( HpRecordStaged version in versions )
		{
			try
			{
				if( SolidWorksFileOf( version ) is not { } file )
					continue;
				int versionId = version.id is int id and not 0 ? id : throw new ArgumentException( "version id not defined" );
				foreach( var (config, name, type, value) in SolidWorksUtil.DocMgr.GetProperties( file ) ?? [] )
				{
					if( HpVersionProperty.FromSwProperty( config, name, type, value, versionId, version.commit_id ) is { } property )
						staged.Add( property );
				}
			}
			catch( Exception e )
			{
				HackLogger.Error( "CommitStaging", $"Unable to create properties for staged version {version.id}: {e.Message}", e );
				return false;
			}
		}
		if( staged.Count > 0 && await HpVersionProperty.MultiStageAsync( staged ) is { Ok: false } failed )
		{
			HackLogger.Error( "CommitStaging", $"Unable to stage properties: {failed.Error}" );
			return false;
		}
		return true;
	}
}
