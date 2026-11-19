using HackPDM.Core.Odoo;
using HackPDM.Core.Odoo.Models;
using HackPDM.Core.Pdm;

namespace HackPDM.Core.Services;

public sealed class BasicStatusMessage
{
	public StatusMessage Status { get; set; } = StatusMessage.OTHER;
	public string Category { get; set; } = string.Empty;
	public string? Message { get; set; }
	public DateTime Timestamp { get; set; } = DateTime.Now;
	public string TimestampText => Timestamp.ToString( "HH:mm:ss" );

	public static implicit operator BasicStatusMessage( (StatusMessage status, string message) log )
		=> new() { Message = log.message, Status = log.status };
}

public class RowData
{
	public virtual string? Name { get; set; } = "";
	public virtual string? Text
	{
		get => field ??= Name;
		set;
	}
}

/// <summary>One row of the file manager's entry list: a remote entry, a local-only file, or both.</summary>
public sealed class EntryRow : RowData
{
	public int? Id { get; set; }
	public string? Type { get; set; }
	public long? Size { get; set; }
	public FileStatus Status { get; set; } = FileStatus.Lo;
	public HpUser? Checkout { get; set; }
	public HpCategory? Category { get; set; }
	public DateTime? LocalDate { get; set; }
	public DateTime? RemoteDate { get; set; }
	public string? FullName { get; set; }
	public int? LatestId { get; set; }
	public int? LatestReleaseId { get; set; }
	public EntryReprType? ReprType { get; set; }
	public RemoteEntryDto? Remote { get; set; }

	public FileInfo? LocalFile
	{
		get
		{
			if( field is not null )
				return field;

			string? path = ReprType switch
			{
				EntryReprType.Both or EntryReprType.Remote => PwaPaths.ToAbsolute( FullName ),
				EntryReprType.Local => FullName,
				_ => null,
			};

			FileInfo? file = string.IsNullOrEmpty( path ) ? null : new( path );
			field = file?.Exists is true ? file : null;
			return field;
		}
		set;
	}

	public bool? IsLocal => Id switch
	{
		null => null,
		0 => true,
		_ => LocalFile?.Exists ?? false,
	};

	public bool? IsRemote => Id switch
	{
		null => null,
		not 0 => true,
		_ => false,
	};

	public bool? IsOnlyLocal => IsRemote is false && IsLocal is true;
	public bool? IsOnlyRemote => IsRemote is true && IsLocal is false;
}

/// <summary>Sort and group keys for the entry list, keyed by <see cref="EntryRow"/> property name.</summary>
public static class EntryRowKeys
{
	private static readonly Dictionary<string, Func<EntryRow, IComparable?>> SortKeys = new()
	{
		[ nameof( EntryRow.Name ) ] = e => e.Name ?? "",
		[ nameof( EntryRow.Id ) ] = e => e.Id ?? 0,
		[ nameof( EntryRow.Checkout ) ] = e => e.Checkout?.name ?? "",
		[ nameof( EntryRow.Size ) ] = e => e.Size,
		[ nameof( EntryRow.Type ) ] = e => e.Type ?? "",
		[ nameof( EntryRow.Status ) ] = e => Enum.GetName( e.Status ) ?? "",
		[ nameof( EntryRow.LatestId ) ] = e => e.LatestId,
		[ nameof( EntryRow.RemoteDate ) ] = e => e.RemoteDate,
		[ nameof( EntryRow.LocalDate ) ] = e => e.LocalDate,
		[ nameof( EntryRow.Category ) ] = e => e.Category?.name ?? "",
		[ nameof( EntryRow.FullName ) ] = e => e.FullName ?? "",
	};

	private static readonly Dictionary<string, Func<EntryRow, IComparable?>> GroupKeys = new( SortKeys )
	{
		[ nameof( EntryRow.RemoteDate ) ] = e => e.RemoteDate?.Date,
		[ nameof( EntryRow.LocalDate ) ] = e => e.LocalDate?.Date,
	};

	public static IReadOnlyCollection<string> Names => SortKeys.Keys;

	public static bool TryGetSortKey( string? fieldName, out Func<EntryRow, IComparable?> key )
		=> TryGet( SortKeys, fieldName, out key );

	public static bool TryGetGroupKey( string? fieldName, out Func<EntryRow, IComparable?> key )
		=> TryGet( GroupKeys, fieldName, out key );

	private static bool TryGet(
		Dictionary<string, Func<EntryRow, IComparable?>> keys,
		string? fieldName,
		out Func<EntryRow, IComparable?> key )
	{
		if( fieldName is not null && keys.TryGetValue( fieldName, out var found ) )
		{
			key = found;
			return true;
		}
		key = _ => null;
		return false;
	}
}

public sealed record EntryListResult( List<EntryRow> Rows, int RemoteCount, int LocalOnlyCount, int LocalAndRemoteCount );

public sealed class HistoryRow : RowData
{
	public int Version { get; set; }
	public HpUser? ModUser { get; set; }
	public DateTime? ModDate { get; set; }
	public long? Size { get; set; }
	public DateTime? RelDate { get; set; }
}

public sealed class ParentRow : RowData
{
	public int Version { get; set; }
	public string? BasePath { get; set; }
}

public sealed class ChildrenRow : RowData
{
	public int Version { get; set; }
	public string? BasePath { get; set; }
}

public sealed class PropertiesRow : RowData
{
	public int Version { get; set; }
	public int? Property { get; set; }
	public string? Configuration { get; set; }
	public PropertyType? Type { get; set; }
	public object? ValueData { get; set; }
}

public sealed class VersionRow : RowData
{
	public int Id { get; set; }
	public long? FileSize { get; set; }
	public int? DirectoryId { get; set; }
	public int? NodeId { get; set; }
	public int? EntryId { get; set; }
	public int? AttachmentId { get; set; }
	public DateTime? ModifyDate { get; set; }
	public string? Checksum { get; set; }
	public string? OdooCompletePath { get; set; }
}

public sealed class SearchRow : RowData
{
	public int? Id { get; set; }
	public string? Directory { get; set; }
	public string DisplayText => string.IsNullOrWhiteSpace( Directory )
		? ( Name ?? string.Empty )
		: $"{Name} — {Directory}";
}

public sealed class SearchPropertiesRow : RowData
{
	public int ID { get; set; }
	public Operators Comparer { get; set; }
	public string? Value { get; set; }
	public bool IsTextOrDate { get; set; }
}

public sealed class OperatorsRow : RowData
{
	public Operators Operator
	{
		get => field;
		set
		{
			field = value;
			OpRepr = OperatorNames.ToOdoo( value );
		}
	}

	public string? OpRepr { get; set; }
}

public sealed class FileTypeRow : RowData
{
	public string? Extension { get; set; }
	public string? Category { get; set; }
	public string? RegEx { get; set; }
	public string? Description { get; set; }
}

public sealed class FileTypeEntryFilterRow : RowData
{
	public int Id { get; set; }
	public string? Proto { get; set; }
	public string? RegEx { get; set; }
	public string? Description { get; set; }
}
