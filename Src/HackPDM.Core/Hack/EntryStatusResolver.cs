using System;

using HackPDM.Shared.GlobalData;

namespace HackPDM.Core.Hack;

/// <summary>
/// Decides the <see cref="FileStatus"/> shown for an entry by comparing the remote record
/// with the local file. Pure: no IO, no Odoo calls.
/// </summary>
public static class EntryStatusResolver
{
	/// <param name="deleted">Odoo's "deleted" flag; null when the field wasn't returned.</param>
	/// <param name="checkoutUserId">User the entry is checked out to, or null.</param>
	/// <param name="myUserId">The current Odoo user.</param>
	/// <param name="latestChecksum">Checksum of the latest remote version; null when the entry has no versions.</param>
	/// <param name="hasLatestVersionField">False when Odoo returned something other than a checksum (e.g. false for "no versions").</param>
	/// <param name="localChecksum">Checksum of the local file, or null when there is no local copy.</param>
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

	/// <summary>Status of a file that only exists locally.</summary>
	/// <param name="restrictTypes">Server setting: only known file types may be committed.</param>
	/// <param name="filtered">The user's "filter unknown types" toggle.</param>
	/// <param name="knownType">The extension is a registered hp.type.</param>
	/// <param name="ignored">The extension matches an ignore filter.</param>
	public static FileStatus ResolveLocal( bool restrictTypes, bool filtered, bool knownType, bool ignored )
	{
		if( restrictTypes && ignored )
			return FileStatus.If;
		if( ( restrictTypes || filtered ) && !knownType )
			return FileStatus.Ft;
		return FileStatus.Lo;
	}
}
