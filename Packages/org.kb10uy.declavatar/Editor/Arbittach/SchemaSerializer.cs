using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace KusakaFactory.Declavatar.Arbittach
{
    internal static class SchemaSerializer
    {
        private static readonly Dictionary<Type, ValueTypeSchema> SimpleSchemaTypes = new Dictionary<Type, ValueTypeSchema>
        {
            [typeof(object)] = ValueTypeSchema.Any,
            [typeof(bool)] = ValueTypeSchema.Boolean,
            [typeof(int)] = ValueTypeSchema.Integer,
            [typeof(long)] = ValueTypeSchema.Integer,
            [typeof(float)] = ValueTypeSchema.Float,
            [typeof(double)] = ValueTypeSchema.Float,
            [typeof(string)] = ValueTypeSchema.String,
            [typeof(Vector2)] = ValueTypeSchema.Vector(2),
            [typeof(Vector3)] = ValueTypeSchema.Vector(3),
            [typeof(Vector4)] = ValueTypeSchema.Vector(4),
            [typeof(GameObject)] = ValueTypeSchema.GameObject,
            [typeof(Material)] = ValueTypeSchema.Material,
            [typeof(AnimationClip)] = ValueTypeSchema.AnimationClip,
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

        internal static IEnumerable<(Type Type, string CustomName)> EnumerateAttachmentTypes()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            return assemblies
                .SelectMany((asm) => asm.GetCustomAttributes<ExportArbittachSchemaAttribute>())
                .Select((attr) => (attr.SchemaType, attr.Name));
        }

        internal static AttachmentSchema SerializeSchema(Type attachmentType, string customName)
        {
            // add schema property
            var properties = new Dictionary<string, PropertySchema>();
            foreach (var defineAttribute in attachmentType.GetCustomAttributes<DefinePropertyAttribute>(false))
            {
                var propertySchema = new PropertySchema
                {
                    Name = defineAttribute.ArbittachPropertyName,
                    Required = defineAttribute.Required,
                    Parameters = Enumerable.Repeat(ValueTypeSchema.Any, defineAttribute.ParametersCount).ToList(),
                    Keywords = new List<KeywordSchema>(),
                };
                properties.Add(propertySchema.Name, propertySchema);
            }

            // traverse type properties
            foreach (var attachmentProperty in attachmentType.GetProperties())
            {
                if (attachmentProperty.GetCustomAttribute<BindValueAttribute>() is not BindValueAttribute bindValue) continue;
                var attachmentPropertyType = attachmentProperty.PropertyType;

                var parsedPath = ParsePath(bindValue.Path);
                if (!properties.TryGetValue(parsedPath.Property, out var targetSchemaProperty))
                {
                    throw new ArgumentException($"property name {parsedPath.Property} not found");
                }

                if (char.IsDigit(parsedPath.Indexer[0]))
                {
                    // parameter access
                    var parameterIndex = Convert.ToInt32(parsedPath.Indexer);
                    if (targetSchemaProperty.Parameters.Count < parameterIndex)
                    {
                        throw new IndexOutOfRangeException($"property {parsedPath.Property} has {targetSchemaProperty.Parameters.Count} arguments");
                    }
                    targetSchemaProperty.Parameters[parameterIndex] = ConstructSchemaType(attachmentPropertyType);
                }
                else
                {
                    // keyword access
                    targetSchemaProperty.Keywords.Add(new KeywordSchema
                    {
                        Name = parsedPath.Indexer,
                        Required = parsedPath.Required,
                        ValueType = ConstructSchemaType(attachmentPropertyType),
                    });
                }
            }

            return new AttachmentSchema()
            {
                Name = customName ?? attachmentType.Name,
                Properties = properties.Values.ToList(),
            };
        }

        private static (string Property, bool Required, string Indexer) ParsePath(string path)
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

            return (property, required, indexer);
        }

        private static ValueTypeSchema ConstructSchemaType(Type originalType)
        {
            // simple type
            if (SimpleSchemaTypes.TryGetValue(originalType, out var simpleSchemaType)) return simpleSchemaType;

            if (originalType.IsGenericType)
            {
                var genericDefinition = originalType.GetGenericTypeDefinition();
                if (genericDefinition.IsAssignableFrom(typeof(List<>)))
                {
                    // list
                    return ValueTypeSchema.List(ConstructSchemaType(originalType.GetGenericArguments()[0]));
                }
                if (TupleGenericDefinitions.Any((tt) => genericDefinition.IsAssignableFrom(tt)))
                {
                    // tuples
                    return ValueTypeSchema.Tuple(originalType.GetGenericArguments().Select(ConstructSchemaType));
                }
            }

            throw new ArgumentException($"type {originalType} cannot be serialized as schema type");
        }
    }
}
