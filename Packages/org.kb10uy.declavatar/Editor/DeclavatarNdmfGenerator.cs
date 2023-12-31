using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using nadena.dev.ndmf;
using nadena.dev.ndmf.localization;
using AnimatorAsCode.V1.NDMFProcessor;
using KusakaFactory.Declavatar;
using KusakaFactory.Declavatar.EditorExtension;
using KusakaFactory.Declavatar.Runtime;
using AnimatorAsCode.V1;


[assembly: ExportsPlugin(typeof(DeclavatarNdmfGenerator))]
[assembly: ExportsPlugin(typeof(DeclavatarComponentRemover))]
namespace KusakaFactory.Declavatar
{
    public sealed class DeclavatarProcessor : Plugin<DeclavatarProcessor>
    {
        public override string DisplayName => "Declavatar Processor";
        public override string QualifiedName => "org.kb10uy.declavatar-processor";

        private JsonSerializerSettings _serializerSettings;
        private Localizer _localizer;


        protected override void Configure()
        {
            _serializerSettings = new JsonSerializerSettings
            {
                ContractResolver = new DefaultContractResolver
                {
                    NamingStrategy = new SnakeCaseNamingStrategy(),
                }
            };
            _localizer = ConstructLocalizer();

            InPhase(BuildPhase.Generating).Run("Compile and generate declavatar files", Execute);
        }

        private void Execute(BuildContext ctx)
        {
            var allChildrenComponents = ctx.AvatarRootObject.GetComponentsInChildren<GenerateByDeclavatar>();
            // TODO: add ordering feature by int
            foreach (var component in allChildrenComponents)
            {
                ProcessForComponent(ctx, component);
            }
        }

        private void ProcessForComponent(BuildContext ctx, GenerateByDeclavatar gbd)
        {
            var (declaration, logs) = CompileDeclaration(gbd.Definition.text, (FormatKind)gbd.Format);
            ReportLogsForNdmf(logs);

            var aac = AacV1.Create(new AacConfiguration
            {
                // MEMO: should it use avatar name from decl file?
                SystemName = "Declavatar",

                // MEMO: should it be Declaration Root?
                AnimatorRoot = ctx.AvatarRootTransform,
                DefaultValueRoot = ctx.AvatarRootTransform,

                AssetKey = GUID.Generate().ToString(),
                AssetContainer = ctx.AssetContainer,
                ContainerMode = AacConfiguration.Container.OnlyWhenPersistenceRequired,
                DefaultsProvider = new AacDefaultsProvider(false),
            });

            var declavatar = new NonDestructiveDeclavatar(
                gbd.DeclarationRoot != null ? gbd.DeclarationRoot : gbd.gameObject,
                gbd.InstallTarget,
                localizer,
                aac,
                definition,
                externalAssets
            );
            declavatar.Execute(my.GenerateMenuInstaller);
        }

        #region Compilation

        private (Data.Avatar, List<string>) CompileDeclaration(string source, FormatKind format)
        {
            using var declavatarPlugin = new Plugin();
            declavatarPlugin.Reset();

            // Load libraries
            foreach (var libraryPath in GetDeclavatarLibraryPaths()) declavatarPlugin.AddLibraryPath(libraryPath);

            // Compile declaration
            var compileResult = declavatarPlugin.Compile(source, format);
            var logs = declavatarPlugin.FetchLogJsons();
            if (!compileResult) return (null, logs);

            var definitionJson = declavatarPlugin.GetAvatarJson();
            var definition = JsonConvert.DeserializeObject<Data.Avatar>(definitionJson, _serializerSettings);
            return (definition, logs);
        }

        private IEnumerable<string> GetDeclavatarLibraryPaths()
        {
            var assetsPath = Application.dataPath;
            var projectPath = Path.GetDirectoryName(assetsPath);
            var config = Configuration.LoadEditorUserSettings();

            return config
                .LibraryRelativePath
                .Select((p) => p.Trim())
                .Where((p) => !string.IsNullOrEmpty(p))
                .Select((p) => Path.Combine(projectPath, p));
        }

        #endregion

        #region Localization

        private static Localizer ConstructLocalizer()
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
            var coreLocalization = Plugin.GetLogLocalization(locale);
            var runtimeLocalization = GetRuntimeLocalization(locale);

            var dictionary = new Dictionary<string, string>(coreLocalization.Union(runtimeLocalization));
            return (key) =>
            {
                return dictionary.TryGetValue(key, out var value) ? value : null;
            };
        }

        private static Dictionary<string, string> GetRuntimeLocalization(string locale)
        {
            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>($"Packages/org.kb10uy.declavatar/Editor/Localization/{locale}.json");
            return JsonConvert.DeserializeObject<Dictionary<string, string>>(asset.text);
        }

        #endregion

        #region Logging

        private void ReportLogsForNdmf(List<string> logJsons)
        {
            foreach (var logJson in logJsons)
            {
                var serializedLog = JsonConvert.DeserializeObject<Data.SerializedLog>(logJson, _serializerSettings);
                var severity = serializedLog.Severity switch
                {
                    "Information" => ErrorSeverity.Information,
                    "Warning" => ErrorSeverity.NonFatal,
                    "Error" => ErrorSeverity.Error,
                    _ => throw new DeclavatarInternalException("unknown severity"),
                };
                ErrorReport.ReportError(_localizer, severity, serializedLog.Kind, serializedLog.Args.ToArray());
            }
        }

        #endregion
    }

    public class DeclavatarNdmfGenerator : AacPlugin<GenerateByDeclavatar>
    {


        protected override AacPluginOutput Execute()
        {
            // Skip if definition is empty
            if (my.Definition == null) return AacPluginOutput.Regular();

            // Compile
            var localizer = ConstructLocalizer();
            string definitionJson;
            using (var declavatarPlugin = new Plugin())
            {
                declavatarPlugin.Reset();

                var config = Configuration.LoadEditorUserSettings();
                foreach (var path in config.LibraryRelativePath)
                {
                    var p = path.Trim();
                    if (!string.IsNullOrEmpty(p)) declavatarPlugin.AddLibraryPath(ConcatenateProjectRelativePath(p));
                }

                if (!declavatarPlugin.Compile(my.Definition.text, (FormatKind)(uint)my.Format))
                {
                    ReportLogsForNdmf(localizer, declavatarPlugin.FetchLogJsons());
                    return AacPluginOutput.Regular();
                }

                definitionJson = declavatarPlugin.GetAvatarJson();
            }

            var definition = JsonConvert.DeserializeObject<Data.Avatar>(definitionJson, _serializerSettings);
            var externalAssets = my.ExternalAssets.Where((ea) => ea != null).ToList();
            Debug.Log($"Declavatar: definition '{definition.Name}' compiled");


            return AacPluginOutput.Regular();
        }

    }

    public class DeclavatarComponentRemover : Plugin<DeclavatarComponentRemover>
    {
        public override string DisplayName => "Declavatar Component Remover";
        public override string QualifiedName => "org.kb10uy.declavatar-remover";

        protected override void Configure()
        {
            InPhase(BuildPhase.Transforming).Run("Remove Declavatar Components", Execute);
        }

        private void Execute(BuildContext ctx)
        {
            var rootObject = ctx.AvatarRootObject;
            var components = rootObject.GetComponentsInChildren<GenerateByDeclavatar>();
            foreach (var component in components) UnityEngine.Object.DestroyImmediate(component);
        }
    }
}
