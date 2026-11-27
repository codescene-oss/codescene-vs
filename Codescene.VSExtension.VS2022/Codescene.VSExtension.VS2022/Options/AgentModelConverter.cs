// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Codescene.VSExtension.Core.Application;

namespace Codescene.VSExtension.VS2022.Options
{
    public class AgentModelConverter : StringConverter
    {
        public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
        {
            return true;
        }

        public override bool GetStandardValuesExclusive(ITypeDescriptorContext context)
        {
            return true;
        }

        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
        {
            return new StandardValuesCollection(AgentModels.AllIds.ToArray());
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is string id)
            {
                return AgentModels.DisplayText(id);
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string text)
            {
                return AgentModels.IdFromDisplayText(text);
            }

            return base.ConvertFrom(context, culture, value);
        }
    }
}
