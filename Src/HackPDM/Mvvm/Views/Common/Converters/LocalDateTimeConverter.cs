using System;
using System.Globalization;

using Microsoft.UI.Xaml.Data;

namespace HackPDM.Mvvm.Views.Common.Converters;

/// <summary>
/// Converts UTC (or Odoo's Unspecified-UTC) <see cref="DateTime"/> values into local short date/time text.
/// </summary>
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

	public object? ConvertBack( object? value, Type targetType, object? parameter, string language )
		=> value;
}
