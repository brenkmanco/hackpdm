using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using HackPDM.Core;
using HackPDM.Core.Hack;
using HackPDM.Domain.OdooModels.Models;
using HackPDM.Domain.Representation;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Infrastructure.Odoo.Models;
using HackPDM.Shared.GlobalData;

using EntryRow = HackPDM.Mvvm.Models.Hack.EntryRow;

namespace HackPDM.Mvvm.Models.Hack;

/// <summary>Rows for one directory plus the counts shown under the entry list.</summary>
/// <param name="RemoteCount">Entries Odoo has in this directory.</param>
/// <param name="LocalOnlyCount">Files on disk with no remote entry.</param>
/// <param name="LocalAndRemoteCount">Remote entries that also exist on disk.</param>
public sealed record EntryListResult( List<EntryRow> Rows, int RemoteCount, int LocalOnlyCount, int LocalAndRemoteCount );

/// <summary>Builds the entry rows for a directory by combining Odoo entries with the files on disk.</summary>
public interface IEntryListService
{
	/// <param name="directoryId">Odoo directory id; null or 0 for a folder that only exists locally.</param>
	/// <param name="treePath">The folder's tree path ("root\a\b").</param>
	/// <param name="includeDeleted">Also list logically deleted entries.</param>
	/// <param name="filterUnknownTypes">Mark local files with unregistered extensions as filtered.</param>
	Task<EntryListResult> LoadAsync( int? directoryId, string? treePath, bool includeDeleted, bool filterUnknownTypes, CancellationToken token );
}

public sealed class EntryListService : IEntryListService
{
	// how many local files are read + hashed at once; IO bound, so a small multiple of the cores
	private static readonly int HashConcurrency = Math.Clamp( Environment.ProcessorCount, 2, 8 );

	public async Task<EntryListResult> LoadAsync( int? directoryId, string? treePath, bool includeDeleted, bool filterUnknownTypes, CancellationToken token )
	{
		List<EntryRow> rows = [];
		Dictionary<string, Task<HackFile>>? localByRemotePath = null;

		if( directoryId is not ( null or 0 ) )
		{
			Hashtable? entries = await HpDirectory.GetEntriesAsync( directoryId, includeDeleted );
			token.ThrowIfCancellationRequested();
			if( entries is not null )
			{
				// local copies are hashed off the UI thread
				localByRemotePath = await HashLocalCopiesAsync( entries );
				token.ThrowIfCancellationRequested();
				foreach( DictionaryEntry pair in entries )
				{
					if( await CreateRemoteRowAsync( pair, localByRemotePath ) is { } row )
						rows.Add( row );
				}
			}
		}

		var (localFiles, localAndRemote) = await FindLocalOnlyFilesAsync( treePath, localByRemotePath );
		token.ThrowIfCancellationRequested();
		foreach( HackFile file in localFiles )
			rows.Add( CreateLocalRow( file, filterUnknownTypes ) );

		return new EntryListResult( rows, localByRemotePath?.Count ?? 0, localFiles.Length, localAndRemote );
	}

	/// <summary>Maps each remote entry's Odoo path to its local file (read and SHA1-hashed for the status column).</summary>
	private static Task<Dictionary<string, Task<HackFile>>> HashLocalCopiesAsync( Hashtable entries )
		=> Task.Run( async () =>
		{
			Dictionary<string, Task<HackFile>> map = new( entries.Count );
			using SemaphoreSlim throttle = new( HashConcurrency );
			foreach( DictionaryEntry pair in entries )
			{
				if( ( pair.Value as Hashtable )?[ "fullname" ] is not string remotePath )
					continue;
				map.Add( remotePath, HashAsync( FileOperations.ConvertToWindowsPath( remotePath, true ), throttle ) );
			}
			await Task.WhenAll( map.Values );
			return map;
		} );

	private static async Task<HackFile> HashAsync( string? localPath, SemaphoreSlim throttle )
	{
		await throttle.WaitAsync();
		try
		{
			// GetHackFileAsync hashes synchronously; Task.Run lets the files hash in parallel
			return ( await Task.Run( () => HackFile.GetHackFileAsync( localPath ) ) )!;
		}
		finally
		{
			throttle.Release();
		}
	}

	/// <summary>Files in the folder that have no remote entry (not hashed: local-only rows never need the checksum).</summary>
	private static Task<(HackFile[] files, int localAndRemoteCount)> FindLocalOnlyFilesAsync( string? treePath, Dictionary<string, Task<HackFile>>? remoteMap )
	{
		string path = HackDefaults.DefaultPath( treePath, true );
		return Task.Run( () =>
		{
			if( treePath is null || !Directory.Exists( path ) )
				return ( Array.Empty<HackFile>(), 0 );
			HackFile[]? files = FileOperations.FilesInDirectory( path, remoteMap, out int localAndRemote, computeChecksums: false );
			return ( files ?? [], localAndRemote );
		} );
	}

	private static async Task<EntryRow?> CreateRemoteRowAsync( DictionaryEntry pair, Dictionary<string, Task<HackFile>> localByRemotePath )
	{
		if( pair.Value is not Hashtable table )
			return null;

		string? fullName = table[ "fullname" ] as string;
		HackFile? local = !string.IsNullOrWhiteSpace( fullName ) && localByRemotePath.TryGetValue( fullName, out var hashing )
			? await hashing // already finished
			: null;

		var checkout = table[ "checkout" ] is int userId and not 0 ? OdooDefaults.Instance.IdToUser.GetValueOrDefault( userId ) : null;
		DateTime remoteDate = DateTime.TryParse( table[ "latest_date" ] as string, out DateTime parsed ) ? parsed : default;

		FileStatus status = EntryStatusResolver.ResolveRemote(
			deleted: table[ "deleted" ] as bool?,
			checkoutUserId: checkout?.id,
			myUserId: OdooDefaults.Instance.OdooId,
			latestChecksum: table[ "latest_checksum" ],
			localChecksum: local?.Checksum,
			remoteDate: remoteDate,
			localModified: local?.ModifiedDate );

		string? type = table[ "type" ] as string;
		return new EntryRow
		{
			Id = table[ "id" ] as int?,
			Name = pair.Key.ToString(),
			Type = type,
			Size = Convert.ToInt64( table[ "size" ] ),
			Checkout = checkout,
			ReprType = local?.Exists is true ? EntryReprType.Both : EntryReprType.Remote,
			LatestReleaseId = table[ "release" ] as int?,
			LatestId = table[ "latest" ] as int?,
			RemoteDate = remoteDate == default ? null : remoteDate,
			LocalDate = local?.ModifiedDate.Year is null or 1 ? null : local?.ModifiedDate,
			Status = status,
			Category = table[ "category" ] is string category
				? OdooDefaults.Instance.HpCategories?.FirstOrDefault( c => c.name?.Equals( category ) is true )
				: null,
			FullName = fullName,
		};
	}

	private static EntryRow CreateLocalRow( HackFile file, bool filterUnknownTypes )
	{
		string extension = file.TypeExt?.ToLower() ?? "";
		var defaults = OdooDefaults.Instance;
		FileStatus status = EntryStatusResolver.ResolveLocal(
			restrictTypes: defaults.RestrictTypes is true,
			filtered: filterUnknownTypes,
			knownType: defaults.ExtToType.ContainsKey( extension ),
			ignored: defaults.ExtToFilter.ContainsKey( extension ) );

		string type = extension.TrimStart( '.' );
		IHpCategoryModel? category = defaults.ExtToCat.GetValueOrDefault( extension );
		return new EntryRow
		{
			Id = null,
			Name = file.Name,
			Type = type,
			Size = file.FileSize,
			LocalDate = file.ModifiedDate,
			RemoteDate = null,
			Status = status,
			Checkout = null,
			Category = category,
			FullName = file.FullPath,
			ReprType = EntryReprType.Local,
		};
	}
}
