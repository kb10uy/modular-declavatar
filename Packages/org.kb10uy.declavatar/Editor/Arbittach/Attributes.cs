using System;

namespace KusakaFactory.Declavatar.Arbittach
{
    [AttributeUsage(AttributeTargets.Assembly, Inherited = false, AllowMultiple = true)]
    public sealed class ExportProcessorAttribute : Attribute
    {
        private readonly Type _processorType;

        public ExportProcessorAttribute(Type processorType)
        {
            _processorType = processorType;
        }

        public Type ProcessorType => _processorType;
        public string Name { get; set; } = null;
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
    public sealed class DefinePropertyAttribute : Attribute
    {
        private readonly string _arbittachPropertyName;
        private int _parametersCount;

        public DefinePropertyAttribute(string propertyName, int parameters)
        {
            _arbittachPropertyName = propertyName;
            _parametersCount = parameters;
        }

        public string ArbittachPropertyName => _arbittachPropertyName;
        public int ParametersCount => _parametersCount;
        public bool Required { get; set; } = true;
    }

    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public sealed class BindValueAttribute : Attribute
    {
        private readonly string _path;

        public BindValueAttribute(string path)
        {
            _path = path;
        }

        public string Path => _path;
    }
}
