using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KusakaFactory.Declavatar.Data
{

    [JsonConverter(typeof(Converters.MenuItemConverter))]
    public abstract class MenuItem
    {
        public sealed class SubMenu : MenuItem
        {
            public string Name { get; set; }
            public List<MenuItem> Items { get; set; }
        }

        public sealed class Button : MenuItem
        {
            public string Name { get; set; }
            public string Parameter { get; set; }
            public ParameterType Value { get; set; }
        }

        public sealed class Toggle : MenuItem
        {
            public string Name { get; set; }
            public string Parameter { get; set; }
            public ParameterType Value { get; set; }
        }

        public sealed class Radial : MenuItem
        {
            public string Name { get; set; }
            public string Parameter { get; set; }
        }

        public sealed class TwoAxis : MenuItem
        {
            public string Name { get; set; }
            public BiAxis HorizontalAxis { get; set; }
            public BiAxis VerticalAxis { get; set; }
        }

        public sealed class FourAxis : MenuItem
        {
            public string Name { get; set; }
            public UniAxis LeftAxis { get; set; }
            public UniAxis RightAxis { get; set; }
            public UniAxis UpAxis { get; set; }
            public UniAxis DownAxis { get; set; }
        }

        public sealed class BiAxis
        {
            public string Parameter { get; set; }
            public string LabelPositive { get; set; }
            public string LabelNegative { get; set; }
        }

        public sealed class UniAxis
        {
            public string Parameter { get; set; }
            public string Label { get; set; }
        }
    }

    public static partial class Converters
    {
        public sealed class MenuItemConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(MenuItem);
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
                switch (obj["type"].Value<string>())
                {
                    case "SubMenu":
                        return new MenuItem.SubMenu
                        {
                            Name = contentObject["name"].Value<string>(),
                            Items = contentObject["items"].ToObject<List<MenuItem>>(),
                        };
                    case "Button":
                        return new MenuItem.Button
                        {
                            Name = contentObject["name"].Value<string>(),
                            Parameter = contentObject["parameter"].Value<string>(),
                            Value = contentObject["value"].ToObject<ParameterType>(),
                        };
                    case "Toggle":
                        return new MenuItem.Toggle
                        {
                            Name = contentObject["name"].Value<string>(),
                            Parameter = contentObject["parameter"].Value<string>(),
                            Value = contentObject["value"].ToObject<ParameterType>(),
                        };
                    case "Radial":
                        return new MenuItem.Radial
                        {
                            Name = contentObject["name"].Value<string>(),
                            Parameter = contentObject["parameter"].Value<string>(),
                        };
                    case "TwoAxis":
                        return new MenuItem.TwoAxis
                        {
                            Name = contentObject["name"].Value<string>(),
                            HorizontalAxis = contentObject["horizontal_axis"].ToObject<MenuItem.BiAxis>(),
                            VerticalAxis = contentObject["vertical_axis"].ToObject<MenuItem.BiAxis>(),
                        };
                    case "FourAxis":
                        return new MenuItem.FourAxis
                        {
                            Name = contentObject["name"].Value<string>(),
                            UpAxis = contentObject["up_axis"].ToObject<MenuItem.UniAxis>(),
                            DownAxis = contentObject["down_axis"].ToObject<MenuItem.UniAxis>(),
                            LeftAxis = contentObject["left_axis"].ToObject<MenuItem.UniAxis>(),
                            RightAxis = contentObject["right_axis"].ToObject<MenuItem.UniAxis>(),
                        };
                    default: throw new JsonException("invalid group type");
                }
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }
        }
    }
}
