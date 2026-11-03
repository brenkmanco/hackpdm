using System;
using System.Collections.Generic;
using HackPDM.Shared.GlobalData;
using HackPDM.UI.Forms.Helper;
using HackPDM.UI.Types;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using FlowDirection = HackPDM.Shared.GlobalData.FlowDirection;

namespace HackPDM.UI.Data;

public static class UIStorage
{
	#region Color Settings

	public static readonly Lazy<SolidColorBrush> BrushWhite = new(() => new(ColorData.White.Value));
	public static readonly Lazy<SolidColorBrush> BrushBlack = new(() => new(ColorData.Black.Value));
	public static readonly Lazy<SolidColorBrush> BrushLightGray = new(() => new(ColorData.LightGray.Value));
	public static readonly Lazy<SolidColorBrush> BrushGray = new(() => new(ColorData.Gray.Value));
	public static readonly Lazy<SolidColorBrush> BrushMustardYellow = new(() => new(ColorData.MustardYellow.Value));
	public static readonly Lazy<SolidColorBrush> BrushDarkGray = new(() => new(ColorData.DarkGray.Value));
	public static readonly Lazy<SolidColorBrush> BrushDarkOliveGreen = new(() => new(ColorData.DarkOliveGreen.Value));
	public static readonly Lazy<SolidColorBrush> BrushDarkBlue = new(() => new(ColorData.DarkBlue.Value));
	public static readonly Lazy<SolidColorBrush> BrushDarkRed = new(() => new(ColorData.DarkRed.Value));

	public static readonly Lazy<LinearGradientBrush> OrangeBrush = new(() => FormHelper.EZGradient(
		FlowDirection.LeftToRight,
		[GlobalData.EColors.OrangeEntry.Value.ModifyColor(40), GlobalData.EColors.OrangeEntry.Value.ModifyColor(10), GlobalData.EColors.OrangeEntry.Value.ModifyColor(0)]));

	public static readonly Lazy<LinearGradientBrush> BlueBrush = new(() => FormHelper.EZGradient(
		FlowDirection.LeftToRight,
		[GlobalData.EColors.BlueEntry.Value.ModifyColor(40), GlobalData.EColors.BlueEntry.Value.ModifyColor(10), GlobalData.EColors.BlueEntry.Value.ModifyColor(0)]));

	public static readonly Lazy<LinearGradientBrush> RedBrush = new(() => FormHelper.EZGradient(
		FlowDirection.LeftToRight,
		[GlobalData.EColors.DarkRed.Value.ModifyColor(55), GlobalData.EColors.DarkRed.Value.ModifyColor(10), GlobalData.EColors.DarkRed.Value.ModifyColor(0)]));

	public static readonly Lazy<LinearGradientBrush> GreenBrush = new(() => FormHelper.EZGradient(
		FlowDirection.LeftToRight,
		[GlobalData.EColors.DarkOliveGreen.Value.ModifyColor(40), GlobalData.EColors.DarkOliveGreen.Value.ModifyColor(10), GlobalData.EColors.DarkOliveGreen.Value.ModifyColor(0)]));

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
