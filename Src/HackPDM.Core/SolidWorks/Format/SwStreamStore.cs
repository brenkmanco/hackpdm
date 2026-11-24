using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

using HackPDM.Core.Pdm;

namespace HackPDM.Core.SolidWorks.Format;

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
	/// <summary>The container layout of a SolidWorks file.</summary>
	public enum ContainerFormat { Unknown, Ole, Modern }

	private enum SourceKind : byte { Modern, OleRaw, OleZlb }
	private readonly record struct StreamSource( SourceKind Kind, uint Start, int Length, uint UncompressedOrSize, uint Crc );

	private readonly byte[] _data;
	// candidate locations per stream name, in file order (the modern format can hold stale copies)
	private readonly Dictionary<string, List<StreamSource>> _sources = new( StringComparer.Ordinal );
	private readonly Dictionary<string, byte[]?> _decoded = new( StringComparer.Ordinal );

	/// <summary>The detected container layout; <see cref="ContainerFormat.Unknown"/> means no streams were indexed.</summary>
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

	/// <summary>Reads a whole file into memory and indexes its streams. Streams are decompressed only when first read.</summary>
	/// <param name="path">Path of the SolidWorks file.</param>
	/// <returns>The store; check <see cref="Format"/> since an unrecognised file gives an empty <see cref="ContainerFormat.Unknown"/> store rather than an error.</returns>
	/// <exception cref="IOException">The file can't be read (including <see cref="FileNotFoundException"/>).</exception>
	/// <exception cref="UnauthorizedAccessException">Access to the file is denied.</exception>
	/// <exception cref="InvalidDataException">An OLE file is shorter than its 512-byte header or its structure is corrupt.</exception>
	public static SwStreamStore Open( string path )
	{
		using FileStream stream = new( path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete );
		if( stream.CanSeek && stream.Length > int.MaxValue )
			throw new InvalidDataException( $"SolidWorks file exceeds maximum in-memory size: {path}" );
		using MemoryStream copy = new( stream.CanSeek ? (int)stream.Length : 0 );
		stream.CopyTo( copy );
		return new( copy.ToByteArrayZeroCopy() );
	}
	/// <summary>Indexes the streams of a SolidWorks file already in memory.</summary>
	/// <param name="data">The whole file; kept (not copied) by the store.</param>
	/// <returns>The store; check <see cref="Format"/>.</returns>
	/// <exception cref="InvalidDataException">An OLE file is shorter than its 512-byte header or its structure is corrupt.</exception>
	public static SwStreamStore FromBytes( byte[] data ) => new( data );

	/// <summary>The names of all indexed streams, such as "Header2" or "docProps/custom.xml" (OLE storages joined with "/", "__ZLB" suffixes removed).</summary>
	public IEnumerable<string> Names => _sources.Keys;

	/// <summary>Gets a stream's decompressed contents, decompressing it on first use and caching the result.</summary>
	/// <param name="name">The stream name, as in <see cref="Names"/> (case-sensitive).</param>
	/// <returns>
	/// The bytes, or null when the stream is missing or no copy of it is intact. In 2015+ files the first copy whose size and
	/// CRC-32 match is used, since files can hold stale copies.
	/// </returns>
	/// <remarks>Not thread-safe: the cache is a plain dictionary.</remarks>
	public byte[]? Get( string name )
	{
		if( _decoded.TryGetValue( name, out var cached ) )
			return cached;
		byte[]? result = null;
		if( _sources.TryGetValue( name, out var candidates ) )
		{
			for( int i = 0; i < candidates.Count; i++ )
			{
				try
				{
					if( ( result = ReadSource( candidates[ i ] ) ) is not null )
						break;
				}
				catch( InvalidDataException ) { }
				catch( ArgumentOutOfRangeException ) { }
			}
		}
		_decoded[ name ] = result;
		return result;
	}

	private byte[]? ReadSource( StreamSource src ) => src.Kind switch
	{
		SourceKind.Modern => InflateChecked( (int)src.Start, src.Length, src.UncompressedOrSize, src.Crc ),
		SourceKind.OleRaw => ReadOleRaw( src.Start, src.UncompressedOrSize ),
		SourceKind.OleZlb => ReadOleRaw( src.Start, src.UncompressedOrSize ) is { Length: > 24 } raw ? InflateZlb( raw ) : null,
		_ => null,
	};

	/// <summary>Gets a stream's contents decoded as UTF-8 text (used for the XML streams of 2015+ files).</summary>
	/// <param name="name">The stream name.</param>
	/// <returns>The text, or null when the stream is missing or damaged.</returns>
	public string? GetText( string name ) => Get( name ) is { } bytes ? Encoding.UTF8.GetString( bytes ) : null;

	/// <summary>Identifies the container layout from the first bytes of a file.</summary>
	/// <param name="data">The file's contents (at least the first 64 bytes).</param>
	/// <returns><see cref="ContainerFormat.Ole"/> for the OLE signature, <see cref="ContainerFormat.Modern"/> when a chunk marker appears in the first 64 bytes, otherwise <see cref="ContainerFormat.Unknown"/>.</returns>
	public static ContainerFormat DetectFormat( ReadOnlySpan<byte> data )
	{
		if( data.Length >= 8 && data[ ..8 ].SequenceEqual( OleSignature ) )
			return ContainerFormat.Ole;
		// the first chunk marker sits within the first few dozen bytes
		if( data[ ..Math.Min( data.Length, 64 ) ].IndexOf( ChunkMarker ) >= 0 )
			return ContainerFormat.Modern;
		return ContainerFormat.Unknown;
	}

	private void Add( string name, StreamSource source )
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
		if( _data.Length < 8 )
			return;
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

			Add( name, new StreamSource( SourceKind.Modern, (uint)payloadStart, (int)compressed, uncompressed, crc ) );
			pos = (int)( payloadStart + compressed );
		}
	}

	private static string? DecodeName( ReadOnlySpan<byte> encoded, int key )
	{
		Span<char> chars = stackalloc char[ encoded.Length ];
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
		// DEFLATE max theoretical expansion ratio is ~1032:1
		if( expectedSize > 1024 * 1024 && (ulong)expectedSize > (ulong)Math.Max( length, 1 ) * 1032 + 1024 )
			return null;
		using var input = new MemoryStream( _data, start, length, writable: false );
		using var inflater = new DeflateStream( input, CompressionMode.Decompress );
		int expected = (int)expectedSize;
		if( expected <= 1024 * 1024 )
		{
			var output = new byte[ expected ];
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
		using var ms = new MemoryStream( Math.Min( expected, Math.Max( length * 4, 65536 ) ) );
		byte[] buf = new byte[ 65536 ];
		long total = 0;
		int chunk;
		while( ( chunk = inflater.Read( buf, 0, buf.Length ) ) > 0 )
		{
			total += chunk;
			if( total > expected )
				return null;
			ms.Write( buf, 0, chunk );
		}
		if( total != expected )
			return null;
		byte[] result = ms.ToByteArrayZeroCopy();
		return Crc32.Compute( result ) == expectedCrc ? result : null;
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
		if( _data.Length < 512 )
			throw new InvalidDataException( "OLE file is shorter than its 512-byte header" );
		var header = _data.AsSpan( 0, 512 );
		ushort sectorShift = BinaryPrimitives.ReadUInt16LittleEndian( header[ 0x1E.. ] );
		ushort miniShift = BinaryPrimitives.ReadUInt16LittleEndian( header[ 0x20.. ] );
		if( sectorShift is < 7 or > 16 || miniShift is < 3 or > 16 || miniShift > sectorShift )
			throw new InvalidDataException( $"invalid OLE sector shift ({sectorShift}/{miniShift})" );
		_sectorSize = 1 << sectorShift;
		_miniSectorSize = 1 << miniShift;
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
		HashSet<uint> visitedDifat = [];
		for( uint n = 0; n < difatSectors && difat < EndOfChain && (uint)fatSectorIds.Count < fatSectors; n++ )
		{
			if( !visitedDifat.Add( difat ) )
				throw new InvalidDataException( "cyclic DIFAT chain in OLE header" );
			var sector = Sector( difat );
			int perSector = _sectorSize / 4;
			for( int i = 0; i < perSector - 1 && (uint)fatSectorIds.Count < fatSectors; i++ )
				fatSectorIds.Add( BinaryPrimitives.ReadUInt32LittleEndian( sector[ ( i * 4 ).. ] ) );
			difat = BinaryPrimitives.ReadUInt32LittleEndian( sector[ ( ( perSector - 1 ) * 4 ).. ] );
		}
		int fatCount = (int)Math.Min( fatSectors, (uint)fatSectorIds.Count );
		int entriesPerSector = _sectorSize / 4;
		long totalFatEntries = (long)fatCount * entriesPerSector;
		if( totalFatEntries > int.MaxValue / 4 || fatCount > ( _data.Length / _sectorSize ) + 109 )
			throw new InvalidDataException( "OLE FAT table exceeds file bounds" );
		_fat = new uint[ fatCount * entriesPerSector ];
		int fatIdx = 0;
		for( int f = 0; f < fatCount; f++ )
		{
			var sector = Sector( fatSectorIds[ f ] );
			for( int i = 0; i + 4 <= sector.Length; i += 4 )
				_fat[ fatIdx++ ] = BinaryPrimitives.ReadUInt32LittleEndian( sector[ i.. ] );
		}

		if( miniFatSectors > 0 )
		{
			var raw = Chain( miniFatStart );
			_miniFat = new uint[ raw.Length / 4 ];
			for( int i = 0; i < _miniFat.Length; i++ )
				_miniFat[ i ] = BinaryPrimitives.ReadUInt32LittleEndian( raw.AsSpan( i * 4 ) );
		}

		var directory = Chain( directoryStart );
		var entries = new List<DirEntry>( directory.Length / 128 );
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
			_miniStream = Chain( root.Start, root.Size ) ?? [];
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
		const string zlb = "__ZLB";
		if( path.EndsWith( zlb, StringComparison.Ordinal ) )
			Add( path[ ..^zlb.Length ], new StreamSource( SourceKind.OleZlb, entry.Start, 0, entry.Size, 0 ) );
		else
			Add( path, new StreamSource( SourceKind.OleRaw, entry.Start, 0, entry.Size, 0 ) );
	}

	private byte[]? ReadOleRaw( uint start, uint size )
	{
		if( size < _miniCutoff )
		{
			if( size > (uint)_miniStream.Length )
				return null;
			var output = new byte[ (int)size ];
			int written = 0;
			uint sector = start;
			HashSet<uint> visited = [];
			for( int guard = 0; sector < EndOfChain && written < output.Length && guard <= _miniFat.Length; guard++ )
			{
				if( !visited.Add( sector ) )
					return null;
				long offset = (long)sector * _miniSectorSize;
				int n = Math.Min( _miniSectorSize, output.Length - written );
				if( offset < 0 || offset + n > _miniStream.Length )
					return null;
				_miniStream.AsSpan( (int)offset, n ).CopyTo( output.AsSpan( written ) );
				written += n;
				sector = sector < _miniFat.Length ? _miniFat[ sector ] : EndOfChain;
			}
			return written == output.Length ? output : null;
		}
		return ChainExact( start, size );
	}

	private static byte[] InflateZlb( byte[] raw )
	{
		// 24-byte header (uncompressed size at 16, compressed size at 20), then a zlib stream
		uint hinted = BinaryPrimitives.ReadUInt32LittleEndian( raw.AsSpan( 16 ) );
		int capacity = hinted is > 0 and <= 64 * 1024 * 1024 ? (int)hinted : Math.Min( raw.Length * 2, 64 * 1024 );
		using var input = new MemoryStream( raw, 24, raw.Length - 24, writable: false );
		using var inflater = new ZLibStream( input, CompressionMode.Decompress );
		using var output = new MemoryStream( capacity );
		inflater.CopyTo( output );
		return output.ToByteArrayZeroCopy();
	}

	private ReadOnlySpan<byte> Sector( uint id )
	{
		long offset = ( (long)id + 1 ) * _sectorSize;
		if( offset + _sectorSize > _data.Length )
			throw new InvalidDataException( $"sector {id} is past the end of the file" );
		return _data.AsSpan( (int)offset, _sectorSize );
	}

	private byte[] Chain( uint start, long maxBytes = -1 )
	{
		long boundedMax = maxBytes >= 0 ? Math.Min( maxBytes, _data.Length ) : _data.Length;
		int initialCapacity = maxBytes > 0 ? (int)Math.Min( boundedMax, 16 * 1024 * 1024 ) : Math.Min( _sectorSize * 4, Math.Max( _data.Length, 0 ) );
		using var output = new MemoryStream( initialCapacity );
		uint sector = start;
		HashSet<uint> visited = [];
		for( int guard = 0; sector < EndOfChain && sector != FreeSector && guard <= _fat.Length; guard++ )
		{
			if( !visited.Add( sector ) )
				throw new InvalidDataException( "cyclic FAT chain in OLE file" );
			ReadOnlySpan<byte> bytes = Sector( sector );
			long remaining = boundedMax - output.Length;
			if( remaining <= 0 )
				break;
			if( bytes.Length > remaining )
				bytes = bytes[ ..(int)remaining ];
			output.Write( bytes );
			sector = sector < _fat.Length ? _fat[ sector ] : EndOfChain;
		}
		return output.ToByteArrayZeroCopy();
	}

	private byte[]? ChainExact( uint start, long expectedSize )
	{
		if( expectedSize < 0 || expectedSize > _data.Length )
			return null;
		int total = (int)expectedSize;
		byte[] output = new byte[ total ];
		int written = 0;
		uint sector = start;
		HashSet<uint> visited = [];
		for( int guard = 0; sector < EndOfChain && sector != FreeSector && written < total && guard <= _fat.Length; guard++ )
		{
			if( !visited.Add( sector ) )
				return null;
			ReadOnlySpan<byte> bytes = Sector( sector );
			int take = Math.Min( bytes.Length, total - written );
			bytes[ ..take ].CopyTo( output.AsSpan( written ) );
			written += take;
			sector = sector < _fat.Length ? _fat[ sector ] : EndOfChain;
		}
		return written == total ? output : null;
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
