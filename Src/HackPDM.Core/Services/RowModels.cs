using System.Collections.Frozen;
using System.ComponentModel;

using HackPDM.Core.Odoo;
using HackPDM.Core.Odoo.Models;
using HackPDM.Core.Pdm;

namespace HackPDM.Core.Services;

[Bindable( true )]
public sealed class BasicStatusMessage
{
	private string? _timestampText;

	public StatusMessage Status { get; set; } = StatusMessage.OTHER;
	public string Category { get; set; } = string.Empty;
	public string? Message { get; set; }
	public DateTime Timestamp
	{
		get;
		set
		{
			field = value;
			_timestampText = null;
		}
	} = DateTime.Now;
	public string TimestampText => _timestampText ??= Timestamp.ToString( "HH:mm:ss" );
}

[Bindable( true )]
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
[Bindable( true )]
public sealed class EntryRow : RowData
{
	public int? Id { get; set; }
	public string? Type { get; set; }
	public long? Size { get; set; }
	public FileStatus Status { get; set; } = FileStatus.Lo;
	public HpUser? Checkout { get; set; }
	public string StatusText => Enum.GetName( Status ) ?? Status.ToString();
	public string CheckoutText => Checkout?.ToString() ?? string.Empty;
	public HpCategory? Category { get; set; }
	public DateTime? LocalDate { get; set; }
	public DateTime? RemoteDate { get; set; }
	private FileInfo? _localFile;
	private bool _localFileChecked;

	public string? FullName
	{
		get;
		set
		{
			field = value;
			_localFile = null;
			_localFileChecked = false;
		}
	}
	public int? LatestId { get; set; }
	public int? LatestReleaseId { get; set; }
	public EntryReprType? ReprType
	{
		get;
		set
		{
			field = value;
			_localFile = null;
			_localFileChecked = false;
		}
	}
	public RemoteEntryDto? Remote { get; set; }

	public FileInfo? LocalFile
	{
		get
		{
			if( _localFileChecked || _localFile is not null )
				return _localFile;
			_localFileChecked = true;

			string? path = ReprType switch
			{
				EntryReprType.Both or EntryReprType.Remote => PwaPaths.ToAbsolute( FullName ),
				EntryReprType.Local => FullName,
				_ => null,
			};

			FileInfo? file = string.IsNullOrEmpty( path ) ? null : new( path );
			_localFile = file?.Exists is true ? file : null;
			return _localFile;
		}
		set
		{
			_localFile = value;
			_localFileChecked = true;
		}
	}
}

/// <summary>Sort and group keys for the entry list, keyed by <see cref="EntryRow"/> property name.</summary>
public static class EntryRowKeys
{
	private static readonly Func<EntryRow, IComparable?> NullKey = static _ => null;

	private static readonly FrozenDictionary<string, Func<EntryRow, IComparable?>> SortKeys = new Dictionary<string, Func<EntryRow, IComparable?>>
	{
		[ nameof( EntryRow.Name ) ] = static e => e.Name ?? "",
		[ nameof( EntryRow.Id ) ] = static e => e.Id ?? 0,
		[ nameof( EntryRow.Checkout ) ] = static e => e.Checkout?.name ?? "",
		[ nameof( EntryRow.Size ) ] = static e => e.Size,
		[ nameof( EntryRow.Type ) ] = static e => e.Type ?? "",
		[ nameof( EntryRow.Status ) ] = static e => Enum.GetName( e.Status ) ?? "",
		[ nameof( EntryRow.LatestId ) ] = static e => e.LatestId,
		[ nameof( EntryRow.RemoteDate ) ] = static e => e.RemoteDate,
		[ nameof( EntryRow.LocalDate ) ] = static e => e.LocalDate,
		[ nameof( EntryRow.Category ) ] = static e => e.Category?.name ?? "",
		[ nameof( EntryRow.FullName ) ] = static e => e.FullName ?? "",
	}.ToFrozenDictionary();

	private static readonly FrozenDictionary<string, Func<EntryRow, IComparable?>> GroupKeys = new Dictionary<string, Func<EntryRow, IComparable?>>( SortKeys )
	{
		[ nameof( EntryRow.RemoteDate ) ] = static e => e.RemoteDate?.Date,
		[ nameof( EntryRow.LocalDate ) ] = static e => e.LocalDate?.Date,
	}.ToFrozenDictionary();

	public static IReadOnlyCollection<string> Names => SortKeys.Keys;

	public static bool TryGetSortKey( string? fieldName, out Func<EntryRow, IComparable?> key )
		=> TryGet( SortKeys, fieldName, out key );

	public static bool TryGetGroupKey( string? fieldName, out Func<EntryRow, IComparable?> key )
		=> TryGet( GroupKeys, fieldName, out key );

	private static bool TryGet(
		FrozenDictionary<string, Func<EntryRow, IComparable?>> keys,
		string? fieldName,
		out Func<EntryRow, IComparable?> key )
	{
		if( fieldName is not null && keys.TryGetValue( fieldName, out var found ) )
		{
			key = found;
			return true;
		}
		key = NullKey;
		return false;
	}
}

public sealed record EntryListResult( List<EntryRow> Rows, int RemoteCount, int LocalOnlyCount, int LocalAndRemoteCount );

[Bindable( true )]
public sealed class HistoryRow : RowData
{
	public int Version { get; set; }
	public HpUser? ModUser { get; set; }
	public DateTime? ModDate { get; set; }
	public long? Size { get; set; }
	public DateTime? RelDate { get; set; }
}

[Bindable( true )]
public sealed class ParentRow : RowData
{
	public int Version { get; set; }
	public string? BasePath { get; set; }
}

[Bindable( true )]
public sealed class ChildrenRow : RowData
{
	public int Version { get; set; }
	public string? BasePath { get; set; }
}

[Bindable( true )]
public sealed class PropertiesRow : RowData
{
	public int Version { get; set; }
	public int? Property { get; set; }
	public string? Configuration { get; set; }
	public PropertyType? Type { get; set; }
	public object? ValueData { get; set; }
}

[Bindable( true )]
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

[Bindable( true )]
public sealed class SearchRow : RowData
{
	public int? Id { get; set; }
	public string? Directory { get; set; }
	public string DisplayText => string.IsNullOrWhiteSpace( Directory )
		? ( Name ?? string.Empty )
		: $"{Name} — {Directory}";
}

[Bindable( true )]
public sealed class SearchPropertiesRow : RowData
{
	public int ID { get; set; }
	public Operators Comparer { get; set; }
	public string? Value { get; set; }
	public bool IsTextOrDate { get; set; }
}

[Bindable( true )]
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

[Bindable( true )]
public sealed class FileTypeRow : RowData
{
	public string? Extension { get; set; }
	public string? Category { get; set; }
	public string? RegEx { get; set; }
	public string? Description { get; set; }
}

[Bindable( true )]
public sealed class FileTypeEntryFilterRow : RowData
{
	public int Id { get; set; }
	public string? Proto { get; set; }
	public string? RegEx { get; set; }
	public string? Description { get; set; }
}
