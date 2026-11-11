using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

using HackPDM.Infrastructure.SldWrks;
using HackPDM.Infrastructure.SldWrks.Format;

namespace HackPDM.Tests.Core;

/// <summary>Sample files under support_files/ in the repository.</summary>
internal static class SwSamples
{
	public const string KitBotFolder = "KitBot FRC 2026_SW2026";

	public static string? Root { get; } = Find();

	private static string? Find()
	{
		for( var dir = new DirectoryInfo( AppContext.BaseDirectory ); dir is not null; dir = dir.Parent )
		{
			string candidate = Path.Combine( dir.FullName, "support_files" );
			if( Directory.Exists( candidate ) )
				return candidate;
		}
		return null;
	}

	public static string Old( string name ) => Path.Combine( Root ?? throw new DirectoryNotFoundException( "support_files not found" ), name );
	public static string KitBot( string name ) => Path.Combine( Root!, KitBotFolder, name );
	public static bool HasKitBot => Root is not null && Directory.Exists( Path.Combine( Root, KitBotFolder ) );
}

/// <summary>A [Fact] that is skipped unless the (gitignored) SolidWorks 2026 KitBot samples are present.</summary>
public sealed class KitBotFactAttribute : FactAttribute
{
	public KitBotFactAttribute()
	{
		if( !SwSamples.HasKitBot )
			Skip = $"support_files/{SwSamples.KitBotFolder} is not present (it is gitignored)";
	}
}

/// <summary>SolidWorks 2014-and-earlier files (OLE compound files), from the tracked samples.</summary>
public class SwFileReaderOleTests
{
	private readonly SwFileReader _reader = new();

	[Fact]
	public void OldSamples_AreOleContainers()
	{
		foreach( string name in new[] { "old.SLDPRT", "old.SLDASM", "new.SLDPRT", "new.SLDASM" } )
			Assert.Equal( SwStreamStore.ContainerFormat.Ole, SwStreamStore.Open( SwSamples.Old( name ) ).Format );
	}

	[Fact]
	public void ConfigurationNames_ComeFromTheConfigurationHeader()
	{
		var store = SwStreamStore.Open( SwSamples.Old( "old.SLDPRT" ) );
		var names = SwFileReader.ConfigurationNames( store, [ 0, 1, 2 ] );
		Assert.Equal( "X000545", names[ 0 ] );
		Assert.Equal( "Mining Edition", names[ 1 ] );
		Assert.Equal( "X002623", names[ 2 ] );
	}

	[Fact]
	public void FileProperties_AreReadFromTheSummaryPropertySet()
	{
		var props = _reader.GetProperties( SwSamples.Old( "old.SLDPRT" ) )!;
		var file = props.Where( p => p.Item1 == "" ).ToDictionary( p => p.Item2, p => p );

		Assert.Equal( "text", file[ "PartNum" ].Item3 );
		Assert.Equal( "X000545", file[ "PartNum" ].Item4 );
		Assert.Equal( "Engine, Cummins 6cyl - QSB6.7-C FR91637", file[ "Description" ].Item4 );
		Assert.Equal( "3/21/2008", file[ "Date1" ].Item4 );
	}

	[Fact]
	public void ConfigurationProperties_CarryTheConfigurationName()
	{
		var props = _reader.GetProperties( SwSamples.Old( "old.SLDPRT" ) )!;

		Assert.Equal( [ "", "Mining Edition", "X000545", "X002623" ], props.Select( p => p.Item1 ).Distinct().Order() );
		Assert.Contains( props, p => p.Item1 == "X000545" && p.Item2 == "PartNum" && (string)p.Item4 == "X000545" );
	}

	[Fact]
	public void References_ComeFromHeader2_WithoutTheDocumentItself()
	{
		var oldAsm = _reader.GetDependencies( SwSamples.Old( "old.SLDASM" ) )!;
		Assert.Equal( [ [ "new.SLDPRT", @"C:\temp\new.SLDPRT" ] ], oldAsm );

		var newAsm = _reader.GetDependencies( SwSamples.Old( "new.SLDASM" ) )!;
		Assert.Equal( [ "new.SLDPRT", "old.SLDASM" ], newAsm.Select( d => d[ 0 ] ).Order() );
	}

	[Fact]
	public void NonSolidWorksFiles_ReturnNull()
	{
		Assert.Null( _reader.GetDependencies( SwSamples.Old( "asm.delta" ) ) );
		Assert.Null( _reader.GetProperties( SwSamples.Old( "asm.delta" ) ) );
	}

	[Fact]
	public void MissingFile_Throws_UnlessNoInterrupt()
	{
		string missing = SwSamples.Old( "does-not-exist.SLDASM" );
		Assert.ThrowsAny<IOException>( () => _reader.GetDependencies( missing ) );
		Assert.Null( _reader.GetDependencies( missing, noInterrupt: true ) );
	}
}

/// <summary>SolidWorks 2015+ files (chunk container), from the SolidWorks 2026 KitBot samples.</summary>
public class SwFileReaderModernTests
{
	private readonly SwFileReader _reader = new();

	private const string Shaft = "am-4722 am-4722_pin am-4722_mag ToughBox S AM14U Length Hex Output Shaft_Inboard Pin  _1__1.SLDASM";
	private const string TopAssembly = "0_Kitbot2026withBumpers.SLDASM";

	[KitBotFact]
	public void Samples_AreModernContainers()
		=> Assert.Equal( SwStreamStore.ContainerFormat.Modern, SwStreamStore.Open( SwSamples.KitBot( Shaft ) ).Format );

	[KitBotFact]
	public void References_AreTheDirectComponents_WithoutTheDocumentItself()
	{
		var deps = _reader.GetDependencies( SwSamples.KitBot( Shaft ) )!;
		Assert.Equal(
			[ "250x1000_dowel_1.SLDPRT", "am-4722 ToughBox S AM14U Length Hex Output Shaft_1.SLDPRT" ],
			deps.Select( d => d[ 0 ] ).Order() );
		Assert.All( deps, d => Assert.EndsWith( @"\" + d[ 0 ], d[ 1 ] ) );
	}

	[KitBotFact]
	public void RenamedTopAssembly_ListsItsSubassemblies_NotItself()
	{
		var deps = _reader.GetDependencies( SwSamples.KitBot( TopAssembly ) )!;
		Assert.NotEmpty( deps );
		Assert.DoesNotContain( deps, d => d[ 0 ].Equals( "Kitbot2026withBumpers.SLDASM", StringComparison.OrdinalIgnoreCase ) );
		Assert.Contains( deps, d => d[ 0 ] == "Kitbot2026withBumpers(2).SLDASM" );
	}

	[KitBotFact]
	public void FileProperties_UseTheEvaluatedValues()
	{
		var props = _reader.GetProperties( SwSamples.KitBot( TopAssembly ) )!;
		var file = props.Where( p => p.Item1 == "" ).ToDictionary( p => p.Item2, p => p );

		Assert.Equal( [ "Description", "Nomenclature", "Part Number", "Source" ], file.Keys.Order() );
		Assert.Equal( ( "text", (object)"Kitbot 2026" ), ( file[ "Part Number" ].Item3, file[ "Part Number" ].Item4 ) );
	}

	[KitBotFact]
	public void SingleConfiguration_IsNamedAfterTheActiveConfiguration()
	{
		var store = SwStreamStore.Open( SwSamples.KitBot( "KB-26014_ Battery Access Panel_1.SLDPRT" ) );
		Assert.Equal( "Default", SwFileReader.ConfigurationNames( store, [ 0 ] )[ 0 ] );
	}

	[KitBotFact]
	public void EveryAssembly_HasReferences_AndNoneReferencesItself()
	{
		var assemblies = Directory.GetFiles( Path.Combine( SwSamples.Root!, SwSamples.KitBotFolder ), "*.SLDASM" );
		Assert.NotEmpty( assemblies );
		foreach( string file in assemblies )
		{
			var deps = _reader.GetDependencies( file )!;
			Assert.True( deps.Count > 0, $"no references read from {Path.GetFileName( file )}" );
			Assert.DoesNotContain( deps, d => d[ 0 ].Equals( Path.GetFileName( file ), StringComparison.OrdinalIgnoreCase ) );
			_ = _reader.GetProperties( file ); // must not throw
		}
	}
}

/// <summary>The 2015+ chunk container, built by hand.</summary>
public class SwStreamStoreChunkTests
{
	private static byte[] Chunk( string name, byte[] content, bool corrupt = false )
	{
		using var deflated = new MemoryStream();
		using( var deflater = new DeflateStream( deflated, CompressionLevel.Optimal, leaveOpen: true ) )
			deflater.Write( content );
		byte[] payload = deflated.ToArray();

		// names are stored rotated right by 4 (the reader rotates them left by the key in byte 7)
		byte[] encodedName = [ .. Encoding.ASCII.GetBytes( name ).Select( b => (byte)( ( b >> 4 ) | ( b << 4 ) ) ) ];
		var chunk = new byte[ 26 + encodedName.Length + payload.Length ];
		new byte[] { 0x14, 0x00, 0x06, 0x00, 0x08, 0x00 }.CopyTo( chunk, 0 );
		BinaryPrimitives.WriteUInt32LittleEndian( chunk.AsSpan( 6 ), 0x7F667FDF );
		BinaryPrimitives.WriteUInt32LittleEndian( chunk.AsSpan( 10 ), Crc( content ) ^ ( corrupt ? 1u : 0u ) ); // a damaged copy no longer matches its CRC
		BinaryPrimitives.WriteUInt32LittleEndian( chunk.AsSpan( 14 ), (uint)payload.Length );
		BinaryPrimitives.WriteUInt32LittleEndian( chunk.AsSpan( 18 ), (uint)content.Length );
		BinaryPrimitives.WriteUInt32LittleEndian( chunk.AsSpan( 22 ), (uint)encodedName.Length );
		encodedName.CopyTo( chunk, 26 );
		payload.CopyTo( chunk, 26 + encodedName.Length );
		return chunk;
	}

	private static uint Crc( byte[] data )
	{
		uint crc = 0xFFFFFFFF;
		foreach( byte b in data )
		{
			crc ^= b;
			for( int k = 0; k < 8; k++ )
				crc = ( crc & 1 ) != 0 ? 0xEDB88320u ^ ( crc >> 1 ) : crc >> 1;
		}
		return ~crc;
	}

	private static byte[] Container( params byte[][] chunks )
		=> [ 0x12, 0x34, 0x56, 0x78, 0x00, 0x00, 0x00, 0x04, .. chunks.SelectMany( c => c ) ];

	[Fact]
	public void DamagedCopies_AreSkipped_ForTheIntactOne()
	{
		byte[] stale = Encoding.UTF8.GetBytes( "stale copy, damaged" );
		byte[] good = Encoding.UTF8.GetBytes( "<Properties>the real one</Properties>" );
		var store = SwStreamStore.FromBytes( Container(
			Chunk( "docProps/custom.xml", stale, corrupt: true ),
			Chunk( "docProps/custom.xml", good ),
			Chunk( "PreviewPNG", [ 0x89, 0x50, 0x4E, 0x47 ] ) ) );

		Assert.Equal( SwStreamStore.ContainerFormat.Modern, store.Format );
		Assert.Equal( good, store.Get( "docProps/custom.xml" ) );
		Assert.Equal( [ 0x89, 0x50, 0x4E, 0x47 ], store.Get( "PreviewPNG" ) );
		Assert.Null( store.Get( "swXmlContents/COMPINSTANCETREE" ) );
	}

	[Fact]
	public void AStreamWithOnlyDamagedCopies_IsMissing()
	{
		var store = SwStreamStore.FromBytes( Container( Chunk( "Header2", [ 1, 2, 3, 4, 5, 6, 7, 8 ], corrupt: true ) ) );
		Assert.Null( store.Get( "Header2" ) );
	}

	[Fact]
	public void UnknownBytes_AreNotAContainer()
		=> Assert.Equal( SwStreamStore.ContainerFormat.Unknown, SwStreamStore.FromBytes( Encoding.ASCII.GetBytes( "not a solidworks file at all" ) ).Format );
}

/// <summary>Saved preview images, read without SolidWorks.</summary>
public class SwPreviewTests
{
	private static readonly byte[] PngSignature = [ 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A ];

	[Fact]
	public void OldFiles_HaveAPngPreview()
	{
		foreach( string name in new[] { "old.SLDPRT", "old.SLDASM" } )
		{
			byte[]? png = new SwFileReader().GetPreviewPng( SwSamples.Old( name ) );
			Assert.NotNull( png );
			Assert.Equal( PngSignature, png![ ..8 ] );
		}
	}

	[KitBotFact]
	public void ModernFiles_HaveAPngPreview()
	{
		byte[]? png = new SwFileReader().GetPreviewPng( SwSamples.KitBot( "0_Kitbot2026withBumpers.SLDASM" ) );
		Assert.NotNull( png );
		Assert.Equal( PngSignature, png![ ..8 ] );
	}

	[Fact]
	public void NonSolidWorksOrMissingFiles_HaveNoPreview()
	{
		var reader = new SwFileReader();
		Assert.Null( reader.GetPreviewPng( SwSamples.Old( "asm.delta" ) ) );
		Assert.Null( reader.GetPreviewPng( SwSamples.Old( "missing.SLDPRT" ) ) );
	}
}

/// <summary>A committed SolidWorks version carries the file's preview, so every client can show it.</summary>
[Collection( HackPDM.Tests.Characterization.SharedStateCollection.Name )]
public class VersionPreviewStagingTests
{
	[Fact]
	public void PreparedVersion_OfASolidWorksFile_IncludesItsPreview()
	{
		var defaults = (HackPDM.Infrastructure.Odoo.OdooDefaults)HackPDM.Infrastructure.Odoo.OdooDefaults.Instance;
		var lookups = defaults.Lookups;
		try
		{
			defaults.Lookups = lookups with { RestrictTypes = false };
			var entry = new HackPDM.Infrastructure.Odoo.Models.HpEntry { id = 1, windows_complete_name = @"root\Projects" };

			var part = HackPDM.Infrastructure.Operations.CommitStaging.PrepareVersion( new HackPDM.Core.Hack.HackFile( SwSamples.Old( "old.SLDPRT" ) ), entry, 1 )!;
			Assert.Equal( new SwFileReader().GetPreviewPng( SwSamples.Old( "old.SLDPRT" ) ), part.preview_image );

			var other = HackPDM.Infrastructure.Operations.CommitStaging.PrepareVersion( new HackPDM.Core.Hack.HackFile( SwSamples.Old( "asm.signature" ) ), entry, 1 )!;
			Assert.Null( other.preview_image );
		}
		finally
		{
			defaults.Lookups = lookups;
		}
	}
}
