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

// any instr edits can affect the end of a loop region
// only moving the start instr can move a loop region
//
public class patch_InstructionInsertInputMode : InstructionInsertInputMode {
    // can't inherit from this because it's sealed
    public class patch_class_220 {
        [MonoModIgnore]
        public InstructionType type;

        [MonoModIgnore]
        public Vector2 field_1913;

        [MonoModIgnore]
        public Maybe<InstructionReference> field_1914;

        internal bool LoopFromHere_HasLoopRegionStart, LoopFromHere_HasLoopRegionEnd;
    }
    [MonoModIgnore]
    private List<patch_class_220> field_1908;

    private class LoopFromHere_HeldLoopRegion(LoopRegionComponent component) {
        public readonly LoopRegionComponent Component = component;

        public Maybe<InstructionReference> NewStartPosition = MaybeHelper.empty;
    }
    private Dictionary<Part, LoopFromHere_HeldLoopRegion> LoopFromHere_heldLoopRegions;

    [MonoModILInject("IInputMode InstructionInsertInputMode::StartInputMode(SolutionEditorScreen,Vector2)")]
    public static void Patch_StartInputMode(MethodDefinition method, CustomAttribute attribute) {
        MonoModRule.Modder.Log("[LoopFromHere] Patching `InstructionInsertInputMode.StartInputMode(SolutionEditorScreen solEditScrn, Vector2 startMousePos)` to update loop regions when editing instructions...");

        MethodDefinition m_InstructionInsertInputMode_RemovePeriodOverrideLimit = method.DeclaringType.FindMethod("LoopFromHere_RemovePeriodOverrideLimit")!;
        MethodDefinition m_InstructionInsertInputMode_UpdateHeldLoopRegions = method.DeclaringType.FindMethod("LoopFromHere_UpdateHeldLoopRegions")!;

        new ILContext(method).Invoke(il => {
            ILCursor cursor = new(il);

            /*
             * IL_0073: dup
             * IL_0074: ldarg.0
             * IL_0075: ldfld class SolutionEditorProgramPanel SolutionEditorScreen::programPanel
             * IL_007a: ldloc.2
             * IL_007b: callvirt instance valuetype Vector2 SolutionEditorProgramPanel::GetScreenPosition(valuetype InstructionReference)
             * IL_0080: stfld valuetype Vector2 InstructionInsertInputMode/class_220::field_1913
             * IL_008d: callvirt instance void class [mscorlib]System.Collections.Generic.List`1<class InstructionInsertInputMode/class_220>::Add(!0)
             */
            if (!cursor.TryGotoNext(MoveType.After,
                    instr => instr.MatchDup(),
                    instr => instr.MatchLdarg0(),
                    instr => instr.MatchLdfld("SolutionEditorScreen", "programPanel"),
                    instr => instr.MatchLdloc2(),
                    instr => instr.MatchCallvirt("SolutionEditorProgramPanel", "GetScreenPosition"),
                    instr => instr.MatchStfld("InstructionInsertInputMode/class_220", "field_1913"),
                    instr => instr.MatchCallvirt(out MethodReference m_List_Add)
                        && m_List_Add.DeclaringType.Name == "List`1"
                        && m_List_Add.Name == "Add"))
                throw new Exception("Failed to patch `InstructionInsertInputMode.StartInputMode(SolutionEditorScreen solEditScrn, Vector2 startMousePos)`: Unable to find call to `List.Add` to patch.");

            cursor.EmitLdloc0();
            cursor.EmitLdloc2();
            cursor.EmitCall(m_InstructionInsertInputMode_UpdateHeldLoopRegions);

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

    private static void LoopFromHere_UpdateHeldLoopRegions(patch_InstructionInsertInputMode inputMode, InstructionReference selectedInstruction) {
        inputMode.LoopFromHere_heldLoopRegions ??= new Dictionary<Part, LoopFromHere_HeldLoopRegion>();

        if (!selectedInstruction.part.TryGetComponent(LoopRegionComponent.Id, out IPartComponent component))
            return;

        LoopRegionComponent loopRegionComponent = (LoopRegionComponent) component;
        if (selectedInstruction.instructionIndex == loopRegionComponent.StartIndex)
            inputMode.LoopFromHere_heldLoopRegions.Add(selectedInstruction.part, new LoopFromHere_HeldLoopRegion(loopRegionComponent));
    }

    // this conditional logic confuses me
    private static bool LoopFromHere_RemovePeriodOverrideLimit(bool orig, SolutionEditorScreen solEditScrn)
        => orig || solEditScrn.GetSolution().parts.Any(p => p.HasComponent(LoopRegionComponent.Id));

    [MonoModILInject("Process")]
    public static void Patch_Process(MethodDefinition method, CustomAttribute attribute) {
        MonoModRule.Modder.Log("[LoopFromHere] Patching `InstructionInsertInputMode.Process` to update loop regions when editing instructions...");

        MethodDefinition m_InstructionInsertInputMode_UpdateHeldInstructions = method.DeclaringType.FindMethod("LoopFromHere_UpdateHeldInstructions")!;
        MethodDefinition m_InstructionInsertInputMode_UpdateLoopRegions = method.DeclaringType.FindMethod("LoopFromHere_UpdateLoopRegions")!;
        MethodDefinition m_InstructionInsertInputMode_RecalculateLoopRegions = method.DeclaringType.FindMethod("LoopFromHere_RecalculateLoopRegions")!;

        new ILContext(method).Invoke(il => {
            ILCursor cursor = new(il);

            /*
             * IL_014d: ldc.i4.1
             * IL_014e: call bool InputManager::IsClickReleased(valuetype MouseButtonType)
             * IL_0153: brfalse IL_02c0
             */
            if (!cursor.TryGotoNext(MoveType.AfterLabel,
                    instr => instr.MatchLdcI4(1),
                    instr => instr.MatchCall("InputManager", "IsClickReleased"),
                    instr => instr.MatchBrfalse(out _)))
                throw new Exception("Failed to patch `InstructionInsertInputMode.Process`: Unable to find call to `InputManager.IsClickReleased` to patch.");

            cursor.EmitLdarg0();
            cursor.EmitLdarg1();
            cursor.EmitLdloc2();
            cursor.EmitCallvirt(m_InstructionInsertInputMode_UpdateHeldInstructions);

            /*
             * IL_015b: ldarg.1
             * IL_015c: ldfld class InstructionSelection SolutionEditorScreen::instructionSelection
             * IL_0161: callvirt instance void InstructionSelection::Clear()
             */
            if (!cursor.TryGotoNext(MoveType.After,
                    instr => instr.MatchLdarg1(),
                    instr => instr.MatchLdfld("SolutionEditorScreen", "instructionSelection"),
                    instr => instr.MatchCallvirt("InstructionSelection", "Clear")))
                throw new Exception("Failed to patch `InstructionInsertInputMode.Process`: Unable to find call to `InstructionSelection.Clear` to patch.");

            cursor.EmitLdarg0();
            cursor.EmitLdarg1();
            cursor.EmitLdloc0();
            cursor.EmitLdloc1();
            cursor.EmitLdloc3();
            cursor.EmitCallvirt(m_InstructionInsertInputMode_UpdateLoopRegions);

            /*
             * IL_0287: ldloc.s 10
             * IL_0289: brfalse.s IL_02a1
             */
            if (!cursor.TryGotoNext(MoveType.AfterLabel,
                    instr => instr.MatchLdloc(10),
                    instr => instr.MatchBrfalse(out _)))
                throw new Exception("Failed to patch `InstructionInsertInputMode.Process`: Unable to find check for local `flag2` to patch.");

            cursor.EmitLdarg0();
            cursor.EmitLdarg1();
            cursor.EmitLdloc0();
            cursor.EmitCallvirt(m_InstructionInsertInputMode_RecalculateLoopRegions);
        });
    }

    // everyone love big incomprehensible update function
    private void LoopFromHere_UpdateHeldInstructions(SolutionEditorScreen solEditScrn, Vector2 mouseDelta) {
        Dictionary<patch_class_220, Maybe<InstructionReference>> coveredInstructions = new();
        Dictionary<Part, patch_class_220> rightmostHeldInstructions = new();
        foreach (patch_class_220 heldInstruction in field_1908) {
            heldInstruction.LoopFromHere_HasLoopRegionStart = heldInstruction.LoopFromHere_HasLoopRegionEnd = false;

            Vector2 coveredPosition = heldInstruction.field_1913 + mouseDelta + SolutionEditorProgramPanel.instructionSize / 2;
            Maybe<InstructionReference> coveredInstruction = solEditScrn.programPanel.GetInstructionAt(coveredPosition);
            coveredInstructions.Add(heldInstruction, coveredInstruction);

            if (coveredInstruction.GetOrDefault(out InstructionReference covered)) {
                // if we are about to delete a loop region by placing a halt instruction, indicate it
                if (heldInstruction.type.category == InstructionCategory.Halt
                    && covered.part.TryGetComponent(LoopRegionComponent.Id, out IPartComponent coveredComponent)) {
                    LoopRegionComponent coveredLoopRegionComponent = (LoopRegionComponent) coveredComponent;
                    coveredLoopRegionComponent.StartAlpha = coveredLoopRegionComponent.EndAlpha = LoopRegionComponent.GhostAlpha;
                    continue;
                }

                // update rightmost instruction for part
                if (!rightmostHeldInstructions.TryGetValue(covered.part, out patch_class_220 instruction)
                    || covered.instructionIndex > coveredInstructions[instruction].GetValue().instructionIndex)
                    rightmostHeldInstructions[covered.part] = heldInstruction;
            }

            if (LoopFromHere_AffectingLoopRegions()
                && heldInstruction.field_1914.GetOrDefault(out InstructionReference originalPosition)
                && LoopFromHere_heldLoopRegions.TryGetValue(originalPosition.part, out LoopFromHere_HeldLoopRegion heldLoopRegion)
                && heldLoopRegion.Component.StartIndex == originalPosition.instructionIndex) {
                // move the start of the loop region
                heldInstruction.LoopFromHere_HasLoopRegionStart = true;

                // if we are moving the region to another part, indicate it
                heldLoopRegion.Component.StartAlpha = LoopRegionComponent.GhostAlpha;
                if (coveredInstruction.GetOrDefault(out InstructionReference covered2)
                    && covered2.part != heldInstruction.field_1914.GetValue().part) {
                    heldLoopRegion.Component.EndAlpha = LoopRegionComponent.GhostAlpha;

                    // if we are moving the region to a part that already has a loop region, indicate that it will be overwritten
                    if (covered2.part.TryGetComponent(LoopRegionComponent.Id, out IPartComponent coveredComponent)) {
                        LoopRegionComponent coveredLoopRegionComponent = (LoopRegionComponent) coveredComponent;
                        coveredLoopRegionComponent.StartAlpha = LoopRegionComponent.GhostAlpha;
                    }
                } else
                    // if we are deleting the region, indicate it
                    heldLoopRegion.Component.EndAlpha = LoopRegionComponent.GhostAlpha;

                heldLoopRegion.NewStartPosition = coveredInstruction;
            }
        }

        foreach ((Part part, patch_class_220 instruction) in rightmostHeldInstructions) {
            // find loop region component from either held or unheld loop regions
            Maybe<LoopRegionComponent> maybeLoopRegionComponent = MaybeHelper.empty;
            if (part.TryGetComponent(LoopRegionComponent.Id, out IPartComponent component))
                maybeLoopRegionComponent = (LoopRegionComponent) component;
            if (LoopFromHere_heldLoopRegions.TryGetValue(part, out LoopFromHere_HeldLoopRegion heldLoopRegion))
                maybeLoopRegionComponent = heldLoopRegion.Component;
            if (!maybeLoopRegionComponent.GetOrDefault(out LoopRegionComponent loopRegionComponent))
                continue;

            // if the part's rightmost held instruction can't affect the end of its loop region, skip it
            int coveredIndex = coveredInstructions[instruction].GetValue().instructionIndex;
            InstructionsWRef partProgram = part.program.GetReferenceForPart(part, solEditScrn.GetSolution().GetTrackUnderPart(part));
            ProgramHelper.ProgramInfo programInfo = new(partProgram);
            if (!programInfo.IsEmpty && programInfo.EndIndex >= coveredIndex)
                continue;

            // move the end of the loop region
            loopRegionComponent.EndAlpha = LoopRegionComponent.GhostAlpha;
            instruction.LoopFromHere_HasLoopRegionEnd = true;
        }
    }

    // update loop regions before instructions are modified
    private void LoopFromHere_UpdateLoopRegions(SolutionEditorScreen solEditScrn, enum_140 insertOperation, bool inserting, Dictionary<InstructionReference, InstructionType> insertedInstructions) {
        if (!LoopFromHere_AffectingLoopRegions() || insertOperation == (enum_140) 0)
            return;

        // remove and replace loop regions at their new positions
        HashSet<Part> loopRegionDestinationParts = [];
        foreach (LoopFromHere_HeldLoopRegion heldLoopRegion in LoopFromHere_heldLoopRegions.Values) {
            heldLoopRegion.Component.Part?.RemoveComponent(LoopRegionComponent.Id);
            if (!heldLoopRegion.NewStartPosition.GetOrDefault(out InstructionReference newStartPosition))
                continue;

            heldLoopRegion.Component.StartIndex = newStartPosition.instructionIndex;
            newStartPosition.part.RemoveComponent(LoopRegionComponent.Id);
            newStartPosition.part.AddComponent(heldLoopRegion.Component);

            loopRegionDestinationParts.Add(newStartPosition.part);
        }
        LoopFromHere_heldLoopRegions.Clear();

        if (inserting) {
            // push the starts of stationary loop regions if an instruction is about to be inserted before them
            foreach ((InstructionReference position, _) in insertedInstructions) {
                if (loopRegionDestinationParts.Contains(position.part)
                    || !position.part.TryGetComponent(LoopRegionComponent.Id, out IPartComponent component))
                    continue;

                LoopRegionComponent loopRegionComponent = (LoopRegionComponent) component;
                if (position.instructionIndex > loopRegionComponent.StartIndex)
                    continue;

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
    // recalculate loop regions after instructions are modified
    private void LoopFromHere_RecalculateLoopRegions(SolutionEditorScreen solEditScrn, enum_140 insertOperation) {
        if (insertOperation == (enum_140) 0)
            return;

        foreach (Part part in solEditScrn.GetSolution().parts) {
            if (!part.TryGetComponent(LoopRegionComponent.Id, out IPartComponent component))
                continue;

            LoopRegionComponent loopRegionComponent = (LoopRegionComponent) component;
            loopRegionComponent.Recalculate(solEditScrn);
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

    internal static bool LoopFromHere_AffectingLoopRegions()
        => LoopRegionInputMode.EditingLoopRegions() ^ LoopFromHereSettings.Instance.InstructionEditsAffectLoopRegionsByDefault;
}
