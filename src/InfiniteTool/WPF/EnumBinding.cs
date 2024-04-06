using Avalonia.Markup.Xaml;
using InfiniteTool.Extensions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace InfiniteTool.WPF
{
    public class EnumBinding : MarkupExtension
    {
        private Type enumType;
        private Dictionary<object, string> valueNames = new();

        public EnumBinding(Type enumType)
        {
            EnumType = enumType ?? throw new ArgumentNullException(nameof(enumType));
        }

        public Type EnumType
        {
            get { return enumType; }
            private set
            {
                if (this.enumType == value)
                    return;

                var enumType = Nullable.GetUnderlyingType(value) ?? value;

                if (enumType.IsEnum == false)
                    throw new ArgumentException("Type must be an Enum.");

                this.enumType = value;
                this.valueNames.Clear();
            }
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return Enum.GetValues(this.EnumType)
                .Cast<object>()
                .Select(e => new EnumerationMember
                {
                    Value = e,
                    Description = GetDescription(e)
                }).ToArray();
        }

        private string GetDescription(object enumValue)
        {
            if (enumValue == null) return string.Empty;

            if (this.valueNames.TryGetValue(enumValue, out var cachedVal))
                return cachedVal;

            if(Enum.IsDefined(EnumType, enumValue) 
                && enumValue.ToString() is string enumString
                && EnumType.GetField(enumString) is FieldInfo field)
            {
                var descriptionAttribute = field.GetCustomAttribute<DescriptionAttribute>(false);

                cachedVal = descriptionAttribute != null
                    ? descriptionAttribute.Description
                    : StringExtensions.DeCamelCase(enumString);

                this.valueNames[enumValue] = cachedVal;
                return cachedVal;
            }

            return string.Empty;
        }

        public class EnumerationMember
        {
            public string Description { get; set; }
            public object Value { get; set; }
        }
    }
}
