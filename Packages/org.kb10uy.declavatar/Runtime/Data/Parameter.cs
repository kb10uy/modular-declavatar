namespace KusakaFactory.Declavatar.Runtime.Data
{
    public sealed class Parameter
    {
        public string Name { get; set; }
        public ParameterType ValueType { get; set; }
        public ParameterScope Scope { get; set; }
        public bool Unique { get; set; }
        public bool ExplicitDefault { get; set; }
    }

    public sealed class ParameterType
    {
        public string Type { get; set; }
        public object Default { get; set; }
    }

    public sealed class ParameterScope
    {
        public string Type { get; set; }
        public bool? Save { get; set; }
    }
}
