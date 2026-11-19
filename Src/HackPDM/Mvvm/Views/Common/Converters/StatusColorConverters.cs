using System;

using HackPDM.Mvvm.Views.Common.Settings;
using HackPDM.Shared.GlobalData;
using HackPDM.Shell;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

using Windows.Foundation;
using Windows.UI;

namespace HackPDM.Mvvm.Views.Common.Converters;

/// <summary>
/// Builds row background fade brushes and status text accent brushes for <see cref="FileStatus"/>,
/// honoring the saved palette in <see cref="UIStorage.ColorData"/>.
/// </summary>
public static class StatusBrushes
{
	/// <summary>
	/// Horizontal fade background brush for a <c>WinUI.TableView</c> row. Uses halved alpha stops
	/// because <c>TableViewRow</c> paints its background on both its outer border and row presenter.
	/// Returns <see langword="null"/> for neutral statuses (<see cref="FileStatus.Ok"/>, etc.).
	/// </summary>
	public static Brush? ForTableRow( FileStatus status ) => BuildRowGradient( status, doublePainted: true );

	/// <summary>
	/// Horizontal fade background brush for a single-painted container such as the grouped list row.
	/// Returns <see langword="null"/> for neutral statuses (<see cref="FileStatus.Ok"/>, etc.).
	/// </summary>
	public static Brush? ForGroupedRow( FileStatus status ) => BuildRowGradient( status, doublePainted: false );

	/// <summary>Accent foreground brush for status text, or <see langword="null"/> to keep the theme default.</summary>
	public static SolidColorBrush? TextBrush( FileStatus status )
	{
		bool dark = AppComposition.Theme.IsDarkActive;
		return status switch
		{
			FileStatus.Lo => new SolidColorBrush( dark ? Color.FromArgb( 255, 0xF5, 0xB9, 0x71 ) : Color.FromArgb( 255, 0xB8, 0x5D, 0x00 ) ),
			FileStatus.Ro => new SolidColorBrush( dark ? Color.FromArgb( 255, 0x79, 0xC8, 0xE8 ) : Color.FromArgb( 255, 0x0B, 0x67, 0xA3 ) ),
			FileStatus.Dt or FileStatus.Ds => new SolidColorBrush( dark ? Color.FromArgb( 255, 0xF4, 0x7B, 0x7B ) : Color.FromArgb( 255, 0xB8, 0x1D, 0x1D ) ),
			FileStatus.Cm => new SolidColorBrush( dark ? Color.FromArgb( 255, 0x7C, 0xD6, 0x7C ) : Color.FromArgb( 255, 0x24, 0x74, 0x24 ) ),
			FileStatus.Co => new SolidColorBrush( dark ? Color.FromArgb( 255, 0xA4, 0xD1, 0x6F ) : Color.FromArgb( 255, 0x4A, 0x6B, 0x1B ) ),
			FileStatus.Lm => new SolidColorBrush( dark ? Color.FromArgb( 255, 0xE8, 0xC8, 0x5A ) : Color.FromArgb( 255, 0x99, 0x6D, 0x00 ) ),
			FileStatus.Nv => new SolidColorBrush( dark ? Color.FromArgb( 255, 0xB4, 0x9A, 0xF7 ) : Color.FromArgb( 255, 0x5B, 0x3D, 0xB8 ) ),
			_ => null,
		};
	}

	private static LinearGradientBrush? BuildRowGradient( FileStatus status, bool doublePainted )
	{
		ColorSettings colors = UIStorage.ColorData;
		(Color Base, byte StartAlpha, byte MidAlpha)? spec = status switch
		{
			FileStatus.Lo => (colors.OrangeEntry, doublePainted ? (byte)28 : (byte)52, doublePainted ? (byte)8 : (byte)14),
			FileStatus.Ro => (colors.BlueEntry, doublePainted ? (byte)28 : (byte)52, doublePainted ? (byte)8 : (byte)14),
			FileStatus.Dt or FileStatus.Ds => (colors.DarkRed, doublePainted ? (byte)42 : (byte)76, doublePainted ? (byte)12 : (byte)22),
			FileStatus.Cm or FileStatus.Co => (colors.DarkOliveGreen, doublePainted ? (byte)38 : (byte)68, doublePainted ? (byte)10 : (byte)18),
			FileStatus.Lm => (colors.MustardYellow, doublePainted ? (byte)26 : (byte)46, doublePainted ? (byte)7 : (byte)12),
			FileStatus.Nv => (Color.FromArgb( 255, 0x7A, 0x5A, 0xD6 ), doublePainted ? (byte)26 : (byte)46, doublePainted ? (byte)7 : (byte)12),
			_ => null,
		};

		if( spec is not var (baseColor, startAlpha, midAlpha) )
			return null;

		LinearGradientBrush brush = new()
		{
			StartPoint = new Point( 0, 0.5 ),
			EndPoint = new Point( 1, 0.5 ),
		};
		brush.GradientStops.Add( new GradientStop { Color = WithAlpha( baseColor, startAlpha ), Offset = 0.0 } );
		brush.GradientStops.Add( new GradientStop { Color = WithAlpha( baseColor, midAlpha ), Offset = 0.55 } );
		brush.GradientStops.Add( new GradientStop { Color = WithAlpha( baseColor, 0 ), Offset = 1.0 } );
		return brush;
	}

	private static Color WithAlpha( Color c, byte alpha ) => Color.FromArgb( alpha, c.R, c.G, c.B );
}

/// <summary>Converts a <see cref="FileStatus"/> into its horizontal row tint brush for single-painted containers.</summary>
public partial class StatusRowBackgroundConverter : IValueConverter
{
	public object? Convert( object? value, Type targetType, object? parameter, string language )
		=> value is FileStatus status
			? StatusBrushes.ForGroupedRow( status )
			: null;

	public object ConvertBack( object? value, Type targetType, object? parameter, string language )
		=> throw new NotSupportedException();
}

/// <summary>Converts a <see cref="FileStatus"/> into an accent text brush (or <see cref="DependencyProperty.UnsetValue"/> for neutral statuses).</summary>
public partial class StatusTextBrushConverter : IValueConverter
{
	public object Convert( object? value, Type targetType, object? parameter, string language )
		=> value is FileStatus status && StatusBrushes.TextBrush( status ) is { } brush
			? brush
			: DependencyProperty.UnsetValue;

	public object ConvertBack( object? value, Type targetType, object? parameter, string language )
		=> throw new NotSupportedException();
}
