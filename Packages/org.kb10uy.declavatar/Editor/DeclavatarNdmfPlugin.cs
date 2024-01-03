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
using KusakaFactory.Data;
using KusakaFactory.Declavatar.Runtime;
using KusakaFactory.Declavatar.Processor;

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

            InPhase(BuildPhase.Generating).Run("Compile and generate declavatar files", ProcessDeclarations);
            InPhase(BuildPhase.Transforming).Run("Remove declavatar components", RemoveComponents);
        }

        private void ProcessDeclarations(BuildContext ctx)
        {
            var allChildrenComponents = ctx.AvatarRootObject.GetComponentsInChildren<GenerateByDeclavatar>();
            // TODO: add ordering feature by int
            foreach (var component in allChildrenComponents) ProcessForComponent(ctx, component);
        }

        private void RemoveComponents(BuildContext ctx)
        {
            var rootObject = ctx.AvatarRootObject;
            var components = rootObject.GetComponentsInChildren<GenerateByDeclavatar>();
            foreach (var component in components) UnityEngine.Object.DestroyImmediate(component);
        }

        private void ProcessForComponent(BuildContext ctx, GenerateByDeclavatar gbd)
        {
            var (declaration, logs) = CompileDeclaration(gbd.Definition.text, (FormatKind)gbd.Format);
            ReportLogsForNdmf(logs);
            if (declaration == null) return;

            var context = new DeclavatarContext(ctx, _localizer, gbd, declaration);
            foreach (var pass in _passes) pass.Execute(context);
        }

        private (Data.Avatar, List<string>) CompileDeclaration(string source, FormatKind format)
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
            var definition = JsonConvert.DeserializeObject<Data.Avatar>(definitionJson, _serializerSettings);
            return (definition, logs);
        }

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
