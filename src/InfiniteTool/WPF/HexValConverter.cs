using Avalonia.Data.Converters;
using System;

namespace InfiniteTool.WPF
{
    public class HexValConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return $"0x{value:x}";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if(value is string stringVal)
            {
                if (targetType == typeof(uint))
                {
                    return System.Convert.ToInt32(stringVal, 16);
                }
                else if (targetType == typeof(uint))
                {
                    return System.Convert.ToUInt32(stringVal, 16);
                }
                else if (targetType == typeof(short))
                {
                    return System.Convert.ToUInt16(stringVal, 16);
                }
                else if (targetType == typeof(ushort))
                {
                    return System.Convert.ToUInt16(stringVal, 16);
                }
                else if (targetType == typeof(long))
                {
                    return System.Convert.ToInt64(stringVal, 16);
                }
                else if(targetType == typeof(ulong))
                {
                    return System.Convert.ToUInt64(stringVal, 16);
                }
                else if (targetType == typeof(byte))
                {
                    return System.Convert.ToByte(stringVal, 16);
                }
            }

            return System.Convert.ChangeType(value, targetType);
        }
    }
}
