using System;
using System.Collections.Generic;
using System.Linq;


using Xunit;

namespace HackPDM.Tests.Core;

public sealed class ProfileStatusSessionTests
{
	[Fact]
	public void LogHelpers_RecordCategoryStatusAndFireEvents()
	{
		LoginSession session = new();
		List<BasicStatusMessage> observed = [];
		bool cleared = false;
		session.MessageLogged += observed.Add;
		session.MessagesCleared += () => cleared = true;

		session.LogInfo( ProfileStatusCategory.Configuration, "Saved Odoo endpoint" );
		session.LogError( ProfileStatusCategory.Connection, "TCP port 8069 refused connection" );
		session.LogWarning( ProfileStatusCategory.Settings, "Restart required for theme style" );
		session.LogSuccess( ProfileStatusCategory.Login, "Signed in as admin (uid 2)" );

		Assert.Equal( 4, observed.Count );
		Assert.Equal( 4, session.LastErrors.Count );
		Assert.Equal( [ ProfileStatusCategory.Configuration, ProfileStatusCategory.Connection, ProfileStatusCategory.Settings, ProfileStatusCategory.Login ],
			session.LastErrors.Select( m => m.Category ).ToArray() );
		Assert.Equal( [ StatusMessage.INFO, StatusMessage.ERROR, StatusMessage.WARNING, StatusMessage.SUCCESS ],
			session.LastErrors.Select( m => m.Status ).ToArray() );
		Assert.All( session.LastErrors, m => Assert.False( string.IsNullOrEmpty( m.TimestampText ) ) );

		session.ClearMessages();
		Assert.True( cleared );
		Assert.Empty( session.LastErrors );
	}

	[Fact]
	public void AddStatus_BoundsStoredHistoryToPreventUnboundedGrowth()
	{
		LoginSession session = new();
		for( int i = 0; i < 230; i++ )
			session.LogInfo( ProfileStatusCategory.Connection, $"Check {i}" );

		Assert.Equal( 200, session.LastErrors.Count );
		Assert.Equal( "Check 30", session.LastErrors[ 0 ].Message );
		Assert.Equal( "Check 229", session.LastErrors[ ^1 ].Message );
	}
}
