using System;
using System.ComponentModel;
using System.Globalization;

namespace Nyris.Crdt.Distributed.Model.Converters
{
    internal class InternalIdTypeConverter<TInternalId, TFactory> : TypeConverter
        where TFactory : IFactory<TInternalId>, new()
        where TInternalId : struct, IFormattable
    {
        private static readonly TFactory Factory = new();

        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
            => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

        public override object? ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string stringValue && !string.IsNullOrEmpty(stringValue))
            {
                return Factory.Parse(stringValue);
            }

            return base.ConvertFrom(context, culture, value);
        }
    }
}