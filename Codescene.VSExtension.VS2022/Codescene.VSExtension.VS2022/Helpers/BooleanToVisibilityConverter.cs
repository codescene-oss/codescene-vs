﻿using System;
using System.Windows;
using System.Windows.Data;

namespace Codescene.VSExtension.VS2022.Helpers;

public class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        bool isVisible = (bool)value;
        bool isInverted = parameter != null && System.Convert.ToBoolean(parameter);
        return (isVisible ^ isInverted) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
