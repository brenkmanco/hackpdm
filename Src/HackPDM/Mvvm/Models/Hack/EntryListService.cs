using System;
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

		if( directoryId is int id and not 0 )
		{
			Result<IReadOnlyList<RemoteEntryDto>> entries = await HpDirectory.GetEntriesAsync( id, includeDeleted );
			token.ThrowIfCancellationRequested();
			if( !entries.Ok )
				System.Diagnostics.Debug.WriteLine( $"entries of directory {id} could not be read: {entries.Error}" );
			if( entries.Value is { } remote )
			{
				// local copies are hashed off the UI thread
				localByRemotePath = await HashLocalCopiesAsync( remote );
				token.ThrowIfCancellationRequested();
				foreach( RemoteEntryDto entry in remote )
					rows.Add( await CreateRemoteRowAsync( entry, localByRemotePath ) );
			}
		}

		var (localFiles, localAndRemote) = await FindLocalOnlyFilesAsync( treePath, localByRemotePath );
		token.ThrowIfCancellationRequested();
		foreach( HackFile file in localFiles )
			rows.Add( CreateLocalRow( file, filterUnknownTypes ) );

		return new EntryListResult( rows, localByRemotePath?.Count ?? 0, localFiles.Length, localAndRemote );
	}

	/// <summary>Maps each remote entry's Odoo path to its local file (read and SHA1-hashed for the status column).</summary>
	private static Task<Dictionary<string, Task<HackFile>>> HashLocalCopiesAsync( IReadOnlyList<RemoteEntryDto> entries )
		=> Task.Run( async () =>
		{
			Dictionary<string, Task<HackFile>> map = new( entries.Count );
			using SemaphoreSlim throttle = new( HashConcurrency );
			foreach( RemoteEntryDto entry in entries )
			{
				if( entry.FullName is not { } remotePath )
					continue;
				map.TryAdd( remotePath, HashAsync( PwaPaths.ToAbsolute( remotePath ), throttle ) );
			}
			await Task.WhenAll( map.Values );
			return map;
		} );

	private static async Task<HackFile> HashAsync( string? localPath, SemaphoreSlim throttle )
	{
		await throttle.WaitAsync();
		try
		{
			// hashing is synchronous; Task.Run lets the files hash in parallel (null for a missing file)
			return ( await Task.Run( () => HackFile.GetFromPath( localPath ) is { Exists: true } hack ? hack : null ) )!;
		}
		finally
		{
			throttle.Release();
		}
	}

	/// <summary>Files in the folder that have no remote entry (not hashed: local-only rows never need the checksum).</summary>
	private static Task<(HackFile[] files, int localAndRemoteCount)> FindLocalOnlyFilesAsync( string? treePath, Dictionary<string, Task<HackFile>>? remoteMap )
	{
		string path = PwaPaths.ToAbsolute( treePath ) ?? PwaPaths.Vault;
		return Task.Run( () =>
		{
			if( treePath is null || !Directory.Exists( path ) )
				return ( Array.Empty<HackFile>(), 0 );
			HackFile[]? files = FileOperations.FilesInDirectory( path, remoteMap, out int localAndRemote, computeChecksums: false );
			return ( files ?? [], localAndRemote );
		} );
	}

	private static async Task<EntryRow> CreateRemoteRowAsync( RemoteEntryDto entry, Dictionary<string, Task<HackFile>> localByRemotePath )
	{
		HackFile? local = !string.IsNullOrWhiteSpace( entry.FullName ) && localByRemotePath.TryGetValue( entry.FullName, out var hashing )
			? await hashing // already finished
			: null;

		var checkout = entry.CheckoutUserId is int userId ? OdooDefaults.Instance.IdToUser.GetValueOrDefault( userId ) : null;

		FileStatus status = EntryStatusResolver.ResolveRemote(
			deleted: entry.Deleted,
			checkoutUserId: checkout?.id,
			myUserId: OdooDefaults.Instance.OdooId,
			// Odoo sends false when there is no checksum: no versions
			latestChecksum: entry.LatestChecksum ?? (object)false,
			localChecksum: local?.Checksum,
			remoteDate: entry.LatestDate ?? default,
			localModified: local?.ModifiedDate );

		return new EntryRow
		{
			Id = entry.Id,
			Name = entry.Name,
			Type = entry.Type,
			Size = entry.Size,
			Checkout = checkout,
			ReprType = local?.Exists is true ? EntryReprType.Both : EntryReprType.Remote,
			LatestReleaseId = entry.LatestReleaseId,
			LatestId = entry.LatestVersionId,
			RemoteDate = entry.LatestDate,
			LocalDate = local?.ModifiedDate.Year is null or 1 ? null : local?.ModifiedDate,
			Status = status,
			Category = entry.Category is string category
				? OdooDefaults.Instance.HpCategories?.FirstOrDefault( c => c.name?.Equals( category ) is true )
				: null,
			FullName = entry.FullName,
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
