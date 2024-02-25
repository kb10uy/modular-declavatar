using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;
using nadena.dev.modular_avatar.core;
using AnimatorAsCode.V1;
using AnimatorAsCode.V1.VRC;
using KusakaFactory.Declavatar.Runtime.Data;

namespace KusakaFactory.Declavatar.Processor
{
    internal sealed class GenerateControllerPass : IDeclavatarPass
    {
        private AacFlBase _currentAac;

        public void Execute(DeclavatarContext context)
        {
            _currentAac = AacV1.Create(new AacConfiguration
            {
                // MEMO: should it use avatar name from decl file?
                SystemName = "Declavatar",

                AnimatorRoot = context.DeclarationRoot.transform,
                DefaultValueRoot = context.DeclarationRoot.transform,

                AssetKey = GUID.Generate().ToString(),
                AssetContainer = context.NdmfContext.AssetContainer,
                ContainerMode = AacConfiguration.Container.OnlyWhenPersistenceRequired,
                DefaultsProvider = new AacDefaultsProvider(false),
            });

            var fxAnimator = _currentAac.NewAnimatorController();
            foreach (var animationGroup in context.AvatarDeclaration.FxController)
            {
                switch (animationGroup.Content)
                {
                    case Layer.GroupLayer g:
                        GenerateGroupLayer(context, fxAnimator, animationGroup.Name, g);
                        break;
                    case Layer.SwitchLayer s:
                        GenerateSwitchLayer(context, fxAnimator, animationGroup.Name, s);
                        break;
                    case Layer.PuppetLayer p:
                        GeneratePuppetLayer(context, fxAnimator, animationGroup.Name, p);
                        break;
                    case Layer.SwitchGateLayer sg:
                        GenerateSwitchGateLayer(context, fxAnimator, animationGroup.Name, sg);
                        break;
                    case Layer.RawLayer r:
                        GenerateRawLayer(context, fxAnimator, animationGroup.Name, r);
                        break;
                    default:
                        context.ReportInternalError("runtime.internal.invalid_animation_group");
                        break;
                }
            }

            // should be absolute path mode
            var mergeAnimator = context.DeclarationRoot.AddComponent<ModularAvatarMergeAnimator>();
            mergeAnimator.animator = fxAnimator.AnimatorController;
            mergeAnimator.layerType = VRCAvatarDescriptor.AnimLayerType.FX;
            mergeAnimator.pathMode = MergeAnimatorPathMode.Relative;
        }

        private void GenerateGroupLayer(DeclavatarContext context, AacFlController controller, string name, Layer.GroupLayer g)
        {
            var layer = controller.NewLayer(name);
            var layerParameter = layer.IntParameter(g.Parameter);

            var idleState = layer.NewState("Disabled", 0, 0);
            WriteStateAnimation(context, layer, idleState, g.Default.Animation);

            foreach (var option in g.Options)
            {
                var state = layer.NewState($"{option.Value} {option.Name}", (int)option.Value / 8 + 1, (int)option.Value % 8);
                WriteStateAnimation(context, layer, state, option.Animation);
                idleState.TransitionsTo(state).When(layerParameter.IsEqualTo((int)option.Value));
                state.Exits().When(layerParameter.IsNotEqualTo((int)option.Value));
            }
        }

        private void GenerateSwitchLayer(DeclavatarContext context, AacFlController controller, string name, Layer.SwitchLayer s)
        {
            var layer = controller.NewLayer(name);
            var layerParameter = layer.BoolParameter(s.Parameter);

            var disabledState = layer.NewState("Disabled");
            var enabledState = layer.NewState("Enabled");

            WriteStateAnimation(context, layer, disabledState, s.Disabled);
            WriteStateAnimation(context, layer, enabledState, s.Enabled);

            disabledState.TransitionsTo(enabledState).When(layerParameter.IsTrue());
            enabledState.TransitionsTo(disabledState).When(layerParameter.IsFalse());
        }

        private void GeneratePuppetLayer(DeclavatarContext context, AacFlController controller, string name, Layer.PuppetLayer puppet)
        {
            var layer = controller.NewLayer(name);
            var layerParameter = layer.FloatParameter(puppet.Parameter);

            var state = layer.NewState(name).MotionTime(layerParameter);
            WriteStateAnimation(context, layer, state, puppet.Animation);
        }

        private void GenerateSwitchGateLayer(DeclavatarContext context, AacFlController controller, string name, Layer.SwitchGateLayer sg)
        {
            var layer = controller.NewLayer(name);
            var disabledState = layer.NewState("Disabled");
            var enabledState = layer.NewState("Enabled");

            WriteStateAnimation(context, layer, disabledState, sg.Disabled);
            WriteStateAnimation(context, layer, enabledState, sg.Enabled);

            var parameters = layer.BoolParameters(context.Exports.GetGateGuardParameters(sg.Gate).ToArray());
            disabledState.TransitionsTo(enabledState).When(parameters.IsAnyTrue());
            enabledState.TransitionsTo(disabledState).When(parameters.AreFalse());
        }

        private void GenerateRawLayer(DeclavatarContext context, AacFlController controller, string name, Layer.RawLayer rawLayer)
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
                    case RawAnimation.Clip clip:
                        WriteStateAnimation(context, layer, state, clip.Animation);
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
                    case RawAnimation.BlendTree blendTree:
                        var tree = new BlendTree();
                        try
                        {
                            switch (blendTree.BlendType)
                            {
                                case "Linear":
                                    tree.blendType = BlendTreeType.Simple1D;
                                    tree.blendParameter = blendTree.Parameters[0];
                                    foreach (var field in blendTree.Fields)
                                    {
                                        tree.AddChild(FetchAnimationClipForBlendTree(context, field.Animation), field.Position[0]);
                                    }
                                    break;
                                case "Simple2D":
                                    tree.blendType = BlendTreeType.SimpleDirectional2D;
                                    tree.blendParameter = blendTree.Parameters[0];
                                    tree.blendParameterY = blendTree.Parameters[1];
                                    foreach (var field in blendTree.Fields)
                                    {
                                        tree.AddChild(FetchAnimationClipForBlendTree(context, field.Animation), new Vector2(field.Position[0], field.Position[1]));
                                    }
                                    break;
                                case "Freeform2D":
                                    tree.blendType = BlendTreeType.FreeformDirectional2D;
                                    tree.blendParameter = blendTree.Parameters[0];
                                    tree.blendParameterY = blendTree.Parameters[1];
                                    foreach (var field in blendTree.Fields)
                                    {
                                        tree.AddChild(FetchAnimationClipForBlendTree(context, field.Animation), new Vector2(field.Position[0], field.Position[1]));
                                    }
                                    break;
                                case "Cartesian2D":
                                    tree.blendType = BlendTreeType.FreeformCartesian2D;
                                    tree.blendParameter = blendTree.Parameters[0];
                                    tree.blendParameterY = blendTree.Parameters[1];
                                    foreach (var field in blendTree.Fields)
                                    {
                                        tree.AddChild(FetchAnimationClipForBlendTree(context, field.Animation), new Vector2(field.Position[0], field.Position[1]));
                                    }
                                    break;
                                default:
                                    context.ReportInternalError("runtime.internal.invalid_blendtree");
                                    break;
                            }
                        }
                        catch (DeclavatarRuntimeException)
                        {
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
                        case RawCondition.Be be:
                            andTerms.And(layer.BoolParameter(be.Parameter).IsTrue());
                            break;
                        case RawCondition.Not not:
                            andTerms.And(layer.BoolParameter(not.Parameter).IsFalse());
                            break;
                        case RawCondition.EqInt eqInt:
                            andTerms.And(layer.IntParameter(eqInt.Parameter).IsEqualTo(eqInt.Value));
                            break;
                        case RawCondition.NeqInt neqInt:
                            andTerms.And(layer.IntParameter(neqInt.Parameter).IsNotEqualTo(neqInt.Value));
                            break;
                        case RawCondition.GtInt gtInt:
                            andTerms.And(layer.IntParameter(gtInt.Parameter).IsGreaterThan(gtInt.Value));
                            break;
                        case RawCondition.LeInt leInt:
                            andTerms.And(layer.IntParameter(leInt.Parameter).IsLessThan(leInt.Value));
                            break;
                        case RawCondition.GtFloat gtFloat:
                            andTerms.And(layer.FloatParameter(gtFloat.Parameter).IsGreaterThan(gtFloat.Value));
                            break;
                        case RawCondition.LeFloat leFloat:
                            andTerms.And(layer.FloatParameter(leFloat.Parameter).IsLessThan(leFloat.Value));
                            break;
                        default:
                            context.ReportInternalError("runtime.internal.invalid_layer_condition");
                            break;
                    }
                }
            }
        }

        private AnimationClip FetchAnimationClipForBlendTree(DeclavatarContext context, LayerAnimation animation)
        {
            switch (animation)
            {
                case LayerAnimation.Inline inline:
                    return CreateInlineClip(context, inline).Clip;
                case LayerAnimation.KeyedInline keyedInline:
                    return CreateKeyedInlineClip(context, keyedInline).Clip;
                case LayerAnimation.External external:
                    return context.GetExternalAnimationClip(external.Name);
                default:
                    context.ReportInternalError("runtime.internal.invalid_layer_animation");
                    throw new DeclavatarInternalException("internal");
            }
        }

        private void WriteStateAnimation(DeclavatarContext context, AacFlLayer layer, AacFlState state, LayerAnimation animation)
        {
            try
            {
                switch (animation)
                {
                    case LayerAnimation.Inline inline:
                        state.WithAnimation(CreateInlineClip(context, inline));
                        AppendPerState(context, layer, state, inline.Targets);
                        break;
                    case LayerAnimation.KeyedInline keyedInline:
                        state.WithAnimation(CreateKeyedInlineClip(context, keyedInline));
                        break;
                    case LayerAnimation.External external:
                        state.WithAnimation(context.GetExternalAnimationClip(external.Name));
                        break;
                    default:
                        context.ReportInternalError("runtime.internal.invalid_layer_animation");
                        throw new DeclavatarInternalException("internal");
                }
            }
            catch (DeclavatarRuntimeException)
            {
            }
        }

        private AacFlClip CreateInlineClip(DeclavatarContext context, LayerAnimation.Inline inline)
        {
            var inlineClip = _currentAac.NewClip();
            foreach (var target in inline.Targets)
            {
                try
                {
                    switch (target)
                    {
                        case Target.Shape shape:
                            var smr = context.FindSkinnedMeshRenderer(shape.Mesh);
                            inlineClip.BlendShape(smr, shape.Name, shape.Value * 100.0f);
                            break;
                        case Target.Object obj:
                            var go = context.FindGameObject(obj.Name);
                            inlineClip.Toggling(go, obj.Enabled);
                            break;
                        case Target.Material material:
                            var mr = context.FindRenderer(material.Mesh);
                            var targetMaterial = context.GetExternalMaterial(material.AssetKey);
                            inlineClip.SwappingMaterial(mr, (int)material.Slot, targetMaterial);
                            break;
                        case Target.MaterialProperty materialProp:
                            var mpr = context.FindRenderer(materialProp.Mesh);
                            inlineClip.Animating((editClip) =>
                            {
                                var qualifiedName = $"material.{materialProp.Property}";
                                switch (materialProp.Value)
                                {
                                    case MaterialValue.Float floatValue:
                                        editClip.Animates(mpr, qualifiedName).WithOneFrame(floatValue.Value);
                                        break;
                                    case MaterialValue.VectorRgba rgbaValue:
                                        var rgbaColor = new Color(rgbaValue.Value[0], rgbaValue.Value[1], rgbaValue.Value[2], rgbaValue.Value[3]);
                                        editClip.AnimatesColor(mpr, qualifiedName).WithOneFrame(rgbaColor);
                                        break;
                                    case MaterialValue.VectorXyzw xyzwValue:
                                        // HDR Color will convert into just xyzw
                                        var xyzwColor = new Color(xyzwValue.Value[0], xyzwValue.Value[1], xyzwValue.Value[2], xyzwValue.Value[3]);
                                        editClip.AnimatesHDRColor(mpr, qualifiedName).WithOneFrame(xyzwColor);
                                        break;
                                    default:
                                        context.ReportInternalError("runtime.internal.invalid_target");
                                        break;
                                }
                            });
                            break;
                        case Target.Drive _:
                        case Target.Tracking _:
                            continue;
                        default:
                            context.ReportInternalError("runtime.internal.invalid_target");
                            break;
                    }
                }
                catch (DeclavatarRuntimeException)
                {
                }
            }

            return inlineClip;
        }

        private AacFlClip CreateKeyedInlineClip(DeclavatarContext context, LayerAnimation.KeyedInline keyedInline)
        {
            var groups = keyedInline.Keyframes
                .SelectMany((kf) => kf.Targets.Select((t) => (Time: kf.Value, Target: t)))
                .GroupBy((p) => p.Target.AsGroupingKey());
            var keyedInlineClip = _currentAac.NewClip().NonLooping();
            keyedInlineClip.Animating((e) =>
            {
                foreach (var group in groups)
                {
                    try
                    {
                        if (group.Key.StartsWith("shape://"))
                        {
                            var points = group.Select((p) => (p.Time, Target: p.Target as Target.Shape)).ToList();
                            var smr = context.FindSkinnedMeshRenderer(points[0].Target.Mesh);
                            e.Animates(smr, $"blendShape.{points[0].Target.Name}").WithFrameCountUnit((kfs) =>
                            {
                                foreach (var point in points) kfs.Linear(point.Time * 100.0f, point.Target.Value * 100.0f);
                            });
                        }
                        else if (group.Key.StartsWith("object://"))
                        {
                            var points = group.Select((p) => (p.Time, Target: p.Target as Target.Object)).ToList();
                            var go = context.FindGameObject(points[0].Target.Name);
                            e.Animates(go).WithFrameCountUnit((kfs) =>
                            {
                                foreach (var point in points) kfs.Constant(point.Time * 100.0f, point.Target.Enabled ? 1.0f : 0.0f);
                            });
                        }
                        else if (group.Key.StartsWith("material://"))
                        {
                            // Use traditional API for matarial swapping
                            var points = group.Select((p) => (p.Time, Target: p.Target as Target.Material)).ToList();
                            var mr = context.FindRenderer(points[0].Target.Mesh);

                            var binding = e.BindingFromComponent(mr, $"m_Materials.Array.data[{points[0].Target.Slot}]");
                            var keyframes = points.Select((p) => new ObjectReferenceKeyframe
                            {
                                time = p.Time * 100.0f,
                                value = context.GetExternalMaterial(p.Target.AssetKey),
                            }).ToArray();
                            AnimationUtility.SetObjectReferenceCurve(keyedInlineClip.Clip, binding, keyframes);
                        }
                        else if (group.Key.StartsWith("material+prop://"))
                        {
                            var points = group.Select((p) => (p.Time, Target: p.Target as Target.MaterialProperty)).ToList();
                            var mr = context.FindRenderer(points[0].Target.Mesh);
                            var qualifiedName = $"material.{points[0].Target.Property}";
                            switch (points[0].Target.Value)
                            {
                                case MaterialValue.Float floatValue:
                                    var floatPoints = points.Select((p) => (p.Time, (p.Target.Value as MaterialValue.Float).Value));
                                    e.Animates(mr, qualifiedName).WithFrameCountUnit((kfs) =>
                                    {
                                        foreach (var point in floatPoints) kfs.Linear(point.Time * 100.0f, point.Value);
                                    });
                                    break;
                                case MaterialValue.VectorRgba rgbaValue:
                                    var rgbaPoints = points.Select((p) =>
                                    {
                                        var castValue = (p.Target.Value as MaterialValue.VectorRgba).Value;
                                        return (p.Time, Value: new Color(castValue[0], castValue[1], castValue[2], castValue[3]));
                                    });
                                    e.AnimatesColor(mr, qualifiedName).WithKeyframes(AacFlUnit.Frames, (kfs) =>
                                    {
                                        foreach (var point in rgbaPoints) kfs.Linear(point.Time * 100.0f, point.Value);
                                    });
                                    break;
                                case MaterialValue.VectorXyzw xyzwValue:
                                    // HDR Color will convert into just xyzw
                                    var xyzwPoints = points.Select((p) =>
                                    {
                                        var castValue = (p.Target.Value as MaterialValue.VectorXyzw).Value;
                                        return (p.Time, Value: new Color(castValue[0], castValue[1], castValue[2], castValue[3]));
                                    });
                                    e.AnimatesColor(mr, qualifiedName).WithKeyframes(AacFlUnit.Frames, (kfs) =>
                                    {
                                        foreach (var point in xyzwPoints) kfs.Linear(point.Time * 100.0f, point.Value);
                                    });
                                    break;
                                default:
                                    context.ReportInternalError("runtime.internal.invalid_target");
                                    break;
                            }
                        }
                    }
                    catch (DeclavatarRuntimeException)
                    {
                    }
                }
            });
            return keyedInlineClip;
        }

        private void AppendPerState(DeclavatarContext context, AacFlLayer layer, AacFlState state, IReadOnlyList<Target> targets)
        {
            foreach (var target in targets)
            {
                switch (target)
                {
                    case Target.Shape _:
                    case Target.Object _:
                    case Target.Material _:
                        continue;
                    case Target.Drive drive:
                        AppendStateParameterDrive(layer, state, drive.ParameterDrive);
                        break;
                    case Target.Tracking control:
                        AppendStateTrackingControl(state, control.Control);
                        break;
                    default:
                        context.ReportInternalError("runtime.internal.invalid_target");
                        break;
                }
            }
        }

        private void AppendStateParameterDrive(AacFlLayer layer, AacFlState state, ParameterDrive drive)
        {
            switch (drive)
            {
                case ParameterDrive.SetInt si:
                    state.Drives(layer.IntParameter(si.Parameter), si.Value);
                    break;
                case ParameterDrive.SetBool sb:
                    state.Drives(layer.BoolParameter(sb.Parameter), sb.Value);
                    break;
                case ParameterDrive.SetFloat sf:
                    state.Drives(layer.FloatParameter(sf.Parameter), sf.Value);
                    break;
                case ParameterDrive.AddInt ai:
                    state.DrivingIncreases(layer.IntParameter(ai.Parameter), ai.Value);
                    break;
                case ParameterDrive.AddFloat af:
                    state.DrivingIncreases(layer.FloatParameter(af.Parameter), af.Value);
                    break;
                case ParameterDrive.RandomInt ri:
                    state.DrivingRandomizesLocally(layer.IntParameter(ri.Parameter), ri.Range[0], ri.Range[1]);
                    break;
                case ParameterDrive.RandomBool rb:
                    state.DrivingRandomizesLocally(layer.BoolParameter(rb.Parameter), rb.Chance);
                    break;
                case ParameterDrive.RandomFloat rf:
                    state.DrivingRandomizesLocally(layer.FloatParameter(rf.Parameter), rf.Range[0], rf.Range[1]);
                    break;
                case ParameterDrive.Copy cp:
                    state.DrivingCopies(layer.FloatParameter(cp.From), layer.FloatParameter(cp.To));
                    break;
                case ParameterDrive.RangedCopy rcp:
                    state.DrivingRemaps(layer.FloatParameter(rcp.From), rcp.FromRange[0], rcp.FromRange[1], layer.FloatParameter(rcp.To), rcp.ToRange[0], rcp.ToRange[1]);
                    break;
            }
        }

        private void AppendStateTrackingControl(AacFlState state, TrackingControl control)
        {
            if (control.AnimationDesired)
            {
                state.TrackingAnimates(control.Target.ConvertToAacTarget());
            }
            else
            {
                state.TrackingTracks(control.Target.ConvertToAacTarget());
            }
        }
    }
}
