using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KusakaFactory.Declavatar.Runtime.Data
{
    [JsonConverter(typeof(Converters.LayerConverter))]
    public sealed class Layer
    {
        public string Name { get; set; }
        public object Content { get; set; }

        public sealed class GroupLayer
        {
            public string Parameter { get; set; }
            public GroupOption Default { get; set; }
            public List<GroupOption> Options { get; set; }
        }

        public sealed class GroupOption
        {
            public string Name { get; set; }
            public uint Value { get; set; }
            public LayerAnimation Animation { get; set; }
        }

        public sealed class SwitchLayer
        {
            public string Parameter { get; set; }
            public LayerAnimation Disabled { get; set; }
            public LayerAnimation Enabled { get; set; }
        }

        public sealed class PuppetLayer
        {
            public string Parameter { get; set; }
            public LayerAnimation Animation { get; set; }
        }

        public sealed class SwitchGateLayer
        {
            public string Gate { get; set; }
            public LayerAnimation Disabled { get; set; }
            public LayerAnimation Enabled { get; set; }
        }

        public sealed class PuppetKeyframe
        {
            public float Value { get; set; }
            public List<Target> Targets { get; set; }
        }

        public sealed class RawLayer
        {
            public uint DefaultIndex { get; set; }
            public List<RawState> States { get; set; }
            public List<RawTransition> Transitions { get; set; }
        }

        public sealed class RawState
        {
            public string Name { get; set; }
            public RawAnimation Animation { get; set; }
        }

        public sealed class RawTransition
        {
            public uint FromIndex { get; set; }
            public uint TargetIndex { get; set; }
            public float Duration { get; set; }
            public List<RawCondition> Conditions { get; set; }
        }
    }

    [JsonConverter(typeof(Converters.LayerAnimationConverter))]
    public abstract class LayerAnimation
    {
        public sealed class Inline : LayerAnimation
        {
            public List<Target> Targets { get; set; }
        }

        public sealed class KeyedInline : LayerAnimation
        {
            public List<Layer.PuppetKeyframe> Keyframes { get; set; }
        }

        public sealed class External : LayerAnimation
        {
            public string Name { get; set; }
        }
    }

    [JsonConverter(typeof(Converters.RawAnimationConverter))]
    public abstract class RawAnimation
    {
        public sealed class Clip : RawAnimation
        {
            public LayerAnimation Animation { get; set; }
            public float? Speed { get; set; }
            public string SpeedBy { get; set; }
            public string TimeBy { get; set; }
        }

        public sealed class BlendTree : RawAnimation
        {
            public string BlendType { get; set; }
            public List<string> Parameters { get; set; }
            public List<RawBlendTreeField> Fields { get; set; }
        }
    }

    [JsonConverter(typeof(Converters.LayerBlendTreeFieldConverter))]
    public sealed class RawBlendTreeField
    {
        public LayerAnimation Animation { get; set; }
        public float[] Position { get; set; }
    }

    [JsonConverter(typeof(Converters.RawConditionConverter))]
    public abstract class RawCondition
    {
        public sealed class Be : RawCondition
        {
            public string Parameter { get; set; }
        }

        public sealed class Not : RawCondition
        {
            public string Parameter { get; set; }
        }

        public sealed class EqInt : RawCondition
        {
            public string Parameter { get; set; }
            public int Value { get; set; }
        }

        public sealed class NeqInt : RawCondition
        {
            public string Parameter { get; set; }
            public int Value { get; set; }
        }

        public sealed class GtInt : RawCondition
        {
            public string Parameter { get; set; }
            public int Value { get; set; }
        }

        public sealed class LeInt : RawCondition
        {
            public string Parameter { get; set; }
            public int Value { get; set; }
        }

        public sealed class GtFloat : RawCondition
        {
            public string Parameter { get; set; }
            public float Value { get; set; }
        }

        public sealed class LeFloat : RawCondition
        {
            public string Parameter { get; set; }
            public float Value { get; set; }
        }
    }

    [JsonConverter(typeof(Converters.TargetConverter))]
    public abstract class Target
    {
        public sealed class Shape : Target
        {
            public string Mesh { get; set; }
            public string Name { get; set; }
            public float Value { get; set; }
        }

        public sealed class Object : Target
        {
            public string Name { get; set; }
            public bool Enabled { get; set; }
        }

        public sealed class Material : Target
        {
            public string Mesh { get; set; }
            public uint Slot { get; set; }
            public string AssetKey { get; set; }
        }

        public sealed class Drive : Target
        {
            public ParameterDrive ParameterDrive { get; set; }
        }

        public sealed class Tracking : Target
        {
            public TrackingControl Control { get; set; }
        }
    }

    internal static partial class Converters
    {
        internal sealed class LayerConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(Layer);
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
                object content;
                switch (contentObject["type"].Value<string>())
                {
                    case "Group":
                        content = contentObject.ToObject<Layer.GroupLayer>(serializer);
                        break;
                    case "Switch":
                        content = contentObject.ToObject<Layer.SwitchLayer>(serializer);
                        break;
                    case "Puppet":
                        content = contentObject.ToObject<Layer.PuppetLayer>(serializer);
                        break;
                    case "SwitchGate":
                        content = contentObject.ToObject<Layer.SwitchGateLayer>(serializer);
                        break;
                    case "Raw":
                        content = contentObject.ToObject<Layer.RawLayer>(serializer);
                        break;
                    default:
                        throw new JsonException("invalid group type");
                }

                return new Layer
                {
                    Name = obj["name"].Value<string>(),
                    Content = content,
                };
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }
        }

        internal sealed class LayerAnimationConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(LayerAnimation);
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                var obj = JObject.Load(reader);
                var type = obj["type"].Value<string>();
                var content = obj["content"];
                switch (type)
                {
                    case "Inline":
                        var targets = content.ToArray().Select((jt) => jt.ToObject<Target>(serializer)).ToList();
                        return new LayerAnimation.Inline { Targets = targets };
                    case "KeyedInline":
                        var keyframes = content.ToArray().Select((jt) => jt.ToObject<Layer.PuppetKeyframe>(serializer)).ToList();
                        return new LayerAnimation.KeyedInline { Keyframes = keyframes };
                    case "External":
                        var name = content.Value<string>();
                        return new LayerAnimation.External { Name = name };
                    default: throw new JsonException("invalid layer animation");
                }
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }
        }

        internal sealed class RawAnimationConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(RawAnimation);
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                var obj = JObject.Load(reader);
                var type = obj["type"].Value<string>();
                var content = obj["content"];
                switch (type)
                {
                    case "Clip":
                        var animation = content["animation"].ToObject<LayerAnimation>(serializer);
                        var speed = content["speed"].Value<float?>();
                        var speed_by = content["speed_by"].Value<string>();
                        var time_by = content["time_by"].Value<string>();
                        return new RawAnimation.Clip
                        {
                            Animation = animation,
                            Speed = speed,
                            SpeedBy = speed_by,
                            TimeBy = time_by,
                        };
                    case "BlendTree":
                        var ty = content["blend_type"].Value<string>();
                        var parameters = content["params"].Values<string>().ToList();
                        var fields = content["fields"].ToArray().Select((jt) => jt.ToObject<RawBlendTreeField>(serializer)).ToList();
                        return new RawAnimation.BlendTree
                        {
                            BlendType = ty,
                            Parameters = parameters,
                            Fields = fields
                        };
                    default: throw new JsonException("invalid layer animation type");
                }
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }
        }

        internal sealed class LayerBlendTreeFieldConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(RawBlendTreeField);
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                var obj = JObject.Load(reader);
                var animation = obj["animation"].ToObject<LayerAnimation>(serializer);
                var position = obj["position"].Values<float>().ToArray();
                return new RawBlendTreeField { Animation = animation, Position = position };
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }
        }

        internal sealed class RawConditionConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(RawCondition);
            }

            public override object ReadJson(
                JsonReader reader,
                Type objectType,
                object existingValue,
                JsonSerializer serializer
            )
            {
                var obj = JObject.Load(reader);
                var type = obj["type"].Value<string>();
                var content = obj["content"];
                switch (type)
                {
                    case "Be": return new RawCondition.Be { Parameter = content.Value<string>() };
                    case "Not": return new RawCondition.Not { Parameter = content.Value<string>() };
                    case "EqInt": return new RawCondition.EqInt { Parameter = content[0].Value<string>(), Value = content[1].Value<int>() };
                    case "NeqInt": return new RawCondition.NeqInt { Parameter = content[0].Value<string>(), Value = content[1].Value<int>() };
                    case "GtInt": return new RawCondition.GtInt { Parameter = content[0].Value<string>(), Value = content[1].Value<int>() };
                    case "LeInt": return new RawCondition.LeInt { Parameter = content[0].Value<string>(), Value = content[1].Value<int>() };
                    case "GtFloat": return new RawCondition.GtFloat { Parameter = content[0].Value<string>(), Value = content[1].Value<float>() };
                    case "LeFloat": return new RawCondition.LeFloat { Parameter = content[0].Value<string>(), Value = content[1].Value<float>() };
                    default: throw new JsonException("invalid driver type");
                }
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }
        }

        internal sealed class TargetConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(Target);
            }

            public override object ReadJson(
                JsonReader reader,
                Type objectType,
                object existingValue,
                JsonSerializer serializer
            )
            {
                var obj = JObject.Load(reader);
                var type = obj["type"].Value<string>();
                var content = obj["content"] as JObject;
                switch (type)
                {
                    case "Shape": return new Target.Shape { Mesh = content["mesh"].Value<string>(), Name = content["shape"].Value<string>(), Value = content["value"].Value<float>(), };
                    case "Object": return new Target.Object { Name = content["object"].Value<string>(), Enabled = content["value"].Value<bool>() };
                    case "Material": return new Target.Material { Mesh = content["mesh"].Value<string>(), Slot = content["index"].Value<uint>(), AssetKey = content["asset"].Value<string>() };
                    case "ParameterDrive": return new Target.Drive { ParameterDrive = content.ToObject<ParameterDrive>(serializer) };
                    case "TrackingControl": return new Target.Tracking { Control = content.ToObject<TrackingControl>(serializer) };
                    default: throw new JsonException("invalid driver type");
                }
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }
        }
    }
}

