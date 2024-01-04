using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using nadena.dev.ndmf;
using nadena.dev.ndmf.localization;
using AnimatorAsCode.V1;
using KusakaFactory.Declavatar.Runtime;
using Avatar = KusakaFactory.Declavatar.Runtime.Data.Avatar;

namespace KusakaFactory.Declavatar
{
    public sealed class DeclavatarContext
    {
        public GameObject AbsoluteAvatarRoot { get; }
        public GameObject DeclarationRoot { get; }
        public GameObject MenuInstallRoot { get; }
        public bool CreateMenuInstaller { get; }

        public Avatar AvatarDeclaration { get; }
        public AacFlBase Aac { get; }

        private Localizer _localizer;
        private Dictionary<string, Material> _externalMaterials;
        private Dictionary<string, AnimationClip> _externalAnimationClips;
        private Dictionary<string, string> _externalLocalizations;
        private Dictionary<string, Renderer> _rendererSearchCache;
        private Dictionary<string, SkinnedMeshRenderer> _skinnedMeshRendererSearchCache;
        private Dictionary<string, GameObject> _gameObjectSearchCache;
        private HashSet<string> _searchedPathCache;

        public DeclavatarContext(BuildContext ndmfContext, Localizer localizer, GenerateByDeclavatar.CompiledDeclavatar compiled)
        {
            AvatarDeclaration = compiled.CompiledAvatar;
            _externalMaterials = compiled.ExternalMaterials;
            _externalAnimationClips = compiled.ExternalAnimationClips;
            _externalLocalizations = compiled.ExternalLocalizations;
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

        public string GetExternalLocalization(string key)
        {
            if (_externalLocalizations.TryGetValue(key, out var localization)) return localization;
            ReportRuntimeError("runtime.localization_not_found", key);
            throw new DeclavatarRuntimeException($"External localization {key} not found");
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
}
