using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace HarvestCraft2.TestClient.Utils
{
    /// <summary>
    /// XAML에서 사용할 수 있는 정적 컨버터 인스턴스들
    /// </summary>
    public static class Converters
    {
        public static readonly BooleanToVisibilityConverter BooleanToVisibilityConverter = new();
        public static readonly IsPositiveConverter IsPositiveConverter = new();
        public static readonly IsNegativeConverter IsNegativeConverter = new();
        public static readonly InverseBooleanConverter InverseBooleanConverter = new();
        public static readonly ConnectionStatusToColorConverter ConnectionStatusToColorConverter = new();
        public static readonly PriceChangeToColorConverter PriceChangeToColorConverter = new();
        public static readonly NumberFormatConverter NumberFormatConverter = new();
    }

    /// <summary>
    /// Boolean 값을 Visibility로 변환
    /// </summary>
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                // parameter가 "Inverse"인 경우 반대로 변환
                bool inverse = parameter?.ToString() == "Inverse";
                bool result = inverse ? !boolValue : boolValue;
                return result ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                bool result = visibility == Visibility.Visible;
                bool inverse = parameter?.ToString() == "Inverse";
                return inverse ? !result : result;
            }
            return false;
        }
    }

    /// <summary>
    /// 숫자가 양수인지 확인
    /// </summary>
    public class IsPositiveConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value switch
            {
                decimal decimalValue => decimalValue > 0,
                double doubleValue => doubleValue > 0,
                float floatValue => floatValue > 0,
                int intValue => intValue > 0,
                long longValue => longValue > 0,
                _ => false
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 숫자가 음수인지 확인
    /// </summary>
    public class IsNegativeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value switch
            {
                decimal decimalValue => decimalValue < 0,
                double doubleValue => doubleValue < 0,
                float floatValue => floatValue < 0,
                int intValue => intValue < 0,
                long longValue => longValue < 0,
                _ => false
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Boolean 값을 반전
    /// </summary>
    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return !boolValue;
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return !boolValue;
            return false;
        }
    }

    /// <summary>
    /// 연결 상태를 색상으로 변환
    /// </summary>
    public class ConnectionStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string status)
            {
                return status.ToLower() switch
                {
                    "connected" or "연결됨" => new SolidColorBrush(Color.FromRgb(76, 175, 80)), // 녹색
                    "connecting" or "연결중" => new SolidColorBrush(Color.FromRgb(255, 152, 0)), // 주황색
                    "disconnected" or "연결끊김" => new SolidColorBrush(Color.FromRgb(244, 67, 54)), // 빨간색
                    _ => new SolidColorBrush(Color.FromRgb(158, 158, 158)) // 회색
                };
            }
            return new SolidColorBrush(Color.FromRgb(158, 158, 158));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 가격 변동을 색상으로 변환
    /// </summary>
    public class PriceChangeToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal changePercent)
            {
                if (changePercent > 0)
                    return new SolidColorBrush(Color.FromRgb(76, 175, 80)); // 녹색 (상승)
                else if (changePercent < 0)
                    return new SolidColorBrush(Color.FromRgb(244, 67, 54)); // 빨간색 (하락)
                else
                    return new SolidColorBrush(Color.FromRgb(97, 97, 97)); // 회색 (변동없음)
            }
            return new SolidColorBrush(Color.FromRgb(97, 97, 97));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 숫자 포맷 변환 (K, M, B 단위)
    /// </summary>
    public class NumberFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal decimalValue)
            {
                return FormatNumber((double)decimalValue);
            }
            if (value is double doubleValue)
            {
                return FormatNumber(doubleValue);
            }
            if (value is int intValue)
            {
                return FormatNumber(intValue);
            }
            if (value is long longValue)
            {
                return FormatNumber(longValue);
            }

            return value?.ToString() ?? "0";
        }

        private static string FormatNumber(double number)
        {
            if (Math.Abs(number) >= 1_000_000_000)
                return (number / 1_000_000_000).ToString("F1") + "B";
            if (Math.Abs(number) >= 1_000_000)
                return (number / 1_000_000).ToString("F1") + "M";
            if (Math.Abs(number) >= 1_000)
                return (number / 1_000).ToString("F1") + "K";

            return number.ToString("F0");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Null 값을 체크하는 컨버터
    /// </summary>
    public class IsNullConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value == null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Null이 아닌 값을 체크하는 컨버터
    /// </summary>
    public class IsNotNullConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 문자열이 비어있는지 확인
    /// </summary>
    public class IsStringEmptyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.IsNullOrWhiteSpace(value?.ToString());
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 컬렉션이 비어있는지 확인
    /// </summary>
    public class IsCollectionEmptyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is System.Collections.ICollection collection)
                return collection.Count == 0;

            if (value is System.Collections.IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    return false; // 하나라도 있으면 비어있지 않음
                }
                return true; // 아무것도 없으면 비어있음
            }

            return true; // null이면 비어있다고 간주
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// DateTime을 상대적 시간으로 변환 ("2분 전", "1시간 전" 등)
    /// </summary>
    public class RelativeTimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime dateTime)
            {
                var timeSpan = DateTime.Now - dateTime;

                if (timeSpan.TotalSeconds < 60)
                    return "방금 전";
                if (timeSpan.TotalMinutes < 60)
                    return $"{(int)timeSpan.TotalMinutes}분 전";
                if (timeSpan.TotalHours < 24)
                    return $"{(int)timeSpan.TotalHours}시간 전";
                if (timeSpan.TotalDays < 7)
                    return $"{(int)timeSpan.TotalDays}일 전";

                return dateTime.ToString("yyyy-MM-dd");
            }

            return value?.ToString() ?? "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}