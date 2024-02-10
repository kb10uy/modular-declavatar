using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KusakaFactory.Declavatar.Runtime.Data
{
    public sealed class Attachment
    {
        public string Name { get; set; }
        public List<AttachmentProperty> Properties { get; set; }
    }

    public sealed class AttachmentProperty
    {
        public string Name { get; set; }
        public List<AttachmentValue> Parameters { get; set; }
        public Dictionary<string, AttachmentValue> Keywords { get; set; }
    }

    [JsonConverter(typeof(Converters.AttachmentValueConverter))]
    public sealed class AttachmentValue
    {
        public string Type { get; set; }
        public object Content { get; set; }
    }

    internal static partial class Converters
    {
        internal sealed class AttachmentValueConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(AttachmentValue);
            }

            public override object ReadJson(
                JsonReader reader,
                Type objectType,
                object existingValue,
                JsonSerializer serializer
            )
            {
                var obj = JObject.Load(reader) as JToken;

                var contentToken = obj["content"];
                switch (obj["type"].Value<string>())
                {
                    case "Null": return new AttachmentValue { Type = "Null", Content = null };
                    case "Boolean": return new AttachmentValue { Type = "Boolean", Content = contentToken.Value<bool>() };
                    case "Integer": return new AttachmentValue { Type = "Integer", Content = contentToken.Value<int>() };
                    case "Float": return new AttachmentValue { Type = "Float", Content = contentToken.Value<float>() };
                    case "String": return new AttachmentValue { Type = "String", Content = contentToken.Value<string>() };
                    case "Vector": return new AttachmentValue { Type = "Vector", Content = contentToken.Values<float>().ToList() };
                    case "GameObject": return new AttachmentValue { Type = "GameObject", Content = contentToken.Value<string>() };
                    case "Material": return new AttachmentValue { Type = "Material", Content = contentToken.Value<string>() };
                    case "AnimationClip": return new AttachmentValue { Type = "AnimationClip", Content = contentToken.Value<string>() };

                    case "List":
                    case "Tuple":
                        var values = contentToken.ToArray().Select((t) => t.ToObject<AttachmentValue>(serializer)).ToList();
                        return new AttachmentValue { Type = "List", Content = values };

                    default: throw new JsonException("invalid attachment value type");
                }
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }
        }
    }
}
