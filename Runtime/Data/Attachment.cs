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
                var valueType = obj["type"].Value<string>();
                var contentToken = obj["content"];

                var attachmentValue = new AttachmentValue { Type = valueType, Content = null };
                switch (valueType)
                {
                    case "Null":
                        break;
                    case "Boolean":
                        attachmentValue.Content = contentToken.Value<bool>();
                        break;
                    case "Integer":
                        attachmentValue.Content = contentToken.Value<int>();
                        break;
                    case "Float":
                        attachmentValue.Content = contentToken.Value<float>();
                        break;
                    case "String":
                        attachmentValue.Content = contentToken.Value<string>();
                        break;
                    case "Vector":
                        attachmentValue.Content = contentToken.Values<float>().ToList();
                        break;
                    case "GameObject":
                        attachmentValue.Content = contentToken.Value<string>();
                        break;
                    case "Material":
                        attachmentValue.Content = contentToken.Value<string>();
                        break;
                    case "AnimationClip":
                        attachmentValue.Content = contentToken.Value<string>();
                        break;

                    case "List":
                    case "Tuple":
                        attachmentValue.Content = contentToken.ToArray().Select((t) => t.ToObject<AttachmentValue>(serializer)).ToList();
                        break;

                    default: throw new JsonException("invalid attachment value type");
                }
                return attachmentValue;
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }
        }
    }
}
