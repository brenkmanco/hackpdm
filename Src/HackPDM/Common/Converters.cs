using System;
using System.Collections.Generic;
using System.Globalization;

using HackPDM.Core.Odoo;
using HackPDM.Core.Pdm;
using HackPDM.Core.Theming;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

using Windows.Foundation;
using Windows.UI;

namespace HackPDM.Common;

public sealed partial class FileSizeConverter : IValueConverter
{
	public object Convert( object value, Type targetType, object parameter, string language ) => value switch
	{
		long bytes => ByteSize.Format( bytes, " " ),
		int bytes => ByteSize.Format( bytes, " " ),
		null => string.Empty,
		_ => value,
	};

	public object ConvertBack( object value, Type targetType, object parameter, string language ) => value;
}

public sealed partial class LocalDateTimeConverter : IValueConverter
{
	public object Convert( object? value, Type targetType, object? parameter, string language )
	{
		if( value is not DateTime dt || dt.Year <= 1 )
			return string.Empty;

		DateTime local = dt.Kind switch
		{
			DateTimeKind.Local => dt,
			DateTimeKind.Utc => dt.ToLocalTime(),
			_ => DateTime.SpecifyKind( dt, DateTimeKind.Utc ).ToLocalTime(),
		};
		return local.ToString( "g", CultureInfo.CurrentCulture );
	}

	public object? ConvertBack( object? value, Type targetType, object? parameter, string language ) => value;
}

public sealed partial class OperatorConverter : IValueConverter
{
	public object Convert( object? value, Type targetType, object? parameter, string language )
		=> value is Operators op ? OperatorNames.ToOdoo( op ) : string.Empty;

	public object ConvertBack( object? value, Type targetType, object? parameter, string language )
		=> throw new NotSupportedException();
}

/// <summary>"#AARRGGBB" or "#RRGGBB" to a cached SolidColorBrush.</summary>
public sealed partial class HexToBrushConverter : IValueConverter
{
	private const int MaxCacheEntries = 256;
	private static readonly Dictionary<uint, SolidColorBrush> BrushCache = new();

	public object Convert( object value, Type targetType, object parameter, string language )
	{
		Color c = ToColor( value as string );
		uint key = ( (uint)c.A << 24 ) | ( (uint)c.R << 16 ) | ( (uint)c.G << 8 ) | c.B;
		if( BrushCache.TryGetValue( key, out SolidColorBrush? cached ) )
			return cached;
		SolidColorBrush brush = new( c );
		if( BrushCache.Count < MaxCacheEntries )
			BrushCache[ key ] = brush;
		return brush;
	}

	public object ConvertBack( object value, Type targetType, object parameter, string language )
		=> throw new NotSupportedException();

	public static Color ToColor( string? hex )
		=> ArgbColor.TryParse( hex, out ArgbColor c ) ? c.ToUiColor() : default;
}

/// <summary>true -> Visible; pass "Invert" as ConverterParameter for the opposite.</summary>
public sealed partial class BoolToVisibilityConverter : IValueConverter
{
	public object Convert( object value, Type targetType, object parameter, string language )
		=> ( value is true ) ^ ( parameter as string == "Invert" ) ? Visibility.Visible : Visibility.Collapsed;

	public object ConvertBack( object value, Type targetType, object parameter, string language )
		=> throw new NotSupportedException();
}

/// <summary>Cached status row tint and foreground brushes adapting to theme and saved color settings.</summary>
public static class StatusBrushes
{
	private const int StatusCount = (int)FileStatus.Unknown + 1;
	private static readonly Brush?[] TableRowCache = new Brush?[ StatusCount ];
	private static readonly Brush?[] GroupedRowCache = new Brush?[ StatusCount ];
	private static readonly SolidColorBrush?[] TextCache = new SolidColorBrush?[ StatusCount ];
	private static bool _cachedDark;
	private static bool _themeSubscribed;

	private static bool IsDarkTheme => AppComposition.Theme.IsDarkActive;

	public static void InvalidateCache()
	{
		Array.Clear( TableRowCache );
		Array.Clear( GroupedRowCache );
		Array.Clear( TextCache );
	}

	private static void EnsureFresh()
	{
		if( !_themeSubscribed )
		{
			_themeSubscribed = true;
			AppComposition.Theme.ThemeChanged += InvalidateCache;
		}
		bool dark = IsDarkTheme;
		if( dark != _cachedDark )
		{
			_cachedDark = dark;
			InvalidateCache();
		}
	}

	public static Brush? ForTableRow( FileStatus status ) => GetRowGradient( status, doublePainted: true );
	public static Brush? ForGroupedRow( FileStatus status ) => GetRowGradient( status, doublePainted: false );

	public static SolidColorBrush? TextBrush( FileStatus status )
	{
		EnsureFresh();
		int idx = (int)status;
		if( (uint)idx >= StatusCount )
			return null;
		if( TextCache[ idx ] is { } cached )
			return cached;

		bool dark = _cachedDark;
		Color? color = status switch
		{
			FileStatus.Lo => dark ? Color.FromArgb( 255, 0xF5, 0xB9, 0x71 ) : Color.FromArgb( 255, 0xB8, 0x5D, 0x00 ),
			FileStatus.Ro => dark ? Color.FromArgb( 255, 0x79, 0xC8, 0xE8 ) : Color.FromArgb( 255, 0x0B, 0x67, 0xA3 ),
			FileStatus.Dt or FileStatus.Ds => dark ? Color.FromArgb( 255, 0xF4, 0x7B, 0x7B ) : Color.FromArgb( 255, 0xB8, 0x1D, 0x1D ),
			FileStatus.Cm => dark ? Color.FromArgb( 255, 0x7C, 0xD6, 0x7C ) : Color.FromArgb( 255, 0x24, 0x74, 0x24 ),
			FileStatus.Co => dark ? Color.FromArgb( 255, 0xA4, 0xD1, 0x6F ) : Color.FromArgb( 255, 0x4A, 0x6B, 0x1B ),
			FileStatus.Lm => dark ? Color.FromArgb( 255, 0xE8, 0xC8, 0x5A ) : Color.FromArgb( 255, 0x99, 0x6D, 0x00 ),
			FileStatus.Nv => dark ? Color.FromArgb( 255, 0xB4, 0x9A, 0xF7 ) : Color.FromArgb( 255, 0x5B, 0x3D, 0xB8 ),
			_ => null,
		};

		return color is { } c ? TextCache[ idx ] = new SolidColorBrush( c ) : null;
	}

	private static Brush? GetRowGradient( FileStatus status, bool doublePainted )
	{
		EnsureFresh();
		int idx = (int)status;
		if( (uint)idx >= StatusCount )
			return null;

		Brush?[] cache = doublePainted ? TableRowCache : GroupedRowCache;
		if( cache[ idx ] is { } cached )
			return cached;

		var colors = UIStorage.ColorData;
		(Color Base, byte StartAlpha, byte MidAlpha)? spec = status switch
		{
			FileStatus.Lo => ( colors.OrangeEntry.ToUiColor(), doublePainted ? (byte)28 : (byte)52, doublePainted ? (byte)8 : (byte)14 ),
			FileStatus.Ro => ( colors.BlueEntry.ToUiColor(), doublePainted ? (byte)28 : (byte)52, doublePainted ? (byte)8 : (byte)14 ),
			FileStatus.Dt or FileStatus.Ds => ( colors.DarkRed.ToUiColor(), doublePainted ? (byte)42 : (byte)76, doublePainted ? (byte)12 : (byte)22 ),
			FileStatus.Cm or FileStatus.Co => ( colors.DarkOliveGreen.ToUiColor(), doublePainted ? (byte)38 : (byte)68, doublePainted ? (byte)10 : (byte)18 ),
			FileStatus.Lm => ( colors.MustardYellow.ToUiColor(), doublePainted ? (byte)26 : (byte)46, doublePainted ? (byte)7 : (byte)12 ),
			FileStatus.Nv => ( Color.FromArgb( 255, 0x7A, 0x5A, 0xD6 ), doublePainted ? (byte)26 : (byte)46, doublePainted ? (byte)7 : (byte)12 ),
			_ => null,
		};

		if( spec is not var (baseColor, startAlpha, midAlpha) )
			return null;

		LinearGradientBrush brush = new()
		{
			StartPoint = new Point( 0, 0.5 ),
			EndPoint = new Point( 1, 0.5 ),
		};
		brush.GradientStops.Add( new GradientStop { Color = Color.FromArgb( startAlpha, baseColor.R, baseColor.G, baseColor.B ), Offset = 0.0 } );
		brush.GradientStops.Add( new GradientStop { Color = Color.FromArgb( midAlpha, baseColor.R, baseColor.G, baseColor.B ), Offset = 0.55 } );
		brush.GradientStops.Add( new GradientStop { Color = Color.FromArgb( 0, baseColor.R, baseColor.G, baseColor.B ), Offset = 1.0 } );
		return cache[ idx ] = brush;
	}
}

public sealed partial class StatusRowBackgroundConverter : IValueConverter
{
	public object? Convert( object? value, Type targetType, object? parameter, string language )
		=> value is FileStatus status ? StatusBrushes.ForGroupedRow( status ) : null;
	public object ConvertBack( object? value, Type targetType, object? parameter, string language ) => throw new NotSupportedException();
}

public sealed partial class StatusTextBrushConverter : IValueConverter
{
	public object Convert( object? value, Type targetType, object? parameter, string language )
		=> value is FileStatus status && StatusBrushes.TextBrush( status ) is { } brush
			? brush
			: DependencyProperty.UnsetValue;
	public object ConvertBack( object? value, Type targetType, object? parameter, string language ) => throw new NotSupportedException();
}
