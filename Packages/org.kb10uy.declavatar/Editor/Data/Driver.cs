using System;
using System.Linq;
using AnimatorAsCode.V1.VRC;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KusakaFactory.Declavatar.Data
{
    [JsonConverter(typeof(Converters.ParameterDriveConverter))]
    public abstract class ParameterDrive
    {
        public sealed class SetInt : ParameterDrive
        {
            public string Parameter { get; set; }
            public byte Value { get; set; }
        }

        public sealed class SetFloat : ParameterDrive
        {
            public string Parameter { get; set; }
            public float Value { get; set; }
        }

        public sealed class SetBool : ParameterDrive
        {
            public string Parameter { get; set; }
            public bool Value { get; set; }
        }

        public sealed class AddInt : ParameterDrive
        {
            public string Parameter { get; set; }
            public byte Value { get; set; }
        }

        public sealed class AddFloat : ParameterDrive
        {
            public string Parameter { get; set; }
            public float Value { get; set; }
        }

        public sealed class RandomInt : ParameterDrive
        {
            public string Parameter { get; set; }
            public byte[] Range { get; set; }
        }

        public sealed class RandomFloat : ParameterDrive
        {
            public string Parameter { get; set; }
            public float[] Range { get; set; }
        }

        public sealed class RandomBool : ParameterDrive
        {
            public string Parameter { get; set; }
            public float Chance { get; set; }
        }

        public sealed class Copy : ParameterDrive
        {
            public string From { get; set; }
            public string To { get; set; }
        }

        public sealed class RangedCopy : ParameterDrive
        {
            public string From { get; set; }
            public string To { get; set; }
            public float[] FromRange { get; set; }
            public float[] ToRange { get; set; }
        }
    }

    public sealed class TrackingControl
    {
        public bool AnimationDesired { get; set; }
        public string Target { get; set; }
    }

    public static partial class Converters
    {
        public sealed class ParameterDriveConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(ParameterDrive);
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
                var content = obj["content"].Value<JArray>();
                switch (type)
                {
                    case "SetInt": return new ParameterDrive.SetInt { Parameter = content[0].Value<string>(), Value = content[1].Value<byte>() };
                    case "SetFloat": return new ParameterDrive.SetFloat { Parameter = content[0].Value<string>(), Value = content[1].Value<float>() };
                    case "SetBool": return new ParameterDrive.SetBool { Parameter = content[0].Value<string>(), Value = content[1].Value<bool>() };
                    case "AddInt": return new ParameterDrive.AddInt { Parameter = content[0].Value<string>(), Value = content[1].Value<byte>() };
                    case "AddFloat": return new ParameterDrive.AddFloat { Parameter = content[0].Value<string>(), Value = content[1].Value<float>() };
                    case "RandomInt": return new ParameterDrive.RandomInt { Parameter = content[0].Value<string>(), Range = content[1].Values<byte>().ToArray() };
                    case "RandomFloat": return new ParameterDrive.RandomFloat { Parameter = content[0].Value<string>(), Range = content[1].Values<float>().ToArray() };
                    case "RandomBool": return new ParameterDrive.RandomBool { Parameter = content[0].Value<string>(), Chance = content[1].Value<float>() };
                    case "Copy": return new ParameterDrive.Copy { From = content[0].Value<string>(), To = content[1].Value<string>() };
                    case "RangedCopy": return new ParameterDrive.RangedCopy { From = content[0].Value<string>(), To = content[1].Value<string>(), FromRange = content[2].Values<float>().ToArray(), ToRange = content[3].Values<float>().ToArray() };
                    default: throw new JsonException("invalid driver type");
                }
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }
        }
    }

    public static partial class VRChatExtension
    {
        public static AacAv3.Av3TrackingElement ConvertToAacTarget(this string target)
        {
            switch (target)
            {
                case "Head": return AacAv3.Av3TrackingElement.Head;
                case "Hip": return AacAv3.Av3TrackingElement.Hip;
                case "Eyes": return AacAv3.Av3TrackingElement.Eyes;
                case "Mouth": return AacAv3.Av3TrackingElement.Mouth;
                case "HandLeft": return AacAv3.Av3TrackingElement.LeftHand;
                case "HandRight": return AacAv3.Av3TrackingElement.RightHand;
                case "FootLeft": return AacAv3.Av3TrackingElement.LeftFoot;
                case "FoorRight": return AacAv3.Av3TrackingElement.RightFoot;
                case "FingersLeft": return AacAv3.Av3TrackingElement.LeftFingers;
                case "FingersRight": return AacAv3.Av3TrackingElement.RightFingers;
                default: throw new JsonException("invalid tracking element");
            }
        }
    }
}
