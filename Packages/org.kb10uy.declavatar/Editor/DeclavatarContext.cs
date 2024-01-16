using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using nadena.dev.ndmf;
using nadena.dev.ndmf.localization;
using AnimatorAsCode.V1;
using KusakaFactory.Declavatar.Runtime;
using KusakaFactory.Declavatar.Runtime.Data;
using Avatar = KusakaFactory.Declavatar.Runtime.Data.Avatar;

namespace KusakaFactory.Declavatar
{
    internal sealed class DeclavatarContext
    {
        public DeclavatarExports AllExports { get; }
        public GameObject AbsoluteAvatarRoot { get; }
        public GameObject DeclarationRoot { get; }
        public GameObject MenuInstallRoot { get; }
        public bool CreateMenuInstaller { get; }

        public Avatar AvatarDeclaration { get; }
        public AacFlBase Aac { get; }

        private Localizer _localizer;
        private Dictionary<string, Material> _externalMaterials;
        private Dictionary<string, AnimationClip> _externalAnimationClips;
        private Dictionary<string, Renderer> _rendererSearchCache;
        private Dictionary<string, SkinnedMeshRenderer> _skinnedMeshRendererSearchCache;
        private Dictionary<string, GameObject> _gameObjectSearchCache;
        private HashSet<string> _searchedPathCache;

        public DeclavatarContext(BuildContext ndmfContext, Localizer localizer, DeclavatarExports exports, GenerateByDeclavatar.CompiledDeclavatar compiled)
        {
            AllExports = exports;
            AvatarDeclaration = compiled.CompiledAvatar;
            _externalMaterials = compiled.ExternalMaterials;
            _externalAnimationClips = compiled.ExternalAnimationClips;
            CreateMenuInstaller = compiled.CreateMenuInstallerComponent;
            DeclarationRoot = compiled.DeclarationRoot != null ? compiled.DeclarationRoot : compiled.gameObject;
            if (compiled.MenuInstallTarget != null)
            {
                MenuInstallRoot = compiled.MenuInstallTarget;
            }
            else
            {
                MenuInstallRoot = new GameObject("DeclavatarMenuRoot");
                MenuInstallRoot.transform.parent = DeclarationRoot.transform;
            }

            AbsoluteAvatarRoot = ndmfContext.AvatarRootObject;
            Aac = AacV1.Create(new AacConfiguration
            {
                // MEMO: should it use avatar name from decl file?
                SystemName = "Declavatar",

                // MEMO: should it be Declaration Root?
                AnimatorRoot = ndmfContext.AvatarRootTransform,
                DefaultValueRoot = ndmfContext.AvatarRootTransform,

                AssetKey = GUID.Generate().ToString(),
                AssetContainer = ndmfContext.AssetContainer,
                ContainerMode = AacConfiguration.Container.OnlyWhenPersistenceRequired,
                DefaultsProvider = new AacDefaultsProvider(false),
            });

            _localizer = localizer;
            _rendererSearchCache = new Dictionary<string, Renderer>();
            _skinnedMeshRendererSearchCache = new Dictionary<string, SkinnedMeshRenderer>();
            _gameObjectSearchCache = new Dictionary<string, GameObject>();
            _searchedPathCache = new HashSet<string>();
        }

        public Renderer FindRenderer(string path)
        {
            var cachedPath = $"mr://{path}";
            if (_searchedPathCache.Contains(cachedPath))
            {
                return _rendererSearchCache.TryGetValue(path, out var mr) ? mr : null;
            }
            else
            {
                var gameObject = FindGameObject(path);
                var renderer = gameObject.GetComponent<Renderer>();
                if (renderer == null)
                {
                    ReportRuntimeError("runtime.renderer_not_found", path, AvatarDeclaration.Name);
                    throw new DeclavatarRuntimeException($"Renderer {path} not found");
                }
                _searchedPathCache.Add(cachedPath);
                _rendererSearchCache[path] = renderer;
                return renderer;
            }
        }

        public SkinnedMeshRenderer FindSkinnedMeshRenderer(string path)
        {
            var cachedPath = $"smr://{path}";
            if (_searchedPathCache.Contains(cachedPath))
            {
                return _skinnedMeshRendererSearchCache.TryGetValue(path, out var smr) ? smr : null;
            }
            else
            {
                var gameObject = FindGameObject(path);
                var skinnedMeshRenderer = gameObject.GetComponent<SkinnedMeshRenderer>();
                if (skinnedMeshRenderer == null)
                {
                    ReportRuntimeError("runtime.skinned_renderer_not_found", path, AvatarDeclaration.Name);
                    throw new DeclavatarRuntimeException($"SkinnedMeshRenderer {path} not found");
                }
                _searchedPathCache.Add(cachedPath);
                _skinnedMeshRendererSearchCache[path] = skinnedMeshRenderer;
                return skinnedMeshRenderer;
            }
        }

        public GameObject FindGameObject(string path)
        {
            var cachedPath = $"go://{path}";
            if (_searchedPathCache.Contains(cachedPath))
            {
                return _gameObjectSearchCache.TryGetValue(path, out var smr) ? smr : null;
            }
            else
            {
                var transform = DeclarationRoot.transform.Find(path);
                if (transform == null)
                {
                    ReportRuntimeError("runtime.object_not_found", path, AvatarDeclaration.Name);
                    throw new DeclavatarRuntimeException($"GameObject {path} not found");
                }
                _searchedPathCache.Add(cachedPath);
                _gameObjectSearchCache[path] = transform.gameObject;
                return transform.gameObject;
            }
        }

        public Material GetExternalMaterial(string key)
        {
            if (_externalMaterials.TryGetValue(key, out var material)) return material;
            ReportRuntimeError("runtime.material_not_found", key);
            throw new DeclavatarRuntimeException($"External material {key} not found");
        }

        public AnimationClip GetExternalAnimationClip(string key)
        {
            if (_externalAnimationClips.TryGetValue(key, out var animationClip)) return animationClip;
            ReportRuntimeError("runtime.animation_not_found", key);
            throw new DeclavatarRuntimeException($"External animation clip {key} not found");
        }

        public void ReportRuntimeError(string errorKey, params object[] args)
        {
            ErrorReport.ReportError(_localizer, ErrorSeverity.Error, errorKey, args);
        }

        public void ReportInternalError(string errorKey, params object[] args)
        {
            ErrorReport.ReportError(_localizer, ErrorSeverity.InternalError, errorKey, args);
        }
    }

    internal sealed class DeclavatarExports
    {
        private HashSet<string> _gates;
        private Dictionary<string, List<string>> _guards;

        public DeclavatarExports(GenerateByDeclavatar.CompiledDeclavatar[] compiledComponents)
        {
            _gates = new HashSet<string>();
            _guards = new Dictionary<string, List<string>>();
            ConstructGatesAndGuards(compiledComponents);
        }

        public IReadOnlyList<string> GetGateGuardParameters(string gate)
        {
            if (_guards.TryGetValue(gate, out var guardParameters))
            {
                return guardParameters;
            }
            else
            {
                return null;
            }
        }

        private void ConstructGatesAndGuards(GenerateByDeclavatar.CompiledDeclavatar[] compiledComponents)
        {
            var gateExportNames = compiledComponents
                .SelectMany((c) => c.CompiledAvatar.Exports)
                .Where((p) => p is ExportItem.GateExport)
                .Cast<ExportItem.GateExport>()
                .Select((g) => g.Name);
            var guardExportGroups = compiledComponents
                .SelectMany((c) => c.CompiledAvatar.Exports)
                .Where((p) => p is ExportItem.GuardExport)
                .Cast<ExportItem.GuardExport>()
                .GroupBy((g) => g.Gate);

            foreach (var gateName in gateExportNames)
            {
                if (_gates.Add(gateName)) _guards.Add(gateName, new List<string>());
            }
            foreach (var guardGroup in guardExportGroups)
            {
                var gateName = guardGroup.Key;
                var guardParameters = guardGroup.Select((g) => g.Parameter).ToList();
                _guards[gateName].AddRange(guardParameters);
            }
        }
    }
}
