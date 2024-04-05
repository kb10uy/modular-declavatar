using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using KusakaFactory.Declavatar.Arbittach;
using Avatar = KusakaFactory.Declavatar.Runtime.Data.Avatar;

namespace KusakaFactory.Declavatar
{
    internal sealed class DeclavatarCompileService
    {
        private JsonSerializerSettings _serializerSettings;
        private List<string> _libraryPaths;
        private Dictionary<string, IErasedProcessor> _processors;

        public IReadOnlyList<string> LibraryPaths => _libraryPaths;
        public IReadOnlyDictionary<string, IErasedProcessor> ArbittachProcessors => _processors;

        private DeclavatarCompileService(List<string> paths, Dictionary<string, IErasedProcessor> processors)
        {
            _libraryPaths = paths;
            _processors = processors;

            var contractResolver = new DefaultContractResolver { NamingStrategy = new SnakeCaseNamingStrategy() };
            _serializerSettings = new JsonSerializerSettings { ContractResolver = contractResolver };
        }

        public static DeclavatarCompileService Create()
        {
            var paths = Configuration.LoadEditorUserSettings().EnumerateAbsoluteLibraryPaths().ToList();
            var processors = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany((asm) => asm.GetCustomAttributes<ExportsProcessorAttribute>())
                .Select((attr) => (attr.ProcessorType, attr.Name, AttachmentType: ScanArbittachProcessorType(attr.ProcessorType)))
                .Where((p) => p.AttachmentType != null)
                .Select((p) =>
                {
                    var constructor = p.ProcessorType.GetConstructor(new Type[] { });
                    var erasedProcessor = constructor.Invoke(new object[] { }) as IErasedProcessor;
                    var definition = AttachmentDefinition.Create(p.AttachmentType, p.Name);
                    erasedProcessor.Configure(definition);
                    return erasedProcessor;
                })
                .ToDictionary((ep) => ep.Definition.RegisteredName);

            return new DeclavatarCompileService(paths, processors);
        }

        internal (Avatar, List<SerializedLog>) CompileDeclaration(string source, DeclavatarFormat format, HashSet<string> symbols, Dictionary<string, string> localizations)
        {
            string avatarJson;
            List<string> logJsons;
            unsafe
            {
                void* declavatar = null;
                void* compiled = null;
                try
                {
                    declavatar = DeclavatarCore.Create();
                    foreach (var path in _libraryPaths) DeclavatarCore.AddLibraryPath(declavatar, path);
                    foreach (var processor in _processors.Values)
                    {
                        var schemaJson = JsonConvert.SerializeObject(processor.Definition.Schema, _serializerSettings);
                        DeclavatarCore.RegisterArbittach(declavatar, schemaJson);
                    }

                    foreach (var symbol in symbols) DeclavatarCore.DefineSymbol(declavatar, symbol);
                    foreach (var (key, value) in localizations) DeclavatarCore.DefineLocalization(declavatar, key, value);

                    compiled = DeclavatarCore.Compile(declavatar, source, format);
                    logJsons = DeclavatarCore.GetLogJsons(compiled);
                    avatarJson = DeclavatarCore.GetAvatarJson(compiled);
                }
                finally
                {
                    DeclavatarCore.DestroyCompiledState(compiled);
                    DeclavatarCore.Destroy(declavatar);
                }
            }

            var definition = avatarJson != null ? JsonConvert.DeserializeObject<Avatar>(avatarJson, _serializerSettings) : null;
            var logs = logJsons.Select((lj) => JsonConvert.DeserializeObject<SerializedLog>(lj, _serializerSettings)).ToList();
            return (definition, logs);
        }

        private static Type ScanArbittachProcessorType(Type type)
        {
            var checkingType = type;
            while (true)
            {
                checkingType = checkingType.BaseType;
                if (checkingType == null) return null;

                if (!checkingType.IsGenericType) continue;
                var genericDefinition = checkingType.GetGenericTypeDefinition();
                if (!genericDefinition.IsAssignableFrom(typeof(ArbittachProcessor<,>))) continue;

                return checkingType.GenericTypeArguments[1];
            }
        }
    }
}
