using System;

namespace KusakaFactory.Declavatar.Arbittach
{
    /// <summary>
    /// Exports Arbitrary Attachment (Arbittach) processor type.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, Inherited = false, AllowMultiple = true)]
    public sealed class ExportsProcessorAttribute : Attribute
    {
        private readonly Type _processorType;

        /// <summary>
        /// </summary>
        /// <param name="processorType">Exporting type.</param>
        public ExportsProcessorAttribute(Type processorType)
        {
            _processorType = processorType;
        }

        public Type ProcessorType => _processorType;
        public string Name { get; set; } = null;
    }

    /// <summary>
    /// Declares that this attachment type has a property.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
    public sealed class DefinePropertyAttribute : Attribute
    {
        private readonly string _arbittachPropertyName;
        private int _parametersCount;

        /// <summary>
        /// Constructs attribute.
        /// </summary>
        /// <param name="propertyName">Serialized property name.</param>
        /// <param name="parameters">Required parameters count.</param>
        public DefinePropertyAttribute(string propertyName, int parameters)
        {
            _arbittachPropertyName = propertyName;
            _parametersCount = parameters;
        }

        /// <summary>
        /// Property name.
        /// </summary>
        public string ArbittachPropertyName => _arbittachPropertyName;

        /// <summary>
        /// Number of required parameters.
        /// </summary>
        public int ParametersCount => _parametersCount;

        /// <summary>
        /// Whether this property is required.
        /// </summary>
        public bool Required { get; set; } = true;
    }

    /// <summary>
    /// Binds corresponding property to attachment object value.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public sealed class BindValueAttribute : Attribute
    {
        private readonly string _path;

        /// <summary>
        /// </summary>
        /// <param name="path">
        /// `PropertyName.0
        /// </param>
        public BindValueAttribute(string path)
        {
            _path = path;
        }

        /// <summary>
        /// Path string.
        /// </summary>
        public string Path => _path;
    }
}
