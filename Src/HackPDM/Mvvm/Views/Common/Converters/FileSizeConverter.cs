using System;
using HackPDM.Shared.GlobalData;
using Microsoft.UI.Xaml.Data;

namespace HackPDM.Mvvm.Views.Common.Converters
{
    /// <summary>
    /// Value converter for formatting numeric byte quantities into human-readable strings (B, KB, MB, GB, TB).
    /// </summary>
    public partial class FileSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value is long bytesize ? ByteSize.Format(bytesize, "   ") : value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return value;
        }		
    }
}
