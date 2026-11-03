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

public class LazyConverterFactory : JsonConverterFactory
{
	public override bool CanConvert( Type typeToConvert )
	{
		// Checks if the property type is Lazy<T>
		return typeToConvert.IsGenericType &&
			   typeToConvert.GetGenericTypeDefinition() == typeof( Lazy<> );
	}

	public override JsonConverter CreateConverter( Type typeToConvert, JsonSerializerOptions options )
	{
		Type valueType = typeToConvert.GetGenericArguments()[0];

		// Dynamically instantiate the generic LazyConverter<T>
		return ( JsonConverter )Activator.CreateInstance(
			typeof( LazyConverter<> ).MakeGenericType( valueType ) );
	}
}
