using System.Globalization;

namespace HackPDM.Infrastructure.Odoo;

/// <summary>An entry as hp.directory.get_entries / get_children_directories_by_id describe it.</summary>
public sealed record RemoteEntryDto(
	string Name,
	int Id,
	string? Type,
	int? CheckoutUserId,
	string? Category,
	string? FullName,
	long Size,
	int? LatestVersionId,
	int? LatestReleaseId,
	string? LatestChecksum,
	DateTime? LatestDate,
	bool Deleted )
{
	internal static RemoteEntryDto FromRow( string name, IReadOnlyDictionary<string, object?> row ) => new(
		Name: name,
		Id: row.Int( "id" ) ?? 0,
		Type: row.Text( "type" ),
		CheckoutUserId: row.Int( "checkout" ) is int user and not 0 ? user : null,
		Category: row.Text( "category" ),
		FullName: row.Text( "fullname" ),
		Size: row.GetValueOrDefault( "size" ) switch { int i => i, long l => l, _ => 0 },
		LatestVersionId: row.Int( "latest" ),
		LatestReleaseId: row.Int( "release" ),
		LatestChecksum: row.Text( "latest_checksum" ),
		LatestDate: DateTime.TryParse( row.Text( "latest_date" ), CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date ) ? date : null,
		Deleted: row.Bool( "deleted" ) );

	/// <summary>The entries of a { name: entry } map, in the server's order.</summary>
	internal static IReadOnlyList<RemoteEntryDto> ListFrom( IReadOnlyDictionary<string, object?>? map )
		=> map is null
			? []
			: [ .. map.Select( pair => pair.Value is IReadOnlyDictionary<string, object?> row ? FromRow( pair.Key, row ) : null ).OfType<RemoteEntryDto>() ];
}

/// <summary>A directory and everything below it, as hp.directory.get_children_directories_by_id returns it.</summary>
public sealed record SubdirectoryDto( int Id, string Name, IReadOnlyList<SubdirectoryDto> Directories, IReadOnlyList<RemoteEntryDto> Entries )
{
	internal static SubdirectoryDto FromRow( IReadOnlyDictionary<string, object?> row ) => new(
		Id: row.Int( "id" ) ?? 0,
		Name: row.Text( "name" ) ?? "",
		Directories: [ .. ( row.Object( "directories" ) ?? new Dictionary<string, object?>() ).Values
			.OfType<IReadOnlyDictionary<string, object?>>().Select( FromRow ) ],
		Entries: RemoteEntryDto.ListFrom( row.Object( "entries" ) ) );
}

/// <summary>How far a path of directory names already exists (hp.directory.last_available_directory).</summary>
/// <param name="Index">Index in the path of the deepest existing directory (0 = root).</param>
/// <param name="DirectoryId">That directory's id.</param>
public readonly record struct LastAvailableDirectory( int Index, int DirectoryId );
