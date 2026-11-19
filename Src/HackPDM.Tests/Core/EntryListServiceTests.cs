using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using HackPDM.Tests.Characterization;

using Xunit;

namespace HackPDM.Tests.Core;

[Collection( SharedStateCollection.Name )]
public class EntryListServiceTests
{
	[Fact]
	public async Task LoadAsync_LocalOnlyDirectory_ListsAllFilesAndPrePopulatesLocalFile()
	{
		using var scope = new PwaScope( create: true );
		string sub = Path.Combine( scope.Absolute, "LocalFolder" );
		Directory.CreateDirectory( sub );
		File.WriteAllText( Path.Combine( sub, "part.txt" ), "abc" );
		File.WriteAllText( Path.Combine( sub, ".hidden.txt" ), "hidden" );

		EntryListService service = new();
		EntryListResult result = await service.LoadAsync(
			directoryId: null,
			treePath: @"root\LocalFolder",
			includeDeleted: false,
			filterUnknownTypes: false,
			progress: null,
			CancellationToken.None );

		Assert.Equal( 0, result.RemoteCount );
		Assert.Equal( 2, result.LocalOnlyCount );
		Assert.Equal( 0, result.LocalAndRemoteCount );
		Assert.Equal( [ ".hidden.txt", "part.txt" ], result.Rows.Select( r => r.Name ?? string.Empty ).Order( StringComparer.Ordinal ).ToArray() );
		Assert.All( result.Rows, row =>
		{
			Assert.NotNull( row.LocalFile );
			Assert.True( row.LocalFile!.Exists );
		} );
	}

	[Fact]
	public async Task LoadAsync_WhenCancelled_ThrowsOperationCanceledException()
	{
		using var scope = new PwaScope( create: true );
		using CancellationTokenSource cts = new();
		await cts.CancelAsync();

		EntryListService service = new();
		await Assert.ThrowsAnyAsync<OperationCanceledException>( () =>
			service.LoadAsync( null, @"root", false, false, null, cts.Token ) );
	}
}
