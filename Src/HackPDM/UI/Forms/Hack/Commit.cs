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
using HackPDM.UI.Forms.Helper;

using Windows.Media;

namespace HackPDM.UI.Forms.Hack;

internal static class Commit
{
	internal static async Task Async_Commit( List<HackFile>? hacks )
	{
		if( hacks is null or { Count: 0 } )
			return;

		HpPDMCommit startCommit = new()
		{ node_by = OdooDefaults.Instance?.MyNode?.id };

		var hfs = hacks is not null && hacks.Count > 0 ? await Commit.FilterCommitHackFiles( hacks ) : ([], []);

		if ( hfs is (null or [], null or []) ) {
			InstanceManager.Session.Log( StatusMessage.ERROR, $"--- No entries to commit ---" );
			return;
		}

		await startCommit.CreateCommitAsync();

		List<HpVersion> localConversions = [];
		InstanceManager.HFM.ProcessCounter = 0;
		InstanceManager.HFM.SkipCounter = 0;
		InstanceManager.HFM.MaxCount = (hfs.Item1?.Length ?? 0) + (hfs.Item2?.Length ?? 0);// localVersions?.Length ?? 0;
		InstanceManager.Dialog.IsInProcess = true;

		InstanceManager.HFM.statusToken = await InstanceManager.HFM.statusToken.RenewTokenSourceAsync();
		InstanceManager.Session.Log( StatusMessage.PROCESSING, $"--- Preparing to stage records ---" );

		foreach( var hack in hfs.Item1 ) { if( !await Commit.CommitRecord( hack, startCommit, false ) ) return; }
		foreach( var hack in hfs.Item2 ) { if( !await Commit.CommitRecord( hack, startCommit, true ) ) return; }

		await startCommit.ServerCommit();
		MessageBox.ShowAsync( $"Completed!" );
		//await _treeHelper.RestartTree( OdooDirectoryTree );
		//_treeHelper.RestartEntries( OdooDirectoryTree, OdooEntryList );
	}
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
			HpEntry? entry = (await HpEntry.GetRecordsBySmartSearchAsync(
				searchFilter: arrList, 
				includedFields: [nameof(HpEntry.id), nameof(HpEntry.name), nameof(HpEntry.dir_id), nameof(HpEntry.checkout_user)], 
				insertFields: ["version_ids.checksum"]))?.FirstOrDefault();

			if( entry is not null and { id: not null and not 0 } )
			{
				if (entry.checkout_user?.id == OdooDefaults.Instance.OdooId) {
					InstanceManager.Session.Log( StatusMessage.FOUND, $"entry found remotely for: {filePath}" );
					hacksFound.Add( hackArr[ i ] );
					continue;
				}
				else {
					InstanceManager.Session.Log( StatusMessage.ERROR, $"entry found remotely but is not checked out to current user: {filePath}" );
					continue;
				}
			}

			InstanceManager.Session.Log( StatusMessage.INFO, $"Queued commit for {hackArr[ i ].Name} (Checksum: {hackArr[ i ].Checksum}) for: {filePath}" );
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
			InstanceManager.Session.Log( StatusMessage.FOUND, $"existing entry found..." );
			InstanceManager.Session.Log( StatusMessage.PROCESSING, $"staging local version..." );
			versionStaged = await HpVersion.StageVersion( hack, entry, commit.id ?? 0 );
		}
		if( entry.returnType is EntryReturnType.Staged )
		{
			HpRecordStaged? entryStaged = await entry?.StageEntry();
			InstanceManager.Session.Log( StatusMessage.PROCESSING, $"staging local entry..." );
			InstanceManager.Session.Log( StatusMessage.PROCESSING, $"staging local version..." );
			versionStaged = await HpVersion.StageVersion( hack, entryStaged );
		}
		if( entry.returnType is EntryReturnType.Failed )
		{
			commit.ServerClear();
			InstanceManager.Session.Log( StatusMessage.ERROR, $"Error creating entry {entry.name}. Rolling back records..." );
			return false;
		}

		try
		{
			if( versionStaged is not { id: not 0 } )
				throw new ArgumentException( "Version staging failed, Version is null or has an id of 0" );
			InstanceManager.Session.Log( StatusMessage.PROCESSING, $"staging local version relationships..." );
			// create new parent, child hp_version_relationship's for versions
			if( !await HpVersionRelationship.StageRelationshipRecords( versionStaged ) )
				throw new ArgumentException( $"Failed to stage version relationships for {versionStaged.payload?[ "name" ]}" );
			InstanceManager.Session.Log( StatusMessage.PROCESSING, $"staging local version properties ..." );
			if( !await HpVersionProperty.StagePropertyRecords( versionStaged ) )
				throw new ArgumentException( $"Failed to stage version properties for {versionStaged.payload?[ "name" ]}" );
			InstanceManager.HFM.ProcessCounter += 1;
			InstanceManager.Dialog?.SetProgressBar( InstanceManager.HFM.ProcessCounter, InstanceManager.HFM.MaxCount );
			//Dialog?.SetProgressBar(MaxCount, MaxCount);

		}
		catch( Exception e )
		{
			commit.ServerClear();
			Debug.WriteLine( e.Message );
			InstanceManager.Session.Log( StatusMessage.ERROR, e.Message );
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