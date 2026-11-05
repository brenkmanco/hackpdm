using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HackPDM.Core.General;
using HackPDM.Core.Hack;
using HackPDM.Domain.Helper;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Infrastructure.Odoo.Models;
using HackPDM.Infrastructure.Operations;
using HackPDM.Shared.GlobalData;
using HackPDM.UI.Forms.Helper;

namespace HackPDM.UI.Forms.Hack;

/// <summary>
/// Presentation layer facade for PDM commit workflows.
/// Delegates business logic to <see cref="PdmCommitOperations"/>.
/// </summary>
internal static class Commit
{
	internal static async Task Async_Commit( List<HackFile>? hacks )
	{
		if( hacks is null or { Count: 0 } )
			return;

		HpPDMCommit startCommit = new()
		{ node_by = OdooDefaults.Instance?.MyNode?.id };

		var hfs = hacks is not null && hacks.Count > 0 
			? await PdmCommitOperations.FilterCommitHackFilesAsync( hacks, (status, msg) => InstanceManager.Session.Log( status, msg ) ) 
			: ([], []);

		if ( hfs is (null or [], null or []) ) {
			InstanceManager.Session.Log( StatusMessage.ERROR, "--- No entries to commit ---" );
			return;
		}

		await startCommit.CreateCommitAsync();

		InstanceManager.HFM.ProcessCounter = 0;
		InstanceManager.HFM.SkipCounter = 0;
		InstanceManager.HFM.MaxCount = (hfs.Item1?.Length ?? 0) + (hfs.Item2?.Length ?? 0);
		if( InstanceManager.Dialog is not null )
			InstanceManager.Dialog.IsInProcess = true;

		InstanceManager.HFM.statusToken = await InstanceManager.HFM.statusToken.RenewTokenSourceAsync();
		InstanceManager.Session.Log( StatusMessage.PROCESSING, "--- Preparing to stage records ---" );

		if( hfs.Item1 is not null )
		foreach( var hack in hfs.Item1 )
		{
			if( !await PdmCommitOperations.CommitRecordAsync(
				hack,
				startCommit,
				false,
				(status, msg) => InstanceManager.Session.Log( status, msg ),
				() =>
				{
					InstanceManager.HFM.ProcessCounter += 1;
					InstanceManager.Dialog?.SetProgressBar( InstanceManager.HFM.ProcessCounter, InstanceManager.HFM.MaxCount );
				}) )
				return;
		}

		if( hfs.Item2 is not null )
		foreach( var hack in hfs.Item2 )
		{
			if( !await PdmCommitOperations.CommitRecordAsync(
				hack,
				startCommit,
				true,
				(status, msg) => InstanceManager.Session.Log( status, msg ),
				() =>
				{
					InstanceManager.HFM.ProcessCounter += 1;
					InstanceManager.Dialog?.SetProgressBar( InstanceManager.HFM.ProcessCounter, InstanceManager.HFM.MaxCount );
				}) )
				return;
		}

		await startCommit.ServerCommit();
		await MessageBox.ShowAsync( "Completed!" );
	}

	internal static async Task<HpDirectory?> CreateDirectories( HackFile? hack )
		=> await PdmCommitOperations.CreateDirectoriesAsync( hack );

	internal static async Task<(HackFile[], HackFile[])> FilesNotInOdooSegmented( IEnumerable<HackFile> hackFiles )
		=> await PdmCommitOperations.FilesNotInOdooSegmentedAsync( hackFiles, (status, msg) => InstanceManager.Session.Log( status, msg ) );

	internal static async Task<bool> CommitRecord( HackFile? hack, HpPDMCommit commit, bool inOdoo = false )
		=> await PdmCommitOperations.CommitRecordAsync(
			hack,
			commit,
			inOdoo,
			(status, msg) => InstanceManager.Session.Log( status, msg ),
			() =>
			{
				InstanceManager.HFM.ProcessCounter += 1;
				InstanceManager.Dialog?.SetProgressBar( InstanceManager.HFM.ProcessCounter, InstanceManager.HFM.MaxCount );
			});

	internal static async Task<(HackFile[], HackFile[])> FilterCommitHackFiles( ConcurrentSet<HackFile> hackFiles )
		=> await PdmCommitOperations.FilterCommitHackFilesAsync( hackFiles, (status, msg) => InstanceManager.Session.Log( status, msg ) );
}
