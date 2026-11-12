using System.Buffers.Binary;
using System.Text;

namespace HackPDM.Infrastructure.SldWrks.Format;

/// <summary>
/// Reads the user-defined section of an OLE property set (MS-OLEPS), as stored in pre-2015
/// SolidWorks files: "\u0005DocumentSummaryInformation" for file properties and
/// "Config-N-Properties" for each configuration's properties.
/// </summary>
public static class OlePropertySet
{
	/// <summary>FMTID_UserDefinedProperties {D5CDD505-2E9C-101B-9397-08002B2CF9AE}.</summary>
	private static readonly Guid UserDefined = new( "D5CDD505-2E9C-101B-9397-08002B2CF9AE" );

	private const uint PidDictionary = 0, PidCodePage = 1;

	/// <summary>Reads the named properties of the user-defined section of an OLE property set stream.</summary>
	/// <param name="stream">The raw property set stream, e.g. "\u0005DocumentSummaryInformation" or "Config-N-Properties".</param>
	/// <returns>
	/// (name, VT type, value) for each named property, in dictionary order. The value is a short, int, uint, long, float,
	/// double, bool, <see cref="DateTime"/> or string for the types in <see cref="VarType"/>, and null for other types or a
	/// truncated value. Empty when the stream isn't a property set or has no user-defined section.
	/// </returns>
	/// <remarks>Strings use the section's code page; code pages other than UTF-8, UTF-16 and those built into .NET are read as Latin-1.</remarks>
	/// <exception cref="ArgumentException">A VT_DATE value is not a valid OLE Automation date.</exception>
	public static List<(string Name, ushort VarType, object? Value)> ReadUserDefined( byte[] stream )
	{
		var result = new List<(string, ushort, object?)>();
		var data = stream.AsSpan();
		if( data.Length < 28 || BinaryPrimitives.ReadUInt16LittleEndian( data ) != 0xFFFE )
			return result;

		uint sections = BinaryPrimitives.ReadUInt32LittleEndian( data[ 24.. ] );
		for( int s = 0; s < sections && 28 + s * 20 + 20 <= data.Length; s++ )
		{
			var fmtid = new Guid( data.Slice( 28 + s * 20, 16 ) );
			if( fmtid != UserDefined )
				continue;
			int sectionStart = (int)BinaryPrimitives.ReadUInt32LittleEndian( data[ ( 28 + s * 20 + 16 ).. ] );
			ReadSection( data, sectionStart, result );
		}
		return result;
	}

	private static void ReadSection( ReadOnlySpan<byte> data, int start, List<(string, ushort, object?)> result )
	{
		if( start + 8 > data.Length )
			return;
		uint count = BinaryPrimitives.ReadUInt32LittleEndian( data[ ( start + 4 ).. ] );
		var offsets = new Dictionary<uint, int>();
		for( int i = 0; i < count && start + 8 + i * 8 + 8 <= data.Length; i++ )
		{
			uint pid = BinaryPrimitives.ReadUInt32LittleEndian( data[ ( start + 8 + i * 8 ).. ] );
			int offset = (int)BinaryPrimitives.ReadUInt32LittleEndian( data[ ( start + 12 + i * 8 ).. ] );
			offsets[ pid ] = start + offset;
		}

		int codePage = 1252;
		if( offsets.TryGetValue( PidCodePage, out int cpAt ) && cpAt + 6 <= data.Length )
			codePage = BinaryPrimitives.ReadUInt16LittleEndian( data[ ( cpAt + 4 ).. ] );
		var encoding = EncodingFor( codePage );

		if( !offsets.TryGetValue( PidDictionary, out int dictAt ) )
			return;
		foreach( var (pid, name) in ReadDictionary( data, dictAt, codePage, encoding ) )
		{
			if( pid is PidDictionary or PidCodePage || !offsets.TryGetValue( pid, out int valueAt ) )
				continue;
			var (type, value) = ReadValue( data, valueAt, codePage, encoding );
			result.Add( ( name, type, value ) );
		}
	}

	private static List<(uint Pid, string Name)> ReadDictionary( ReadOnlySpan<byte> data, int at, int codePage, Encoding encoding )
	{
		var names = new List<(uint, string)>();
		if( at + 4 > data.Length )
			return names;
		uint count = BinaryPrimitives.ReadUInt32LittleEndian( data[ at.. ] );
		int pos = at + 4;
		for( int i = 0; i < count && pos + 8 <= data.Length; i++ )
		{
			uint pid = BinaryPrimitives.ReadUInt32LittleEndian( data[ pos.. ] );
			int length = (int)BinaryPrimitives.ReadUInt32LittleEndian( data[ ( pos + 4 ).. ] );
			pos += 8;
			int bytes = codePage == 1200 ? length * 2 : length;
			if( length < 0 || pos + bytes > data.Length )
				break;
			names.Add( ( pid, encoding.GetString( data.Slice( pos, bytes ) ).TrimEnd( '\0' ) ) );
			pos += bytes;
			if( codePage == 1200 )
				pos = ( pos + 3 ) & ~3; // Unicode entries are padded to 4 bytes
		}
		return names;
	}

	private static (ushort Type, object? Value) ReadValue( ReadOnlySpan<byte> data, int at, int codePage, Encoding encoding )
	{
		if( at + 4 > data.Length )
			return ( 0, null );
		ushort type = BinaryPrimitives.ReadUInt16LittleEndian( data[ at.. ] );
		var v = data[ ( at + 4 ).. ];
		try
		{
			object? value = type switch
			{
				VarType.I2 => BinaryPrimitives.ReadInt16LittleEndian( v ),
				VarType.I4 or VarType.Int => BinaryPrimitives.ReadInt32LittleEndian( v ),
				VarType.UI4 or VarType.UInt => BinaryPrimitives.ReadUInt32LittleEndian( v ),
				VarType.I8 => BinaryPrimitives.ReadInt64LittleEndian( v ),
				VarType.R4 => BinaryPrimitives.ReadSingleLittleEndian( v ),
				VarType.R8 => BinaryPrimitives.ReadDoubleLittleEndian( v ),
				VarType.Bool => BinaryPrimitives.ReadInt16LittleEndian( v ) != 0,
				VarType.Date => DateTime.FromOADate( BinaryPrimitives.ReadDoubleLittleEndian( v ) ),
				VarType.FileTime => DateTime.FromFileTimeUtc( BinaryPrimitives.ReadInt64LittleEndian( v ) ),
				VarType.LpStr => ReadLpStr( v, codePage, encoding ),
				VarType.LpWStr => ReadLpWStr( v ),
				_ => null,
			};
			return ( type, value );
		}
		catch( ArgumentOutOfRangeException )
		{
			return ( type, null );
		}
	}

	private static string ReadLpStr( ReadOnlySpan<byte> v, int codePage, Encoding encoding )
	{
		int length = (int)BinaryPrimitives.ReadUInt32LittleEndian( v );
		if( codePage == 1200 )
			return ReadLpWStr( v );
		return encoding.GetString( v.Slice( 4, length ) ).TrimEnd( '\0' );
	}

	private static string ReadLpWStr( ReadOnlySpan<byte> v )
	{
		int chars = (int)BinaryPrimitives.ReadUInt32LittleEndian( v );
		return Encoding.Unicode.GetString( v.Slice( 4, chars * 2 ) ).TrimEnd( '\0' );
	}

	private static Encoding EncodingFor( int codePage )
	{
		try
		{
			return codePage switch
			{
				65001 => Encoding.UTF8,
				1200 => Encoding.Unicode,
				_ => Encoding.GetEncoding( codePage ),
			};
		}
		catch( Exception e ) when( e is ArgumentException or NotSupportedException )
		{
			return Encoding.Latin1; // code pages beyond Latin-1/UTF need System.Text.Encoding.CodePages
		}
	}

	/// <summary>The VARENUM codes property sets use (also the TypeID in the 2015+ XML).</summary>
	public static class VarType
	{
		public const ushort I2 = 2, I4 = 3, R4 = 4, R8 = 5, Date = 7, Bool = 11, I8 = 20,
			UI4 = 19, Int = 22, UInt = 23, LpStr = 30, LpWStr = 31, FileTime = 64;
	}
}
