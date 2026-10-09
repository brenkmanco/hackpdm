using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using HackPDM.Core;
using HackPDM.Core.General;
using HackPDM.Core.Hack;
using HackPDM.Domain.Helper;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Infrastructure.Odoo.Models;
using HackPDM.Shared.GlobalData;

namespace HackPDM.UI.Forms.Hack;

internal static class Commit
{
	internal async static Task<HpDirectory?> CreateDirectories( HackFile? hack )
	{
		// create directories that don't exist in odoo
		ArrayList? paths = hack?.RelativePath?.Split<ArrayList>("\\", StringSplitOptions.RemoveEmptyEntries);
		paths?.RemoveAt( paths.Count - 1 );
		return ( await HpDirectory.CreateNew( paths ) )?.LastOrDefault()
			?? throw new Exception( $"{HpDirectory.GetHpModel()} didn't create any records" );
	}
	internal async static Task<(HackFile[], HackFile[])> FilesNotInOdooSegmented( IEnumerable<HackFile> hackFiles )
	{
		HackFile[] hackArr = [.. hackFiles];
		List<HackFile> hacks = [];
		List<HackFile> hacksFound = [];

		ArrayList[] arrayList = new ArrayList[hackArr.Length];


		for( int i = 0; i < hackArr.Length; i++ )
		{
			string filepath = hackArr[i].TypeExt.ToLower();
			if( OdooDefaults.Instance.RestrictTypes is true && !OdooDefaults.Instance.ExtToType.ContainsKey( filepath ?? "-=-=-" ) )
				continue;


			string filePath = FileOperations.WindowsToOdooPath(hackArr[i].RelativePath);
			ArrayList arrList =
			[
				new ArrayList() { "name", "=", hackArr[i].Name },
				new ArrayList() { "windows_complete_name", "=", (@"root\" + hackArr[i].RelativePath) },
			];
			HpEntry? entry = (await HpEntry.GetRecordsBySmartSearchAsync(searchFilter: arrList, includedFields: [nameof(HpEntry.name), nameof(HpEntry.dir_id)], insertFields: ["version_ids.checksum"]))?.FirstOrDefault();
			ArrayList fields = [];
			if( entry is not null and { id: not 0 } )
			{
				HackFileManager.Dialog?.AddStatusLine( StatusMessage.FOUND, $"entry found remotely for: {filePath}" );
				hacksFound.Add( hackArr[ i ] );
				continue;
			}
			//if (entry is not null && entry.HashedValues.TryGetValue("version_ids.checksum", out ArrayList? arr))
			//{
			//	// this means that this hackFile is in the database so it can be skipped
			//	if (arr.FirstOrDefault<Hashtable>(x =>   x.TryGetValue("value", out string? checksum)  &&  checksum == hackArr[i].GetChecksum()   ) is Hashtable foundChecksum)
			//	{

			//		HackFileManager.Dialog?.AddStatusLine(StatusMessage.FOUND, $"checksum found remotely ({hackArr[ i ].Checksum}) for: {filePath}");
			//		continue;
			//	}
			//}

			HackFileManager.Dialog?.AddStatusLine( StatusMessage.INFO, $"Queued commit for {hackArr[ i ].Name} (Checksum: {hackArr[ i ].Checksum}) for: {filePath}" );
			hacks.Add( hackArr[ i ] );
		}
		return ([ .. hacks ], [ .. hacksFound ]);
	}

	internal static async Task<bool> CommitRecord( HackFile? hack, HpPDMCommit commit, bool inOdoo = false )
	{
		if( hack is null )
			return false;

		// commit directories
		HpDirectory? dir = await CreateDirectories(hack);

		// stage / retrieve entry record

		HpEntry entry = await HpEntry.ConvertToEntry(hack, dir?.id ?? 0, commit) ?? new();
		if( inOdoo )
		{
			entry = await entry?.FindRemoteEntry() ?? entry;
		}

		// stage version record

		HpRecordStaged? versionStaged = null;
		if( entry.returnType is EntryReturnType.GotExisting )
		{
			HackFileManager.Dialog?.AddStatusLine( StatusMessage.FOUND, $"existing entry found..." );
			HackFileManager.Dialog?.AddStatusLine( StatusMessage.PROCESSING, $"staging local version..." );
			versionStaged = await HpVersion.StageVersion( hack, entry, commit.id ?? 0 );
		}
		if( entry.returnType is EntryReturnType.Staged )
		{
			HpRecordStaged? entryStaged = await entry?.StageEntry();
			HackFileManager.Dialog?.AddStatusLine( StatusMessage.PROCESSING, $"staging local entry..." );
			HackFileManager.Dialog?.AddStatusLine( StatusMessage.PROCESSING, $"staging local version..." );
			versionStaged = await HpVersion.StageVersion( hack, entryStaged );
		}
		if( entry.returnType is EntryReturnType.Failed )
		{
			commit.ServerClear();
			HackFileManager.Dialog?.AddStatusLine( StatusMessage.ERROR, $"Error creating entry {entry.name}. Rolling back records..." );
			return false;
		}

		try
		{
			if( versionStaged is not { id: not 0 } )
				throw new ArgumentException( "Version staging failed, Version is null or has an id of 0" );
			HackFileManager.

						// staging HpVersionRelationship's from version

						Dialog?.AddStatusLine( StatusMessage.PROCESSING, $"staging local version relationships..." );
			// create new parent, child hp_version_relationship's for versions
			if( !await HpVersionRelationship.StageRelationshipRecords( versionStaged ) )
				throw new ArgumentException( $"Failed to stage version relationships for {versionStaged.payload?[ "name" ]}" );
			HackFileManager.

						// staging HpVersionProperty's from version
						Dialog?.AddStatusLine( StatusMessage.PROCESSING, $"staging local version properties ..." );
			if( !await HpVersionProperty.StagePropertyRecords( versionStaged ) )
				throw new ArgumentException( $"Failed to stage version properties for {versionStaged.payload?[ "name" ]}" );
			Latest.
						ProcessCounter += 1;
			HackFileManager.Dialog?.SetProgressBar( Latest.ProcessCounter, Latest.MaxCount );
			//Dialog?.SetProgressBar(MaxCount, MaxCount);

		}
		catch( Exception e )
		{
			commit.ServerClear();
			Debug.WriteLine( e.Message );
			HackFileManager.Dialog?.AddStatusLine( StatusMessage.ERROR, e.Message );
			return false;
		}
		return true;
	}

	internal static async Task<(HackFile[], HackFile[])> FilterCommitHackFiles( ConcurrentSet<HackFile> hackFiles )
	{
		List<Task<HackFile>> tasks = [];
		object lockObject = new();
		string combinedPattern = string.Join("|", OdooDefaults.Instance?.EntryFilterPatterns ?? []);
		var regex = new Regex(combinedPattern, RegexOptions.IgnoreCase);
		//string[] filePaths = hackFiles.Select(hack => hack.FullPath).ToArray();

		List<HackFile> hacks = [];
		foreach( HackFile hack in hackFiles )
		{
			regex = new Regex( combinedPattern, RegexOptions.IgnoreCase );
			if( !regex.IsMatch( $".{hack.TypeExt?.ToLower()}" ) )
			{
				hacks.Add( hack );
			}
		}
		return await FilesNotInOdooSegmented( hacks );
	}
}