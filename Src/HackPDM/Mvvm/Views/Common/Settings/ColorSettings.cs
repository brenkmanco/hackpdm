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

/// <summary>The status colours, saved with the app settings so a user can change them.</summary>
public class ColorSettings
{
	public Color White			{ get; set; } = Color.FromArgb( 255, 255, 255, 255 );
	public Color Black			{ get; set; } = Color.FromArgb( 255, 0, 0, 0 );
	public Color LightGray		{ get; set; } = Color.FromArgb( 255, 211, 211, 211 );
	public Color Gray			{ get; set; } = Color.FromArgb( 255, 128, 128, 128 );
	public Color MustardYellow	{ get; set; } = Color.FromArgb( 255, 150, 150, 0 );
	public Color DarkGray		{ get; set; } = Color.FromArgb( 255, 64, 64, 64 );
	public Color DarkRed		{ get; set; } = Color.FromArgb( 255, 139, 0, 0 );
	public Color DarkBlue		{ get; set; } = Color.FromArgb( 255, 0, 0, 139 );
	public Color DarkOliveGreen	{ get; set; } = Color.FromArgb( 255, 85, 107, 47 );
	public Color OrangeEntry	{ get; set; } = UIStorage.ToColor( UIStorage.TransformUIntToRGB( ColorNames.OrangeBlast ) );
	public Color BlueEntry		{ get; set; } = UIStorage.ToColor( UIStorage.TransformUIntToRGB( ColorNames.BluishWater ) );
}
