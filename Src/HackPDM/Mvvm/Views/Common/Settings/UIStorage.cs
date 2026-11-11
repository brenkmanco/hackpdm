using System;
using System.Collections.Generic;
using HackPDM.Shared.GlobalData;
using HackPDM.Mvvm.Views.Common.Controls;
using HackPDM.UI.Types;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using FlowDirection = HackPDM.Shared.GlobalData.FlowDirection;

namespace HackPDM.Mvvm.Views.Common.Settings;

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
	public static GlobalSerializedData GlobalData
	{
		get => _globalData ??= AppConfigStore.Load();
		set => _globalData = value;
	}
	public static ColorSettings ColorData => GlobalData.EColors;
	public static OdooEntryColumns ColumnsInfo => GlobalData.ColumnsSettings;
	public static Dictionary<string, OdooEntryColumnID?> Columns => GlobalData.ColumnsSettings.Columns;
	#endregion

	public static Color ToColor((byte, byte, byte, byte) argb) => Color.FromArgb(argb.Item1, argb.Item2, argb.Item3, argb.Item4);
	public static (byte, byte, byte, byte) TransformUIntToRGB(ColorNames names)
		=> (
				(byte)(((uint)names >> 24) & 0xFF),
				(byte)(((uint)names >> 16) & 0xFF),
				(byte)(((uint)names >> 8) & 0xFF),
				(byte)(((uint)names) & 0xFF)
		);
}
