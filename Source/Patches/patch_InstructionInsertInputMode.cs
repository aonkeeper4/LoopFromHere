using System;
using System.Collections.Generic;
using System.Linq;
using LoopFromHere;
using LoopFromHere.Components;
using LoopFromHere.Helpers;
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

        internal Maybe<InstructionReference> LoopFromHere_CoveredInstruction;

        internal bool LoopFromHere_HasLoopRegionStart, LoopFromHere_HasLoopRegionEnd;
    }

    [MonoModIgnore]
    private List<patch_class_220> field_1908;

    internal HashSet<LoopRegionComponent> LoopFromHere_EditedLoopRegions;

    [MonoModILInject("IInputMode InstructionInsertInputMode::StartInputMode(SolutionEditorScreen,Vector2)")]
    public static void Patch_StartInputMode(MethodDefinition method, CustomAttribute attribute) {
        MonoModRule.Modder.Log("[LoopFromHere] Patching `InstructionInsertInputMode.StartInputMode(SolutionEditorScreen solEditScrn, Vector2 startMousePos)` to update loop regions when editing instructions...");

        MethodDefinition m_InstructionInsertInputMode_RemovePeriodOverrideLimit = method.DeclaringType.FindMethod("LoopFromHere_RemovePeriodOverrideLimit")!;
        MethodDefinition m_InstructionInsertInputMode_GetEditedLoopRegions = method.DeclaringType.FindMethod("LoopFromHere_GetEditedLoopRegions")!;

        FieldReference f_InstructionInsertInputMode_editedLoopRegions = method.DeclaringType.FindField("LoopFromHere_EditedLoopRegions")!;

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

            /*
             * IL_00fb: ldarg.0
             * IL_00fc: ldfld class InstructionSelection SolutionEditorScreen::instructionSelection
             * IL_0101: callvirt instance void InstructionSelection::Clear()
             * IL_0106: ldloc.0
             * IL_0107: ret
             */
            if (!cursor.TryGotoNext(MoveType.AfterLabel,
                    instr => instr.MatchLdarg0(),
                    instr => instr.MatchLdfld("SolutionEditorScreen", "instructionSelection"),
                    instr => instr.MatchCallvirt("InstructionSelection", "Clear"),
                    instr => instr.MatchLdloc0(),
                    instr => instr.MatchRet()))
                throw new Exception("Failed to patch `InstructionInsertInputMode.StartInputMode(SolutionEditorScreen solEditScrn, Vector2 startMousePos)`: Unable to find call to `InstructionSelection.Clear` to patch.");

            cursor.EmitLdloc0();
            cursor.EmitLdarg0();
            cursor.EmitCall(m_InstructionInsertInputMode_GetEditedLoopRegions);
            cursor.EmitStfld(f_InstructionInsertInputMode_editedLoopRegions);
        });
    }

    // this conditional logic confuses me
    private static bool LoopFromHere_RemovePeriodOverrideLimit(bool orig, SolutionEditorScreen solEditScrn)
        => orig || solEditScrn.GetSolution().parts.Any(p => p.HasComponent(LoopRegionComponent.Id));

    // i think this is slightly too lenient but it should be fine?
    private static HashSet<LoopRegionComponent> LoopFromHere_GetEditedLoopRegions(SolutionEditorScreen solEditScrn) {
        HashSet<LoopRegionComponent> editedLoopRegions = [];
        foreach (InstructionReference instruction in solEditScrn.instructionSelection) {
            if (!instruction.part.TryGetComponent(LoopRegionComponent.Id, out IPartComponent component))
                continue;

            LoopRegionComponent loopRegionComponent = (LoopRegionComponent) component;
            if (AffectingLoopRegions() && instruction.instructionIndex == loopRegionComponent.StartIndex
                || instruction.instructionIndex == loopRegionComponent.EndIndex)
                editedLoopRegions.Add(loopRegionComponent);
        }

        return editedLoopRegions;
    }

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
             * IL_0157: ldc.i4.1
             * IL_0158: call bool InputManager::IsClickReleased(valuetype MouseButtonType)
             * IL_015d: brfalse IL_02ca
             */
            if (!cursor.TryGotoNext(MoveType.AfterLabel,
                    instr => instr.MatchLdcI4(1),
                    instr => instr.MatchCall("InputManager", "IsClickReleased"),
                    instr => instr.MatchBrfalse(out _)))
                throw new Exception("Failed to patch `InstructionInsertInputMode.Process`: Unable to find call to `InputManager.IsClickReleased` to patch.");

            cursor.EmitLdarg0();
            cursor.EmitLdarg1();
            cursor.EmitLdloc0();
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
            cursor.EmitLdloc(l_newLoopRegionStartIndices);
            cursor.EmitLdloc3();
            cursor.EmitLdloc1();
            cursor.EmitCallvirt(m_InstructionInsertInputMode_RepositionLoopRegions);
        });
    }

    // this code is the bane of my existence
    private Dictionary<LoopRegionComponent, int> LoopFromHere_UpdateHeldInstructions(SolutionEditorScreen solEditScrn, enum_140 insertOperation, Vector2 mouseDelta) {
        Dictionary<LoopRegionComponent, int> newLoopRegionStartIndices = new();
        if (insertOperation != (enum_140) 2) {
            foreach (patch_class_220 heldInstruction in field_1908) {
                heldInstruction.LoopFromHere_HasLoopRegionStart = heldInstruction.LoopFromHere_HasLoopRegionEnd = false;

                if (insertOperation == (enum_140) 1
                    && heldInstruction.field_1914.GetOrDefault(out InstructionReference originalPosition)
                    && originalPosition.part.TryGetComponent(LoopRegionComponent.Id, out IPartComponent component)) {
                    LoopRegionComponent loopRegionComponent = (LoopRegionComponent) component;
                    loopRegionComponent.LoopStartAlpha = loopRegionComponent.LoopEndAlpha = LoopRegionComponent.GhostAlpha;
                }
            }

            return newLoopRegionStartIndices;
        }

        Dictionary<Part, patch_class_220> rightmostHeldInstructions = new();
        foreach (patch_class_220 heldInstruction in field_1908) {
            heldInstruction.LoopFromHere_HasLoopRegionStart = heldInstruction.LoopFromHere_HasLoopRegionEnd = false;

            Vector2 coveredPosition = heldInstruction.field_1913 + mouseDelta + SolutionEditorProgramPanel.instructionSize / 2;
            heldInstruction.LoopFromHere_CoveredInstruction = solEditScrn.programPanel.GetInstructionAt(coveredPosition);
            if (!heldInstruction.LoopFromHere_CoveredInstruction.GetOrDefault(out InstructionReference coveredInstruction)) {
                heldInstruction.LoopFromHere_HasLoopRegionStart = heldInstruction.LoopFromHere_HasLoopRegionEnd = false;
                continue;
            }

            if (AffectingLoopRegions()
                && heldInstruction.field_1914.GetOrDefault(out InstructionReference originalPosition)
                && originalPosition.part.TryGetComponent(LoopRegionComponent.Id, out IPartComponent component)) {
                LoopRegionComponent loopRegionComponent = (LoopRegionComponent) component;
                if (originalPosition.part == coveredInstruction.part
                    && originalPosition.instructionIndex == loopRegionComponent.StartIndex) {
                    // add loop region start
                    loopRegionComponent.LoopStartAlpha = LoopRegionComponent.GhostAlpha;
                    heldInstruction.LoopFromHere_HasLoopRegionStart = true;

                    newLoopRegionStartIndices.Add(loopRegionComponent, coveredInstruction.instructionIndex);
                }
            }
            if (!rightmostHeldInstructions.TryGetValue(coveredInstruction.part, out patch_class_220 instruction)
                || coveredInstruction.instructionIndex > instruction.LoopFromHere_CoveredInstruction.GetValue().instructionIndex)
                // add candidate for loop region end
                rightmostHeldInstructions[coveredInstruction.part] = heldInstruction;
        }

        foreach ((Part part, patch_class_220 instruction) in rightmostHeldInstructions) {
            if (!part.TryGetComponent(LoopRegionComponent.Id, out IPartComponent component))
                continue;

            int coveredIndex = instruction.LoopFromHere_CoveredInstruction.GetValue().instructionIndex;
            InstructionsWRef partProgram = part.program.GetReferenceForPart(part, solEditScrn.GetSolution().GetTrackUnderPart(part));
            ProgramHelper.ProgramInfo programInfo = new(partProgram);
            if (!programInfo.IsEmpty && programInfo.EndIndex >= coveredIndex)
                continue;

            // add loop region end
            LoopRegionComponent loopRegionComponent = (LoopRegionComponent) component;
            loopRegionComponent.LoopEndAlpha = LoopRegionComponent.GhostAlpha;
            instruction.LoopFromHere_HasLoopRegionEnd = true;
        }

        return newLoopRegionStartIndices;
    }

    private void LoopFromHere_RepositionLoopRegions(Dictionary<LoopRegionComponent, int> newLoopRegionStartIndices, Dictionary<InstructionReference, InstructionType> insertedInstructions, bool inserting) {
        if (!AffectingLoopRegions())
            return;

        foreach ((LoopRegionComponent component, int newLoopRegionStartIndex) in newLoopRegionStartIndices)
            component.StartIndex = newLoopRegionStartIndex;

        if (inserting) {
            foreach ((InstructionReference position, _) in insertedInstructions) {
                if (!position.part.TryGetComponent(LoopRegionComponent.Id, out IPartComponent component))
                    continue;

                LoopRegionComponent loopRegionComponent = (LoopRegionComponent) component;
                if (position.instructionIndex > loopRegionComponent.StartIndex)
                    continue;

                // hmm
                EditableProgram program = position.part.program;
                bool shouldPushLoopRegion = false;
                int endOffset = program.GetInstruction(loopRegionComponent.StartIndex).HasValue() ? 1 : 0;
                for (int i = position.instructionIndex; i < loopRegionComponent.StartIndex + endOffset; i++) {
                    if (program.GetInstruction(i).HasValue())
                        shouldPushLoopRegion = true;
                    else {
                        shouldPushLoopRegion = false;
                        break;
                    }
                }

                if (shouldPushLoopRegion)
                    loopRegionComponent.StartIndex++;
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

    private void LoopFromHere_DrawLoopRegionMarkers(patch_class_220 heldInstruction, Vector2 heldInstructionPos) {
        Vector2 instructionOrigin = heldInstructionPos + new Vector2(0f, 2f);

        if (heldInstruction.LoopFromHere_HasLoopRegionStart) {
            Vector2 markerPos = instructionOrigin + new Vector2(0f, SolutionEditorProgramPanel.instructionSize.Y / 2f);
            TextureRenderer.Render(LoopRegionTextures.LoopRegionLeft, markerPos - LoopRegionTextures.LoopRegionLeft.GetHalfSize());
        }
        if (heldInstruction.LoopFromHere_HasLoopRegionEnd) {
            Vector2 markerPos = instructionOrigin + new Vector2(SolutionEditorProgramPanel.instructionSize.X, SolutionEditorProgramPanel.instructionSize.Y / 2f);
            TextureRenderer.Render(LoopRegionTextures.LoopRegionRight, markerPos - LoopRegionTextures.LoopRegionRight.GetHalfSize());
        }
    }

    internal static bool AffectingLoopRegions()
        => LoopRegionInputMode.EditingLoopRegions() ^ LoopFromHereSettings.Instance.InstructionEditsAffectLoopRegions;
}
