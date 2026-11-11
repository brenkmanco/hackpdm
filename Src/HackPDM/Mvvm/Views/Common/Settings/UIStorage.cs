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

	public static readonly Lazy<SolidColorBrush> BrushWhite = new(() => new(ColorData.White.Value));
	public static readonly Lazy<SolidColorBrush> BrushBlack = new(() => new(ColorData.Black.Value));
	public static readonly Lazy<SolidColorBrush> BrushMustardYellow = new(() => new(ColorData.MustardYellow.Value));
	public static readonly Lazy<SolidColorBrush> BrushDarkGray = new(() => new(ColorData.DarkGray.Value));
	public static readonly Lazy<SolidColorBrush> BrushDarkOliveGreen = new(() => new(ColorData.DarkOliveGreen.Value));
	public static readonly Lazy<SolidColorBrush> BrushDarkBlue = new(() => new(ColorData.DarkBlue.Value));
	public static readonly Lazy<SolidColorBrush> BrushDarkRed = new(() => new(ColorData.DarkRed.Value));





	#endregion

	#region AppSettings
	private static GlobalSerializedData? _globalData;
	public static GlobalSerializedData GlobalData
	{
		get => _globalData ??= GlobalSerializedData.LoadFromFile(GlobalSerializedData.FilePath ?? "");
		set => _globalData = value;
	}
	public static ColorSettings ColorData => GlobalData.EColors;
	public static OdooEntryColumns ColumnsInfo => GlobalData.ColumnsSettings;
	public static Lazy<Dictionary<string, OdooEntryColumnID?>> Columns => GlobalData.ColumnsSettings.Columns;
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
