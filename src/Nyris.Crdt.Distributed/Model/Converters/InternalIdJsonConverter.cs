using Newtonsoft.Json;
using System;

namespace Nyris.Crdt.Distributed.Model.Converters
{
    public sealed class InternalIdJsonConverter<TInternalId, TFactory> : JsonConverter<TInternalId>
        where TFactory : IFactory<TInternalId>, new()
        where TInternalId : IFormattable
    {
        private static readonly TFactory Factory = new();

        public override void WriteJson(JsonWriter writer, TInternalId? value, JsonSerializer serializer)
        {
            if (value != null) writer.WriteValue(value.ToString());
        }

        public override TInternalId ReadJson(
            JsonReader reader,
            Type objectType,
            TInternalId? existingValue,
            bool hasExistingValue,
            JsonSerializer serializer
        )
        {
            if (reader.ValueType != typeof(string))
                throw new FormatException($"{GetType().Name} can be only deserialized from string");

            var value = (string?) reader.Value;

            return string.IsNullOrEmpty(value) ? Factory.Empty : Factory.Parse(value);
        }
    }
}
