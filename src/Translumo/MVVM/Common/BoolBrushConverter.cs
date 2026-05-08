using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Translumo.MVVM.Common
{
    public class BoolBrushConverter : DependencyObject, IValueConverter
    {
        public static readonly DependencyProperty TrueBrushProperty = DependencyProperty.Register(
            nameof(TrueBrush),
            typeof(SolidColorBrush),
            typeof(BoolBrushConverter));

        public static readonly DependencyProperty FalseBrushProperty = DependencyProperty.Register(
            nameof(FalseBrush),
            typeof(SolidColorBrush),
            typeof(BoolBrushConverter));

        public SolidColorBrush TrueBrush
        {
            get => (SolidColorBrush)GetValue(TrueBrushProperty);
            set => SetValue(TrueBrushProperty, value);
        }

        public SolidColorBrush FalseBrush
        {
            get => (SolidColorBrush)GetValue(FalseBrushProperty);
            set => SetValue(FalseBrushProperty, value);
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var boolValue = (bool)value;

            return boolValue ? TrueBrush : FalseBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
