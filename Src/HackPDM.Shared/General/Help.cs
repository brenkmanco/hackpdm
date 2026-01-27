using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

using HackPDM.Shared.GlobalData;

namespace HackPDM.Shared.General;

public static class Help
{
	public static bool ParseGoodName( string s ) => s.Trim().Equals( "x", StringComparison.OrdinalIgnoreCase );
	public static uint HexToUint( string hex )
	{
		// remove leading
		string hexStr = hex.Trim();
		if( hex.StartsWith( "#" ) )
			hexStr = hexStr.Substring( 1 );

		// convert hex to argb
		uint rgb = Convert.ToUInt32(hexStr, 16);
		return 0xFF000000u | rgb; // force alpha
	}
	public static string ToEnumName( string raw )
	{
		// remove invalid chars
		var cleaned = new string([ .. raw.Where(char.IsLetterOrDigit) ]);

		if( string.IsNullOrWhiteSpace( cleaned ) )
			cleaned = "Unnamed";

		// pascalCase
		cleaned = char.ToUpper( cleaned[ 0 ] ) + cleaned.Substring( 1 );

		// if starts with digit, prefix
		cleaned.ReplaceAnyChar( StorageBox.DIGITS, c => $"{c}_" );

		return cleaned;
	}
	public static string ReplaceAnyChar( this string str, char[] chArr, Func<char, string> foundCharSelector )
	{
		StringBuilder sb = new();
		foreach ( var item in str.ReplaceAll(chArr, foundCharSelector))
		{
			sb.Append( item );
		}
		return sb.ToString();
	}
	public static string ReplaceAnyChar( this string str, string chArr, Func<char, string> foundCharSelector )
		=> str.ReplaceAnyChar( chArr.ToCharArray(), foundCharSelector );

	public static IEnumerable<string> ReplaceAll( this string str, string chArr, Func<char, string> foundCharSelector )
		=> str.ReplaceAll( chArr, foundCharSelector );
	public static IEnumerable<string> ReplaceAll(this string str, char[] chArr, Func<char, string> foundCharSelector)
	{
		for( int i = 0; i < str.Length; i++ )
		{
			char c = chArr[i];
			for( int j = 0; j < chArr.Length; j++ )
			{
				if( c != chArr[ j ] )
					continue;
				yield return foundCharSelector( c );
			}
		}
	}

	public static IEnumerable<ColorCsvRow> LoadCsv( string path )
		=> ParseCsv( File.ReadLines( path ).Skip( 1 ) );
	public static IEnumerable<ColorCsvRow> ParseCsv( string text )
		=> ParseCsv( text.Split('\n') );
	public static IEnumerable<ColorCsvRow> ParseCsv(IEnumerable<string> lines)
	{
		foreach( var line in lines)
		{
			if( string.IsNullOrWhiteSpace( line ) )
				continue;
			var parts = line.Split(',');

			yield return new ColorCsvRow(
				Name: parts[ 0 ].Trim(),
				Hex: parts[ 1 ].Trim(),
				GoodName: ParseGoodName( parts[ 2 ] )
			);
		}
	}

}
public sealed record ColorCsvRow( string Name, string Hex, bool GoodName );
public struct Except<T>(Expression<Func<T>> expr)
{

}
