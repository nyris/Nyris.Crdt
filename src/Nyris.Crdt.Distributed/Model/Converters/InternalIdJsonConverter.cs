using System;
using Newtonsoft.Json;

namespace Nyris.Crdt.Distributed.Model.Converters
{
    internal class InternalIdJsonConverter<TInternalId, TFactory> : JsonConverter<TInternalId>
        where TFactory : IFactory<TInternalId>, new()
        where TInternalId : struct, IFormattable
    {
        private static readonly TFactory Factory = new();

        public override void WriteJson(JsonWriter writer, TInternalId value, JsonSerializer serializer)
            => writer.WriteValue(value.ToString());

        public override TInternalId ReadJson(JsonReader reader, Type objectType, TInternalId existingValue, bool hasExistingValue,
            JsonSerializer serializer)
        {
            if (reader.ValueType != typeof(string))
                throw new FormatException($"{GetType().Name} can be only deserialized from string");

            var value = (string?) reader.Value;

            return string.IsNullOrEmpty(value) ? Factory.Empty : Factory.Parse(value);
        }
    }
}