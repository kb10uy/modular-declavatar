using System.Collections.Generic;

namespace KusakaFactory.Declavatar.Arbittach
{
    internal sealed class AttachmentSchema
    {
        public string Name { get; set; }

        public List<PropertySchema> Properties { get; set; }
    }

    public sealed class PropertySchema
    {
        public string Name { get; set; }
        public bool Required { get; set; }
        public List<ValueTypeSchema> Parameters { get; set; }
        public List<KeywordSchema> Keywords { get; set; }
    }

    public sealed class KeywordSchema
    {
        public string Name { get; set; }
        public bool Required { get; set; }
        public ValueTypeSchema ValueType { get; set; }
    }

    public sealed class ValueTypeSchema
    {
        public string Type { get; set; } = "Any";
        public object Content { get; set; } = null;

        public static readonly ValueTypeSchema Null = new() { Type = "Null" };
        public static readonly ValueTypeSchema Boolean = new() { Type = "Boolean" };
        public static readonly ValueTypeSchema Integer = new() { Type = "Integer" };
        public static readonly ValueTypeSchema Float = new() { Type = "Float" };
        public static readonly ValueTypeSchema String = new() { Type = "String" };
        public static ValueTypeSchema Vector(int dimension) => new() { Type = "Vector", Content = dimension };
        public static readonly ValueTypeSchema GameObject = new() { Type = "GameObject" };
        public static readonly ValueTypeSchema Material = new() { Type = "Material" };
        public static readonly ValueTypeSchema AnimationClip = new() { Type = "AnimationClip" };

        public static readonly ValueTypeSchema Any = new();
        public static ValueTypeSchema OneOf(IEnumerable<ValueTypeSchema> types) => new()
        {
            Type = "OneOf",
            Content = new List<ValueTypeSchema>(types),
        };
        public static ValueTypeSchema List(ValueTypeSchema type) => new()
        {
            Type = "List",
            Content = type,
        };
        public static ValueTypeSchema Tuple(IEnumerable<ValueTypeSchema> types) => new()
        {
            Type = "Tuple",
            Content = new List<ValueTypeSchema>(types),
        };

        // TODO: implement Map Variant
    }
}
