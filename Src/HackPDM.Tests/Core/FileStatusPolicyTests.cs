using HackPDM.Core.Hack;
using HackPDM.Shared.GlobalData;
using Xunit;

namespace HackPDM.Tests.Core;

public class FileStatusPolicyTests
{
	[Theory]
	[InlineData( FileStatus.Lo )]
	[InlineData( FileStatus.Ro )]
	[InlineData( FileStatus.Unknown )]
	public void LocalOnlyEntriesAlwaysOpenLocally( FileStatus status )
	{
		Assert.Equal( EntryOpenTarget.Local, FileStatusPolicy.ForOpen( false, status ) );
		Assert.Equal( EntryOpenTarget.Local, FileStatusPolicy.ForLatestLocal( false, status ) );
		Assert.Equal( EntryOpenTarget.None, FileStatusPolicy.ForLatestRemote( false, status ) );
	}

	[Theory]
	[InlineData( FileStatus.Ro, EntryOpenTarget.Remote )]
	[InlineData( FileStatus.Nv, EntryOpenTarget.Remote )]
	[InlineData( FileStatus.Ok, EntryOpenTarget.Local )]
	[InlineData( FileStatus.Lm, EntryOpenTarget.Local )]
	[InlineData( FileStatus.Cm, EntryOpenTarget.Local )]
	[InlineData( FileStatus.Co, EntryOpenTarget.Local )]
	[InlineData( FileStatus.Dt, EntryOpenTarget.None )]
	[InlineData( FileStatus.Ds, EntryOpenTarget.None )]
	public void OpenPrefersLocalCopy( FileStatus status, EntryOpenTarget expected )
		=> Assert.Equal( expected, FileStatusPolicy.ForOpen( true, status ) );

	[Theory]
	[InlineData( FileStatus.Ro, EntryOpenTarget.None )]
	[InlineData( FileStatus.Nv, EntryOpenTarget.Local )]
	[InlineData( FileStatus.Ok, EntryOpenTarget.Local )]
	[InlineData( FileStatus.Dt, EntryOpenTarget.None )]
	public void LatestLocalNeedsLocalCopy( FileStatus status, EntryOpenTarget expected )
		=> Assert.Equal( expected, FileStatusPolicy.ForLatestLocal( true, status ) );

	[Theory]
	[InlineData( FileStatus.Ro, EntryOpenTarget.Remote )]
	[InlineData( FileStatus.Ok, EntryOpenTarget.Remote )]
	[InlineData( FileStatus.Dt, EntryOpenTarget.None )]
	[InlineData( FileStatus.Lo, EntryOpenTarget.None )]
	public void LatestRemoteNeedsRemoteVersion( FileStatus status, EntryOpenTarget expected )
		=> Assert.Equal( expected, FileStatusPolicy.ForLatestRemote( true, status ) );

	[Theory]
	[InlineData( FileStatus.Lo, true )]
	[InlineData( FileStatus.Ft, true )]
	[InlineData( FileStatus.If, true )]
	[InlineData( FileStatus.Ok, false )]
	public void IconUploadOnlyForUncommittedFiles( FileStatus status, bool expected )
		=> Assert.Equal( expected, FileStatusPolicy.CanUploadIcon( status ) );
}
