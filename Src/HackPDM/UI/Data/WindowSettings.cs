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

public class WindowSettings
{
	[JsonPropertyName( "Windows" )]
	public Dictionary<string, WindowConfig> PresetWindowConfig { get; set; } = new ()
	{
		{ nameof(ProfileManager),		new () { Title = "Profile Manager",     PositionAndSize = (0, 0, 500, 350) }},
		{ nameof(OdooSettings),			new () { Title = "Odoo Settings",		PositionAndSize = (0, 0, 600, 500) }},
		{ nameof(HackSettings),			new () { Title = "Hack Settings",		PositionAndSize = (0, 0, 500, 500) }},
		{ nameof(DynamicJsonForm),      new () { Title = "Json Editor",			PositionAndSize = (0, 0, 1600, 900) }},
		{ "ConfigSettings",				new () { Title = "Configuration Settings", PositionAndSize = (0, 0, 700, 400) }},
		{ nameof(HackFileManager),		new () { Title = "Hack File Manager",	PositionAndSize = (0, 0, 1600, 900) }},
		{ nameof(NotLoggedIn),			new () { Title = "Not Logged In",		PositionAndSize = (0, 0, 1600, 900) }},
		{ nameof(MessageBox),			new () { Title = "Info",				PositionAndSize = (0, 0, 450, 250), WindowKind = AppWindowPresenterKind.CompactOverlay }},
		{ "TemplateWindow",				new () { Title = "Template Window",		PositionAndSize = (0, 0, 500, 300) }},
	};
}
