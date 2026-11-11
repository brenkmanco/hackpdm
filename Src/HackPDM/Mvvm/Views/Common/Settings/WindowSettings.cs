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
using HackPDM.Mvvm.Views.Dialogs;
using HackPDM.UI.Types;

using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media;
using HackPDM.Mvvm.Views.Settings;
using HackPDM.Mvvm.Views.Odoo;
using HackPDM.Mvvm.Views.Hack;
using HackPDM.Mvvm.Views.Tools;

using Color = Windows.UI.Color;
using EntryRow = HackPDM.Mvvm.Models.Hack.EntryRow;

namespace HackPDM.Mvvm.Views.Common.Settings;

public class WindowSettings
{
	// keys are saved in the settings file: they keep the original page names so saved layouts still apply
	[JsonPropertyName( "Windows" )]
	public Dictionary<string, WindowConfig> PresetWindowConfig { get; set; } = new ()
	{
		{ "ProfileManager",		new () { Title = "Profile Manager",     PositionAndSize = (0, 0, 500, 350) }},
		{ "OdooSettings",			new () { Title = "Odoo Settings",		PositionAndSize = (0, 0, 600, 500) }},
		{ "HackSettings",			new () { Title = "Hack Settings",		PositionAndSize = (0, 0, 500, 500) }},
		{ "DynamicJsonForm",      new () { Title = "Json Editor",			PositionAndSize = (0, 0, 1600, 900) }},
		{ "ConfigSettings",				new () { Title = "Configuration Settings", PositionAndSize = (0, 0, 700, 400) }},
		{ "HackFileManager",		new () { Title = "Hack File Manager",	PositionAndSize = (0, 0, 1600, 900) }},
		{ "NotLoggedIn",			new () { Title = "Not Logged In",		PositionAndSize = (0, 0, 1600, 900) }},
		{ nameof(MessageBox),			new () { Title = "Info",				PositionAndSize = (0, 0, 450, 250), WindowKind = AppWindowPresenterKind.CompactOverlay }},
		{ "TemplateWindow",				new () { Title = "Template Window",		PositionAndSize = (0, 0, 500, 300) }},
	};
}
