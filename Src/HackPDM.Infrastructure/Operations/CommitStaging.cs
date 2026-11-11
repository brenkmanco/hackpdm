using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using HackPDM.Core;
using HackPDM.Core.Hack;
using HackPDM.Domain.OdooModels.Models;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Infrastructure.Odoo.Models;
using HackPDM.Infrastructure.SldWrks;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Infrastructure.Operations;

/// <summary>
/// The records a commit stages for one local file: its entry, its new version, and for a
/// SolidWorks file the version's references and custom properties. The server turns staged
/// records into real ones when the commit is pushed.
/// </summary>
public static class CommitStaging
{
	// ---------------------------------------------------------------- entries

	/// <summary>A new entry for <paramref name="hack"/> in directory <paramref name="dirId"/>; null for a restricted type.</summary>
	public static HpEntry? NewEntry( HackFile hack, int dirId, HpPDMCommit? commit = null )
	{
		IHpTypeModel? type = TypeOf( hack, out bool restricted );
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

	/// <summary>The live server entry with the same name in the same directory, marked as found; null when there is none.</summary>
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

	// ---------------------------------------------------------------- versions

	/// <summary>A new version of an existing entry; null for a restricted type.</summary>
	internal static HpVersion? PrepareVersion( HackFile hack, IHpEntryModel entry, int commitId )
		=> NewVersion( hack, entry.id, entry.dir_id as Many2One, entry.windows_complete_name, (Many2One?)commitId );

	/// <summary>The first version of an entry staged in this commit; null for a restricted type.</summary>
	internal static HpVersion? PrepareVersion( HackFile hack, IHpRecordStagedModel stagedEntry )
		=> NewVersion( hack, stagedEntry.id, stagedEntry.payload?.GetValueOrDefault( "dir_id" ) as int?,
			stagedEntry.payload?.GetValueOrDefault( "windows_complete_name" ) as string, stagedEntry.commit_id );

	/// <summary>Stages a new version of an existing entry; null when it couldn't be.</summary>
	public static Task<HpRecordStaged?> StageVersionAsync( HackFile hack, IHpEntryModel entry, int commitId )
		=> StageAsync( PrepareVersion( hack, entry, commitId ), existingEntry: true );

	/// <summary>Stages the first version of an entry staged in this commit; null when it couldn't be.</summary>
	public static Task<HpRecordStaged?> StageVersionAsync( HackFile hack, IHpRecordStagedModel stagedEntry )
		=> StageAsync( PrepareVersion( hack, stagedEntry ), existingEntry: false );

	private static async Task<HpRecordStaged?> StageAsync( HpVersion? version, bool existingEntry )
	{
		if( version is null )
			return null;
		if( existingEntry )
			version.HashedValues[ "__internal__existing" ] = true;
		HpRecordStaged? staged = await version.StageRecAsync();
		return staged?.id is not null and not 0 ? staged : null;
	}

	private static HpVersion? NewVersion( HackFile hack, Many2One? entryId, Many2One? dirId, string? serverName, IMany2One? commit )
	{
		TypeOf( hack, out bool restricted );
		if( restricted )
			return null;
		HpVersion version = new()
		{
			name = hack.Name,
			dir_id = dirId,
			entry_id = entryId,
			file_ext = hack.TypeExt?[ 1.. ].ToLower(),
			WinPathway = serverName,
			node_id = OdooDefaults.Instance.MyNode?.id,
			file_contents = hack.FileContents ?? ReadContents( hack ),
			preview_image = PreviewOf( hack ),
			commit_id = commit,
			HashedValues = { [ "windows_complete_name" ] = serverName },
		};
		return version;
	}

	private static byte[]? ReadContents( HackFile hack )
	{
		try
		{
			return File.ReadAllBytes( hack.FullPath ?? "" );
		}
		catch( Exception ex )
		{
			Debug.WriteLine( ex );
			return null;
		}
	}

	/// <summary>The saved preview of a SolidWorks file, sent with its new version so every client can show it.</summary>
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
			Debug.WriteLine( $"no preview for {path}: {ex.Message}" );
			return null;
		}
	}

	/// <summary>The file's type; <paramref name="restricted"/> when the server only allows known types and this isn't one.</summary>
	private static IHpTypeModel? TypeOf( HackFile hack, out bool restricted )
	{
		bool known = OdooDefaults.Instance.ExtToType.TryGetValue( hack.TypeExt?.ToLower() ?? "", out IHpTypeModel? type );
		restricted = OdooDefaults.Instance.RestrictTypes is true && !known;
		return type;
	}

	// ---------------------------------------------------------------- SolidWorks references and properties

	/// <summary>The local file of a staged SolidWorks version; null for any other file.</summary>
	private static string? SolidWorksFileOf( HpRecordStaged? version )
	{
		if( version is null || !SwDocumentTypes.IsSolidWorksExtension( version.payload?.GetValueOrDefault( "file_ext" )?.ToString() ) )
			return null;
		string? serverName = version.payload?.GetValueOrDefault( "WinPathway" )?.ToString()
			?? version.HashedValues.GetValueOrDefault( "windows_complete_name" ) as string
			?? throw new ArgumentException( $"staged version {version.id} has no path" );
		// "root\dir\file" whatever the OS
		return PwaPaths.ToAbsolute( serverName );
	}

	/// <summary>Stages a relationship for every file each staged SolidWorks version references.</summary>
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
				// the server links a dependency by its file name, so keep every reference: one saved
				// from another folder or machine (not under the vault) used to be dropped here
				staged.AddRange( SwDependencies.Of( file, resolver ).Select( r => r.Local ?? r.Stored ).Select( path => new HpRecordStaged
				{
					commit_id = version.commit_id,
					committing_id = (Many2One?)version.commit_id,
					target_model = HpVersionRelationship.GetHpModel(),
					payload = new Dictionary<string, object?>
					{
						[ "path" ] = path,
						// lets the server link this exact file, not just any file of that name
						[ "windows_complete_name" ] = PwaPaths.ToServerName( path ),
						[ "version" ] = new Dictionary<string, object?>
						{
							[ "name" ] = version.payload?.GetValueOrDefault( "name" ),
							[ "entry_id" ] = version.payload?.GetValueOrDefault( "entry_id" ),
							[ "version_staged_id" ] = version.id,
						},
					},
				} ) );
			}
			if( staged.Count > 0 )
				await HpRecordStaged.MultiCreateAsync( staged );
			return true;
		}
		catch( Exception e )
		{
			Debug.WriteLine( $"unable to create relationships: {e.Message}" );
			return false;
		}
	}

	/// <summary>Stages the custom properties of each staged SolidWorks version.</summary>
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
					if( HpVersionProperty.FromSwProperty( config, name, type, value, versionId, version.commit_id ) is { } property )
						staged.Add( property );
			}
			catch( Exception e )
			{
				Debug.WriteLine( $"unable to create properties for {version.id}\n{e}" );
				return false;
			}
		}
		if( staged.Count > 0 )
			await HpVersionProperty.MultiStageAsync( staged );
		return true;
	}
}
