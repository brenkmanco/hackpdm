using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;


namespace HackPDM.Tests.Core;

/// <summary>Sample files under support_files/ in the repository.</summary>
internal static class SwSamples
{
	public const string KitBotFolder = "KitBot FRC 2026_SW2026";

	private static readonly byte[] MinimalPng =
	[
		0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
		0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53, 0xDE,
		0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x63, 0xF8, 0xCF, 0xC0, 0x00, 0x00, 0x03, 0x01, 0x01, 0x00, 0x18, 0xDD, 0x8D, 0xB0,
		0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82,
	];

	public static string? Root { get; } = Find();

	private static string? Find()
	{
		for( var dir = new DirectoryInfo( AppContext.BaseDirectory ); dir is not null; dir = dir.Parent )
		{
			string candidate = Path.Combine( dir.FullName, "support_files" );
			if( Directory.Exists( candidate ) )
			{
				EnsureKitBot( Path.Combine( candidate, KitBotFolder ) );
				return candidate;
			}
		}
		return null;
	}

	public static string Old( string name ) => Path.Combine( Root ?? throw new DirectoryNotFoundException( "support_files not found" ), name );
	public static string KitBot( string name ) => Path.Combine( Root!, KitBotFolder, name );
	public static bool HasKitBot => Root is not null && Directory.Exists( Path.Combine( Root, KitBotFolder ) );

	private static void EnsureKitBot( string dir )
	{
		if( Directory.Exists( dir ) && Directory.GetFiles( dir, "*.SLD*" ).Length >= 9 )
			return;
		Directory.CreateDirectory( dir );

		string[] parts =
		[
			"10-32x0500_shcs_HX-SHCS 0.19-32x1.375x1-C-15_1.SLDPRT",
			"10-32 Nylock Nut 1_90631A411_1.SLDPRT",
			"10-32 Nylock Nut 1_90631A411-16_1.SLDPRT",
			"250x1000_dowel_1.SLDPRT",
			"am-4722 ToughBox S AM14U Length Hex Output Shaft_1.SLDPRT",
			"KB-26014_ Battery Access Panel_1.SLDPRT",
		];
		foreach( string p in parts )
			WriteModernSwFile( dir, p );

		WriteModernSwFile(
			dir,
			"am-1002 and am-1042 10-32 x 0500 and Nylock Nut Assembly_Square Configuration (19) _1__1.SLDASM",
			refs:
			[
				"10-32x0500_shcs_HX-SHCS 0.19-32x1.375x1-C-15_1.SLDPRT",
				"10-32 Nylock Nut 1_90631A411_1.SLDPRT",
				"10-32 Nylock Nut 1_90631A411-16_1.SLDPRT",
			] );

		WriteModernSwFile(
			dir,
			"am-4722 am-4722_pin am-4722_mag ToughBox S AM14U Length Hex Output Shaft_Inboard Pin  _1__1.SLDASM",
			refs:
			[
				"250x1000_dowel_1.SLDPRT",
				"am-4722 ToughBox S AM14U Length Hex Output Shaft_1.SLDPRT",
			] );

		WriteModernSwFile(
			dir,
			"Kitbot2026withBumpers(2).SLDASM",
			refs: [ "KB-26014_ Battery Access Panel_1.SLDPRT" ] );

		const string topCustomXml =
			"<Properties><propertySection name=\"UserDefinedProperties\">" +
			"<property name=\"Description\"><lpstr>Top KitBot Assembly</lpstr></property>" +
			"<property name=\"Nomenclature\"><lpstr>KitBot</lpstr></property>" +
			"<property name=\"Part Number\"><lpstr>$PRPSHEET:\"SW-File Name\"</lpstr><FPVals><lpstr>Kitbot 2026</lpstr></FPVals></property>" +
			"<property name=\"Source\"><lpstr>REV/AndyMark</lpstr></property>" +
			"</propertySection></Properties>";

		WriteModernSwFile(
			dir,
			"0_Kitbot2026withBumpers.SLDASM",
			refs: [ "Kitbot2026withBumpers(2).SLDASM" ],
			selfSavedName: "Kitbot2026withBumpers.SLDASM",
			treeAllFiles:
			[
				"0_Kitbot2026withBumpers.SLDASM",
				"Kitbot2026withBumpers(2).SLDASM",
				"KB-26014_ Battery Access Panel_1.SLDPRT",
			],
			customXml: topCustomXml );
	}

	private static void WriteModernSwFile(
		string dir,
		string fileName,
		string[]? refs = null,
		string? selfSavedName = null,
		string[]? treeAllFiles = null,
		string? customXml = null )
	{
		List<byte[]> chunks =
		[
			SwStreamStoreChunkTests.Chunk( "PreviewPNG", MinimalPng ),
			SwStreamStoreChunkTests.Chunk(
				"docProps/ISolidWorksInformation.xml",
				Encoding.UTF8.GetBytes( "<ISolidWorksInformation><property name=\"SW-Configuration Name\"><lpstr>Default</lpstr></property></ISolidWorksInformation>" ) ),
			SwStreamStoreChunkTests.Chunk(
				"docProps/custom.xml",
				Encoding.UTF8.GetBytes( customXml ?? $"<Properties><propertySection name=\"UserDefinedProperties\"><property name=\"Description\"><lpstr>{fileName}</lpstr></property><property name=\"Part Number\"><lpstr>SW-PN</lpstr><FPVals><lpstr>Kitbot 2026</lpstr></FPVals></property></propertySection></Properties>" ) ),
		];

		if( refs is { Length: > 0 } )
		{
			string savedSelf = selfSavedName ?? fileName;
			string[] allPaths = [ $@"C:\KitBot\{savedSelf}", .. refs.Select( r => $@"C:\KitBot\{r}" ) ];
			byte[] utf16Blob = Encoding.Unicode.GetBytes( string.Join( "\0", allPaths ) + "\0" );
			chunks.Add( SwStreamStoreChunkTests.Chunk( "SwDocContentMgr/SwDocContentMgrInfo", utf16Blob ) );

			string[] treeItems = treeAllFiles ?? [ fileName, .. refs ];
			string treeXml = "<COMPINSTANCETREE>" + string.Join( "", treeItems.Select( f => $"<swFile swPath=\"C:\\KitBot\\{f}\" />" ) ) + "</COMPINSTANCETREE>";
			chunks.Add( SwStreamStoreChunkTests.Chunk( "swXmlContents/COMPINSTANCETREE", Encoding.UTF8.GetBytes( treeXml ) ) );
		}

		File.WriteAllBytes( Path.Combine( dir, fileName ), SwStreamStoreChunkTests.Container( [ .. chunks ] ) );
	}
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
	internal static byte[] Chunk( string name, byte[] content, bool corrupt = false )
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

	internal static byte[] Container( params byte[][] chunks )
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
		var defaults = (HackPDM.Core.Odoo.OdooDefaults)HackPDM.Core.Odoo.OdooDefaults.Instance;
		var lookups = defaults.Lookups;
		try
		{
			defaults.Lookups = lookups with { RestrictTypes = false };
			var entry = new HackPDM.Core.Odoo.Models.HpEntry { id = 1, windows_complete_name = @"root\Projects" };

			var part = HackPDM.Core.Pdm.CommitStaging.PrepareVersion( new HackPDM.Core.Pdm.HackFile( SwSamples.Old( "old.SLDPRT" ) ), entry, 1 )!;
			Assert.Equal( new SwFileReader().GetPreviewPng( SwSamples.Old( "old.SLDPRT" ) ), part.preview_image );

			var other = HackPDM.Core.Pdm.CommitStaging.PrepareVersion( new HackPDM.Core.Pdm.HackFile( SwSamples.Old( "asm.signature" ) ), entry, 1 )!;
			Assert.Null( other.preview_image );
		}
		finally
		{
			defaults.Lookups = lookups;
		}
	}
}

/// <summary>Hardening tests for OLE bounds/cycles, Unicode paths, MFC CString overflow, COM SAFEARRAY helpers, and DocMgr reset.</summary>
[Collection( HackPDM.Tests.Characterization.SharedStateCollection.Name )]
public sealed class SwHardeningTests : IDisposable
{
	private readonly string _dir = Path.Combine( Path.GetTempPath(), $"hackpdm-sw-hard-{Guid.NewGuid():N}" );

	public SwHardeningTests() => Directory.CreateDirectory( _dir );

	public void Dispose()
	{
		try { Directory.Delete( _dir, recursive: true ); } catch { }
	}

	[Fact]
	public void SwStreamStore_TruncatedOleHeader_ThrowsInvalidDataException_AndNoInterruptReturnsNull()
	{
		byte[] truncatedOle = [ 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0x00, 0x00 ];
		Assert.Throws<InvalidDataException>( () => SwStreamStore.FromBytes( truncatedOle ) );

		string path = Path.Combine( _dir, "short.SLDPRT" );
		File.WriteAllBytes( path, truncatedOle );
		SwFileReader reader = new();
		Assert.Throws<InvalidDataException>( () => reader.GetDependencies( path, noInterrupt: false ) );
		Assert.Null( reader.GetDependencies( path, noInterrupt: true ) );
	}

	[Fact]
	public void SwStreamStore_CyclicDifat_ThrowsInvalidDataExceptionWithoutOom()
	{
		byte[] data = new byte[ 1024 ];
		byte[] sig = [ 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 ];
		sig.CopyTo( data, 0 );
		BinaryPrimitives.WriteUInt16LittleEndian( data.AsSpan( 0x1E ), 9 ); // 512B sectors
		BinaryPrimitives.WriteUInt16LittleEndian( data.AsSpan( 0x20 ), 6 ); // 64B mini-sectors
		BinaryPrimitives.WriteUInt32LittleEndian( data.AsSpan( 0x2C ), 200_000 ); // fatSectors
		BinaryPrimitives.WriteUInt32LittleEndian( data.AsSpan( 0x44 ), 0 ); // difatStart = sector 0
		BinaryPrimitives.WriteUInt32LittleEndian( data.AsSpan( 0x48 ), 1_000_000 ); // huge difatSectors
		for( int i = 0; i < 109; i++ )
			BinaryPrimitives.WriteUInt32LittleEndian( data.AsSpan( 0x4C + i * 4 ), 0xFFFFFFFF );
		// Sector 0 (offset 512): self-looping next DIFAT sector at last uint (508)
		BinaryPrimitives.WriteUInt32LittleEndian( data.AsSpan( 512 + 508 ), 0 );

		Assert.Throws<InvalidDataException>( () => SwStreamStore.FromBytes( data ) );
	}

	[Fact]
	public void SwStreamStore_OversizedStreamAndMiniSectorOverflow_ReturnNullWithoutThrowing()
	{
		// 4 sectors: header + FAT(0) + Dir(1) + MiniStream(2)
		byte[] data = new byte[ 512 * 4 ];
		new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }.CopyTo( data, 0 );
		BinaryPrimitives.WriteUInt16LittleEndian( data.AsSpan( 0x1E ), 9 );
		BinaryPrimitives.WriteUInt16LittleEndian( data.AsSpan( 0x20 ), 6 );
		BinaryPrimitives.WriteUInt32LittleEndian( data.AsSpan( 0x2C ), 1 ); // fatSectors
		BinaryPrimitives.WriteUInt32LittleEndian( data.AsSpan( 0x30 ), 1 ); // directoryStart = sector 1
		BinaryPrimitives.WriteUInt32LittleEndian( data.AsSpan( 0x38 ), 4096 ); // miniCutoff
		BinaryPrimitives.WriteUInt32LittleEndian( data.AsSpan( 0x3C ), 0xFFFFFFFE ); // miniFatStart
		BinaryPrimitives.WriteUInt32LittleEndian( data.AsSpan( 0x44 ), 0xFFFFFFFE ); // difatStart
		BinaryPrimitives.WriteUInt32LittleEndian( data.AsSpan( 0x4C ), 0 ); // fatSectorIds[0] = 0
		for( int i = 1; i < 109; i++ )
			BinaryPrimitives.WriteUInt32LittleEndian( data.AsSpan( 0x4C + i * 4 ), 0xFFFFFFFF );

		// FAT (sector 0 at 512)
		BinaryPrimitives.WriteUInt32LittleEndian( data.AsSpan( 512 ), 0xFFFFFFFD );
		BinaryPrimitives.WriteUInt32LittleEndian( data.AsSpan( 516 ), 0xFFFFFFFE );
		BinaryPrimitives.WriteUInt32LittleEndian( data.AsSpan( 520 ), 0xFFFFFFFE );

		static void WriteDirEntry( Span<byte> entry, string name, byte type, uint left, uint right, uint child, uint start, uint size )
		{
			byte[] utf16 = Encoding.Unicode.GetBytes( name + "\0" );
			utf16.CopyTo( entry );
			BinaryPrimitives.WriteUInt16LittleEndian( entry[ 0x40.. ], (ushort)utf16.Length );
			entry[ 0x42 ] = type;
			BinaryPrimitives.WriteUInt32LittleEndian( entry[ 0x44.. ], left );
			BinaryPrimitives.WriteUInt32LittleEndian( entry[ 0x48.. ], right );
			BinaryPrimitives.WriteUInt32LittleEndian( entry[ 0x4C.. ], child );
			BinaryPrimitives.WriteUInt32LittleEndian( entry[ 0x74.. ], start );
			BinaryPrimitives.WriteUInt32LittleEndian( entry[ 0x78.. ], size );
		}

		// Dir (sector 1 at 1024): Root Entry claims >= 2 GiB size; HugeStream claims >= 2 GiB; BadMini uses sector 0x04000000
		WriteDirEntry( data.AsSpan( 1024, 128 ), "Root Entry", 5, 0xFFFFFFFF, 0xFFFFFFFF, 1, 2, 0x80000040 );
		WriteDirEntry( data.AsSpan( 1152, 128 ), "HugeStream", 2, 0xFFFFFFFF, 2, 0xFFFFFFFF, 2, 0x80001000 );
		WriteDirEntry( data.AsSpan( 1280, 128 ), "BadMini", 2, 0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF, 0x04000000, 16 );

		var store = SwStreamStore.FromBytes( data );
		Assert.Equal( SwStreamStore.ContainerFormat.Ole, store.Format );
		Assert.Null( store.Get( "HugeStream" ) );
		Assert.Null( store.Get( "BadMini" ) );
	}

	[Fact]
	public void SwStreamStore_Open_ReadsFileHeldWithReadWriteShare()
	{
		string path = Path.Combine( _dir, "locked.SLDPRT" );
		byte[] bytes = SwStreamStoreChunkTests.Container( SwStreamStoreChunkTests.Chunk( "docProps/custom.xml", "<Properties />"u8.ToArray() ) );
		File.WriteAllBytes( path, bytes );

		using FileStream cadLock = new( path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete );
		var store = SwStreamStore.Open( path );
		Assert.Equal( SwStreamStore.ContainerFormat.Modern, store.Format );
		Assert.Equal( "<Properties />", store.GetText( "docProps/custom.xml" ) );
	}

	[Fact]
	public void SwFileReader_PreservesNonLatin1UnicodePaths()
	{
		const string unicodeRef = @"C:\Projects\Robot – 2026\Śruba_łódź_部品_деталь.SLDPRT";
		byte[] contentMgrBytes = Encoding.Unicode.GetBytes( $@"C:\Projects\Top.SLDASM\0{unicodeRef}\0".Replace( @"\0", "\0" ) );
		var store = SwStreamStore.FromBytes( SwStreamStoreChunkTests.Container(
			SwStreamStoreChunkTests.Chunk( "SwDocContentMgr/SwDocContentMgrInfo", contentMgrBytes ) ) );

		var refs = SwFileReader.GetReferences( store, "Top.SLDASM" );
		Assert.Equal( [ unicodeRef ], refs );

		// Also verify Header2 path extraction
		byte[] header2Bytes = [ 0x01, 0x00, 0x00, 0x00, .. Encoding.Unicode.GetBytes( unicodeRef + "\0" ) ];
		var oleLike = SwStreamStore.FromBytes( SwStreamStoreChunkTests.Container(
			SwStreamStoreChunkTests.Chunk( "Header2", header2Bytes ) ) );
		Assert.Equal( [ unicodeRef ], SwFileReader.GetReferences( oleLike, "Top.SLDASM" ) );
	}

	[Fact]
	public void ConfigHeaderReader_CStringOverflow_DoesNotThrowArgumentOutOfRange()
	{
		using var ms = new MemoryStream();
		using var bw = new BinaryWriter( ms );
		bw.Write( (ushort)0xFFFF ); // tag
		bw.Write( (ushort)1 );      // schema
		bw.Write( (ushort)0 );      // class name length
		bw.Write( (ushort)1 );      // config count = 1
		bw.Write( (ushort)0xFFFF ); // dmConfigHeader_c tag
		bw.Write( (ushort)1 );
		bw.Write( (ushort)0 );
		bw.Write( 0u );             // u32
		// CString Unicode marker + DWORD length 0x40000001 (0x40000001 * 2 overflows 32-bit int!)
		bw.Write( (byte)0xFF );
		bw.Write( (ushort)0xFFFE );
		bw.Write( (byte)0xFF );
		bw.Write( (ushort)0xFFFF );
		bw.Write( 0x40000001u );

		var store = SwStreamStore.FromBytes( SwStreamStoreChunkTests.Container(
			SwStreamStoreChunkTests.Chunk( "_MO_VERSION_13000/stub", [ 1 ] ),
			SwStreamStoreChunkTests.Chunk( "Contents/CMgrHdr2", ms.ToArray() ) ) );

		var names = ConfigHeaderReader.ReadNames( store );
		Assert.Empty( names );
	}

	[Fact]
	public void SwDocMgr_ComArrayHelpers_HandleVariantArraysAndEmptyRefs()
	{
		Assert.Empty( SwDocMgr.ExtractDependencies( null, null ) );

		object[] deps = [ @"C:\vault\part1.SLDPRT", @"C:\vault\Virt^asm.SLDPRT", @"D:\lib\bolt.SLDPRT" ];
		short[] isVirtual = [ 0, -1, 0 ];
		var parsed = SwDocMgr.ExtractDependencies( deps, isVirtual );

		Assert.Equal( 2, parsed.Count );
		Assert.Equal( [ "part1.SLDPRT", @"C:\vault\part1.SLDPRT" ], parsed[ 0 ] );
		Assert.Equal( [ "bolt.SLDPRT", @"D:\lib\bolt.SLDPRT" ], parsed[ 1 ] );

		object?[] rawProps = [ "PartNo", null, "", "Material" ];
		Assert.Equal( [ "PartNo", "Material" ], SwDocMgr.AsStrings( rawProps ).ToArray() );
	}

	[Fact]
	public void SolidWorksUtil_DocMgr_SupportsExplicitOverrideAndLazyReset()
	{
		var prevDocMgr = SolidWorksUtil.DocMgr;
		var prevLookups = OdooDefaults.Instance.Lookups;
		try
		{
			SolidWorksUtil.Reset();
			OdooDefaults.Instance.Lookups = prevLookups with { SwApi = "" };
			ISwDocumentReader initial = SolidWorksUtil.DocMgr;
			Assert.IsType<SwFileReader>( initial );

			// Explicit test override must not be overwritten when SwApi changes
			SwFileReader explicitReader = new();
			SolidWorksUtil.DocMgr = explicitReader;
			OdooDefaults.Instance.Lookups = prevLookups with { SwApi = "DUMMY-INVALID-KEY" };
			Assert.Same( explicitReader, SolidWorksUtil.DocMgr );

			// Reset clears explicit override and re-evaluates cleanly without throwing
			SolidWorksUtil.Reset();
			Assert.NotNull( SolidWorksUtil.DocMgr );
		}
		finally
		{
			OdooDefaults.Instance.Lookups = prevLookups;
			SolidWorksUtil.DocMgr = prevDocMgr;
			SolidWorksUtil.Reset();
		}
	}
}
