using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using nadena.dev.ndmf;
using nadena.dev.ndmf.localization;
using KusakaFactory.Declavatar;
using KusakaFactory.Declavatar.Runtime;
using KusakaFactory.Declavatar.Processor;
using Avatar = KusakaFactory.Declavatar.Runtime.Data.Avatar;

[assembly: ExportsPlugin(typeof(DeclavatarNdmfPlugin))]
namespace KusakaFactory.Declavatar
{
    public sealed class DeclavatarNdmfPlugin : Plugin<DeclavatarNdmfPlugin>
    {
        public override string DisplayName => "Declavatar";
        public override string QualifiedName => "org.kb10uy.declavatar";

        private Localizer _localizer;
        private JsonSerializerSettings _serializerSettings;
        private IDeclavatarPass[] _passes;

        protected override void Configure()
        {
            _localizer = DeclavatarLocalizer.ConstructLocalizer();
            _serializerSettings = new JsonSerializerSettings
            {
                ContractResolver = new DefaultContractResolver { NamingStrategy = new SnakeCaseNamingStrategy() }
            };
            _passes = new IDeclavatarPass[]
            {
                new GenerateControllerPass(),
                new GenerateParameterPass(),
                new GenerateMenuPass(),
            };

            InPhase(BuildPhase.Resolving).Run("Compile declaration files", CompileAllDeclarations);
            InPhase(BuildPhase.Generating).Run("Process declaration elements", ProcessDeclarations);
            InPhase(BuildPhase.Transforming).Run("Remove declavatar components", RemoveComponents);
        }

        #region Compile declavatar files

        private void CompileAllDeclarations(BuildContext ctx)
        {
            var declavatarComponents = ctx.AvatarRootObject.GetComponentsInChildren<GenerateByDeclavatar>();
            foreach (var component in declavatarComponents)
            {
                if (component.Definition == null) return;
                var (declaration, logs) = CompileDeclaration(component.Definition.text, (FormatKind)component.Format);
                ReportLogsForNdmf(logs);
                if (declaration == null) return;

                var compiledComponent = component.gameObject.AddComponent<GenerateByDeclavatar.CompiledDeclavatar>();
                compiledComponent.CompiledAvatar = declaration;
                compiledComponent.DeclarationRoot = component.DeclarationRoot;
                compiledComponent.MenuInstallTarget = component.InstallTarget;
                compiledComponent.CreateMenuInstallerComponent = component.GenerateMenuInstaller;
                AggregateExternalAssets(compiledComponent, component.ExternalAssets);
            }

            foreach (var component in declavatarComponents) UnityEngine.Object.DestroyImmediate(component);
        }

        private (Avatar, List<string>) CompileDeclaration(string source, FormatKind format)
        {
            using var declavatarPlugin = new DeclavatarCore();
            declavatarPlugin.Reset();

            // Load libraries
            var configuration = Configuration.LoadEditorUserSettings();
            foreach (var libraryPath in configuration.EnumerateAbsoluteLibraryPaths()) declavatarPlugin.AddLibraryPath(libraryPath);

            // Compile declaration
            var compileResult = declavatarPlugin.Compile(source, format);
            var logs = declavatarPlugin.FetchLogJsons();
            if (!compileResult) return (null, logs);

            var definitionJson = declavatarPlugin.GetAvatarJson();
            var definition = JsonConvert.DeserializeObject<Avatar>(definitionJson, _serializerSettings);
            return (definition, logs);
        }

        private void AggregateExternalAssets(GenerateByDeclavatar.CompiledDeclavatar compiled, ExternalAsset[] externalAssets)
        {
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

                foreach (var pair in validMaterials) compiled.ExternalMaterials.Add(pair.Key, pair.Material);
                foreach (var pair in validAnimationClips) compiled.ExternalAnimationClips.Add(pair.Key, pair.Animation);
                foreach (var pair in validLocalizations) compiled.ExternalLocalizations.Add(pair.Key, pair.Localization);
            }
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
            var components = rootObject.GetComponentsInChildren<GenerateByDeclavatar.CompiledDeclavatar>();
            foreach (var component in components) UnityEngine.Object.DestroyImmediate(component);
        }

        #endregion

        private void ReportLogsForNdmf(List<string> logJsons)
        {
            foreach (var logJson in logJsons)
            {
                var serializedLog = JsonConvert.DeserializeObject<SerializedLog>(logJson, _serializerSettings);
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
    }

    public static class DeclavatarLocalizer
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
