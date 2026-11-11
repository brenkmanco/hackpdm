using System;
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
            if (value is long bytesize) 
            {
                return bytesize switch 
                { 
                    < 1024 => $"{bytesize}     B",
                    < 1048576 => $"{bytesize / 1024f:.##}   KB",
                    < 1073741824 => $"{bytesize / 1048576f:.##}   MB",
                    < 1099511627776 => $"{bytesize / 1073741824f:.##}   GB",
                    <= 1125899906842624 => $"{bytesize / 1099511627776f:.##}   TB",
                    _ => $"{bytesize}     B",
                };
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return value;
        }		
    }
}
