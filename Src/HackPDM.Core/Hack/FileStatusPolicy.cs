using HackPDM.Shared.GlobalData;

namespace HackPDM.Core.Hack;

/// <summary>Where an entry should be opened from.</summary>
public enum EntryOpenTarget
{
	/// <summary>The entry can't be opened this way.</summary>
	None,
	/// <summary>Open the file on local disk.</summary>
	Local,
	/// <summary>Download the latest remote version to a temp folder and open that.</summary>
	Remote,
}

/// <summary>
/// Pure rules that decide how an entry may be opened, based on its <see cref="FileStatus"/>.
/// Kept free of UI and IO so it can be unit tested.
/// </summary>
public static class FileStatusPolicy
{
	/// <summary>"Open": prefer the local copy, fall back to the remote one when there is no local copy.</summary>
	/// <param name="hasRemoteId">Whether the entry exists on the server.</param>
	/// <param name="status">The entry's status.</param>
	/// <returns>
	/// <see cref="EntryOpenTarget.Local"/> for local-only entries and statuses with a local copy;
	/// <see cref="EntryOpenTarget.Remote"/> for <see cref="FileStatus.Ro"/> and <see cref="FileStatus.Nv"/>;
	/// otherwise <see cref="EntryOpenTarget.None"/>.
	/// </returns>
	public static EntryOpenTarget ForOpen( bool hasRemoteId, FileStatus status )
	{
		if( !hasRemoteId )
			return EntryOpenTarget.Local;

		return status switch
		{
			FileStatus.Ro or FileStatus.Nv => EntryOpenTarget.Remote,
			FileStatus.Lm or FileStatus.Ok or FileStatus.Co or FileStatus.Ft or FileStatus.If or FileStatus.Cm => EntryOpenTarget.Local,
			_ => EntryOpenTarget.None,
		};
	}

	/// <summary>"Preview latest remote": only entries that exist remotely.</summary>
	/// <param name="hasRemoteId">Whether the entry exists on the server.</param>
	/// <param name="status">The entry's status.</param>
	/// <returns>
	/// <see cref="EntryOpenTarget.Remote"/> for a remote entry with a version (not deleted, destroyed or without versions);
	/// otherwise <see cref="EntryOpenTarget.None"/>.
	/// </returns>
	public static EntryOpenTarget ForLatestRemote( bool hasRemoteId, FileStatus status )
	{
		if( !hasRemoteId )
			return EntryOpenTarget.None;

		return status switch
		{
			FileStatus.Ro or FileStatus.Nv or FileStatus.Lm or FileStatus.Ok
				or FileStatus.Co or FileStatus.Ft or FileStatus.If or FileStatus.Cm => EntryOpenTarget.Remote,
			_ => EntryOpenTarget.None,
		};
	}

	/// <summary>"Latest local": only entries that have a local copy.</summary>
	/// <param name="hasRemoteId">Whether the entry exists on the server.</param>
	/// <param name="status">The entry's status.</param>
	/// <returns>
	/// <see cref="EntryOpenTarget.Local"/> for local-only entries and statuses with a local copy (not
	/// <see cref="FileStatus.Ro"/>); otherwise <see cref="EntryOpenTarget.None"/>.
	/// </returns>
	public static EntryOpenTarget ForLatestLocal( bool hasRemoteId, FileStatus status )
	{
		if( !hasRemoteId )
			return EntryOpenTarget.Local;

		return status switch
		{
			FileStatus.Nv or FileStatus.Lm or FileStatus.Ok or FileStatus.Co
				or FileStatus.Ft or FileStatus.If or FileStatus.Cm => EntryOpenTarget.Local,
			_ => EntryOpenTarget.None,
		};
	}

	/// <summary>Only entries that were never committed can have their icon uploaded.</summary>
	/// <param name="status">The entry's status.</param>
	/// <returns><see langword="true"/> for <see cref="FileStatus.Lo"/>, <see cref="FileStatus.Ft"/> and <see cref="FileStatus.If"/>.</returns>
	public static bool CanUploadIcon( FileStatus status )
		=> status is FileStatus.Lo or FileStatus.Ft or FileStatus.If;
}
