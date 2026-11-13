using System;
using System.IO;
using System.IO.Compression;

namespace HackPDM.Shell.Testing;

/// <summary>A minimal PNG writer for the smoke test's screenshots (RGBA, no filtering).</summary>
internal static class Png
{
	private static readonly uint[] CrcTable = BuildCrcTable();

	/// <summary>Encodes premultiplied BGRA pixels (as RenderTargetBitmap gives them) as a PNG.</summary>
	public static byte[] FromBgra( byte[] bgra, int width, int height )
	{
		if( bgra.Length < width * height * 4 )
			throw new ArgumentException( $"{bgra.Length} bytes for {width}x{height}" );
		using MemoryStream raw = new();
		using( ZLibStream z = new( raw, CompressionLevel.Fastest, leaveOpen: true ) )
		{
			byte[] row = new byte[ 1 + width * 4 ];
			for( int y = 0; y < height; y++ )
			{
				row[ 0 ] = 0;
				for( int x = 0; x < width; x++ )
				{
					int i = ( y * width + x ) * 4, o = 1 + x * 4;
					byte a = bgra[ i + 3 ];
					row[ o ] = Unpremultiply( bgra[ i + 2 ], a );
					row[ o + 1 ] = Unpremultiply( bgra[ i + 1 ], a );
					row[ o + 2 ] = Unpremultiply( bgra[ i ], a );
					row[ o + 3 ] = a;
				}
				z.Write( row );
			}
		}

		using MemoryStream png = new();
		png.Write( [ 137, 80, 78, 71, 13, 10, 26, 10 ] );
		byte[] header = new byte[ 13 ];
		WriteBigEndian( header, 0, (uint)width );
		WriteBigEndian( header, 4, (uint)height );
		header[ 8 ] = 8; // bits per channel
		header[ 9 ] = 6; // RGBA
		Chunk( png, "IHDR", header );
		Chunk( png, "IDAT", raw.ToArray() );
		Chunk( png, "IEND", [] );
		return png.ToArray();
	}

	private static byte Unpremultiply( byte c, byte a ) => a is 0 or 255 ? c : (byte)Math.Min( 255, c * 255 / a );

	private static void Chunk( Stream s, string type, byte[] data )
	{
		byte[] head = new byte[ 8 ];
		WriteBigEndian( head, 0, (uint)data.Length );
		for( int i = 0; i < 4; i++ )
			head[ 4 + i ] = (byte)type[ i ];
		s.Write( head );
		s.Write( data );
		uint crc = 0xFFFFFFFF;
		foreach( byte b in head.AsSpan( 4 ) )
			crc = CrcTable[ ( crc ^ b ) & 0xFF ] ^ ( crc >> 8 );
		foreach( byte b in data )
			crc = CrcTable[ ( crc ^ b ) & 0xFF ] ^ ( crc >> 8 );
		byte[] tail = new byte[ 4 ];
		WriteBigEndian( tail, 0, crc ^ 0xFFFFFFFF );
		s.Write( tail );
	}

	private static void WriteBigEndian( byte[] buffer, int at, uint value )
	{
		buffer[ at ] = (byte)( value >> 24 );
		buffer[ at + 1 ] = (byte)( value >> 16 );
		buffer[ at + 2 ] = (byte)( value >> 8 );
		buffer[ at + 3 ] = (byte)value;
	}

	private static uint[] BuildCrcTable()
	{
		uint[] table = new uint[ 256 ];
		for( uint n = 0; n < 256; n++ )
		{
			uint c = n;
			for( int k = 0; k < 8; k++ )
				c = ( c & 1 ) != 0 ? 0xEDB88320 ^ ( c >> 1 ) : c >> 1;
			table[ n ] = c;
		}
		return table;
	}
}
