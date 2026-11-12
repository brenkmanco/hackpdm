using System;

using HackPDM.Shared.GlobalData;

namespace HackPDM.Core.Hack;

/// <summary>
/// Decides the <see cref="FileStatus"/> shown for an entry by comparing the remote record
/// with the local file. Pure: no IO, no Odoo calls.
/// </summary>
public static class EntryStatusResolver
{
	/// <summary>Decides the status of an entry that exists on the server.</summary>
	/// <param name="deleted">Odoo's "deleted" flag; null when the field wasn't returned (treated as deleted).</param>
	/// <param name="checkoutUserId">User the entry is checked out to, or null / 0.</param>
	/// <param name="myUserId">The current Odoo user.</param>
	/// <param name="latestChecksum">
	/// The latest version's checksum as Odoo returned it: a string, or a bool (Odoo's false) when the entry has no versions.
	/// </param>
	/// <param name="localChecksum">Checksum of the local file, or null when there is no local copy.</param>
	/// <param name="remoteDate">The latest remote version's modified date.</param>
	/// <param name="localModified">The local file's modified date.</param>
	/// <returns>
	/// In order of precedence: <see cref="FileStatus.Dt"/> unless <paramref name="deleted"/> is false;
	/// <see cref="FileStatus.Cm"/> / <see cref="FileStatus.Co"/> when checked out to this / another user;
	/// <see cref="FileStatus.NoVersions"/> for a bool checksum; <see cref="FileStatus.Ro"/> without a local copy;
	/// <see cref="FileStatus.Ok"/> when the checksums match; otherwise <see cref="FileStatus.Nv"/> when the remote date is
	/// later, else <see cref="FileStatus.Lm"/>; <see cref="FileStatus.Unknown"/> for any other checksum value (including null).
	/// </returns>
	public static FileStatus ResolveRemote(
		bool? deleted,
		int? checkoutUserId,
		int myUserId,
		object? latestChecksum,
		string? localChecksum,
		DateTime? remoteDate,
		DateTime? localModified )
	{
		if( deleted is not false )
			return FileStatus.Dt;

		if( checkoutUserId is not ( null or 0 ) )
			return checkoutUserId == myUserId ? FileStatus.Cm : FileStatus.Co;

		return latestChecksum switch
		{
			bool => FileStatus.NoVersions,
			string latest when localChecksum is null => FileStatus.Ro,
			string latest when localChecksum == latest => FileStatus.Ok,
			string => remoteDate > localModified ? FileStatus.Nv : FileStatus.Lm,
			_ => FileStatus.Unknown,
		};
	}

	/// <summary>Decides the status of a file that only exists locally.</summary>
	/// <param name="restrictTypes">Server setting: only known file types may be committed.</param>
	/// <param name="filtered">The user's "filter unknown types" toggle.</param>
	/// <param name="knownType">The extension is a registered hp.type.</param>
	/// <param name="ignored">The extension matches an ignore filter.</param>
	/// <returns>
	/// <see cref="FileStatus.If"/> when types are restricted and the name is ignored; <see cref="FileStatus.Ft"/> when
	/// types are restricted or filtered and the type is unknown; otherwise <see cref="FileStatus.Lo"/>.
	/// </returns>
	public static FileStatus ResolveLocal( bool restrictTypes, bool filtered, bool knownType, bool ignored )
	{
		if( restrictTypes && ignored )
			return FileStatus.If;
		if( ( restrictTypes || filtered ) && !knownType )
			return FileStatus.Ft;
		return FileStatus.Lo;
	}
}
