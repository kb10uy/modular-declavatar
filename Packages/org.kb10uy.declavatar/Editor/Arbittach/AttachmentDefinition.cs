using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using KusakaFactory.Declavatar.Runtime.Data;

namespace KusakaFactory.Declavatar.Arbittach
{
    /// <summary>
    /// Analyzed attachment type information.
    /// </summary>
    public sealed class AttachmentDefinition
    {
        /// <summary>
        /// Name that will be registered in comiler.
        /// </summary>
        public string RegisteredName { get; private set; }

        /// <summary>
        /// Original type.
        /// </summary>
        public Type AttachmentType { get; private set; }

        /// <summary>
        /// Serialized schema.
        /// </summary>
        public RawAttachmentSchema Schema { get; private set; }

        /// <summary>
        /// Map between properties in attachment type and binding path.
        /// </summary>
        public IReadOnlyDictionary<PropertyInfo, PropertyBindPath> Paths { get; private set; }

        private AttachmentDefinition() { }

        internal static AttachmentDefinition Create(Type attachmentType, string customName)
        {
            var definition = new AttachmentDefinition
            {
                RegisteredName = customName ?? attachmentType.Name,
                AttachmentType = attachmentType,
                Paths = EnumerateBoundProperties(attachmentType).ToDictionary((p) => p.Item1, (p) => p.Item2),
            };
            definition.SerializeAttachmentType();
            return definition;
        }

        #region Schema Serialization

        private static readonly Dictionary<Type, RawValueTypeSchema> SimpleSchemaTypes = new Dictionary<Type, RawValueTypeSchema>
        {
            [typeof(object)] = RawValueTypeSchema.Any,
            [typeof(bool)] = RawValueTypeSchema.Boolean,
            [typeof(int)] = RawValueTypeSchema.Integer,
            [typeof(long)] = RawValueTypeSchema.Integer,
            [typeof(float)] = RawValueTypeSchema.Float,
            [typeof(double)] = RawValueTypeSchema.Float,
            [typeof(string)] = RawValueTypeSchema.String,
            [typeof(Vector2)] = RawValueTypeSchema.Vector(2),
            [typeof(Vector3)] = RawValueTypeSchema.Vector(3),
            [typeof(Vector4)] = RawValueTypeSchema.Vector(4),
            [typeof(GameObject)] = RawValueTypeSchema.GameObject,
            [typeof(Material)] = RawValueTypeSchema.Material,
            [typeof(AnimationClip)] = RawValueTypeSchema.AnimationClip,
        };
        private static readonly List<Type> TupleGenericDefinitions = new List<Type>
        {
            typeof(ValueTuple<>),
            typeof(ValueTuple<,>),
            typeof(ValueTuple<,,>),
            typeof(ValueTuple<,,,>),
            typeof(ValueTuple<,,,,>),
            typeof(ValueTuple<,,,,,>),
            typeof(ValueTuple<,,,,,,>),
        };

        private void SerializeAttachmentType()
        {
            // add schema property
            var properties = new Dictionary<string, RawPropertySchema>();
            foreach (var defineAttribute in AttachmentType.GetCustomAttributes<DefinePropertyAttribute>(false))
            {
                var propertySchema = new RawPropertySchema
                {
                    Name = defineAttribute.ArbittachPropertyName,
                    Required = defineAttribute.Required,
                    Parameters = Enumerable.Repeat(
                        new RawParameterSchema { Name = "<unreferenced>", ValueType = RawValueTypeSchema.Any },
                        defineAttribute.ParametersCount
                    ).ToList(),
                    Keywords = new List<RawKeywordSchema>(),
                };
                properties.Add(propertySchema.Name, propertySchema);
            }

            // traverse type properties
            foreach (var (propertyInfo, bindPath) in Paths)
            {
                if (!properties.TryGetValue(bindPath.Property, out var targetSchemaProperty))
                {
                    throw new ArgumentException($"property name {bindPath.Property} not found");
                }

                if (char.IsDigit(bindPath.Indexer[0]))
                {
                    // parameter access
                    var parameterIndex = Convert.ToInt32(bindPath.Indexer);
                    if (targetSchemaProperty.Parameters.Count < parameterIndex)
                    {
                        throw new IndexOutOfRangeException($"property {bindPath.Property} has {targetSchemaProperty.Parameters.Count} arguments");
                    }
                    targetSchemaProperty.Parameters[parameterIndex] = new RawParameterSchema
                    {
                        Name = propertyInfo.Name,
                        ValueType = ConstructValueTypeSchema(propertyInfo.PropertyType),
                    };
                }
                else
                {
                    // keyword access
                    targetSchemaProperty.Keywords.Add(new RawKeywordSchema
                    {
                        Name = bindPath.Indexer,
                        Required = bindPath.Required,
                        ValueType = ConstructValueTypeSchema(propertyInfo.PropertyType),
                    });
                }
            }

            Schema = new RawAttachmentSchema()
            {
                Name = RegisteredName,
                Properties = properties.Values.ToList(),
            };
        }

        private static IEnumerable<(PropertyInfo, PropertyBindPath)> EnumerateBoundProperties(Type attachmentType)
        {
            return attachmentType
                .GetProperties()
                .Select((pi) => (Property: pi, Attribute: pi.GetCustomAttribute<BindValueAttribute>()))
                .Where((p) => p.Attribute != null)
                .Select((p) => (p.Property, Path: ParseBindingPath(p.Attribute.Path)));
        }

        private static PropertyBindPath ParseBindingPath(string path)
        {
            var splitPath = path.Split('.');
            if (splitPath.Length >= 3) throw new ArgumentException($"too many dots in property path: {path}");

            var property = splitPath[0].Trim();
            if (string.IsNullOrEmpty(property)) throw new ArgumentException("property name cannot be empty");

            var required = true;
            var indexer = "0";
            if (splitPath.Length >= 2)
            {
                var maybeIndexer = splitPath[1].Trim();
                required = !maybeIndexer.StartsWith('?');
                indexer = splitPath[1].TrimStart('?').Trim();
            }
            if (string.IsNullOrEmpty(indexer)) throw new ArgumentException("property indexer cannot be empty");

            return new PropertyBindPath { Property = property, Required = required, Indexer = indexer };
        }

        private static RawValueTypeSchema ConstructValueTypeSchema(Type originalType)
        {
            // simple type
            if (SimpleSchemaTypes.TryGetValue(originalType, out var simpleSchemaType)) return simpleSchemaType;

            if (originalType.IsGenericType)
            {
                var genericDefinition = originalType.GetGenericTypeDefinition();
                if (genericDefinition.IsAssignableFrom(typeof(List<>)))
                {
                    // list
                    return RawValueTypeSchema.List(ConstructValueTypeSchema(originalType.GetGenericArguments()[0]));
                }
                if (TupleGenericDefinitions.Any((tt) => genericDefinition.IsAssignableFrom(tt)))
                {
                    // tuples
                    return RawValueTypeSchema.Tuple(originalType.GetGenericArguments().Select(ConstructValueTypeSchema));
                }
            }

            throw new ArgumentException($"type {originalType} cannot be serialized as schema type");
        }

        #endregion

        #region Attachment Deserialization

        internal object Deserialize(Runtime.Data.Attachment rawAttachment, DeclavatarContext context)
        {
            // TODO: use Expression.XXXX for performance

            var defaultConstructor = AttachmentType.GetConstructor(new Type[] { });
            var attachmentInstance = defaultConstructor.Invoke(new object[] { });

            var rawProperties = rawAttachment.Properties.ToDictionary((p) => p.Name);

            foreach (var (propInfo, bindPath) in Paths)
            {
                if (!rawProperties.TryGetValue(bindPath.Property, out var targetRawProperty))
                {
                    throw new ArgumentException($"property name {bindPath.Property} not found");
                }

                AttachmentValue rawValue;
                if (char.IsDigit(bindPath.Indexer[0]))
                {
                    // parameter access
                    var parameterIndex = Convert.ToInt32(bindPath.Indexer);
                    if (targetRawProperty.Parameters.Count < parameterIndex)
                    {
                        throw new IndexOutOfRangeException($"property {bindPath.Property} has {targetRawProperty.Parameters.Count} arguments");
                    }
                    rawValue = targetRawProperty.Parameters[parameterIndex];
                }
                else
                {
                    if (!targetRawProperty.Keywords.ContainsKey(bindPath.Indexer) && !bindPath.Required) continue;
                    rawValue = targetRawProperty.Keywords[bindPath.Indexer];
                }

                propInfo.SetValue(attachmentInstance, ConstructDeserializedValue(rawValue, context, propInfo.PropertyType));
            }

            return attachmentInstance;
        }

        private static object ConstructDeserializedValue(AttachmentValue rawValue, DeclavatarContext context, Type propertyType)
        {
            switch (rawValue.Type)
            {
                case "Null":
                    return null;

                case "Boolean":
                    if (propertyType != typeof(bool)) throw new ArgumentException("type mismatch");
                    return rawValue.Content;
                case "Integer":
                    if (propertyType != typeof(int)) throw new ArgumentException("type mismatch");
                    return rawValue.Content;
                case "Float":
                    if (propertyType != typeof(float)) throw new ArgumentException("type mismatch");
                    return rawValue.Content;
                case "String":
                    if (propertyType != typeof(string)) throw new ArgumentException("type mismatch");
                    return rawValue.Content;

                case "Vector":
                    var values = rawValue.Content as List<float>;
                    switch (values.Count)
                    {
                        case 2:
                            if (propertyType != typeof(Vector2)) throw new ArgumentException("type mismatch");
                            return new Vector2(values[0], values[1]);
                        case 3:
                            if (propertyType != typeof(Vector3)) throw new ArgumentException("type mismatch");
                            return new Vector3(values[0], values[1], values[2]);
                        case 4:
                            if (propertyType != typeof(Vector4)) throw new ArgumentException("type mismatch");
                            return new Vector4(values[0], values[1], values[2], values[3]);
                        default:
                            throw new ArgumentException("invalid vector length");
                    }
                case "GameObject":
                    if (propertyType != typeof(GameObject)) throw new ArgumentException("type mismatch");
                    return context.FindGameObject(rawValue.Content as string);
                case "Material":
                    if (propertyType != typeof(Material)) throw new ArgumentException("type mismatch");
                    return context.GetExternalMaterial(rawValue.Content as string);
                case "AnimationClip":
                    if (propertyType != typeof(AnimationClip)) throw new ArgumentException("type mismatch");
                    return context.GetExternalAnimationClip(rawValue.Content as string);

                case "List":
                    {
                        var itemType = propertyType.GenericTypeArguments[0];
                        var constructor = propertyType.GetConstructor(new Type[] { });
                        var addMethod = propertyType.GetMethod("Add");

                        var listInstance = constructor.Invoke(new object[] { });
                        var rawValues = rawValue.Content as List<AttachmentValue>;
                        foreach (var rawItem in rawValues)
                        {
                            var value = ConstructDeserializedValue(rawItem, context, itemType);
                            addMethod.Invoke(listInstance, new object[] { });
                        }
                        return listInstance;
                    }

                case "Tuple":
                    {
                        var rawValues = rawValue.Content as List<AttachmentValue>;
                        var constructor = propertyType.GetConstructor(propertyType.GenericTypeArguments);
                        return constructor.Invoke(
                            rawValues
                                .Zip(propertyType.GenericTypeArguments, (rv, t) => (rv, t))
                                .Select((p) => ConstructDeserializedValue(p.rv, context, p.t))
                                .ToArray()
                        );
                    }

                default: throw new ArgumentException("invalid attachment value type");
            }
        }

        #endregion

        /// <summary>
        /// Attachment property path.
        /// </summary>
        public class PropertyBindPath
        {
            /// <summary>
            /// Accessing property.
            /// </summary>
            public string Property { get; internal set; }

            /// <summary>
            /// Whether this value is required.
            /// </summary>
            public bool Required { get; internal set; }

            /// <summary>
            /// Property access indexer.
            /// If this path is for parameter, it should represent a number
            /// </summary>
            public string Indexer { get; internal set; }
        }
    }
}
