using System;
using Newtonsoft.Json;
using nadena.dev.modular_avatar.core;
using AnimatorAsCode.V1.VRC;
using KusakaFactory.Declavatar.Runtime.Data;

namespace KusakaFactory.Declavatar
{
    internal static partial class DeclavatarExtension
    {
        public static string AsGroupingKey(this Target target)
        {
            switch (target)
            {
                case Target.Shape s: return $"shape://{s.Mesh}/{s.Name}";
                case Target.Object o: return $"object://{o.Name}";
                case Target.Material m: return $"material://{m.Mesh}/{m.Slot}";
                default: throw new ArgumentException("invalid target type");
            }
        }

        public static float ConvertToVRCParameterValue(this ParameterType value)
        {
            switch (value.Type)
            {
                case "Int": return (float)(long)value.Default;
                case "Float": return (float)(double)value.Default;
                case "Bool": return (bool)value.Default ? 1.0f : 0.0f;
                default: throw new ArgumentException("invalid parameter type");
            }
        }

        public static ParameterSyncType ConvertToMASyncType(this Parameter parameter)
        {
            if (parameter.Scope.Type == "Internal") return ParameterSyncType.NotSynced;
            switch (parameter.ValueType.Type)
            {
                case "Int": return ParameterSyncType.Int;
                case "Float": return ParameterSyncType.Float;
                case "Bool": return ParameterSyncType.Bool;
                default: throw new ArgumentException("invalid parameter type");
            }
        }

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
