using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace KusakaFactory.Declavatar.Runtime.Data
{
    [JsonConverter(typeof(Converters.ExportItemConverter))]
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
                Debug.Log(contentObject);
                switch (obj["type"].Value<string>())
                {
                    case "Gate": return new ExportItem.GateExport { Name = contentObject["name"].Value<string>() };
                    case "Guard": return new ExportItem.GuardExport { Gate = contentObject["gate"].Value<string>(), Parameter = contentObject["parameter"].Value<string>() };
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
