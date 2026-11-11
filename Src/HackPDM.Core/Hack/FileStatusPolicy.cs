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
	public static bool CanUploadIcon( FileStatus status )
		=> status is FileStatus.Lo or FileStatus.Ft or FileStatus.If;
}
