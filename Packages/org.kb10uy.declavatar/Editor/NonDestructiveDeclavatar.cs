using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using AnimatorAsCode.V1;
using AnimatorAsCode.V1.ModularAvatar;
using AnimatorAsCode.V1.VRC;
using nadena.dev.modular_avatar.core;
using KusakaFactory.Declavatar.Runtime;
using KusakaFactory.Declavatar.Editor;

namespace KusakaFactory.Declavatar
{
    public sealed class NonDestructiveDeclavatar
    {
        private GameObject _rootGameObject;
        private GameObject _installTarget;
        private Avatar _declavatarDefinition;
        private IReadOnlyList<ExternalAsset> _externalAssets;
        private AacFlBase _ndmfAac;
        // private MaAc _maAc;
        private BuildLogWindow _logWindow;

        private GameObjectSearcher _searcher;

        public NonDestructiveDeclavatar(GameObject root, GameObject installTarget, AacFlBase aac, Avatar definition, IReadOnlyList<ExternalAsset> assets)
        {
            _rootGameObject = root;
            _installTarget = installTarget;
            _declavatarDefinition = definition;
            _externalAssets = assets;
            _ndmfAac = aac;
            // _maAc = MaAc.Create(root);
            _logWindow = null;

            _searcher = new GameObjectSearcher(root);
        }

        public void Execute()
        {
            GenerateFXLayerNonDestructive();
            GenerateParametersNonDestructive();
            GenerateMenuNonDestructive();
        }

        private void GenerateFXLayerNonDestructive()
        {
            var fxAnimator = _ndmfAac.NewAnimatorController();

            GeneratePreventionLayers(fxAnimator);
            foreach (var animationGroup in _declavatarDefinition.AnimationGroups)
            {
                try
                {
                    switch (animationGroup.Content)
                    {
                        case AnimationGroup.Group g:
                            GenerateGroupLayer(fxAnimator, animationGroup.Name, g);
                            break;
                        case AnimationGroup.Switch s:
                            GenerateSwitchLayer(fxAnimator, animationGroup.Name, s);
                            break;
                        case AnimationGroup.Puppet p:
                            GeneratePuppetLayer(fxAnimator, animationGroup.Name, p);
                            break;
                        case AnimationGroup.Layer l:
                            GenerateRawLayer(fxAnimator, animationGroup.Name, l);
                            break;
                        default:
                            throw new DeclavatarException("Invalid AnimationGroup deserialization object");
                    }
                }
                catch (DeclavatarAssetException ex)
                {
                    LogRuntimeError(ex.Message);
                }
            }

            // _maAc.NewMergeAnimator(fxAnimator, VRCAvatarDescriptor.AnimLayerType.FX).Relative();
            // MEMO: should be absolute path mode?
            var mergeAnimator = _rootGameObject.AddComponent<ModularAvatarMergeAnimator>();
            mergeAnimator.animator = fxAnimator.AnimatorController;
            mergeAnimator.layerType = VRCAvatarDescriptor.AnimLayerType.FX;
            mergeAnimator.pathMode = MergeAnimatorPathMode.Relative;
        }

        #region FX Layer Generation

        private void GeneratePreventionLayers(AacFlController controller)
        {
            var preventions = _declavatarDefinition.AnimationGroups.Select((ag) =>
            {
                switch (ag.Content)
                {
                    case AnimationGroup.Group g: return (g.Preventions, g.Parameter, IsInt: true);
                    case AnimationGroup.Switch s: return (s.Preventions, s.Parameter, IsInt: false);
                    default: return (new Preventions(), null, false);
                }
            });

            var mouthPreventions = preventions.Where((p) => p.Preventions.Mouth).Select((p) => (p.Parameter, p.IsInt)).ToList();
            var mouthPreventionLayer = controller.NewLayer("MouthPrevention");
            var mouthTrackingState = mouthPreventionLayer.NewState("Tracking").TrackingTracks(AacAv3.Av3TrackingElement.Mouth);
            var mouthAnimationState = mouthPreventionLayer.NewState("Animation").TrackingAnimates(AacAv3.Av3TrackingElement.Mouth);

            if (mouthPreventions.Count > 0)
            {
                var (firstName, firstIsInt) = mouthPreventions[0];
                AacFlTransitionContinuation mouthTrackingConditon;
                AacFlTransitionContinuation mouthAnimationCondition;
                if (firstIsInt)
                {
                    var firstParameter = mouthPreventionLayer.IntParameter(firstName);
                    mouthTrackingConditon = mouthAnimationState.TransitionsTo(mouthTrackingState).When(firstParameter.IsEqualTo(0));
                    mouthAnimationCondition = mouthTrackingState.TransitionsTo(mouthAnimationState).When(firstParameter.IsNotEqualTo(0));
                }
                else
                {
                    var firstParameter = mouthPreventionLayer.BoolParameter(firstName);
                    mouthTrackingConditon = mouthAnimationState.TransitionsTo(mouthTrackingState).When(firstParameter.IsFalse());
                    mouthAnimationCondition = mouthTrackingState.TransitionsTo(mouthAnimationState).When(firstParameter.IsTrue());
                }
                foreach (var (name, isInt) in mouthPreventions.Skip(1))
                {
                    if (isInt)
                    {
                        var parameter = mouthPreventionLayer.IntParameter(name);
                        mouthTrackingConditon.And(parameter.IsEqualTo(0));
                        mouthAnimationCondition.Or().When(parameter.IsNotEqualTo(0));
                    }
                    else
                    {
                        var parameter = mouthPreventionLayer.BoolParameter(name);
                        mouthTrackingConditon.And(parameter.IsFalse());
                        mouthAnimationCondition.Or().When(parameter.IsTrue());
                    }
                }
            }

            var eyelidsPreventions = preventions.Where((p) => p.Preventions.Eyelids).Select((p) => (p.Parameter, p.IsInt)).ToList();
            var eyelidsPreventionLayer = controller.NewLayer("EyelidsPrevention");
            var eyelidsTrackingState = eyelidsPreventionLayer.NewState("Tracking").TrackingTracks(AacAv3.Av3TrackingElement.Eyes);
            var eyelidsAnimationState = eyelidsPreventionLayer.NewState("Animation").TrackingAnimates(AacAv3.Av3TrackingElement.Eyes);

            if (eyelidsPreventions.Count > 0)
            {
                var (firstName, firstIsInt) = eyelidsPreventions[0];
                AacFlTransitionContinuation eyelidsTrackingConditon;
                AacFlTransitionContinuation eyelidsAnimationCondition;
                if (firstIsInt)
                {
                    var firstParameter = eyelidsPreventionLayer.IntParameter(firstName);
                    eyelidsTrackingConditon = eyelidsAnimationState.TransitionsTo(eyelidsTrackingState).When(firstParameter.IsEqualTo(0));
                    eyelidsAnimationCondition = eyelidsTrackingState.TransitionsTo(eyelidsAnimationState).When(firstParameter.IsNotEqualTo(0));
                }
                else
                {
                    var firstParameter = eyelidsPreventionLayer.BoolParameter(firstName);
                    eyelidsTrackingConditon = eyelidsAnimationState.TransitionsTo(eyelidsTrackingState).When(firstParameter.IsFalse());
                    eyelidsAnimationCondition = eyelidsTrackingState.TransitionsTo(eyelidsAnimationState).When(firstParameter.IsTrue());
                }
                foreach (var (name, isInt) in eyelidsPreventions.Skip(1))
                {
                    if (isInt)
                    {
                        var parameter = eyelidsPreventionLayer.IntParameter(name);
                        eyelidsTrackingConditon.And(parameter.IsEqualTo(0));
                        eyelidsAnimationCondition.Or().When(parameter.IsNotEqualTo(0));
                    }
                    else
                    {
                        var parameter = eyelidsPreventionLayer.BoolParameter(name);
                        eyelidsTrackingConditon.And(parameter.IsFalse());
                        eyelidsAnimationCondition.Or().When(parameter.IsTrue());
                    }
                }
            }
        }

        private void GenerateGroupLayer(AacFlController controller, string name, AnimationGroup.Group g)
        {
            var layer = controller.NewLayer(name);
            var layerParameter = layer.IntParameter(g.Parameter);

            var idleClip = _ndmfAac.NewClip($"sg-{name}-0");
            foreach (var target in g.DefaultTargets)
            {
                try
                {
                    switch (target)
                    {
                        case Target.Shape shape:
                            var smr = _searcher.FindSkinnedMeshRenderer(shape.Mesh);
                            idleClip.BlendShape(smr, shape.Name, shape.Value * 100.0f);
                            break;
                        case Target.Object obj:
                            var go = _searcher.FindGameObject(obj.Name);
                            idleClip.Toggling(go, obj.Enabled);
                            break;
                        case Target.Material material:
                            var mr = _searcher.FindRenderer(material.Mesh);
                            var targetMaterial = SearchExternalMaterial(material.AssetKey);
                            idleClip.SwappingMaterial(mr, (int)material.Slot, targetMaterial);
                            break;
                        default:
                            throw new DeclavatarException("Invalid Target deserialization object");
                    }
                }
                catch (DeclavatarRuntimeException ex)
                {
                    LogRuntimeError(ex.Message);
                }
            }
            var idleState = layer.NewState("Disabled", 0, 0).WithAnimation(idleClip);

            foreach (var option in g.Options)
            {
                var clip = _ndmfAac.NewClip($"sg-{name}-{option.Order}");
                foreach (var target in option.Targets)
                {
                    try
                    {
                        switch (target)
                        {
                            case Target.Shape shape:
                                var smr = _searcher.FindSkinnedMeshRenderer(shape.Mesh);
                                clip.BlendShape(smr, shape.Name, shape.Value * 100.0f);
                                break;
                            case Target.Object obj:
                                var go = _searcher.FindGameObject(obj.Name);
                                clip.Toggling(go, obj.Enabled);
                                break;
                            case Target.Material material:
                                var mr = _searcher.FindRenderer(material.Mesh);
                                var targetMaterial = SearchExternalMaterial(material.AssetKey);
                                clip.SwappingMaterial(mr, (int)material.Slot, targetMaterial);
                                break;
                            default:
                                throw new DeclavatarException("Invalid Target deserialization object");
                        }
                    }
                    catch (DeclavatarRuntimeException ex)
                    {
                        LogRuntimeError(ex.Message);
                    }
                }
                var state = layer.NewState($"{option.Order} {option.Name}", (int)option.Order / 8 + 1, (int)option.Order % 8).WithAnimation(clip);
                idleState.TransitionsTo(state).When(layerParameter.IsEqualTo((int)option.Order));
                state.Exits().When(layerParameter.IsNotEqualTo((int)option.Order));
            }
        }

        private void GenerateSwitchLayer(AacFlController controller, string name, AnimationGroup.Switch s)
        {
            var layer = controller.NewLayer(name);
            var layerParameter = layer.BoolParameter(s.Parameter);

            var disabledClip = _ndmfAac.NewClip($"ss-{name}-disabled");
            var enabledClip = _ndmfAac.NewClip($"ss-{name}-enabled");
            foreach (var target in s.Disabled)
            {
                try
                {
                    switch (target)
                    {
                        case Target.Shape shape:
                            var smr = _searcher.FindSkinnedMeshRenderer(shape.Mesh);
                            disabledClip.BlendShape(smr, shape.Name, shape.Value * 100.0f);
                            break;
                        case Target.Object obj:
                            var go = _searcher.FindGameObject(obj.Name);
                            disabledClip.Toggling(go, obj.Enabled);
                            break;
                        case Target.Material material:
                            var mr = _searcher.FindRenderer(material.Mesh);
                            var targetMaterial = SearchExternalMaterial(material.AssetKey);
                            disabledClip.SwappingMaterial(mr, (int)material.Slot, targetMaterial);
                            break;
                        default:
                            throw new DeclavatarException("Invalid Target deserialization object");
                    }
                }
                catch (DeclavatarRuntimeException ex)
                {
                    LogRuntimeError(ex.Message);
                }
            }
            foreach (var target in s.Enabled)
            {
                try
                {
                    switch (target)
                    {
                        case Target.Shape shape:
                            var smr = _searcher.FindSkinnedMeshRenderer(shape.Mesh);
                            enabledClip.BlendShape(smr, shape.Name, shape.Value * 100.0f);
                            break;
                        case Target.Object obj:
                            var go = _searcher.FindGameObject(obj.Name);
                            enabledClip.Toggling(go, obj.Enabled);
                            break;
                        case Target.Material material:
                            var mr = _searcher.FindRenderer(material.Mesh);
                            var targetMaterial = SearchExternalMaterial(material.AssetKey);
                            enabledClip.SwappingMaterial(mr, (int)material.Slot, targetMaterial);
                            break;
                        default:
                            throw new DeclavatarException("Invalid Target deserialization object");
                    }
                }
                catch (DeclavatarRuntimeException ex)
                {
                    LogRuntimeError(ex.Message);
                }
            }
            var disabledState = layer.NewState("Disabled").WithAnimation(disabledClip);
            var enabledState = layer.NewState("Enabled").WithAnimation(enabledClip);
            disabledState.TransitionsTo(enabledState).When(layerParameter.IsTrue());
            enabledState.TransitionsTo(disabledState).When(layerParameter.IsFalse());
        }

        private void GeneratePuppetLayer(AacFlController controller, string name, AnimationGroup.Puppet puppet)
        {
            var layer = controller.NewLayer(name);
            var layerParameter = layer.FloatParameter(puppet.Parameter);

            var groups = puppet.Keyframes
                .SelectMany((kf) => kf.Targets.Select((t) => (kf.Position, Target: t)))
                .GroupBy((p) => p.Target.AsGroupingKey());

            var clip = _ndmfAac.NewClip($"p-{name}").NonLooping();
            clip.Animating((e) =>
            {
                foreach (var group in groups)
                {
                    try
                    {
                        if (group.Key.StartsWith("s://"))
                        {
                            var points = group.Select((p) => (p.Position, Target: p.Target as Target.Shape)).ToList();
                            var smr = _searcher.FindSkinnedMeshRenderer(points[0].Target.Mesh);
                            e.Animates(smr, $"blendShape.{points[0].Target.Name}").WithFrameCountUnit((kfs) =>
                            {
                                foreach (var point in points) kfs.Linear(point.Position * 100.0f, point.Target.Value * 100.0f);
                            });
                        }
                        else if (group.Key.StartsWith("o://"))
                        {
                            var points = group.Select((p) => (p.Position, Target: p.Target as Target.Object)).ToList();
                            var go = _searcher.FindGameObject(points[0].Target.Name);
                            e.Animates(go).WithFrameCountUnit((kfs) =>
                            {
                                foreach (var point in points) kfs.Constant(point.Position * 100.0f, point.Target.Enabled ? 1.0f : 0.0f);
                            });
                        }
                        else if (group.Key.StartsWith("m://"))
                        {
                            // Use traditional API for matarial swapping
                            var points = group.Select((p) => (p.Position, Target: p.Target as Target.Material)).ToList();
                            var mr = _searcher.FindRenderer(points[0].Target.Mesh);

                            var binding = e.BindingFromComponent(mr, $"m_Materials.Array.data[{points[0].Target.Slot}]");
                            var keyframes = points.Select((p) => new ObjectReferenceKeyframe
                            {
                                time = p.Position * 100.0f,
                                value = SearchExternalMaterial(p.Target.AssetKey),
                            }).ToArray();
                            AnimationUtility.SetObjectReferenceCurve(clip.Clip, binding, keyframes);
                        }
                    }
                    catch (DeclavatarRuntimeException ex)
                    {
                        LogRuntimeError(ex.Message);
                    }
                }
            });

            var state = layer.NewState(name).WithAnimation(clip);
            state.MotionTime(layerParameter);
        }

        private void GenerateRawLayer(AacFlController controller, string name, AnimationGroup.Layer agLayer)
        {
            var layer = controller.NewLayer(name);

            // Create states
            var states = new List<AacFlState>();
            foreach (var agState in agLayer.States)
            {
                var state = layer.NewState(agState.Name);
                states.Add(state);
                switch (agState.Animation)
                {
                    case LayerAnimation.Clip clip:
                        state.WithAnimation(SearchExternalAnimationClip(clip.AssetKey));
                        if (agState.Time != null)
                        {
                            var speedParameter = layer.FloatParameter(agState.Time);
                            state.MotionTime(speedParameter);
                        }
                        // TODO: Speed parameters
                        break;
                    case LayerAnimation.BlendTree blendTree:
                        var tree = new BlendTree();
                        switch (blendTree.BlendType)
                        {
                            case "Linear":
                                tree.blendType = BlendTreeType.Simple1D;
                                tree.blendParameter = blendTree.Parameters[0];
                                foreach (var field in blendTree.Fields)
                                {
                                    var fieldAnimation = SearchExternalAnimationClip(field.AssetKey);
                                    tree.AddChild(fieldAnimation, field.Position[0]);
                                }
                                break;
                            case "Simple2D":
                                tree.blendType = BlendTreeType.SimpleDirectional2D;
                                tree.blendParameter = blendTree.Parameters[0];
                                tree.blendParameterY = blendTree.Parameters[1];
                                foreach (var field in blendTree.Fields)
                                {
                                    var fieldAnimation = SearchExternalAnimationClip(field.AssetKey);
                                    tree.AddChild(fieldAnimation, new Vector2(field.Position[0], field.Position[1]));
                                }
                                break;
                            case "Freeform2D":
                                tree.blendType = BlendTreeType.FreeformDirectional2D;
                                tree.blendParameter = blendTree.Parameters[0];
                                tree.blendParameterY = blendTree.Parameters[1];
                                foreach (var field in blendTree.Fields)
                                {
                                    var fieldAnimation = SearchExternalAnimationClip(field.AssetKey);
                                    tree.AddChild(fieldAnimation, new Vector2(field.Position[0], field.Position[1]));
                                }
                                break;
                            case "Cartesian2D":
                                tree.blendType = BlendTreeType.FreeformCartesian2D;
                                tree.blendParameter = blendTree.Parameters[0];
                                tree.blendParameterY = blendTree.Parameters[1];
                                foreach (var field in blendTree.Fields)
                                {
                                    var fieldAnimation = SearchExternalAnimationClip(field.AssetKey);
                                    tree.AddChild(fieldAnimation, new Vector2(field.Position[0], field.Position[1]));
                                }
                                break;
                            default:
                                throw new DeclavatarInternalException($"Invalid BlendTree Type {blendTree.BlendType}");
                        }
                        state.WithAnimation(tree);
                        break;
                }
            }

            // Set transitions
            for (int i = 0; i < states.Count; ++i)
            {
                var fromState = states[i];
                var agState = agLayer.States[i];
                foreach (var transition in agState.Transitions)
                {
                    var targetState = states[(int)transition.Target];
                    var conds = fromState.TransitionsTo(targetState).WithTransitionDurationSeconds(transition.Duration).WhenConditions();
                    foreach (var condBlock in transition.Conditions)
                    {
                        switch (condBlock)
                        {
                            case LayerCondition.Be be:
                                conds.And(layer.BoolParameter(be.Parameter).IsTrue());
                                break;
                            case LayerCondition.Not not:
                                conds.And(layer.BoolParameter(not.Parameter).IsFalse());
                                break;
                            case LayerCondition.EqInt eqInt:
                                conds.And(layer.IntParameter(eqInt.Parameter).IsEqualTo(eqInt.Value));
                                break;
                            case LayerCondition.NeqInt neqInt:
                                conds.And(layer.IntParameter(neqInt.Parameter).IsNotEqualTo(neqInt.Value));
                                break;
                            case LayerCondition.GtInt gtInt:
                                conds.And(layer.IntParameter(gtInt.Parameter).IsGreaterThan(gtInt.Value));
                                break;
                            case LayerCondition.LeInt leInt:
                                conds.And(layer.IntParameter(leInt.Parameter).IsLessThan(leInt.Value));
                                break;
                            case LayerCondition.GtFloat gtFloat:
                                conds.And(layer.FloatParameter(gtFloat.Parameter).IsGreaterThan(gtFloat.Value));
                                break;
                            case LayerCondition.LeFloat leFloat:
                                conds.And(layer.FloatParameter(leFloat.Parameter).IsLessThan(leFloat.Value));
                                break;
                            default:
                                throw new DeclavatarInternalException("Invalid LayerCondition deserialization object");
                        }
                    }
                }
            }
        }

        #endregion

        #region Parameter Generation

        private void GenerateParametersNonDestructive()
        {
            var parameterObject = new GameObject("DeclavatarParameters");
            var parametersComponent = parameterObject.AddComponent<ModularAvatarParameters>();
            parametersComponent.parameters = _declavatarDefinition.Parameters
                .Select((pd) => new ParameterConfig
                {
                    nameOrPrefix = pd.Name,
                    syncType = pd.ConvertToMASyncType(),
                    defaultValue = pd.ValueType.ConvertToVRCParameterValue(),
                    saved = pd.Scope.Save ?? false,
                    localOnly = pd.Scope.Type != "Synced",
                    internalParameter = false, // never rename
                    isPrefix = false, // TODO: PhysBones prefix
                })
                .ToList();

            parameterObject.transform.parent = _rootGameObject.transform.transform;
        }

        #endregion

        #region Menu Generation

        private void GenerateMenuNonDestructive()
        {
            var rootMenuItem = new GameObject("DeclavatarMenuRoot");
            // var installer = 
            if (_installTarget != null)
            {
                _installTarget.AddComponent<ModularAvatarMenuInstaller>();
                var targetingGroup = _installTarget.AddComponent<ModularAvatarMenuGroup>();
                targetingGroup.targetObject = rootMenuItem;
            }
            else
            {
                rootMenuItem.AddComponent<ModularAvatarMenuInstaller>();
                rootMenuItem.AddComponent<ModularAvatarMenuGroup>();
            }

            foreach (var item in _declavatarDefinition.TopMenuGroup.Items)
            {
                GameObject menuItem;
                switch (item)
                {
                    case ExMenuItem.ExMenuGroup submenu:
                        menuItem = GenerateMenuGroupObject(submenu);
                        break;
                    case ExMenuItem.Button button:
                        menuItem = GenerateMenuButtonObject(button);
                        break;
                    case ExMenuItem.Toggle toggle:
                        menuItem = GenerateMenuToggleObject(toggle);
                        break;
                    case ExMenuItem.Radial radial:
                        menuItem = GenerateMenuRadialObject(radial);
                        break;
                    case ExMenuItem.TwoAxis twoAxis:
                        menuItem = GenerateMenuTwoAxisObject(twoAxis);
                        break;
                    case ExMenuItem.FourAxis fourAxis:
                        menuItem = GenerateMenuFourAxisObject(fourAxis);
                        break;
                    default:
                        continue;
                }
                menuItem.transform.parent = rootMenuItem.gameObject.transform;
            }

            rootMenuItem.transform.parent = _rootGameObject.transform.transform;
        }

        private GameObject GenerateMenuGroupObject(ExMenuItem.ExMenuGroup group)
        {
            var menuGroupRoot = new GameObject(group.Name);
            var menuItemComponent = menuGroupRoot.AddComponent<ModularAvatarMenuItem>();
            menuItemComponent.MenuSource = SubmenuSource.Children;

            var control = menuItemComponent.Control;
            control.type = VRCExpressionsMenu.Control.ControlType.SubMenu;
            control.name = group.Name;

            foreach (var item in group.Items)
            {
                GameObject menuItem;
                switch (item)
                {
                    case ExMenuItem.ExMenuGroup submenu:
                        menuItem = GenerateMenuGroupObject(submenu);
                        break;
                    case ExMenuItem.Button button:
                        menuItem = GenerateMenuButtonObject(button);
                        break;
                    case ExMenuItem.Toggle toggle:
                        menuItem = GenerateMenuToggleObject(toggle);
                        break;
                    case ExMenuItem.Radial radial:
                        menuItem = GenerateMenuRadialObject(radial);
                        break;
                    case ExMenuItem.TwoAxis twoAxis:
                        menuItem = GenerateMenuTwoAxisObject(twoAxis);
                        break;
                    case ExMenuItem.FourAxis fourAxis:
                        menuItem = GenerateMenuFourAxisObject(fourAxis);
                        break;
                    default:
                        continue;
                }
                menuItem.transform.parent = menuGroupRoot.gameObject.transform;
            }

            return menuGroupRoot;
        }

        private GameObject GenerateMenuButtonObject(ExMenuItem.Button button)
        {
            var menuItemObject = new GameObject(button.Name);
            var menuItemComponent = menuItemObject.AddComponent<ModularAvatarMenuItem>();

            var control = menuItemComponent.Control;
            control.type = VRCExpressionsMenu.Control.ControlType.Button;
            control.name = button.Name;
            control.parameter = new VRCExpressionsMenu.Control.Parameter { name = button.Parameter };
            control.value = button.Value.ConvertToVRCParameterValue();

            return menuItemObject;
        }

        private GameObject GenerateMenuToggleObject(ExMenuItem.Toggle toggle)
        {
            var menuItemObject = new GameObject(toggle.Name);
            var menuItemComponent = menuItemObject.AddComponent<ModularAvatarMenuItem>();

            var control = menuItemComponent.Control;
            control.type = VRCExpressionsMenu.Control.ControlType.Toggle;
            control.name = toggle.Name;
            control.parameter = new VRCExpressionsMenu.Control.Parameter { name = toggle.Parameter };
            control.value = toggle.Value.ConvertToVRCParameterValue();

            return menuItemObject;
        }

        private GameObject GenerateMenuRadialObject(ExMenuItem.Radial radial)
        {
            var menuItemObject = new GameObject(radial.Name);
            var menuItemComponent = menuItemObject.AddComponent<ModularAvatarMenuItem>();

            var control = menuItemComponent.Control;
            control.type = VRCExpressionsMenu.Control.ControlType.RadialPuppet;
            control.name = radial.Name;
            control.subParameters = new VRCExpressionsMenu.Control.Parameter[]
            {
                new VRCExpressionsMenu.Control.Parameter { name = radial.Parameter },
            };
            control.labels = new VRCExpressionsMenu.Control.Label[]
            {
                new VRCExpressionsMenu.Control.Label { name = "Should Insert Label here" },
            };

            return menuItemObject;
        }

        private GameObject GenerateMenuTwoAxisObject(ExMenuItem.TwoAxis twoAxis)
        {
            var menuItemObject = new GameObject(twoAxis.Name);
            var menuItemComponent = menuItemObject.AddComponent<ModularAvatarMenuItem>();

            var control = menuItemComponent.Control;
            control.type = VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet;
            control.name = twoAxis.Name;
            control.subParameters = new[]
            {
                new VRCExpressionsMenu.Control.Parameter { name = twoAxis.HorizontalAxis.Parameter },
                new VRCExpressionsMenu.Control.Parameter { name = twoAxis.VerticalAxis.Parameter },
            };
            control.labels = new[]
            {
                new VRCExpressionsMenu.Control.Label { name = twoAxis.VerticalAxis.LabelPositive },
                new VRCExpressionsMenu.Control.Label { name = twoAxis.HorizontalAxis.LabelPositive },
                new VRCExpressionsMenu.Control.Label { name = twoAxis.VerticalAxis.LabelNegative },
                new VRCExpressionsMenu.Control.Label { name = twoAxis.HorizontalAxis.LabelNegative },
            };

            return menuItemObject;
        }

        private GameObject GenerateMenuFourAxisObject(ExMenuItem.FourAxis fourAxis)
        {
            var menuItemObject = new GameObject(fourAxis.Name);
            var menuItemComponent = menuItemObject.AddComponent<ModularAvatarMenuItem>();

            var control = menuItemComponent.Control;
            control.type = VRCExpressionsMenu.Control.ControlType.FourAxisPuppet;
            control.name = fourAxis.Name;
            control.subParameters = new VRCExpressionsMenu.Control.Parameter[]
            {
                new VRCExpressionsMenu.Control.Parameter { name = fourAxis.UpAxis.Parameter },
                new VRCExpressionsMenu.Control.Parameter { name = fourAxis.RightAxis.Parameter },
                new VRCExpressionsMenu.Control.Parameter { name = fourAxis.DownAxis.Parameter },
                new VRCExpressionsMenu.Control.Parameter { name = fourAxis.LeftAxis.Parameter },
            };
            control.labels = new VRCExpressionsMenu.Control.Label[]
            {
                new VRCExpressionsMenu.Control.Label { name = fourAxis.UpAxis.Label },
                new VRCExpressionsMenu.Control.Label { name = fourAxis.RightAxis.Label },
                new VRCExpressionsMenu.Control.Label { name = fourAxis.DownAxis.Label },
                new VRCExpressionsMenu.Control.Label { name = fourAxis.LeftAxis.Label },
            };

            return menuItemObject;
        }

        #endregion

        #region External Asset

        private AnimationClip SearchExternalAnimationClip(string key)
        {
            foreach (var assetSet in _externalAssets)
            {
                if (assetSet.Animations == null) continue;
                var value = assetSet.Animations.FirstOrDefault((a) => a.Key == key);
                if (value != null) return value.Animation;
            }
            throw new DeclavatarAssetException($"AnimationClip {key} not defined");
        }

        private Material SearchExternalMaterial(string key)
        {
            foreach (var assetSet in _externalAssets)
            {
                if (assetSet.Materials == null) continue;
                var value = assetSet.Materials.FirstOrDefault((a) => a.Key == key);
                if (value != null) return value.Material;
            }
            throw new DeclavatarAssetException($"Material {key} not defined");
        }

        private string SearchExternalLocalization(string key)
        {
            foreach (var assetSet in _externalAssets)
            {
                if (assetSet.Localizations == null) continue;
                var value = assetSet.Localizations.FirstOrDefault((a) => a.Key == key);
                if (value != null) return value.Localization;
            }
            throw new DeclavatarAssetException($"Localization {key} not defined");
        }

        #endregion

        #region Object Searching

        private void LogRuntimeError(string message)
        {
            if (_logWindow == null) _logWindow = BuildLogWindow.ShowLogWindow();
            _logWindow.AddLog(ErrorKind.RuntimeError, message);
        }

        internal sealed class GameObjectSearcher
        {
            private GameObject _root = null;
            private Dictionary<string, Renderer> _renderers = new Dictionary<string, Renderer>();
            private Dictionary<string, SkinnedMeshRenderer> _skinnedMeshRenderers = new Dictionary<string, SkinnedMeshRenderer>();
            private Dictionary<string, GameObject> _objects = new Dictionary<string, GameObject>();
            private HashSet<string> _searchedPaths = new HashSet<string>();

            public GameObjectSearcher(GameObject root)
            {
                _root = root;
            }

            public Renderer FindRenderer(string path)
            {
                var cachedPath = $"mr://{path}";
                if (_searchedPaths.Contains(cachedPath))
                {
                    return _renderers.TryGetValue(path, out var mr) ? mr : null;
                }
                else
                {
                    var mr = _root.transform.Find(path)?.GetComponent<Renderer>()
                        ?? throw new DeclavatarRuntimeException($"Renderer '{path}' not found");
                    _searchedPaths.Add(cachedPath);
                    _renderers[path] = mr;
                    return mr;
                }
            }

            public SkinnedMeshRenderer FindSkinnedMeshRenderer(string path)
            {
                var cachedPath = $"smr://{path}";
                if (_searchedPaths.Contains(cachedPath))
                {
                    return _skinnedMeshRenderers.TryGetValue(path, out var smr) ? smr : null;
                }
                else
                {
                    var smr = _root.transform.Find(path)?.GetComponent<SkinnedMeshRenderer>()
                        ?? throw new DeclavatarRuntimeException($"SkinnedMeshRenderer '{path}' not found");
                    _searchedPaths.Add(cachedPath);
                    _skinnedMeshRenderers[path] = smr;
                    return smr;
                }
            }

            public GameObject FindGameObject(string path)
            {
                var cachedPath = $"go://{path}";
                if (_searchedPaths.Contains(cachedPath))
                {
                    return _objects.TryGetValue(path, out var smr) ? smr : null;
                }
                else
                {
                    var go = _root.transform.Find(path)?.gameObject
                        ?? throw new DeclavatarRuntimeException($"GameObject '{path}' not found");
                    _searchedPaths.Add(cachedPath);
                    _objects[path] = go;
                    return go;
                }
            }
        }

        #endregion
    }
}
