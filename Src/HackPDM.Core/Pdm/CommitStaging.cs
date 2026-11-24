using HackPDM.Core.Configuration;
using HackPDM.Core.Odoo;
using HackPDM.Core.Odoo.Models;
using HackPDM.Core.Services;
using HackPDM.Core.SolidWorks;
using HackPDM.Core.SolidWorks.Format;

namespace HackPDM.Core.Pdm;

/// <summary>Builds and stages entry, version, dependency relationship, and SolidWorks property records.</summary>
public static class CommitStaging
{
	internal const string UploadSource = "__upload_source";
	internal const string UploadChecksum = "__upload_sha1";

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
			entry.cat_id = type.cat_id?.id;
			entry.type_id = type.id;
		}
		return entry;
	}

	public static async Task<HpEntry?> FindRemoteEntryAsync( HpEntry entry, CancellationToken token = default )
	{
		string escapedName = CommitOperations.EscapeLikeLiteral( entry.name ?? "" );
		OdooDomain domain = OdooDomain.Where( nameof( HpEntry.name ), Operators.ILikeEqual, escapedName )
			.And( nameof( HpEntry.dir_id ), entry.dir_id?.id );
		Result<HpEntry[]> res = await Repository<HpEntry>.SearchReadAsync( domain, token: token ).ConfigureAwait( false );
		if( !res.Ok )
			throw new OdooException( $"Could not look up remote entry '{entry.name}': {res.Error}" );

		HpEntry? found = res.Value?.FirstOrDefault( static e => e.deleted is not true )
			?? res.Value?.FirstOrDefault();
		if( found?.deleted is true )
			throw new InvalidOperationException( $"Entry '{entry.name}' already exists on the server as deleted (Dt)." );
		if( found is { id: not 0 } )
			found.returnType = EntryReturnType.GotExisting;
		return found;
	}

	public static async Task<HpRecordStaged?> StageEntryAsync( HpEntry entry, CancellationToken token = default )
	{
		HpRecordStaged? staged = await entry.StageRecAsync( false, token: token ).ConfigureAwait( false );
		entry.returnType = staged?.id is null or 0 ? EntryReturnType.Failed : EntryReturnType.Staged;
		return staged;
	}

	internal static HpVersion? PrepareVersion( HackFile hack, HpEntry entry, int commitId, byte[]? preReadPreview = null, bool hasPreReadPreview = false )
		=> NewVersion( hack, entry.id, entry.dir_id, entry.windows_complete_name, (Many2One?)commitId, preReadPreview, hasPreReadPreview );

	internal static HpVersion? PrepareVersion( HackFile hack, HpRecordStaged stagedEntry, byte[]? preReadPreview = null, bool hasPreReadPreview = false )
		=> NewVersion(
			hack,
			stagedEntry.id,
			stagedEntry.payload?.GetValueOrDefault( nameof( HpEntry.dir_id ) ) as int?,
			stagedEntry.payload?.GetValueOrDefault( nameof( HpEntry.windows_complete_name ) ) as string,
			stagedEntry.commit_id,
			preReadPreview,
			hasPreReadPreview );

	public static Task<HpRecordStaged?> StageVersionAsync(
		HackFile hack,
		HpEntry entry,
		int commitId,
		Action<string>? reportError = null,
		Action<long>? onBytesUploaded = null,
		CancellationToken token = default )
		=> StageAsync( PrepareVersion( hack, entry, commitId ), existingEntry: true, reportError, onBytesUploaded, token );

	internal static Task<HpRecordStaged?> StageVersionAsync(
		HackFile hack,
		HpEntry entry,
		int commitId,
		Action<string>? reportError,
		Action<long>? onBytesUploaded,
		byte[]? preReadPreview,
		bool hasPreReadPreview,
		CancellationToken token = default,
		OperationPauseController? pause = null )
		=> StageAsync( PrepareVersion( hack, entry, commitId, preReadPreview, hasPreReadPreview ), existingEntry: true, reportError, onBytesUploaded, token, pause );

	public static Task<HpRecordStaged?> StageVersionAsync(
		HackFile hack,
		HpRecordStaged stagedEntry,
		Action<string>? reportError = null,
		Action<long>? onBytesUploaded = null,
		CancellationToken token = default )
		=> StageAsync( PrepareVersion( hack, stagedEntry ), existingEntry: false, reportError, onBytesUploaded, token );

	internal static Task<HpRecordStaged?> StageVersionAsync(
		HackFile hack,
		HpRecordStaged stagedEntry,
		Action<string>? reportError,
		Action<long>? onBytesUploaded,
		byte[]? preReadPreview,
		bool hasPreReadPreview,
		CancellationToken token = default,
		OperationPauseController? pause = null )
		=> StageAsync( PrepareVersion( hack, stagedEntry, preReadPreview, hasPreReadPreview ), existingEntry: false, reportError, onBytesUploaded, token, pause );

	private static async Task<HpRecordStaged?> StageAsync(
		HpVersion? version,
		bool existingEntry,
		Action<string>? reportError,
		Action<long>? onBytesUploaded,
		CancellationToken token = default,
		OperationPauseController? pause = null )
	{
		if( version is null )
			return null;
		if( pause is not null )
			await pause.WaitIfPausedAsync( token ).ConfigureAwait( false );
		token.ThrowIfCancellationRequested();
		if( existingEntry )
			version.HashedValues[ "__internal__existing" ] = true;

		int? uploadId = null;
		if( version.HashedValues.GetValueOrDefault( UploadSource ) is string path )
		{
			string? precomputedSha1 = version.HashedValues.GetValueOrDefault( UploadChecksum ) as string;
			Result<int> uploaded = await ContentTransfer.UploadFileAsync( path, token, onBytesUploaded, precomputedSha1, pause ).ConfigureAwait( false );
			if( !uploaded.Ok )
			{
				version.LastError = uploaded.Error;
				reportError?.Invoke( $"upload of {Path.GetFileName( path )} failed: {uploaded.Error}" );
				return null;
			}
			uploadId = uploaded.Value;
		}

		if( pause is not null )
			await pause.WaitIfPausedAsync( token ).ConfigureAwait( false );

		HpRecordStaged? staged = version.PreStageRec();
		int inlineBytes = version.file_contents?.Length ?? 0;
		version.file_contents = null;
		if( staged is null )
			return null;
		if( uploadId is { } id )
			staged.payload![ "upload_id" ] = id;

		staged.CreateTimeoutMs = ContentTransfer.TimeoutMs( inlineBytes );
		using( OperationPauseController.EnterScope( pause ) )
			await staged.CreateAsync( token: token ).ConfigureAwait( false );
		staged.payload?.Remove( nameof( HpVersion.file_contents ) );
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

	private static HpVersion? NewVersion(
		HackFile hack,
		Many2One? entryId,
		Many2One? dirId,
		string? serverName,
		Many2One? commit,
		byte[]? preReadPreview = null,
		bool hasPreReadPreview = false )
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
			file_ext = hack.TypeExt is { Length: > 1 } ext ? ext[ 1.. ].ToLowerInvariant() : "",
			WinPathway = serverName,
			node_id = OdooDefaults.Instance.MyNode?.id,
			file_contents = contents,
			preview_image = hasPreReadPreview ? preReadPreview : PreviewOf( hack ),
			commit_id = commit,
			HashedValues = { [ nameof( HpVersion.windows_complete_name ) ] = serverName },
		};
		if( uploadFrom is not null )
		{
			version.HashedValues[ UploadSource ] = uploadFrom;
			if( hack.Checksum is { Length: 40 } sha1 )
				version.HashedValues[ UploadChecksum ] = sha1;
		}
		return version;
	}

	private static bool LargeFileOnDisk( HackFile hack )
	{
		try
		{
			if( hack.FullPath is not { Length: > 0 } path )
				return false;
			FileInfo info = new( path );
			return info.Exists && ContentTransfer.IsLarge( info.Length );
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
			HackLogger.Error( nameof( CommitStaging ), $"Failed reading '{hack.FullPath}': {ex.Message}", ex );
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
			HackLogger.Debug( nameof( CommitStaging ), $"No preview extracted for '{path}': {ex.Message}", ex );
			return null;
		}
	}

	private static HpType? TypeOf( HackFile hack, out bool restricted )
	{
		HpType? type = null;
		bool known = hack.TypeExt is { Length: > 0 } ext && OdooDefaults.Instance.ExtToType.TryGetValue( ext, out type );
		restricted = OdooDefaults.Instance.RestrictTypes is true && !known;
		return type;
	}

	private static string? SolidWorksFileOf( HpRecordStaged? version )
	{
		if( version is null || !SwDocumentTypes.IsSolidWorksExtension( version.payload?.GetValueOrDefault( nameof( HpVersion.file_ext ) )?.ToString() ) )
			return null;
		string serverName = version.HashedValues.GetValueOrDefault( nameof( HpVersion.windows_complete_name ) ) as string
			?? throw new ArgumentException( $"staged version {version.id} has no path" );
		return PwaPaths.ToAbsolute( serverName );
	}

	public static Task<bool> StageRelationshipsAsync( params HpRecordStaged[] versions )
		=> StageRelationshipsInternalAsync( versions, preReadDependencies: null, CancellationToken.None );

	internal static Task<bool> StageRelationshipsAsync( HpRecordStaged version, List<string[]>? preReadDependencies, CancellationToken token = default )
		=> StageRelationshipsInternalAsync( [ version ], preReadDependencies, token );

	private static async Task<bool> StageRelationshipsInternalAsync( HpRecordStaged[] versions, List<string[]>? preReadDependencies, CancellationToken token )
	{
		try
		{
			token.ThrowIfCancellationRequested();
			SwReferenceResolver resolver = new( HackDefaults.Instance?.PwaPathAbsolute );
			List<HpRecordStaged> staged = [];
			for( int i = 0; i < versions.Length; i++ )
			{
				HpRecordStaged version = versions[ i ];
				if( SolidWorksFileOf( version ) is not { } file )
					continue;
				List<SwDependencies.Reference> references;
				if( preReadDependencies is not null && versions.Length == 1 )
				{
					references = new List<SwDependencies.Reference>( preReadDependencies.Count );
					for( int r = 0; r < preReadDependencies.Count; r++ )
					{
						string stored = preReadDependencies[ r ][ 1 ];
						references.Add( new SwDependencies.Reference( stored, resolver.Resolve( stored, file ) ) );
					}
				}
				else
				{
					references = SwDependencies.Of( file, resolver, token );
				}

				for( int r = 0; r < references.Count; r++ )
				{
					string path = references[ r ].Local ?? references[ r ].Stored;
					staged.Add( new HpRecordStaged
					{
						commit_id = version.commit_id,
						committing_id = version.commit_id,
						target_model = HpVersionRelationship.ModelName,
						payload = new Dictionary<string, object?>
						{
							[ "path" ] = path,
							[ nameof( HpVersion.windows_complete_name ) ] = PwaPaths.ToServerName( path ),
							[ "version" ] = new Dictionary<string, object?>
							{
								[ nameof( HpVersion.name ) ] = version.payload?.GetValueOrDefault( nameof( HpVersion.name ) ),
								[ nameof( HpVersion.entry_id ) ] = version.payload?.GetValueOrDefault( nameof( HpVersion.entry_id ) ),
								[ "version_staged_id" ] = version.id,
							},
						},
					} );
				}
			}
			if( staged.Count > 0 && await HpRecordStaged.MultiCreateAsync( staged, token: token ).ConfigureAwait( false ) is { Ok: false } failed )
			{
				HackLogger.Error( nameof( CommitStaging ), $"Unable to stage relationships: {failed.Error}" );
				return false;
			}
			return true;
		}
		catch( OperationCanceledException )
		{
			throw;
		}
		catch( Exception e )
		{
			HackLogger.Error( nameof( CommitStaging ), $"Unable to create relationships: {e.Message}", e );
			return false;
		}
	}

	public static Task<bool> StagePropertiesAsync( params HpRecordStaged[] versions )
		=> StagePropertiesInternalAsync( versions, preOpenedStore: null, preReadProperties: null, CancellationToken.None );

	internal static Task<bool> StagePropertiesAsync(
		HpRecordStaged version,
		List<SwFileReader.SwProperty>? preReadProperties,
		CancellationToken token = default )
		=> StagePropertiesInternalAsync( [ version ], preOpenedStore: null, preReadProperties, token );

	internal static Task<bool> StagePropertiesAsync( HpRecordStaged version, SwStreamStore? preOpenedStore, CancellationToken token = default )
		=> StagePropertiesInternalAsync( [ version ], preOpenedStore, preReadProperties: null, token );

	private static async Task<bool> StagePropertiesInternalAsync(
		HpRecordStaged[] versions,
		SwStreamStore? preOpenedStore,
		List<SwFileReader.SwProperty>? preReadProperties,
		CancellationToken token )
	{
		List<HpVersionProperty> staged = [];
		foreach( HpRecordStaged version in versions )
		{
			token.ThrowIfCancellationRequested();
			try
			{
				if( SolidWorksFileOf( version ) is not { } file )
					continue;
				int versionId = version.id is int id and not 0 ? id : throw new ArgumentException( "version id not defined" );
				if( preReadProperties is not null && versions.Length == 1 )
				{
					foreach( SwFileReader.SwProperty p in preReadProperties )
					{
						if( HpVersionProperty.FromSwProperty( p.Configuration, p.Name, p.Type, p.Value, versionId, version.commit_id ) is { } property )
							staged.Add( property );
					}
				}
				else if( preOpenedStore is not null && versions.Length == 1 )
				{
					foreach( SwFileReader.SwProperty p in SwFileReader.ReadProperties( preOpenedStore ) )
					{
						if( HpVersionProperty.FromSwProperty( p.Configuration, p.Name, p.Type, p.Value, versionId, version.commit_id ) is { } property )
							staged.Add( property );
					}
				}
				else
				{
					foreach( var (config, name, type, value) in SolidWorksUtil.DocMgr?.GetProperties( file ) ?? [] )
					{
						if( HpVersionProperty.FromSwProperty( config, name, type, value, versionId, version.commit_id ) is { } property )
							staged.Add( property );
					}
				}
			}
			catch( Exception e )
			{
				HackLogger.Error( nameof( CommitStaging ), $"Unable to create properties for staged version {version.id}: {e.Message}", e );
				return false;
			}
		}
		if( staged.Count > 0 && await HpVersionProperty.MultiStageAsync( staged, token: token ).ConfigureAwait( false ) is { Ok: false } failed )
		{
			HackLogger.Error( nameof( CommitStaging ), $"Unable to stage properties: {failed.Error}" );
			return false;
		}
		return true;
	}
}
