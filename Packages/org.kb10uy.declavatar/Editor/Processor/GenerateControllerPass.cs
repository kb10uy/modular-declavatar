using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;
using nadena.dev.modular_avatar.core;
using AnimatorAsCode.V1;
using AnimatorAsCode.V1.VRC;

namespace KusakaFactory.Declavatar.Processor
{
    public sealed class GenerateControllerPass : IDeclavatarPass
    {
        public void Execute(DeclavatarContext context)
        {
            var fxAnimator = context.Aac.NewAnimatorController();

            foreach (var animationGroup in context.AvatarDeclaration.FxController)
            {
                switch (animationGroup.Content)
                {
                    case Data.Layer.GroupLayer g:
                        GenerateGroupLayer(context, fxAnimator, animationGroup.Name, g);
                        break;
                    case Data.Layer.SwitchLayer s:
                        GenerateSwitchLayer(context, fxAnimator, animationGroup.Name, s);
                        break;
                    case Data.Layer.PuppetLayer p:
                        GeneratePuppetLayer(context, fxAnimator, animationGroup.Name, p);
                        break;
                    case Data.Layer.RawLayer r:
                        GenerateRawLayer(context, fxAnimator, animationGroup.Name, r);
                        break;
                    default:
                        context.ReportInternalError("runtime.internal.invalid_animation_group");
                        break;
                }
            }

            // MEMO: should be absolute path mode?
            var mergeAnimator = context.DeclarationRoot.AddComponent<ModularAvatarMergeAnimator>();
            mergeAnimator.animator = fxAnimator.AnimatorController;
            mergeAnimator.layerType = VRCAvatarDescriptor.AnimLayerType.FX;
            mergeAnimator.pathMode = MergeAnimatorPathMode.Relative;
        }

        private void GenerateGroupLayer(DeclavatarContext context, AacFlController controller, string name, Data.Layer.GroupLayer g)
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

        private void GenerateSwitchLayer(DeclavatarContext context, AacFlController controller, string name, Data.Layer.SwitchLayer s)
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

        private void GeneratePuppetLayer(DeclavatarContext context, AacFlController controller, string name, Data.Layer.PuppetLayer puppet)
        {
            var layer = controller.NewLayer(name);
            var layerParameter = layer.FloatParameter(puppet.Parameter);

            var state = layer.NewState(name).MotionTime(layerParameter);
            WriteStateAnimation(context, layer, state, puppet.Animation);
        }

        private void GenerateRawLayer(DeclavatarContext context, AacFlController controller, string name, Data.Layer.RawLayer rawLayer)
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
                    case Data.RawAnimation.BlendTree blendTree:
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
                            context.ReportInternalError("runtime.internal.invalid_layer_condition");
                            break;
                    }
                }
            }
        }

        private AnimationClip FetchAnimationClipForBlendTree(DeclavatarContext context, Data.LayerAnimation animation)
        {
            switch (animation)
            {
                case Data.LayerAnimation.Inline inline:
                    return CreateInlineClip(context, inline).Clip;
                case Data.LayerAnimation.KeyedInline keyedInline:
                    return CreateKeyedInlineClip(context, keyedInline).Clip;
                case Data.LayerAnimation.External external:
                    return context.GetExternalAnimationClip(external.Name);
                default:
                    context.ReportInternalError("runtime.internal.invalid_layer_animation");
                    throw new DeclavatarInternalException("internal");
            }
        }

        private void WriteStateAnimation(DeclavatarContext context, AacFlLayer layer, AacFlState state, Data.LayerAnimation animation)
        {
            try
            {
                switch (animation)
                {
                    case Data.LayerAnimation.Inline inline:
                        state.WithAnimation(CreateInlineClip(context, inline));
                        AppendPerState(context, layer, state, inline.Targets);
                        break;
                    case Data.LayerAnimation.KeyedInline keyedInline:
                        state.WithAnimation(CreateKeyedInlineClip(context, keyedInline));
                        break;
                    case Data.LayerAnimation.External external:
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

        private AacFlClip CreateInlineClip(DeclavatarContext context, Data.LayerAnimation.Inline inline)
        {
            var inlineClip = context.Aac.NewClip();
            foreach (var target in inline.Targets)
            {
                try
                {
                    switch (target)
                    {
                        case Data.Target.Shape shape:
                            var smr = context.FindSkinnedMeshRenderer(shape.Mesh);
                            inlineClip.BlendShape(smr, shape.Name, shape.Value * 100.0f);
                            break;
                        case Data.Target.Object obj:
                            var go = context.FindGameObject(obj.Name);
                            inlineClip.Toggling(go, obj.Enabled);
                            break;
                        case Data.Target.Material material:
                            var mr = context.FindRenderer(material.Mesh);
                            var targetMaterial = context.GetExternalMaterial(material.AssetKey);
                            inlineClip.SwappingMaterial(mr, (int)material.Slot, targetMaterial);
                            break;
                        case Data.Target.Drive _:
                        case Data.Target.Tracking _:
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

        private AacFlClip CreateKeyedInlineClip(DeclavatarContext context, Data.LayerAnimation.KeyedInline keyedInline)
        {
            var groups = keyedInline.Keyframes
                .SelectMany((kf) => kf.Targets.Select((t) => (kf.Value, Target: t)))
                .GroupBy((p) => Data.VRChatExtension.AsGroupingKey(p.Target));
            var keyedInlineClip = context.Aac.NewClip().NonLooping();
            keyedInlineClip.Animating((e) =>
            {
                foreach (var group in groups)
                {
                    try
                    {
                        if (group.Key.StartsWith("shape://"))
                        {
                            var points = group.Select((p) => (p.Value, Target: p.Target as Data.Target.Shape)).ToList();
                            var smr = context.FindSkinnedMeshRenderer(points[0].Target.Mesh);
                            e.Animates(smr, $"blendShape.{points[0].Target.Name}").WithFrameCountUnit((kfs) =>
                            {
                                foreach (var point in points) kfs.Linear(point.Value * 100.0f, point.Target.Value * 100.0f);
                            });
                        }
                        else if (group.Key.StartsWith("object://"))
                        {
                            var points = group.Select((p) => (p.Value, Target: p.Target as Data.Target.Object)).ToList();
                            var go = context.FindGameObject(points[0].Target.Name);
                            e.Animates(go).WithFrameCountUnit((kfs) =>
                            {
                                foreach (var point in points) kfs.Constant(point.Value * 100.0f, point.Target.Enabled ? 1.0f : 0.0f);
                            });
                        }
                        else if (group.Key.StartsWith("material://"))
                        {
                            // Use traditional API for matarial swapping
                            var points = group.Select((p) => (p.Value, Target: p.Target as Data.Target.Material)).ToList();
                            var mr = context.FindRenderer(points[0].Target.Mesh);

                            var binding = e.BindingFromComponent(mr, $"m_Materials.Array.data[{points[0].Target.Slot}]");
                            var keyframes = points.Select((p) => new ObjectReferenceKeyframe
                            {
                                time = p.Value * 100.0f,
                                value = context.GetExternalMaterial(p.Target.AssetKey),
                            }).ToArray();
                            AnimationUtility.SetObjectReferenceCurve(keyedInlineClip.Clip, binding, keyframes);
                        }
                    }
                    catch (DeclavatarRuntimeException)
                    {
                    }
                }
            });
            return keyedInlineClip;
        }

        private void AppendPerState(DeclavatarContext context, AacFlLayer layer, AacFlState state, IReadOnlyList<Data.Target> targets)
        {
            foreach (var target in targets)
            {
                switch (target)
                {
                    case Data.Target.Shape _:
                    case Data.Target.Object _:
                    case Data.Target.Material _:
                        continue;
                    case Data.Target.Drive drive:
                        AppendStateParameterDrive(layer, state, drive.ParameterDrive);
                        break;
                    case Data.Target.Tracking control:
                        AppendStateTrackingControl(state, control.Control);
                        break;
                    default:
                        context.ReportInternalError("runtime.internal.invalid_target");
                        break;
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
            if (control.AnimationDesired)
            {
                state.TrackingAnimates(Data.VRChatExtension.ConvertToAacTarget(control.Target));
            }
            else
            {
                state.TrackingTracks(Data.VRChatExtension.ConvertToAacTarget(control.Target));
            }
        }
    }
}
