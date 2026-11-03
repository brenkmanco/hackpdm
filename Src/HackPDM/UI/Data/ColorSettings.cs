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

public class ColorSettings
{
	[JsonConverter( typeof( LazyConverterFactory ) )]
	public Lazy<Color> White				{ get; set; } = new Lazy<Color>(() => Color.FromArgb( 255, 255, 255, 255 ));
	[JsonConverter( typeof( LazyConverterFactory ) )]
	public Lazy<Color> Black				{ get; set; } = new Lazy<Color>(() => Color.FromArgb( 255, 0, 0, 0 ));
	[JsonConverter( typeof( LazyConverterFactory ) )]
	public Lazy<Color> LightGray			{ get; set; } = new Lazy<Color>(() => Color.FromArgb( 255, 211, 211, 211 ));
	[JsonConverter( typeof( LazyConverterFactory ) )]
	public Lazy<Color> Gray					{ get; set; } = new Lazy<Color>(() => Color.FromArgb( 255, 128, 128, 128 ));
	[JsonConverter( typeof( LazyConverterFactory ) )]
	public Lazy<Color> MustardYellow		{ get; set; } = new Lazy<Color>(() => Color.FromArgb( 255, 150, 150, 0 ));
	[JsonConverter( typeof( LazyConverterFactory ) )]
	public Lazy<Color> DarkGray				{ get; set; } = new Lazy<Color>(() => Color.FromArgb( 255, 64, 64, 64 ));
	[JsonConverter( typeof( LazyConverterFactory ) )]
	public Lazy<Color> DarkRed				{ get; set; } = new Lazy<Color>(() => Color.FromArgb( 255, 139, 0, 0 ));
	[JsonConverter( typeof( LazyConverterFactory ) )]
	public Lazy<Color> DarkBlue				{ get; set; } = new Lazy<Color>(() => Color.FromArgb( 255, 0, 0, 139 ));
	[JsonConverter( typeof( LazyConverterFactory ) )]
	public Lazy<Color> DarkOliveGreen		{ get; set; } = new Lazy<Color>(() => Color.FromArgb( 255, 85, 107, 47 ));
	[JsonConverter( typeof( LazyConverterFactory ) )]
	public Lazy<Color> OrangeEntry			{ get; set; } = new Lazy<Color>(() => UIStorage.ToColor( UIStorage.TransformUIntToRGB( ColorNames.OrangeBlast ) ));
	[JsonConverter( typeof( LazyConverterFactory ) )]
	public Lazy<Color> BlueEntry			{ get; set; } = new Lazy<Color>(() => UIStorage.ToColor( UIStorage.TransformUIntToRGB( ColorNames.BluishWater ) ));
}
