using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KusakaFactory.Declavatar.Runtime.Data
{
    public abstract class ExportItem
    {
        public sealed class GateExport : ExportItem
        {
            public string Name { get; set; }
        }

        public sealed class GuardExport : ExportItem
        {
            public string Gate { get; set; }
            public string Parameter { get; set; }
        }
    }

    public static partial class Converters
    {
        public sealed class ExportItemConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(ExportItem);
            }

            public override object ReadJson(
                JsonReader reader,
                Type objectType,
                object existingValue,
                JsonSerializer serializer
            )
            {
                var obj = JObject.Load(reader) as JToken;

                var contentObject = obj["content"] as JObject;
                switch (contentObject["type"].Value<string>())
                {
                    case "Gate": return contentObject.ToObject<ExportItem.GateExport>(serializer);
                    case "Guard": return contentObject.ToObject<ExportItem.GuardExport>(serializer);
                    default: throw new JsonException("invalid export type");
                }
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }
        }
    }
}
