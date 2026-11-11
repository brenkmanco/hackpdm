using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace HackPDM.Infrastructure.SldWrks.Format;

/// <summary>
/// The named streams inside a SolidWorks file, read without SolidWorks or its Document Manager.
/// Handles both container formats:
/// <list type="bullet">
/// <item>SolidWorks 2014 and earlier: an OLE compound file (MS-CFB); streams named "…__ZLB" are
/// zlib-compressed after a 24-byte header and are exposed without the suffix.</item>
/// <item>SolidWorks 2015 and later: a sequence of chunks, each starting with 14 00 06 00 08 00,
/// with a bit-rotated stream name and a raw-DEFLATE payload checked by CRC-32
/// (layout as documented by the MIT-licensed openswx project).</item>
/// </list>
/// Streams are decompressed on first use.
/// </summary>
public sealed class SwStreamStore
{
	public enum ContainerFormat { Unknown, Ole, Modern }

	private readonly byte[] _data;
	// candidate locations per stream name, in file order (the modern format can hold stale copies)
	private readonly Dictionary<string, List<Func<byte[]?>>> _sources = new( StringComparer.Ordinal );
	private readonly Dictionary<string, byte[]?> _decoded = new( StringComparer.Ordinal );

	public ContainerFormat Format { get; }

	private SwStreamStore( byte[] data )
	{
		_data = data;
		Format = DetectFormat( data );
		switch( Format )
		{
			case ContainerFormat.Ole: IndexOle(); break;
			case ContainerFormat.Modern: IndexModern(); break;
		}
	}

	public static SwStreamStore Open( string path ) => new( File.ReadAllBytes( path ) );
	public static SwStreamStore FromBytes( byte[] data ) => new( data );

	public IEnumerable<string> Names => _sources.Keys;

	public bool Contains( string name ) => Get( name ) is not null;

	/// <summary>The decompressed stream, or null when it is missing or no copy of it is intact.</summary>
	public byte[]? Get( string name )
	{
		if( _decoded.TryGetValue( name, out var cached ) )
			return cached;
		byte[]? result = null;
		if( _sources.TryGetValue( name, out var candidates ) )
		{
			foreach( var candidate in candidates )
			{
				try
				{
					if( ( result = candidate() ) is not null )
						break;
				}
				catch( InvalidDataException ) { }
				catch( ArgumentOutOfRangeException ) { }
			}
		}
		_decoded[ name ] = result;
		return result;
	}

	public string? GetText( string name ) => Get( name ) is { } bytes ? Encoding.UTF8.GetString( bytes ) : null;

	public static ContainerFormat DetectFormat( ReadOnlySpan<byte> data )
	{
		if( data.Length >= 8 && data[ ..8 ].SequenceEqual( OleSignature ) )
			return ContainerFormat.Ole;
		// the first chunk marker sits within the first few dozen bytes
		if( data[ ..Math.Min( data.Length, 64 ) ].IndexOf( ChunkMarker ) >= 0 )
			return ContainerFormat.Modern;
		return ContainerFormat.Unknown;
	}

	private void Add( string name, Func<byte[]?> source )
	{
		if( !_sources.TryGetValue( name, out var list ) )
			_sources[ name ] = list = [];
		list.Add( source );
	}

	#region Modern container (2015+)

	private static ReadOnlySpan<byte> ChunkMarker => [ 0x14, 0x00, 0x06, 0x00, 0x08, 0x00 ];
	private const int ChunkHeaderSize = 26;
	private const int MaxNameSize = 512;

	private void IndexModern()
	{
		// stream names are bit-rotated left by the key stored in byte 7 (4 in every file seen so far)
		int key = _data[ 7 ] & 7;
		var data = _data.AsSpan();
		int pos = 0;
		while( pos < data.Length )
		{
			int found = data[ pos.. ].IndexOf( ChunkMarker );
			if( found < 0 )
				break;
			int marker = pos + found;
			pos = marker + ChunkMarker.Length;
			if( marker + ChunkHeaderSize > data.Length )
				break;

			uint crc = BinaryPrimitives.ReadUInt32LittleEndian( data[ ( marker + 10 ).. ] );
			uint compressed = BinaryPrimitives.ReadUInt32LittleEndian( data[ ( marker + 14 ).. ] );
			uint uncompressed = BinaryPrimitives.ReadUInt32LittleEndian( data[ ( marker + 18 ).. ] );
			uint nameSize = BinaryPrimitives.ReadUInt32LittleEndian( data[ ( marker + 22 ).. ] );
			if( nameSize is 0 or > MaxNameSize )
				continue;
			long nameStart = marker + ChunkHeaderSize;
			long payloadStart = nameStart + nameSize;
			if( payloadStart + compressed > data.Length )
				continue;
			if( DecodeName( data.Slice( (int)nameStart, (int)nameSize ), key ) is not { } name )
				continue;

			int start = (int)payloadStart, length = (int)compressed;
			Add( name, () => InflateChecked( start, length, uncompressed, crc ) );
		}
	}

	private static string? DecodeName( ReadOnlySpan<byte> encoded, int key )
	{
		var chars = new char[ encoded.Length ];
		for( int i = 0; i < encoded.Length; i++ )
		{
			byte b = (byte)( ( encoded[ i ] << key ) | ( encoded[ i ] >> ( 8 - key ) ) );
			if( b is < 0x20 or >= 0x7F )
				return null;
			chars[ i ] = (char)b;
		}
		return new string( chars );
	}

	/// <summary>Inflates a payload, accepting it only if its size and CRC-32 match the chunk header.</summary>
	private byte[]? InflateChecked( int start, int length, uint expectedSize, uint expectedCrc )
	{
		if( expectedSize > 512 * 1024 * 1024 )
			return null;
		using var input = new MemoryStream( _data, start, length, writable: false );
		using var inflater = new DeflateStream( input, CompressionMode.Decompress );
		var output = new byte[ expectedSize ];
		int read = 0;
		while( read < output.Length )
		{
			int n = inflater.Read( output, read, output.Length - read );
			if( n == 0 )
				return null;
			read += n;
		}
		if( inflater.ReadByte() >= 0 )
			return null; // longer than the header says
		return Crc32.Compute( output ) == expectedCrc ? output : null;
	}

	#endregion

	#region OLE compound file (2014 and earlier)

	private static ReadOnlySpan<byte> OleSignature => [ 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 ];
	private const uint EndOfChain = 0xFFFFFFFE, FreeSector = 0xFFFFFFFF, NoStream = 0xFFFFFFFF;

	private int _sectorSize, _miniSectorSize;
	private uint _miniCutoff;
	private uint[] _fat = [], _miniFat = [];
	private byte[] _miniStream = [];

	private void IndexOle()
	{
		var header = _data.AsSpan( 0, 512 );
		_sectorSize = 1 << BinaryPrimitives.ReadUInt16LittleEndian( header[ 0x1E.. ] );
		_miniSectorSize = 1 << BinaryPrimitives.ReadUInt16LittleEndian( header[ 0x20.. ] );
		uint fatSectors = BinaryPrimitives.ReadUInt32LittleEndian( header[ 0x2C.. ] );
		uint directoryStart = BinaryPrimitives.ReadUInt32LittleEndian( header[ 0x30.. ] );
		_miniCutoff = BinaryPrimitives.ReadUInt32LittleEndian( header[ 0x38.. ] );
		uint miniFatStart = BinaryPrimitives.ReadUInt32LittleEndian( header[ 0x3C.. ] );
		uint miniFatSectors = BinaryPrimitives.ReadUInt32LittleEndian( header[ 0x40.. ] );
		uint difatStart = BinaryPrimitives.ReadUInt32LittleEndian( header[ 0x44.. ] );
		uint difatSectors = BinaryPrimitives.ReadUInt32LittleEndian( header[ 0x48.. ] );

		// sector allocation table: the first 109 FAT sector numbers are in the header, the rest in DIFAT sectors
		var fatSectorIds = new List<uint>();
		for( int i = 0; i < 109; i++ )
			fatSectorIds.Add( BinaryPrimitives.ReadUInt32LittleEndian( header[ ( 0x4C + i * 4 ).. ] ) );
		uint difat = difatStart;
		for( uint n = 0; n < difatSectors && difat < EndOfChain; n++ )
		{
			var sector = Sector( difat );
			int perSector = _sectorSize / 4;
			for( int i = 0; i < perSector - 1; i++ )
				fatSectorIds.Add( BinaryPrimitives.ReadUInt32LittleEndian( sector[ ( i * 4 ).. ] ) );
			difat = BinaryPrimitives.ReadUInt32LittleEndian( sector[ ( ( perSector - 1 ) * 4 ).. ] );
		}
		var fat = new List<uint>();
		foreach( uint id in fatSectorIds.Take( (int)fatSectors ) )
		{
			var sector = Sector( id );
			for( int i = 0; i + 4 <= sector.Length; i += 4 )
				fat.Add( BinaryPrimitives.ReadUInt32LittleEndian( sector[ i.. ] ) );
		}
		_fat = [ .. fat ];

		if( miniFatSectors > 0 )
		{
			var raw = Chain( miniFatStart );
			_miniFat = new uint[ raw.Length / 4 ];
			for( int i = 0; i < _miniFat.Length; i++ )
				_miniFat[ i ] = BinaryPrimitives.ReadUInt32LittleEndian( raw.AsSpan( i * 4 ) );
		}

		var directory = Chain( directoryStart );
		var entries = new List<DirEntry>();
		for( int off = 0; off + 128 <= directory.Length; off += 128 )
		{
			var e = directory.AsSpan( off, 128 );
			int nameLength = Math.Max( BinaryPrimitives.ReadUInt16LittleEndian( e[ 0x40.. ] ) - 2, 0 );
			entries.Add( new DirEntry(
				Encoding.Unicode.GetString( e[ ..Math.Min( nameLength, 64 ) ] ),
				e[ 0x42 ],
				BinaryPrimitives.ReadUInt32LittleEndian( e[ 0x44.. ] ),
				BinaryPrimitives.ReadUInt32LittleEndian( e[ 0x48.. ] ),
				BinaryPrimitives.ReadUInt32LittleEndian( e[ 0x4C.. ] ),
				BinaryPrimitives.ReadUInt32LittleEndian( e[ 0x74.. ] ),
				// version 3 files only use the low 32 bits of the size
				BinaryPrimitives.ReadUInt32LittleEndian( e[ 0x78.. ] ) ) );
		}
		if( entries.Count == 0 )
			return;

		var root = entries[ 0 ];
		if( root.Start < EndOfChain )
		{
			var mini = Chain( root.Start );
			_miniStream = mini.Length > root.Size ? mini[ ..(int)root.Size ] : mini;
		}

		// walk the red-black tree of each storage to build "Storage/Stream" paths
		var visited = new HashSet<uint>();
		void Walk( uint index, string prefix )
		{
			if( index >= entries.Count || !visited.Add( index ) )
				return;
			var entry = entries[ (int)index ];
			Walk( entry.Left, prefix );
			string path = prefix + entry.Name;
			if( entry.Type == 1 )
				Walk( entry.Child, path + "/" );
			else if( entry.Type == 2 )
				AddOleStream( path, entry );
			Walk( entry.Right, prefix );
		}
		Walk( root.Child, "" );
	}

	private void AddOleStream( string path, DirEntry entry )
	{
		byte[]? Raw()
		{
			if( entry.Size < _miniCutoff )
			{
				var output = new byte[ entry.Size ];
				int written = 0;
				uint sector = entry.Start;
				for( int guard = 0; sector < EndOfChain && written < output.Length && guard <= _miniFat.Length; guard++ )
				{
					int offset = (int)sector * _miniSectorSize;
					int n = Math.Min( _miniSectorSize, output.Length - written );
					if( offset + n > _miniStream.Length )
						return null;
					_miniStream.AsSpan( offset, n ).CopyTo( output.AsSpan( written ) );
					written += n;
					sector = sector < _miniFat.Length ? _miniFat[ sector ] : EndOfChain;
				}
				return written == output.Length ? output : null;
			}
			var chain = Chain( entry.Start );
			return chain.Length >= entry.Size ? chain.AsSpan( 0, (int)entry.Size ).ToArray() : null;
		}

		const string zlb = "__ZLB";
		if( path.EndsWith( zlb, StringComparison.Ordinal ) )
			Add( path[ ..^zlb.Length ], () => Raw() is { Length: > 24 } raw ? InflateZlb( raw ) : null );
		else
			Add( path, Raw );
	}

	private static byte[] InflateZlb( byte[] raw )
	{
		// 24-byte header (uncompressed size at 16, compressed size at 20), then a zlib stream
		using var input = new MemoryStream( raw, 24, raw.Length - 24, writable: false );
		using var inflater = new ZLibStream( input, CompressionMode.Decompress );
		using var output = new MemoryStream();
		inflater.CopyTo( output );
		return output.ToArray();
	}

	private ReadOnlySpan<byte> Sector( uint id )
	{
		long offset = ( (long)id + 1 ) * _sectorSize;
		if( offset + _sectorSize > _data.Length )
			throw new InvalidDataException( $"sector {id} is past the end of the file" );
		return _data.AsSpan( (int)offset, _sectorSize );
	}

	private byte[] Chain( uint start )
	{
		using var output = new MemoryStream();
		uint sector = start;
		for( int guard = 0; sector < EndOfChain && sector != FreeSector && guard <= _fat.Length; guard++ )
		{
			output.Write( Sector( sector ) );
			sector = sector < _fat.Length ? _fat[ sector ] : EndOfChain;
		}
		return output.ToArray();
	}

	private readonly record struct DirEntry( string Name, byte Type, uint Left, uint Right, uint Child, uint Start, uint Size );

	#endregion
}

/// <summary>CRC-32 (IEEE 802.3, as used by zlib).</summary>
internal static class Crc32
{
	private static readonly uint[] Table = BuildTable();

	private static uint[] BuildTable()
	{
		var table = new uint[ 256 ];
		for( uint i = 0; i < 256; i++ )
		{
			uint c = i;
			for( int k = 0; k < 8; k++ )
				c = ( c & 1 ) != 0 ? 0xEDB88320u ^ ( c >> 1 ) : c >> 1;
			table[ i ] = c;
		}
		return table;
	}

	public static uint Compute( ReadOnlySpan<byte> data )
	{
		uint crc = 0xFFFFFFFFu;
		foreach( byte b in data )
			crc = Table[ ( crc ^ b ) & 0xFF ] ^ ( crc >> 8 );
		return crc ^ 0xFFFFFFFFu;
	}
}
