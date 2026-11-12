using System.Buffers.Binary;
using System.Text;
using System.Text.RegularExpressions;

namespace HackPDM.Infrastructure.SldWrks.Format;

/// <summary>
/// Configuration names from "Contents/CMgrHdr2" (or "Contents/CMgrHdr" in old files): an MFC
/// CArchive holding one dmConfigMgrHeader_c with a dmConfigHeader_c record per configuration.
/// Each record's optional fields depend on the document version (the N in "_MO_VERSION_N/…").
/// Field order and version gates follow the MIT-licensed openswx project's cmgrhdr_reader.
/// </summary>
public static partial class ConfigHeaderReader
{
	// minimum document versions at which each optional dmConfigHeader_c field was added
	private const int VerField4 = 1341, VerParent = 2037, VerField7 = 2136, VerAltName = 2610,
		VerField10 = 3928, VerField11 = 11142, VerField12 = 12155, VerField13 = 12216;

	[GeneratedRegex( @"^_MO_VERSION_(\d+)/" )]
	private static partial Regex VersionStream();

	/// <summary>Reads the document version from the name of its "_MO_VERSION_N/..." streams.</summary>
	/// <param name="store">The document's streams.</param>
	/// <returns>The first N found, or null when there is no such stream.</returns>
	public static int? DocumentVersion( SwStreamStore store )
	{
		foreach( string name in store.Names )
			if( VersionStream().Match( name ) is { Success: true } m && int.TryParse( m.Groups[ 1 ].Value, out int v ) )
				return v;
		return null;
	}

	/// <summary>Reads configuration names from "Contents/CMgrHdr2" (or "Contents/CMgrHdr").</summary>
	/// <param name="store">The document's streams.</param>
	/// <returns>
	/// Configuration index (the N in "Config-N-...") to name. Empty when the stream or the document version is missing;
	/// when the stream stops making sense part-way, the names read before that point.
	/// </returns>
	public static Dictionary<int, string> ReadNames( SwStreamStore store )
	{
		var names = new Dictionary<int, string>();
		byte[]? stream = store.Get( "Contents/CMgrHdr2" ) ?? store.Get( "Contents/CMgrHdr" );
		if( stream is null || DocumentVersion( store ) is not int version )
			return names;
		try
		{
			Parse( new Reader( stream ), version, names );
		}
		catch( Exception e ) when( e is ArgumentOutOfRangeException or InvalidDataException )
		{
			// keep what was read before the stream stopped making sense
		}
		return names;
	}

	private static void Parse( Reader r, int version, Dictionary<int, string> names )
	{
		r.ReadObjectTag();           // dmConfigMgrHeader_c
		int count = r.ReadCount();
		for( int i = 0; i < count; i++ )
		{
			r.ReadObjectTag();       // dmConfigHeader_c
			r.U32();
			string name = r.CString();
			int id = (int)r.U32();
			if( version >= VerField4 ) r.U32();
			if( version >= VerParent ) { r.CString(); r.U32(); }
			if( version >= VerField7 ) r.U32();
			if( version >= VerAltName ) { r.CString(); r.CString(); }
			if( version >= VerField10 ) r.U32();
			if( version >= VerField11 ) r.U32();
			if( version >= VerField12 ) r.U32();
			if( version >= VerField13 ) r.U32();
			if( name.Length > 0 )
				names.TryAdd( id, name );
		}
	}

	private sealed class Reader( byte[] data )
	{
		private int _pos;

		public byte U8() => _pos < data.Length ? data[ _pos++ ] : throw new InvalidDataException( "unexpected end of stream" );
		public ushort U16() { var v = BinaryPrimitives.ReadUInt16LittleEndian( data.AsSpan( _pos ) ); _pos += 2; return v; }
		public uint U32() { var v = BinaryPrimitives.ReadUInt32LittleEndian( data.AsSpan( _pos ) ); _pos += 4; return v; }

		/// <summary>MFC WriteObject tag: 0xFFFF + schema + class name for a new class, or a 0x8000|index class back-reference.</summary>
		public void ReadObjectTag()
		{
			ushort tag = U16();
			if( tag == 0xFFFF )
			{
				U16();                    // schema
				int length = U16();
				_pos += length;           // class name
			}
			else if( ( tag & 0x8000 ) == 0 )
			{
				throw new InvalidDataException( $"unexpected object tag {tag:X4}" );
			}
		}

		/// <summary>MFC WriteCount: a WORD, or 0xFFFF followed by a DWORD.</summary>
		public int ReadCount()
		{
			ushort w = U16();
			return w != 0xFFFF ? w : (int)U32();
		}

		/// <summary>MFC CString: an optional FF FE FF Unicode marker, then a byte / WORD / DWORD length.</summary>
		public string CString()
		{
			bool unicode = false;
			while( true )
			{
				int length = U8();
				if( length == 0xFF )
				{
					ushort w = U16();
					if( w == 0xFFFE )
					{
						unicode = true;
						continue;
					}
					length = w == 0xFFFF ? (int)U32() : w;
				}
				int bytes = unicode ? length * 2 : length;
				if( length < 0 || _pos + bytes > data.Length )
					throw new InvalidDataException( "string runs past the end of the stream" );
				string s = unicode ? Encoding.Unicode.GetString( data, _pos, bytes ) : Encoding.Latin1.GetString( data, _pos, bytes );
				_pos += bytes;
				return s;
			}
		}
	}
}
