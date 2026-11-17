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
		{ "ProfileManager",		new () { Title = "Profile Manager",     PositionAndSize = (0, 0, 660, 440) }},
		{ "OdooSettings",			new () { Title = "Odoo Settings",		PositionAndSize = (0, 0, 620, 520) }},
		{ "HackSettings",			new () { Title = "Hack Settings",		PositionAndSize = (0, 0, 560, 320) }},
		{ "DynamicJsonForm",      new () { Title = "Json Editor",			PositionAndSize = (0, 0, 1440, 860) }},
		{ "ConfigSettings",				new () { Title = "Configuration Settings", PositionAndSize = (0, 0, 820, 780) }},
		{ "HackFileManager",		new () { Title = "Hack File Manager",	PositionAndSize = (0, 0, 1480, 880) }},
		{ "DependencyGraph",		new () { Title = "Dependency Graph",	PositionAndSize = (0, 0, 1480, 880) }},
		{ "NotLoggedIn",			new () { Title = "Not Logged In",		PositionAndSize = (0, 0, 900, 560) }},
		{ nameof(MessageBox),			new () { Title = "Info",				PositionAndSize = (0, 0, 450, 240), WindowKind = AppWindowPresenterKind.CompactOverlay }},
		{ nameof(StatusDialogView),		new () { Title = "Status",				PositionAndSize = (0, 0, 1180, 700) }},
		{ nameof(SearchOdooView),		new () { Title = "Search Files",		PositionAndSize = (0, 0, 1160, 760) }},
		{ nameof(ApplicationSettingsView), new () { Title = "Settings",			PositionAndSize = (0, 0, 960, 680) }},
		{ "AppSettings",				new () { Title = "Settings",				PositionAndSize = (0, 0, 1280, 820) }},
		{ "TemplateWindow",				new () { Title = "Template Window",		PositionAndSize = (0, 0, 500, 300) }},
	};
}
