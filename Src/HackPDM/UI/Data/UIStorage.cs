using System.Drawing;

using HackPDM.Shared.GlobalData;
using HackPDM.UI.Forms.Helper;
using HackPDM.UI.Types;

using Microsoft.UI.Xaml.Media;
using Color = Windows.UI.Color;

namespace HackPDM.UI.Data;

public static class UIStorage
{
	#region Color Settings
		public static readonly Color White              = Color.FromArgb(255, 255, 255, 255);
		public static readonly Color Black              = Color.FromArgb(255, 0, 0, 0);
		public static readonly Color LightGray			= Color.FromArgb(255, 211, 211, 211);
		public static readonly Color Gray               = Color.FromArgb(255, 128, 128, 128);
		public static readonly Color MustardYellow		= Color.FromArgb(255, 150, 150, 0);
		public static readonly Color DarkGray			= Color.FromArgb(255, 64, 64, 64);
		public static readonly Color DarkRed			= Color.FromArgb(255, 139, 0, 0);
		public static readonly Color DarkBlue		    = Color.FromArgb(255, 0, 0, 139);
		public static readonly Color DarkOliveGreen		= Color.FromArgb(255, 85, 107, 47);
		public static readonly Color OrangeEntry		= ToColor(TransformUIntToRGB(ColorNames.OrangeBlast));
		public static readonly Color BlueEntry			= ToColor(TransformUIntToRGB(ColorNames.BluishWater));

		public static readonly SolidColorBrush BrushWhite				= new(White);
		public static readonly SolidColorBrush BrushBlack				= new(Black);
		public static readonly SolidColorBrush BrushLightGray			= new(LightGray);
		public static readonly SolidColorBrush BrushGray				= new(Gray);
		public static readonly SolidColorBrush BrushMustardYellow		= new(MustardYellow);
		public static readonly SolidColorBrush BrushDarkGray			= new(DarkGray);
		public static readonly SolidColorBrush BrushDarkOliveGreen		= new(DarkOliveGreen);
		public static readonly SolidColorBrush BrushDarkBlue			= new(DarkBlue);
		public static readonly SolidColorBrush BrushDarkRed				= new(DarkRed);

		public static readonly LinearGradientBrush OrangeBrush = FormHelper.EZGradient(
			FlowDirection.LeftToRight, 
			[OrangeEntry.ModifyColor(40), OrangeEntry.ModifyColor(10), OrangeEntry.ModifyColor(0)]);

		public static readonly LinearGradientBrush BlueBrush = FormHelper.EZGradient(
			FlowDirection.LeftToRight,
			[BlueEntry.ModifyColor(40), BlueEntry.ModifyColor(10), BlueEntry.ModifyColor(0)]);

		public static readonly LinearGradientBrush RedBrush = FormHelper.EZGradient(
			FlowDirection.LeftToRight,
			[DarkRed.ModifyColor(55), DarkRed.ModifyColor(10), DarkRed.ModifyColor(0)]);		

		public static readonly LinearGradientBrush GreenBrush = FormHelper.EZGradient(
			FlowDirection.LeftToRight,
			[DarkOliveGreen.ModifyColor(40), DarkOliveGreen.ModifyColor(10), DarkOliveGreen.ModifyColor(0)]);	
	//public static readonly LinearGradientBrush LinGradOrange		= FormHelper.EZGradient(FlowDirection.LeftToRight, [Colo]);
	#endregion
	private static Color ToColor((byte, byte, byte, byte) argb) => Color.FromArgb(argb.Item1, argb.Item2, argb.Item3, argb.Item4);
	private static (byte, byte, byte, byte)  TransformUIntToRGB(ColorNames names)
		=> (
				(byte)(((uint)names >> 24) & 0xFF),
				(byte)(((uint)names >> 16) & 0xFF),
				(byte)(((uint)names >> 8) & 0xFF),
				(byte)(((uint)names) & 0xFF)
		);
	
}