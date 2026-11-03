using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

using CommunityToolkit.WinUI.UI.Controls;

using HackPDM.Domain.Representation;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Shared.GlobalData;
using HackPDM.UI.Controls;
using HackPDM.UI.Forms.Hack;
using HackPDM.UI.Forms.Helper;
using HackPDM.UI.Forms.Odoo;
using HackPDM.UI.Forms.Settings;
using HackPDM.UI.Types;

using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media;

using Color = Windows.UI.Color;
using EntryRow = HackPDM.UI.Types.EntryRow;

namespace HackPDM.UI.Data;

public static class CustomSerializers
{
	public class ColorSerializer : JsonConverter<Color>
	{
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
	public class Vector4Serializer : JsonConverter<Vector4<int>>
	{
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
