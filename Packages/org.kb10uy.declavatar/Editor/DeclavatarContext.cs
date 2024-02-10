using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using nadena.dev.ndmf;
using nadena.dev.ndmf.localization;
using KusakaFactory.Declavatar.Runtime;
using KusakaFactory.Declavatar.Runtime.Data;
using Avatar = KusakaFactory.Declavatar.Runtime.Data.Avatar;

namespace KusakaFactory.Declavatar
{
    /// <summary>
    /// Contextual information set per declaration file.
    /// </summary>
    public sealed class DeclavatarContext
    {
        /// <summary>
        /// Current BuildContext from NDMF pass.
        /// </summary>
        public BuildContext NdmfContext { get; }

        /// <summary>
        /// Compiled and deserialized avatar declaration.
        /// </summary>
        public Avatar AvatarDeclaration { get; }

        /// <summary>
        /// Aggregated exports over the whole avatar.
        /// </summary>
        public AggregatedExports Exports { get; }

        /// <summary>
        /// Declaration root object of this declaration.
        /// </summary>
        public GameObject DeclarationRoot { get; }

        /// <summary>
        /// Menu root object of this declaration.
        /// </summary>
        public GameObject MenuInstallRoot { get; }

        /// <summary>
        /// Whether it should create the MenuInstaller component for this declaration.
        /// </summary>
        public bool CreateMenuInstaller { get; }

        internal DeclavatarContext(BuildContext ndmfContext, Localizer localizer, GenerateByDeclavatar.CompiledDeclavatar compiled, AggregatedExports exports)
        {
            NdmfContext = ndmfContext;

            AvatarDeclaration = compiled.CompiledAvatar;
            Exports = exports;
            DeclarationRoot = compiled.DeclarationRoot != null ? compiled.DeclarationRoot : compiled.gameObject;
            CreateMenuInstaller = compiled.CreateMenuInstallerComponent;
            if (compiled.MenuInstallTarget != null)
            {
                MenuInstallRoot = compiled.MenuInstallTarget;
            }
            else
            {
                MenuInstallRoot = new GameObject("DeclavatarMenuRoot");
                MenuInstallRoot.transform.parent = DeclarationRoot.transform;
            }

            _localizer = localizer;
            _externalMaterials = compiled.ExternalMaterials;
            _externalAnimationClips = compiled.ExternalAnimationClips;
        }

        #region Hierarchy Search

        private Dictionary<string, Renderer> _rendererSearchCache = new();
        private Dictionary<string, SkinnedMeshRenderer> _skinnedMeshRendererSearchCache = new();
        private Dictionary<string, GameObject> _gameObjectSearchCache = new();
        private HashSet<string> _searchedPathCache = new();

        /// <summary>
        /// Finds Renderer component in the declaring object by GameObject path.
        /// </summary>
        /// <param name="path">FindGameObject based path.</param>
        /// <returns>Renderer.</returns>
        /// <exception cref="DeclavatarRuntimeException">Renderer not found.</exception>
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

        /// <summary>
        /// Finds SkinnedMeshRenderer component in the declaring object by GameObject path.
        /// </summary>
        /// <param name="path">FindGameObject based path.</param>
        /// <returns>SkinnedMeshRenderer.</returns>
        /// <exception cref="DeclavatarRuntimeException">SkinnedMeshRenderer not found.</exception>
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

        /// <summary>
        /// Finds GameObject in the declaring object by GameObject path.
        /// </summary>
        /// <param name="path">FindGameObject based path.</param>
        /// <returns>GameObject.</returns>
        /// <exception cref="DeclavatarRuntimeException">GameObject not found.</exception>
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

        #endregion

        #region External Assets

        private Dictionary<string, Material> _externalMaterials;
        private Dictionary<string, AnimationClip> _externalAnimationClips;

        /// <summary>
        /// Searches external asset Material.
        /// </summary>
        /// <param name="key">Asset key.</param>
        /// <returns>Defined Material.</returns>
        /// <exception cref="DeclavatarRuntimeException">Key not found.</exception>
        public Material GetExternalMaterial(string key)
        {
            if (_externalMaterials.TryGetValue(key, out var material)) return material;
            ReportRuntimeError("runtime.material_not_found", key);
            throw new DeclavatarRuntimeException($"External material {key} not found");
        }

        /// <summary>
        /// Searches external asset AnimationClip.
        /// </summary>
        /// <param name="key">Asset key.</param>
        /// <returns>Defined AnimationClip.</returns>
        /// <exception cref="DeclavatarRuntimeException">Key not found.</exception>
        internal AnimationClip GetExternalAnimationClip(string key)
        {
            if (_externalAnimationClips.TryGetValue(key, out var animationClip)) return animationClip;
            ReportRuntimeError("runtime.animation_not_found", key);
            throw new DeclavatarRuntimeException($"External animation clip {key} not found");
        }

        #endregion

        #region Error Reporting

        private Localizer _localizer;

        internal void ReportRuntimeError(string errorKey, params object[] args)
        {
            ErrorReport.ReportError(_localizer, ErrorSeverity.Error, errorKey, args);
        }

        internal void ReportInternalError(string errorKey, params object[] args)
        {
            ErrorReport.ReportError(_localizer, ErrorSeverity.InternalError, errorKey, args);
        }

        #endregion
    }

    /// <summary>
    /// Aggregated exports block data over the root avatar.
    /// </summary>
    public sealed class AggregatedExports
    {
        private HashSet<string> _gates;
        private Dictionary<string, List<string>> _guards;

        internal AggregatedExports(GenerateByDeclavatar.CompiledDeclavatar[] compiledComponents)
        {
            _gates = new HashSet<string>();
            _guards = new Dictionary<string, List<string>>();
            ConstructGatesAndGuards(compiledComponents);
        }

        /// <summary>
        /// Get list of parameter names that are driven by specified gate.
        /// </summary>
        /// <param name="gate">Gate name.</param>
        /// <returns>List of parameters, or null if the gate does not exist.</returns>
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
