using System;

using HackPDM.Mvvm.Models.Settings;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

using Color = Windows.UI.Color;

namespace HackPDM.Mvvm.Views.Common.Converters;

/// <summary>"#AARRGGBB" → a brush of that color (transparent if it doesn't parse).</summary>
public sealed partial class HexToBrushConverter : IValueConverter
{
	public object Convert( object value, Type targetType, object parameter, string language )
		=> new SolidColorBrush( ToColor( value as string ) );

	public object ConvertBack( object value, Type targetType, object parameter, string language )
		=> throw new NotSupportedException();

	public static Color ToColor( string? hex )
		=> ArgbColor.TryParse( hex, out var c ) ? Color.FromArgb( c.A, c.R, c.G, c.B ) : default;
}

/// <summary>true → Visible; pass "Invert" as the parameter for the opposite.</summary>
public sealed partial class BoolToVisibilityConverter : IValueConverter
{
	public object Convert( object value, Type targetType, object parameter, string language )
		=> ( value is true ) ^ ( parameter as string == "Invert" ) ? Visibility.Visible : Visibility.Collapsed;

	public object ConvertBack( object value, Type targetType, object parameter, string language )
		=> throw new NotSupportedException();
}
