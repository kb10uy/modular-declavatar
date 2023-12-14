using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using AnimatorAsCode.V1;
using AnimatorAsCode.V1.VRC;
using nadena.dev.modular_avatar.core;
using KusakaFactory.Declavatar.EditorExtension;
using KusakaFactory.Declavatar.Runtime;

namespace KusakaFactory.Declavatar
{
    public sealed class NonDestructiveDeclavatar
    {
        private GameObject _rootGameObject;
        private GameObject _installTarget;
        private Data.Avatar _declavatarDefinition;
        private IReadOnlyList<ExternalAsset> _externalAssets;
        private AacFlBase _ndmfAac;
        private BuildLogWindow _logWindow;

        private GameObjectSearcher _searcher;

        public NonDestructiveDeclavatar(GameObject root, GameObject installTarget, AacFlBase aac, Data.Avatar definition, IReadOnlyList<ExternalAsset> assets)
        {
            _rootGameObject = root;
            _installTarget = installTarget;
            _declavatarDefinition = definition;
            _externalAssets = assets;
            _ndmfAac = aac;
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

            foreach (var animationGroup in _declavatarDefinition.FxController)
            {
                try
                {
                    switch (animationGroup.Content)
                    {
                        case Data.Layer.GroupLayer g:
                            GenerateGroupLayer(fxAnimator, animationGroup.Name, g);
                            break;
                        case Data.Layer.SwitchLayer s:
                            GenerateSwitchLayer(fxAnimator, animationGroup.Name, s);
                            break;
                        case Data.Layer.PuppetLayer p:
                            GeneratePuppetLayer(fxAnimator, animationGroup.Name, p);
                            break;
                        case Data.Layer.RawLayer r:
                            GenerateRawLayer(fxAnimator, animationGroup.Name, r);
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

            // MEMO: should be absolute path mode?
            var mergeAnimator = _rootGameObject.AddComponent<ModularAvatarMergeAnimator>();
            mergeAnimator.animator = fxAnimator.AnimatorController;
            mergeAnimator.layerType = VRCAvatarDescriptor.AnimLayerType.FX;
            mergeAnimator.pathMode = MergeAnimatorPathMode.Relative;
        }

        #region FX Layer Generation

        private void GenerateGroupLayer(AacFlController controller, string name, Data.Layer.GroupLayer g)
        {
            var layer = controller.NewLayer(name);
            var layerParameter = layer.IntParameter(g.Parameter);

            var idleClip = _ndmfAac.NewClip($"sg-{name}-0");
            var idleState = layer.NewState("Disabled", 0, 0).WithAnimation(idleClip);
            foreach (var target in g.Default.Targets)
            {
                try
                {
                    switch (target)
                    {
                        case Data.Target.Shape shape:
                            var smr = _searcher.FindSkinnedMeshRenderer(shape.Mesh);
                            idleClip.BlendShape(smr, shape.Name, shape.Value * 100.0f);
                            break;
                        case Data.Target.Object obj:
                            var go = _searcher.FindGameObject(obj.Name);
                            idleClip.Toggling(go, obj.Enabled);
                            break;
                        case Data.Target.Material material:
                            var mr = _searcher.FindRenderer(material.Mesh);
                            var targetMaterial = SearchExternalMaterial(material.AssetKey);
                            idleClip.SwappingMaterial(mr, (int)material.Slot, targetMaterial);
                            break;
                        case Data.Target.Drive drive:
                            AppendStateParameterDrive(layer, idleState, drive.ParameterDrive);
                            break;
                        case Data.Target.Tracking control:
                            AppendStateTrackingControl(idleState, control.Control);
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

            foreach (var option in g.Options)
            {
                var clip = _ndmfAac.NewClip($"sg-{name}-{option.Value}");
                var state = layer.NewState($"{option.Value} {option.Name}", (int)option.Value / 8 + 1, (int)option.Value % 8).WithAnimation(clip);
                foreach (var target in option.Targets)
                {
                    try
                    {
                        switch (target)
                        {
                            case Data.Target.Shape shape:
                                var smr = _searcher.FindSkinnedMeshRenderer(shape.Mesh);
                                clip.BlendShape(smr, shape.Name, shape.Value * 100.0f);
                                break;
                            case Data.Target.Object obj:
                                var go = _searcher.FindGameObject(obj.Name);
                                clip.Toggling(go, obj.Enabled);
                                break;
                            case Data.Target.Material material:
                                var mr = _searcher.FindRenderer(material.Mesh);
                                var targetMaterial = SearchExternalMaterial(material.AssetKey);
                                clip.SwappingMaterial(mr, (int)material.Slot, targetMaterial);
                                break;
                            case Data.Target.Drive drive:
                                AppendStateParameterDrive(layer, state, drive.ParameterDrive);
                                break;
                            case Data.Target.Tracking control:
                                AppendStateTrackingControl(state, control.Control);
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
                idleState.TransitionsTo(state).When(layerParameter.IsEqualTo((int)option.Value));
                state.Exits().When(layerParameter.IsNotEqualTo((int)option.Value));
            }
        }

        private void GenerateSwitchLayer(AacFlController controller, string name, Data.Layer.SwitchLayer s)
        {
            var layer = controller.NewLayer(name);
            var layerParameter = layer.BoolParameter(s.Parameter);

            var disabledClip = _ndmfAac.NewClip($"ss-{name}-disabled");
            var enabledClip = _ndmfAac.NewClip($"ss-{name}-enabled");
            var disabledState = layer.NewState("Disabled").WithAnimation(disabledClip);
            var enabledState = layer.NewState("Enabled").WithAnimation(enabledClip);
            foreach (var target in s.Disabled)
            {
                try
                {
                    switch (target)
                    {
                        case Data.Target.Shape shape:
                            var smr = _searcher.FindSkinnedMeshRenderer(shape.Mesh);
                            disabledClip.BlendShape(smr, shape.Name, shape.Value * 100.0f);
                            break;
                        case Data.Target.Object obj:
                            var go = _searcher.FindGameObject(obj.Name);
                            disabledClip.Toggling(go, obj.Enabled);
                            break;
                        case Data.Target.Material material:
                            var mr = _searcher.FindRenderer(material.Mesh);
                            var targetMaterial = SearchExternalMaterial(material.AssetKey);
                            disabledClip.SwappingMaterial(mr, (int)material.Slot, targetMaterial);
                            break;
                        case Data.Target.Drive drive:
                            AppendStateParameterDrive(layer, disabledState, drive.ParameterDrive);
                            break;
                        case Data.Target.Tracking control:
                            AppendStateTrackingControl(disabledState, control.Control);
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
                        case Data.Target.Shape shape:
                            var smr = _searcher.FindSkinnedMeshRenderer(shape.Mesh);
                            enabledClip.BlendShape(smr, shape.Name, shape.Value * 100.0f);
                            break;
                        case Data.Target.Object obj:
                            var go = _searcher.FindGameObject(obj.Name);
                            enabledClip.Toggling(go, obj.Enabled);
                            break;
                        case Data.Target.Material material:
                            var mr = _searcher.FindRenderer(material.Mesh);
                            var targetMaterial = SearchExternalMaterial(material.AssetKey);
                            enabledClip.SwappingMaterial(mr, (int)material.Slot, targetMaterial);
                            break;
                        case Data.Target.Drive drive:
                            AppendStateParameterDrive(layer, enabledState, drive.ParameterDrive);
                            break;
                        case Data.Target.Tracking control:
                            AppendStateTrackingControl(enabledState, control.Control);
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

            disabledState.TransitionsTo(enabledState).When(layerParameter.IsTrue());
            enabledState.TransitionsTo(disabledState).When(layerParameter.IsFalse());
        }

        private void GeneratePuppetLayer(AacFlController controller, string name, Data.Layer.PuppetLayer puppet)
        {
            var layer = controller.NewLayer(name);
            var layerParameter = layer.FloatParameter(puppet.Parameter);

            var groups = puppet.Keyframes
                .SelectMany((kf) => kf.Targets.Select((t) => (kf.Value, Target: t)))
                .GroupBy((p) => Data.VRChatExtension.AsGroupingKey(p.Target));

            var clip = _ndmfAac.NewClip($"p-{name}").NonLooping();
            clip.Animating((e) =>
            {
                foreach (var group in groups)
                {
                    try
                    {
                        if (group.Key.StartsWith("s://"))
                        {
                            var points = group.Select((p) => (p.Value, Target: p.Target as Data.Target.Shape)).ToList();
                            var smr = _searcher.FindSkinnedMeshRenderer(points[0].Target.Mesh);
                            e.Animates(smr, $"blendShape.{points[0].Target.Name}").WithFrameCountUnit((kfs) =>
                            {
                                foreach (var point in points) kfs.Linear(point.Value * 100.0f, point.Target.Value * 100.0f);
                            });
                        }
                        else if (group.Key.StartsWith("o://"))
                        {
                            var points = group.Select((p) => (p.Value, Target: p.Target as Data.Target.Object)).ToList();
                            var go = _searcher.FindGameObject(points[0].Target.Name);
                            e.Animates(go).WithFrameCountUnit((kfs) =>
                            {
                                foreach (var point in points) kfs.Constant(point.Value * 100.0f, point.Target.Enabled ? 1.0f : 0.0f);
                            });
                        }
                        else if (group.Key.StartsWith("m://"))
                        {
                            // Use traditional API for matarial swapping
                            var points = group.Select((p) => (p.Value, Target: p.Target as Data.Target.Material)).ToList();
                            var mr = _searcher.FindRenderer(points[0].Target.Mesh);

                            var binding = e.BindingFromComponent(mr, $"m_Materials.Array.data[{points[0].Target.Slot}]");
                            var keyframes = points.Select((p) => new ObjectReferenceKeyframe
                            {
                                time = p.Value * 100.0f,
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

        private void GenerateRawLayer(AacFlController controller, string name, Data.Layer.RawLayer rawLayer)
        {
            var layer = controller.NewLayer(name);

            // Create states
            var states = new List<AacFlState>();
            foreach (var agState in rawLayer.States)
            {
                var state = layer.NewState(agState.Name);
                states.Add(state);
                switch (agState.Animation)
                {
                    case Data.RawAnimation.Clip clip:
                        state.WithAnimation(SearchExternalAnimationClip(clip.Name));
                        if (clip.Speed != null)
                        {
                            var speedParameter = layer.FloatParameter(clip.SpeedBy);
                            state.WithSpeed(speedParameter);
                        }
                        if (clip.TimeBy != null)
                        {
                            var timeParameter = layer.FloatParameter(clip.TimeBy);
                            state.MotionTime(timeParameter);
                        }
                        break;
                    case Data.RawAnimation.BlendTree blendTree:
                        var tree = new BlendTree();
                        switch (blendTree.BlendType)
                        {
                            case "Linear":
                                tree.blendType = BlendTreeType.Simple1D;
                                tree.blendParameter = blendTree.Parameters[0];
                                foreach (var field in blendTree.Fields)
                                {
                                    var fieldAnimation = SearchExternalAnimationClip(field.Name);
                                    tree.AddChild(fieldAnimation, field.Position[0]);
                                }
                                break;
                            case "Simple2D":
                                tree.blendType = BlendTreeType.SimpleDirectional2D;
                                tree.blendParameter = blendTree.Parameters[0];
                                tree.blendParameterY = blendTree.Parameters[1];
                                foreach (var field in blendTree.Fields)
                                {
                                    var fieldAnimation = SearchExternalAnimationClip(field.Name);
                                    tree.AddChild(fieldAnimation, new Vector2(field.Position[0], field.Position[1]));
                                }
                                break;
                            case "Freeform2D":
                                tree.blendType = BlendTreeType.FreeformDirectional2D;
                                tree.blendParameter = blendTree.Parameters[0];
                                tree.blendParameterY = blendTree.Parameters[1];
                                foreach (var field in blendTree.Fields)
                                {
                                    var fieldAnimation = SearchExternalAnimationClip(field.Name);
                                    tree.AddChild(fieldAnimation, new Vector2(field.Position[0], field.Position[1]));
                                }
                                break;
                            case "Cartesian2D":
                                tree.blendType = BlendTreeType.FreeformCartesian2D;
                                tree.blendParameter = blendTree.Parameters[0];
                                tree.blendParameterY = blendTree.Parameters[1];
                                foreach (var field in blendTree.Fields)
                                {
                                    var fieldAnimation = SearchExternalAnimationClip(field.Name);
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
            foreach (var transition in rawLayer.Transitions)
            {
                var fromState = states[(int)transition.FromIndex];
                var targetState = states[(int)transition.TargetIndex];
                var andTerms = fromState.TransitionsTo(targetState).WithTransitionDurationSeconds(transition.Duration).WhenConditions();

                foreach (var condBlock in transition.Conditions)
                {
                    switch (condBlock)
                    {
                        case Data.RawCondition.Be be:
                            andTerms.And(layer.BoolParameter(be.Parameter).IsTrue());
                            break;
                        case Data.RawCondition.Not not:
                            andTerms.And(layer.BoolParameter(not.Parameter).IsFalse());
                            break;
                        case Data.RawCondition.EqInt eqInt:
                            andTerms.And(layer.IntParameter(eqInt.Parameter).IsEqualTo(eqInt.Value));
                            break;
                        case Data.RawCondition.NeqInt neqInt:
                            andTerms.And(layer.IntParameter(neqInt.Parameter).IsNotEqualTo(neqInt.Value));
                            break;
                        case Data.RawCondition.GtInt gtInt:
                            andTerms.And(layer.IntParameter(gtInt.Parameter).IsGreaterThan(gtInt.Value));
                            break;
                        case Data.RawCondition.LeInt leInt:
                            andTerms.And(layer.IntParameter(leInt.Parameter).IsLessThan(leInt.Value));
                            break;
                        case Data.RawCondition.GtFloat gtFloat:
                            andTerms.And(layer.FloatParameter(gtFloat.Parameter).IsGreaterThan(gtFloat.Value));
                            break;
                        case Data.RawCondition.LeFloat leFloat:
                            andTerms.And(layer.FloatParameter(leFloat.Parameter).IsLessThan(leFloat.Value));
                            break;
                        default:
                            throw new DeclavatarInternalException("Invalid LayerCondition deserialization object");
                    }
                }
            }
        }

        private void AppendStateParameterDrive(AacFlLayer layer, AacFlState state, Data.ParameterDrive drive)
        {
            switch (drive)
            {
                case Data.ParameterDrive.SetInt si:
                    state.Drives(layer.IntParameter(si.Parameter), si.Value);
                    break;
                case Data.ParameterDrive.SetBool sb:
                    state.Drives(layer.BoolParameter(sb.Parameter), sb.Value);
                    break;
                case Data.ParameterDrive.SetFloat sf:
                    state.Drives(layer.FloatParameter(sf.Parameter), sf.Value);
                    break;
                case Data.ParameterDrive.AddInt ai:
                    state.DrivingIncreases(layer.IntParameter(ai.Parameter), ai.Value);
                    break;
                case Data.ParameterDrive.AddFloat af:
                    state.DrivingIncreases(layer.FloatParameter(af.Parameter), af.Value);
                    break;
                case Data.ParameterDrive.RandomInt ri:
                    state.DrivingRandomizesLocally(layer.IntParameter(ri.Parameter), ri.Range[0], ri.Range[1]);
                    break;
                case Data.ParameterDrive.RandomBool rb:
                    state.DrivingRandomizesLocally(layer.BoolParameter(rb.Parameter), rb.Chance);
                    break;
                case Data.ParameterDrive.RandomFloat rf:
                    state.DrivingRandomizesLocally(layer.FloatParameter(rf.Parameter), rf.Range[0], rf.Range[1]);
                    break;
                case Data.ParameterDrive.Copy cp:
                    state.DrivingCopies(layer.FloatParameter(cp.From), layer.FloatParameter(cp.To));
                    break;
                case Data.ParameterDrive.RangedCopy rcp:
                    state.DrivingRemaps(layer.FloatParameter(rcp.From), rcp.FromRange[0], rcp.FromRange[1], layer.FloatParameter(rcp.To), rcp.ToRange[0], rcp.ToRange[1]);
                    break;
            }
        }

        private void AppendStateTrackingControl(AacFlState state, Data.TrackingControl control)
        {
            foreach (var target in control.Targets)
            {
                if (control.AnimationDesired) {
                    state.TrackingAnimates(Data.VRChatExtension.ConvertToAacTarget(target));
                } else {
                    state.TrackingTracks(Data.VRChatExtension.ConvertToAacTarget(target));
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
                    syncType = Data.VRChatExtension.ConvertToMASyncType(pd),
                    defaultValue = Data.VRChatExtension.ConvertToVRCParameterValue(pd.ValueType),
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

            foreach (var item in _declavatarDefinition.MenuItems)
            {
                GameObject menuItem;
                switch (item)
                {
                    case Data.MenuItem.SubMenu submenu:
                        menuItem = GenerateMenuGroupObject(submenu);
                        break;
                    case Data.MenuItem.Button button:
                        menuItem = GenerateMenuButtonObject(button);
                        break;
                    case Data.MenuItem.Toggle toggle:
                        menuItem = GenerateMenuToggleObject(toggle);
                        break;
                    case Data.MenuItem.Radial radial:
                        menuItem = GenerateMenuRadialObject(radial);
                        break;
                    case Data.MenuItem.TwoAxis twoAxis:
                        menuItem = GenerateMenuTwoAxisObject(twoAxis);
                        break;
                    case Data.MenuItem.FourAxis fourAxis:
                        menuItem = GenerateMenuFourAxisObject(fourAxis);
                        break;
                    default:
                        continue;
                }
                menuItem.transform.parent = rootMenuItem.gameObject.transform;
            }

            rootMenuItem.transform.parent = _rootGameObject.transform.transform;
        }

        private GameObject GenerateMenuGroupObject(Data.MenuItem.SubMenu submenu)
        {
            var menuGroupRoot = new GameObject(submenu.Name);
            var menuItemComponent = menuGroupRoot.AddComponent<ModularAvatarMenuItem>();
            menuItemComponent.MenuSource = SubmenuSource.Children;

            var control = menuItemComponent.Control;
            control.type = VRCExpressionsMenu.Control.ControlType.SubMenu;
            control.name = submenu.Name;

            foreach (var item in submenu.Items)
            {
                GameObject menuItem;
                switch (item)
                {
                    case Data.MenuItem.SubMenu submenu2:
                        menuItem = GenerateMenuGroupObject(submenu2);
                        break;
                    case Data.MenuItem.Button button:
                        menuItem = GenerateMenuButtonObject(button);
                        break;
                    case Data.MenuItem.Toggle toggle:
                        menuItem = GenerateMenuToggleObject(toggle);
                        break;
                    case Data.MenuItem.Radial radial:
                        menuItem = GenerateMenuRadialObject(radial);
                        break;
                    case Data.MenuItem.TwoAxis twoAxis:
                        menuItem = GenerateMenuTwoAxisObject(twoAxis);
                        break;
                    case Data.MenuItem.FourAxis fourAxis:
                        menuItem = GenerateMenuFourAxisObject(fourAxis);
                        break;
                    default:
                        continue;
                }
                menuItem.transform.parent = menuGroupRoot.gameObject.transform;
            }

            return menuGroupRoot;
        }

        private GameObject GenerateMenuButtonObject(Data.MenuItem.Button button)
        {
            var menuItemObject = new GameObject(button.Name);
            var menuItemComponent = menuItemObject.AddComponent<ModularAvatarMenuItem>();

            var control = menuItemComponent.Control;
            control.type = VRCExpressionsMenu.Control.ControlType.Button;
            control.name = button.Name;
            control.parameter = new VRCExpressionsMenu.Control.Parameter { name = button.Parameter };
            control.value = Data.VRChatExtension.ConvertToVRCParameterValue(button.Value);

            return menuItemObject;
        }

        private GameObject GenerateMenuToggleObject(Data.MenuItem.Toggle toggle)
        {
            var menuItemObject = new GameObject(toggle.Name);
            var menuItemComponent = menuItemObject.AddComponent<ModularAvatarMenuItem>();

            var control = menuItemComponent.Control;
            control.type = VRCExpressionsMenu.Control.ControlType.Toggle;
            control.name = toggle.Name;
            control.parameter = new VRCExpressionsMenu.Control.Parameter { name = toggle.Parameter };
            control.value = Data.VRChatExtension.ConvertToVRCParameterValue(toggle.Value);

            return menuItemObject;
        }

        private GameObject GenerateMenuRadialObject(Data.MenuItem.Radial radial)
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

        private GameObject GenerateMenuTwoAxisObject(Data.MenuItem.TwoAxis twoAxis)
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

        private GameObject GenerateMenuFourAxisObject(Data.MenuItem.FourAxis fourAxis)
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
