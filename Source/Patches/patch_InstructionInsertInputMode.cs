using System;
using System.Collections.Generic;
using System.Linq;
using LoopFromHere;
using LoopFromHere.Components;
using LoopFromHere.UI;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod;
using MonoMod.Cil;
using MonoMod.InlineRT;
using MonoMod.Utils;
using Quintessential.Components;

public class patch_InstructionInsertInputMode : InstructionInsertInputMode {
    // can't inherit from this because it's sealed
    public class patch_class_220 {
        [MonoModIgnore]
        public Vector2 field_1913;

        [MonoModIgnore]
        public Maybe<InstructionReference> field_1914;

        internal bool LoopFromHere_hasLoopRegionStart, LoopFromHere_hasLoopRegionEnd;
    }

    [MonoModIgnore]
    private List<class_220> field_1908;

    [MonoModILInject("IInputMode InstructionInsertInputMode::StartInputMode(SolutionEditorScreen,Vector2)")]
    public static void Patch_StartInputMode(MethodDefinition method, CustomAttribute attribute) {
        MonoModRule.Modder.Log("[LoopFromHere] Patching `InstructionInsertInputMode.StartInputMode(SolutionEditorScreen solEditScrn, Vector2 startMousePos)` to remove period override instruction limit...");

        MethodDefinition m_InstructionInsertInputMode_RemovePeriodOverrideLimit = method.DeclaringType.FindMethod("LoopFromHere_RemovePeriodOverrideLimit")!;

        new ILContext(method).Invoke(il => {
            ILCursor cursor = new(il);

            /*
             * IL_00be: ldc.i4.1
             * IL_00bf: stloc.s 4
             * IL_00c1: brfalse.s IL_00c7
             * IL_00c3: ldloc.s 4
             */
            if (!cursor.TryGotoNext(MoveType.After,
                    instr => instr.MatchLdcI4(1),
                    instr => instr.MatchStloc(4),
                    instr => instr.MatchBrfalse(out _),
                    instr => instr.MatchLdloc(4)))
                throw new Exception("Failed to patch `InstructionInsertInputMode.StartInputMode(SolutionEditorScreen solEditScrn, Vector2 startMousePos)`: Unable to find period override instruction detection to patch.");

            cursor.EmitLdarg0();
            cursor.EmitCall(m_InstructionInsertInputMode_RemovePeriodOverrideLimit);
        });
    }

    // this conditional logic confuses me
    private static bool LoopFromHere_RemovePeriodOverrideLimit(bool orig, SolutionEditorScreen solEditScrn)
        => orig || solEditScrn.GetSolution().parts.Any(p => p.HasComponent(LoopRegionComponent.Id));

    [MonoModILInject("Process")]
    public static void Patch_Process(MethodDefinition method, CustomAttribute attribute) {
        MonoModRule.Modder.Log("[LoopFromHere] Patching `InstructionInsertInputMode.Process` to update loop regions when editing instructions...");

        MethodDefinition m_InstructionInsertInputMode_UpdateHeldInstructions = method.DeclaringType.FindMethod("LoopFromHere_UpdateHeldInstructions")!;
        MethodDefinition m_InstructionInsertInputMode_RepositionLoopRegions = method.DeclaringType.FindMethod("LoopFromHere_RepositionLoopRegions")!;

        TypeDefinition t_Dictionary = MonoModRule.Modder.FindType("System.Collections.Generic.Dictionary`2").Resolve();
        TypeDefinition t_LoopRegionComponent = MonoModRule.Modder.FindType("LoopFromHere.Components.LoopRegionComponent").Resolve();
        TypeDefinition t_int = MonoModRule.Modder.Module.TypeSystem.Int32.Resolve();
        GenericInstanceType t_Dictionary_LoopRegionComponent_int = new(t_Dictionary); t_Dictionary_LoopRegionComponent_int.GenericArguments.Add(t_LoopRegionComponent); t_Dictionary_LoopRegionComponent_int.GenericArguments.Add(t_int);

        new ILContext(method).Invoke(il => {
            ILCursor cursor = new(il);
            VariableDefinition l_newLoopRegionStartIndices = cursor.CreateLocal(t_Dictionary_LoopRegionComponent_int);

            /*
             * IL_00f5: ldarg.1
             * IL_00f6: ldfld class SolutionEditorProgramPanel SolutionEditorScreen::programPanel
             * IL_00fb: callvirt instance valuetype Bounds2 SolutionEditorProgramPanel::ProgramAreaBounds()
             * IL_0100: stloc.s 8
             */
            if (!cursor.TryGotoNext(MoveType.AfterLabel,
                    instr => instr.MatchLdarg1(),
                    instr => instr.MatchLdfld("SolutionEditorScreen", "programPanel"),
                    instr => instr.MatchCallvirt("SolutionEditorProgramPanel", "ProgramAreaBounds"),
                    instr => instr.MatchStloc(8)))
                throw new Exception("Failed to patch `InstructionInsertInputMode.Process`: Unable to find assignment to `programAreaBounds` local to patch.");

            cursor.EmitLdarg0();
            cursor.EmitLdarg1();
            cursor.EmitLdloc2();
            cursor.EmitCallvirt(m_InstructionInsertInputMode_UpdateHeldInstructions);
            cursor.EmitStloc(l_newLoopRegionStartIndices);

            /*
             * IL_0168: ldloc.3
             * IL_0169: callvirt instance valuetype [mscorlib]System.Collections.Generic.Dictionary`2/Enumerator<!0, !1> class [mscorlib]System.Collections.Generic.Dictionary`2<valuetype InstructionReference, class InstructionType>::GetEnumerator()
             * IL_016e: stloc.s 11
             */
            if (!cursor.TryGotoNext(MoveType.AfterLabel,
                    instr => instr.MatchLdloc3(),
                    instr => instr.MatchCallvirt(out MethodReference m_Dictionary_GetEnumerator)
                        && m_Dictionary_GetEnumerator.DeclaringType.Name == "Dictionary`2"
                        && m_Dictionary_GetEnumerator.Name == "GetEnumerator",
                    instr => instr.MatchStloc(11)))
                throw new Exception("Failed to patch `InstructionInsertInputMode.Process`: Unable to find beginning of `foreach` loop to patch.");

            cursor.EmitLdarg0();
            cursor.EmitLdarg1();
            cursor.EmitLdloc(l_newLoopRegionStartIndices);
            cursor.EmitLdloc3();
            cursor.EmitLdloc1();
            cursor.EmitCallvirt(m_InstructionInsertInputMode_RepositionLoopRegions);
        });
    }

    private Dictionary<LoopRegionComponent, int> LoopFromHere_UpdateHeldInstructions(SolutionEditorScreen solEditScrn, Vector2 mouseDelta) {
        Dictionary<LoopRegionComponent, int> newLoopRegionStartIndices = new();

        Dictionary<Part, (int, patch_class_220)> rightmostHeldInstructions = new();
        foreach (patch_class_220 heldInstruction in field_1908.Cast<patch_class_220>()) {
            heldInstruction.LoopFromHere_hasLoopRegionStart = heldInstruction.LoopFromHere_hasLoopRegionEnd = false;

            Vector2 coveredPosition = heldInstruction.field_1913 + mouseDelta + SolutionEditorProgramPanel.instructionSize / 2;
            Maybe<InstructionReference> maybeCoveredInstruction = solEditScrn.programPanel.GetInstructionAt(coveredPosition);
            if (!maybeCoveredInstruction.GetOrDefault(out InstructionReference coveredInstruction)) {
                heldInstruction.LoopFromHere_hasLoopRegionStart = heldInstruction.LoopFromHere_hasLoopRegionEnd = false;
                continue;
            }

            if (AffectingLoopRegions()
                && heldInstruction.field_1914.GetOrDefault(out InstructionReference originalPosition)
                && originalPosition.part.TryGetComponent(LoopRegionComponent.Id, out IPartComponent component)) {
                LoopRegionComponent loopRegionComponent = (LoopRegionComponent) component;
                if (originalPosition.part == coveredInstruction.part
                    && originalPosition.instructionIndex == loopRegionComponent.LoopStartIndex) {
                    // add loop region start
                    loopRegionComponent.LoopStartAlpha = LoopRegionComponent.GhostAlpha;
                    heldInstruction.LoopFromHere_hasLoopRegionStart = true;

                    newLoopRegionStartIndices.Add(loopRegionComponent, coveredInstruction.instructionIndex);
                }
            }
            if (!rightmostHeldInstructions.TryGetValue(coveredInstruction.part, out (int index, patch_class_220 instruction) info)
                || coveredInstruction.instructionIndex > info.index)
                // add candidate for loop region end
                rightmostHeldInstructions[coveredInstruction.part] = (coveredInstruction.instructionIndex, heldInstruction);
        }

        foreach ((Part part, (int index, patch_class_220 instruction) info) in rightmostHeldInstructions) {
            if (!part.TryGetComponent(LoopRegionComponent.Id, out IPartComponent component))
                continue;

            LoopRegionComponent loopRegionComponent = (LoopRegionComponent) component;
            if (!loopRegionComponent.RecalculateEndIndex(solEditScrn).GetOrDefault(out int programEndIndex)
                || info.index <= programEndIndex)
                continue;

            // add loop region end
            loopRegionComponent.LoopEndAlpha = LoopRegionComponent.GhostAlpha;
            info.instruction.LoopFromHere_hasLoopRegionEnd = true;
        }

        return newLoopRegionStartIndices;
    }

    private void LoopFromHere_RepositionLoopRegions(SolutionEditorScreen solEditScrn, Dictionary<LoopRegionComponent, int> newLoopRegionStartIndices, Dictionary<InstructionReference, InstructionType> insertedInstructions, bool inserting) {
        if (!AffectingLoopRegions())
            return;

        foreach ((LoopRegionComponent component, int newLoopRegionStartIndex) in newLoopRegionStartIndices)
            component.LoopStartIndex = newLoopRegionStartIndex;

        if (inserting) {
            foreach ((InstructionReference position, _) in insertedInstructions) {
                if (!position.part.TryGetComponent(LoopRegionComponent.Id, out IPartComponent component))
                    continue;

                LoopRegionComponent loopRegionComponent = (LoopRegionComponent) component;
                if (position.instructionIndex > loopRegionComponent.LoopStartIndex)
                    continue;

                // hmm
                bool shouldPushLoopRegion = false;
                int endOffset = position.part.program.GetInstruction(loopRegionComponent.LoopStartIndex).HasValue() ? 1 : 0;
                for (int i = position.instructionIndex; i < loopRegionComponent.LoopStartIndex + endOffset; i++) {
                    if (position.part.program.GetInstruction(i).HasValue())
                        shouldPushLoopRegion = true;
                    else {
                        shouldPushLoopRegion = false;
                        break;
                    }
                }

                if (shouldPushLoopRegion)
                    loopRegionComponent.LoopStartIndex++;
            }
        }
    }

    [MonoModILInject("ProcessAfterFrame")]
    public static void Patch_ProcessAfterFrame(MethodDefinition method, CustomAttribute attribute) {
        MonoModRule.Modder.Log("[LoopFromHere] Patching `InstructionInsertInputMode.ProcessAfterFrame` to update loop regions when editing instructions...");

        TypeDefinition t_InstructionInsertInputMode = method.DeclaringType;
        MethodDefinition m_InstructionInsertInputMode_DrawLoopRegionMarkers = t_InstructionInsertInputMode.FindMethod("LoopFromHere_DrawLoopRegionMarkers")!;

        TypeDefinition t_InstructionInsertInputMode_HeldInstruction = t_InstructionInsertInputMode.NestedTypes.First(t => t.Name == "class_220");

        new ILContext(method).Invoke(il => {
            ILCursor cursor = new(il);
            VariableDefinition l_heldInstruction = cursor.CreateLocal(t_InstructionInsertInputMode_HeldInstruction);

            // i'm not sure whether `get_Current` is safe to call multiple times so i'm using a local
            // also i don't rly wanna deal with generic bs :p
            // todo: maybe don't
            /*
             * IL_009f: ldloca.s 3
             * IL_00a1: call instance !0 valuetype [mscorlib]System.Collections.Generic.List`1/Enumerator<class InstructionInsertInputMode/class_220>::get_Current()
             */
            if (!cursor.TryGotoNext(MoveType.After,
                    instr => instr.MatchLdloca(3),
                    instr => instr.MatchCall(out MethodReference m_Enumerator_get_Current)
                        && m_Enumerator_get_Current.DeclaringType.Name == "Enumerator"
                        && m_Enumerator_get_Current.Name == "get_Current"))
                throw new Exception("Failed patching `InstructionInsertInputMode.ProcessAfterFrame`: Unable to find reference to iteration variable to patch.");

            cursor.EmitStloc(l_heldInstruction);
            cursor.EmitLdloc(l_heldInstruction);

            /*
             * IL_015f: ldloc.s 4
             * IL_0161: ldc.r4 -3
             * IL_0166: ldc.r4 0.0
             * IL_016b: newobj instance void Vector2::.ctor(float32, float32)
             * IL_0170: call valuetype Vector2 Vector2::op_Addition(valuetype Vector2, valuetype Vector2)
             * IL_0175: call void TextureRenderer::Render(class Texture, valuetype Vector2)
             */
            if (!cursor.TryGotoNext(MoveType.After,
                    instr => instr.MatchLdloc(4),
                    instr => instr.MatchLdcR4(-3f),
                    instr => instr.MatchLdcR4(0f),
                    instr => instr.MatchNewobj("Vector2"),
                    instr => instr.MatchCall("Vector2", "op_Addition"),
                    instr => instr.MatchCall("TextureRenderer", "Render")))
                throw new Exception("Failed patching `InstructionInsertInputMode.ProcessAfterFrame`: Unable to find call to `TextureRenderer.Render` to patch.");

            cursor.EmitLdarg0();
            cursor.EmitLdloc(l_heldInstruction);
            cursor.EmitLdloc(4);
            cursor.EmitCallvirt(m_InstructionInsertInputMode_DrawLoopRegionMarkers);
        });
    }

    private void LoopFromHere_DrawLoopRegionMarkers(class_220 heldInstruction, Vector2 heldInstructionPos) {
        bool drawLoopRegionStartMarker = ((patch_class_220) (object) heldInstruction).LoopFromHere_hasLoopRegionStart,
            drawLoopRegionEndMarker = ((patch_class_220) (object) heldInstruction).LoopFromHere_hasLoopRegionEnd;
        Vector2 instructionOrigin = heldInstructionPos + new Vector2(0f, 2f);

        if (drawLoopRegionStartMarker) {
            Vector2 markerPos = instructionOrigin + new Vector2(0f, SolutionEditorProgramPanel.instructionSize.Y / 2f);
            TextureRenderer.Render(LoopRegionTextures.LoopRegionLeft, markerPos - LoopRegionTextures.LoopRegionLeft.GetHalfSize());
        }
        if (drawLoopRegionEndMarker) {
            Vector2 markerPos = instructionOrigin + new Vector2(SolutionEditorProgramPanel.instructionSize.X, SolutionEditorProgramPanel.instructionSize.Y / 2f);
            TextureRenderer.Render(LoopRegionTextures.LoopRegionRight, markerPos - LoopRegionTextures.LoopRegionRight.GetHalfSize());
        }
    }

    internal static bool AffectingLoopRegions()
        => LoopRegionInputMode.EditingLoopRegions() ^ LoopFromHereSettings.Instance.InstructionEditsAffectLoopRegions;
}
