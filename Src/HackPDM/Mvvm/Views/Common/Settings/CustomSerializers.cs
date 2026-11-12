using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;


using HackPDM.Domain.Representation;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Shared.GlobalData;
using HackPDM.Mvvm.Views.Common.Controls;
using HackPDM.UI.Types;

using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media;

using Color = Windows.UI.Color;
using EntryRow = HackPDM.Mvvm.Models.Hack.EntryRow;

namespace HackPDM.Mvvm.Views.Common.Settings;

/// <summary>JSON converters for the UI types stored in AppConfig.json.</summary>
public static class CustomSerializers
{
	/// <summary>Stores a <see cref="Windows.UI.Color"/> as a "#AARRGGBB" string.</summary>
	public class ColorSerializer : JsonConverter<Color>
	{
		/// <summary>Reads a color from "#AARRGGBB" or "#RRGGBB" (the "#" is optional; six digits are fully opaque).</summary>
		/// <param name="reader">The reader positioned on the string token.</param>
		/// <param name="typeToConvert">The type being converted (always <see cref="Windows.UI.Color"/>).</param>
		/// <param name="options">The serializer options in use.</param>
		/// <returns>The parsed color.</returns>
		/// <exception cref="System.Text.Json.JsonException">The value is null or not a hex color.</exception>
		/// <exception cref="System.InvalidOperationException">The token is not a string.</exception>
		public override Color Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )
		{
			string colorString = reader.GetString() ?? throw new JsonException("Color string is null");
			if( colorString.StartsWith( '#' ) )
			{
				colorString = colorString[ 1.. ];
			}
			if( colorString.Length == 6 )
			{
				colorString = "FF" + colorString; // Add alpha channel if not present
			}
			if( uint.TryParse( colorString, System.Globalization.NumberStyles.HexNumber, null, out uint argb ) )
			{
				return Color.FromArgb(
					( byte )( ( argb >> 24 ) & 0xFF ),
					( byte )( ( argb >> 16 ) & 0xFF ),
					( byte )( ( argb >> 8 ) & 0xFF ),
					( byte )( argb & 0xFF )
				);
			}
			else
			{
				throw new JsonException( $"Invalid color string: {colorString}" );
			}
		}
		public override void Write( Utf8JsonWriter writer, Color value, JsonSerializerOptions options )
		{
			string colorString = $"#{value.A:X2}{value.R:X2}{value.G:X2}{value.B:X2}";
			writer.WriteStringValue( colorString );
		}
	}
	/// <summary>Stores a four-component integer vector as a JSON array <c>[x, y, z, w]</c>.</summary>
	public class Vector4Serializer : JsonConverter<Vector4<int>>
	{
		/// <summary>Reads a vector from a four-element integer array.</summary>
		/// <param name="reader">The reader positioned on the array's start token.</param>
		/// <param name="typeToConvert">The type being converted.</param>
		/// <param name="options">The serializer options in use.</param>
		/// <returns>The vector.</returns>
		/// <exception cref="System.Text.Json.JsonException">The value is not an array of exactly four elements.</exception>
		/// <exception cref="System.InvalidOperationException">An element is not a number.</exception>
		/// <exception cref="System.FormatException">An element does not fit in an <see cref="int"/>.</exception>
		public override Vector4<int> Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )
		{
			if( reader.TokenType != JsonTokenType.StartArray )
			{
				throw new JsonException( "Expected StartArray token" );
			}
			reader.Read();
			int x = reader.GetInt32();
			reader.Read();
			int y = reader.GetInt32();
			reader.Read();
			int z = reader.GetInt32();
			reader.Read();
			int w = reader.GetInt32();
			reader.Read();
			return  reader.TokenType != JsonTokenType.EndArray
				? throw new JsonException( "Expected EndArray token" )
				:  new Vector4<int>( x, y, z, w );
		}
		public override void Write( Utf8JsonWriter writer, Vector4<int> value, JsonSerializerOptions options )
		{
			writer.WriteStartArray();
			writer.WriteNumberValue( value.x );
			writer.WriteNumberValue( value.y );
			writer.WriteNumberValue( value.z );
			writer.WriteNumberValue( value.w );
			writer.WriteEndArray();
		}
	}
}
