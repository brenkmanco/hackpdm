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

public class LazyConverter<T> : JsonConverter<Lazy<T>>
{
	public override Lazy<T> Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )
	{
		// Deserialize the JSON value natively into T first
		T value = JsonSerializer.Deserialize<T>(ref reader, options);

		// Return it wrapped in a Lazy<T> container
		return new Lazy<T>( () => value );
	}

	public override void Write( Utf8JsonWriter writer, Lazy<T> value, JsonSerializerOptions options )
	{
		if( value == null )
		{
			writer.WriteNullValue();
		}
		else
		{
			// Serialize the evaluated inner value T to the JSON string
			JsonSerializer.Serialize( writer, value.Value, options );
		}
	}
}
