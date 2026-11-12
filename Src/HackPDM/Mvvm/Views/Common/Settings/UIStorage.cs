using System;
using System.Collections.Generic;
using HackPDM.Shared.GlobalData;
using HackPDM.Mvvm.Views.Common.Controls;
using HackPDM.UI.Types;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using FlowDirection = HackPDM.Shared.GlobalData.FlowDirection;

namespace HackPDM.Mvvm.Views.Common.Settings;

/// <summary>Process-wide access to the saved UI settings (AppConfig.json) and the entry-list colors.</summary>
public static class UIStorage
{
	#region Color Settings

	public static SolidColorBrush BrushWhite => new( ColorData.White );
	public static SolidColorBrush BrushBlack => new( ColorData.Black );
	public static SolidColorBrush BrushMustardYellow => new( ColorData.MustardYellow );
	public static SolidColorBrush BrushDarkGray => new( ColorData.DarkGray );
	public static SolidColorBrush BrushDarkOliveGreen => new( ColorData.DarkOliveGreen );
	public static SolidColorBrush BrushDarkBlue => new( ColorData.DarkBlue );
	public static SolidColorBrush BrushDarkRed => new( ColorData.DarkRed );





	#endregion

	#region AppSettings
	private static GlobalSerializedData? _globalData;
	/// <summary>The app's saved UI settings.</summary>
	/// <value>Loaded from AppConfig.json by <see cref="AppConfigStore.Load"/> on first read; setting it replaces the
	/// in-memory copy without saving.</value>
	public static GlobalSerializedData GlobalData
	{
		get => _globalData ??= AppConfigStore.Load();
		set => _globalData = value;
	}
	public static ColorSettings ColorData => GlobalData.EColors;
	public static OdooEntryColumns ColumnsInfo => GlobalData.ColumnsSettings;
	public static Dictionary<string, OdooEntryColumnID?> Columns => GlobalData.ColumnsSettings.Columns;
	#endregion

	/// <summary>Builds a UI color from its four channels.</summary>
	/// <param name="argb">The alpha, red, green and blue channels, in that order.</param>
	/// <returns>The color.</returns>
	public static Color ToColor((byte, byte, byte, byte) argb) => Color.FromArgb(argb.Item1, argb.Item2, argb.Item3, argb.Item4);
	/// <summary>Splits a named color's packed 0xAARRGGBB value into its channels.</summary>
	/// <param name="names">The named color.</param>
	/// <returns>The alpha, red, green and blue channels, in that order.</returns>
	public static (byte, byte, byte, byte) TransformUIntToRGB(ColorNames names)
		=> (
				(byte)(((uint)names >> 24) & 0xFF),
				(byte)(((uint)names >> 16) & 0xFF),
				(byte)(((uint)names >> 8) & 0xFF),
				(byte)(((uint)names) & 0xFF)
		);
}
