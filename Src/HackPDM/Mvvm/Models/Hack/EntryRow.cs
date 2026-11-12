using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

using HackPDM.Core;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Mvvm.Models.Hack;

// No UI-framework types in this file: view models use these directly.

/// <summary>
/// One row of the file manager's entry list: a remote entry, a local-only file, or both. Resolves its
/// local file from the vault path.
/// </summary>
public class EntryRow : HackPDM.Domain.Representation.EntryRow
{
	/// <summary>The local file for this row.</summary>
	/// <value>Resolved on first read from <c>FullName</c> (a server path for remote rows, an absolute path for local rows)
	/// and cached; null when the file does not exist on disk.</value>
	public override FileInfo? LocalFile
	{
		get
		{
			if (field is not null) return field;

			string? path = ReprType switch
			{
				EntryReprType.Both or EntryReprType.Remote => PwaPaths.ToAbsolute( FullName ),
				EntryReprType.Local => FullName,
				_ => null,
			};
			
			FileInfo? file = string.IsNullOrEmpty(path) ? null : new(path);
			field = file?.Exists is true ? file : null;
			return field;
		}
		set;
	}
	public override bool? IsLocal
	{
		get
		{
			field = Id switch
			{
				null => null,
				0 => true,
				_ => LocalFile?.Exists ?? false,
			};
			
			return field;
		}
	}
	public override bool? IsRemote
	{
		get
		{
			field = Id switch
			{
				null => null,
				not 0 => true,
				_ => false,
			};
			return field;
		}
	}
	/// <summary>The server's description of this entry when the row was listed; null for a local-only file.
	/// Lets one row be rebuilt when its local file changes, without asking the server again.</summary>
	public HackPDM.Infrastructure.Odoo.RemoteEntryDto? Remote { get; init; }

	// need IsLocal to be hit so that LocalFile is evaluated
	public override bool? IsOnlyLocal => IsRemote is false & IsLocal is true;
	public override bool? IsOnlyRemote => IsRemote is true & IsLocal is false;
}
