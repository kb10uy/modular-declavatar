using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json;
using nadena.dev.ndmf;
using nadena.dev.ndmf.localization;
using KusakaFactory.Declavatar;
using KusakaFactory.Declavatar.Processor;
using KusakaFactory.Declavatar.Runtime;

[assembly: ExportsPlugin(typeof(DeclavatarNdmfPlugin))]
namespace KusakaFactory.Declavatar
{
    internal sealed class DeclavatarNdmfPlugin : Plugin<DeclavatarNdmfPlugin>
    {
        public override string DisplayName => "Declavatar";
        public override string QualifiedName => "org.kb10uy.declavatar";

        private Localizer _localizer;
        private DeclavatarCompileService _compileService;
        private IDeclavatarPass[] _passes;

        protected override void Configure()
        {
            _localizer = DeclavatarLocalizer.ConstructLocalizer();
            _compileService = DeclavatarCompileService.Create();
            _passes = new IDeclavatarPass[]
            {
                new ExecuteArbittachPass(_compileService.ArbittachProcessors),
                new GenerateControllerPass(),
                new GenerateParameterPass(),
                new GenerateMenuPass(),
            };

            InPhase(BuildPhase.Resolving).Run("Compile declaration files", PrepareDeclarations);
            InPhase(BuildPhase.Generating).Run("Process declaration elements", ProcessDeclarations);
            InPhase(BuildPhase.Transforming).Run("Remove declavatar components", RemoveComponents);
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

                var externalAssets = AggregateErasedExternalAssets(component.ExternalAssets);
                var symbols = component.Symbols.ToHashSet();
                var (declaration, logs) = _compileService.CompileDeclaration(
                    component.Definition.text,
                    (DeclavatarFormat)component.Format,
                    symbols,
                    new Dictionary<string, string>()
                );
                ReportLogsForNdmf(logs);
                if (declaration == null) continue;

                var compiledComponent = component.gameObject.AddComponent<GenerateByDeclavatar.CompiledDeclavatar>();
                compiledComponent.CompiledAvatar = declaration;
                compiledComponent.ExternalAssets = externalAssets;
                compiledComponent.DeclarationRoot = component.DeclarationRoot;
                compiledComponent.MenuInstallTarget = component.InstallTarget;
                compiledComponent.CreateMenuInstallerComponent = component.GenerateMenuInstaller;
            }

            foreach (var component in declavatarComponents) UnityEngine.Object.DestroyImmediate(component);
        }

        private Dictionary<string, (string, UnityEngine.Object)> AggregateErasedExternalAssets(DeclavatarExternalAssets[] externalAssets)
        {
            var aggregated = new Dictionary<string, (string, UnityEngine.Object)>();
            foreach (var entry in externalAssets.Where((ea) => ea != null).SelectMany((ea) => ea.Entries))
            {
                if (string.IsNullOrWhiteSpace(entry.Key)) continue;
                if (entry.Asset == null) continue;
                aggregated.Add(entry.Key, (entry.Type, entry.Asset));
            }
            return aggregated;
        }

        #endregion

        #region Process declaration elements

        private void ProcessDeclarations(BuildContext ctx)
        {
            var compiledComponents = ctx.AvatarRootObject.GetComponentsInChildren<GenerateByDeclavatar.CompiledDeclavatar>();
            // TODO: add ordering feature by int
            var exports = new AggregatedExports(compiledComponents);
            foreach (var component in compiledComponents) ProcessForComponent(ctx, exports, component);
        }

        private void ProcessForComponent(BuildContext ctx, AggregatedExports exports, GenerateByDeclavatar.CompiledDeclavatar compiled)
        {
            var context = new DeclavatarContext(ctx, _localizer, compiled, exports);
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
                    _ => throw new DeclavatarInternalException($"unknown severity: {log.Severity}"),
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
            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>($"Packages/org.kb10uy.declavatar/Resources/Localizations/{locale}.json");
            return JsonConvert.DeserializeObject<Dictionary<string, string>>(asset.text);
        }
    }
}
