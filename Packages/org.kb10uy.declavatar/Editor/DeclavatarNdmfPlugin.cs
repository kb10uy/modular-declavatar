using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using nadena.dev.ndmf;
using nadena.dev.ndmf.localization;
using KusakaFactory.Declavatar;
using KusakaFactory.Declavatar.Arbittach;
using KusakaFactory.Declavatar.Processor;
using KusakaFactory.Declavatar.Runtime;
using Avatar = KusakaFactory.Declavatar.Runtime.Data.Avatar;

[assembly: ExportsPlugin(typeof(DeclavatarNdmfPlugin))]
namespace KusakaFactory.Declavatar
{
    internal sealed class DeclavatarNdmfPlugin : Plugin<DeclavatarNdmfPlugin>
    {
        public override string DisplayName => "Declavatar";
        public override string QualifiedName => "org.kb10uy.declavatar";

        private JsonSerializerSettings _serializerSettings;
        private Localizer _localizer;
        private List<string> _libraryPaths;
        private Dictionary<string, IErasedProcessor> _processors;
        private IDeclavatarPass[] _passes;

        protected override void Configure()
        {
            var contractResolver = new DefaultContractResolver { NamingStrategy = new SnakeCaseNamingStrategy() };
            _serializerSettings = new JsonSerializerSettings { ContractResolver = contractResolver };
            _localizer = DeclavatarLocalizer.ConstructLocalizer();

            PrepareDeclavatarCommon();
            _passes = new IDeclavatarPass[]
            {
                new ExecuteArbittachPass(_processors),
                new GenerateControllerPass(),
                new GenerateParameterPass(),
                new GenerateMenuPass(),
            };

            InPhase(BuildPhase.Resolving).Run("Compile declaration files", PrepareDeclarations);
            InPhase(BuildPhase.Generating).Run("Process declaration elements", ProcessDeclarations);
            InPhase(BuildPhase.Transforming).Run("Remove declavatar components", RemoveComponents);
        }

        private void PrepareDeclavatarCommon()
        {
            _libraryPaths = Configuration.LoadEditorUserSettings().EnumerateAbsoluteLibraryPaths().ToList();

            _processors = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany((asm) => asm.GetCustomAttributes<ExportProcessorAttribute>())
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

        #region Compile declavatar files

        private void PrepareDeclarations(BuildContext ctx)
        {
            CompileAndReplaceDeclarations(ctx);
        }

        private void CompileAndReplaceDeclarations(BuildContext ctx)
        {
            var declavatarComponents = ctx.AvatarRootObject.GetComponentsInChildren<GenerateByDeclavatar>();
            foreach (var component in declavatarComponents)
            {
                if (component.Definition == null) continue;

                var (materials, animationClips, localizations) = AggregateExternalAssets(component.ExternalAssets);
                var symbols = component.Symbols.ToHashSet();
                var (declaration, logs) = CompileDeclaration(
                    component.Definition.text,
                    (DeclavatarFormat)component.Format,
                    symbols,
                    localizations
                );
                ReportLogsForNdmf(logs);
                if (declaration == null) continue;

                var compiledComponent = component.gameObject.AddComponent<GenerateByDeclavatar.CompiledDeclavatar>();
                compiledComponent.CompiledAvatar = declaration;
                compiledComponent.DeclarationRoot = component.DeclarationRoot;
                compiledComponent.MenuInstallTarget = component.InstallTarget;
                compiledComponent.CreateMenuInstallerComponent = component.GenerateMenuInstaller;
                compiledComponent.ExternalMaterials = materials;
                compiledComponent.ExternalAnimationClips = animationClips;
            }

            foreach (var component in declavatarComponents) UnityEngine.Object.DestroyImmediate(component);
        }

        private (Avatar, List<SerializedLog>) CompileDeclaration(string source, DeclavatarFormat format, HashSet<string> symbols, Dictionary<string, string> localizations)
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

        private (
            Dictionary<string, Material>,
            Dictionary<string, AnimationClip>,
            Dictionary<string, string>
        ) AggregateExternalAssets(
            ExternalAsset[] externalAssets
        )
        {
            var materials = new Dictionary<string, Material>();
            var animationClips = new Dictionary<string, AnimationClip>();
            var localizations = new Dictionary<string, string>();

            foreach (var externalAsset in externalAssets.Where((ea) => ea != null))
            {
                var validMaterials = externalAsset
                    .Materials
                    .Where((p) => !string.IsNullOrWhiteSpace(p.Key) && p.Material != null);
                var validAnimationClips = externalAsset
                    .Animations
                    .Where((p) => !string.IsNullOrWhiteSpace(p.Key) && p.Animation != null);
                var validLocalizations = externalAsset
                    .Localizations
                    .Where((p) => !string.IsNullOrWhiteSpace(p.Key) && !string.IsNullOrWhiteSpace(p.Localization));

                foreach (var pair in validMaterials) materials.Add(pair.Key, pair.Material);
                foreach (var pair in validAnimationClips) animationClips.Add(pair.Key, pair.Animation);
                foreach (var pair in validLocalizations) localizations.Add(pair.Key, pair.Localization);
            }

            return (materials, animationClips, localizations);
        }

        #endregion

        #region Process declaration elements

        private void ProcessDeclarations(BuildContext ctx)
        {
            var compiledComponents = ctx.AvatarRootObject.GetComponentsInChildren<GenerateByDeclavatar.CompiledDeclavatar>();
            // TODO: add ordering feature by int
            var exports = new DeclavatarExports(compiledComponents);
            foreach (var component in compiledComponents) ProcessForComponent(ctx, exports, component);
        }

        private void ProcessForComponent(BuildContext ctx, DeclavatarExports exports, GenerateByDeclavatar.CompiledDeclavatar compiled)
        {
            var context = new DeclavatarContext(ctx, _localizer, exports, compiled);
            foreach (var pass in _passes) pass.Execute(context);
        }

        #endregion

        #region Remove declavatar components

        private void RemoveComponents(BuildContext ctx)
        {
            var rootObject = ctx.AvatarRootObject;
            foreach (var component in rootObject.GetComponentsInChildren<GenerateByDeclavatar.CompiledDeclavatar>())
            {
                UnityEngine.Object.DestroyImmediate(component);
            }
            foreach (var component in rootObject.GetComponentsInChildren<GenerateByDeclavatar>())
            {
                UnityEngine.Object.DestroyImmediate(component);
            }
        }

        #endregion

        private void ReportLogsForNdmf(List<SerializedLog> logs)
        {
            foreach (var log in logs)
            {
                var severity = log.Severity switch
                {
                    "Information" => ErrorSeverity.Information,
                    "Warning" => ErrorSeverity.NonFatal,
                    "Error" => ErrorSeverity.Error,
                    _ => throw new DeclavatarInternalException("unknown severity"),
                };
                ErrorReport.ReportError(_localizer, severity, log.Kind, log.Args.ToArray());
            }
        }
    }

    internal static class DeclavatarLocalizer
    {
        public static Localizer ConstructLocalizer()
        {
            var localizations = new List<(string, Func<string, string>)>
            {
                ("en-us", CreateLocalizerFunc("en-us")),
                ("ja-jp", CreateLocalizerFunc("ja-jp")),
            };
            return new Localizer("en-us", () => localizations);
        }

        private static Func<string, string> CreateLocalizerFunc(string locale)
        {
            var coreLocalization = DeclavatarCore.GetLogLocalization(locale);
            var runtimeLocalization = GetRuntimeLocalization(locale);

            var dictionary = new Dictionary<string, string>(coreLocalization.Union(runtimeLocalization));
            return (key) =>
            {
                return dictionary.TryGetValue(key, out var value) ? value : null;
            };
        }

        private static Dictionary<string, string> GetRuntimeLocalization(string locale)
        {
            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>($"Packages/org.kb10uy.declavatar/Localization/{locale}.json");
            return JsonConvert.DeserializeObject<Dictionary<string, string>>(asset.text);
        }
    }
}
