using System.Collections.Generic;

namespace KusakaFactory.Declavatar.Arbittach
{
    public sealed class RawAttachmentSchema
    {
        public string Name { get; set; }

        public List<RawPropertySchema> Properties { get; set; }
    }

    public sealed class RawPropertySchema
    {
        public string Name { get; set; }
        public bool Required { get; set; }
        public List<RawParameterSchema> Parameters { get; set; }
        public List<RawKeywordSchema> Keywords { get; set; }
    }

    public sealed class RawParameterSchema
    {
        public string Name { get; set; }
        public RawValueTypeSchema ValueType { get; set; }
    }

    public sealed class RawKeywordSchema
    {
        public string Name { get; set; }
        public bool Required { get; set; }
        public RawValueTypeSchema ValueType { get; set; }
    }

    public sealed class RawValueTypeSchema
    {
        public string Type { get; set; } = "Any";
        public object Content { get; set; } = null;

        public static readonly RawValueTypeSchema Null = new() { Type = "Null" };
        public static readonly RawValueTypeSchema Boolean = new() { Type = "Boolean" };
        public static readonly RawValueTypeSchema Integer = new() { Type = "Integer" };
        public static readonly RawValueTypeSchema Float = new() { Type = "Float" };
        public static readonly RawValueTypeSchema String = new() { Type = "String" };
        public static RawValueTypeSchema Vector(int dimension) => new() { Type = "Vector", Content = dimension };
        public static readonly RawValueTypeSchema GameObject = new() { Type = "GameObject" };
        public static readonly RawValueTypeSchema Material = new() { Type = "Material" };
        public static readonly RawValueTypeSchema AnimationClip = new() { Type = "AnimationClip" };

        public static readonly RawValueTypeSchema Any = new();
        public static RawValueTypeSchema OneOf(IEnumerable<RawValueTypeSchema> types) => new()
        {
            Type = "OneOf",
            Content = new List<RawValueTypeSchema>(types),
        };
        public static RawValueTypeSchema List(RawValueTypeSchema type) => new()
        {
            Type = "List",
            Content = type,
        };
        public static RawValueTypeSchema Tuple(IEnumerable<RawValueTypeSchema> types) => new()
        {
            Type = "Tuple",
            Content = new List<RawValueTypeSchema>(types),
        };

        // TODO: implement Map Variant
    }
}
