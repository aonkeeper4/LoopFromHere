using System;
using System.Collections.Generic;
using System.Linq;
using LoopFromHere.Components;
using LoopFromHere.Helpers;
using LoopFromHere.UI;
using Mono.Cecil;
using MonoMod;
using MonoMod.Cil;
using MonoMod.InlineRT;
using MonoMod.Utils;
using Quintessential.Components;

public class patch_SolutionEditorProgramPanel : SolutionEditorProgramPanel {
    [MonoModIgnore]
    public patch_SolutionEditorProgramPanel(SolutionEditorScreen solEditScrn) : base(solEditScrn) { }

    [MonoModIgnore]
    private Vector2 programOffset;

    [MonoModIgnore]
    private static readonly Vector2 programAreaOffset;
    [MonoModIgnore]
    private static readonly int programAreaHeight;

    [MonoModIgnore]
    private extern Vector2 GetScreenPosition(Index2 instructionPos);

    [MonoModIgnore]
    private extern bool orig_IsLimitReached(InstructionType instructionType);
    private bool IsLimitReached(InstructionType instructionType) {
        if (solEditScrn.GetSolution().parts.All(p => !p.HasComponent(LoopRegionComponent.Id)))
            return orig_IsLimitReached(instructionType);

        // remove period override limit
        return !instructionType.isPeriodOverride && orig_IsLimitReached(instructionType);
    }

    [MonoModILInject("RenderFrame")]
    public static void Patch_RenderFrame(MethodDefinition method, CustomAttribute attribute) {
        MonoModRule.Modder.Log("[LoopFromHere] Patching `SolutionEditorProgramPanel.RenderFrame` to add UI for loop regions...");

        TypeDefinition t_SolutionEditorProgramPanel = method.DeclaringType;
        MethodDefinition m_SolutionEditorProgramPanel_RenderLoopRegionMarkers = t_SolutionEditorProgramPanel.FindMethod("LoopFromHere_RenderLoopRegionMarkers")!;
        MethodDefinition m_SolutionEditorProgramPanel_UseCustomPeriodRendering = t_SolutionEditorProgramPanel.FindMethod("LoopFromHere_UseCustomPeriodRendering")!;
        MethodDefinition m_SolutionEditorProgramPanel_RenderPeriodHighlights = t_SolutionEditorProgramPanel.FindMethod("LoopFromHere_RenderPeriodHighlights")!;
        MethodDefinition m_SolutionEditorProgramPanel_RenderGhostInstructionIndicator = t_SolutionEditorProgramPanel.FindMethod("LoopFromHere_RenderGhostInstructionIndicator")!;
        MethodDefinition m_SolutionEditorProgramPanel_RenderLoopRegionPlacementIndicator = t_SolutionEditorProgramPanel.FindMethod("LoopFromHere_RenderLoopRegionPlacementIndicator")!;

        TypeDefinition t_Maybe = MonoModRule.Modder.FindType("Maybe`1").Resolve();
        TypeDefinition t_InstructionReference = MonoModRule.Modder.FindType("InstructionReference").Resolve();
        GenericInstanceType t_Maybe_InstructionReference = new(t_Maybe); t_Maybe_InstructionReference.GenericArguments.Add(t_InstructionReference);
        MethodReference m_Maybe_InstructionReference_GetValue = t_Maybe.FindMethod("GetValue")!.Clone()!; m_Maybe_InstructionReference_GetValue.DeclaringType = t_Maybe_InstructionReference;

        new ILContext(method).Invoke(il => {
            ILCursor cursor = new(il);
            ILLabel renderPeriodHighlights = cursor.DefineLabel(), afterRenderPeriodHighlights = cursor.DefineLabel();

            /*
             * IL_0992: call bool SolutionEditorProgramPanel::IsFastForwardHeld()
             * IL_0997: brfalse IL_1355
             * IL_099c: ldarg.0
             * IL_099d: callvirt instance valuetype Bounds2 SolutionEditorProgramPanel::ProgramAreaBounds()
             * IL_09a2: stloc.s 25
             */
            if (!cursor.TryGotoNext(MoveType.AfterLabel,
                    instr => instr.MatchCall("SolutionEditorProgramPanel", "IsFastForwardHeld"),
                    instr => instr.MatchBrfalse(out _),
                    instr => instr.MatchLdarg0(),
                    instr => instr.MatchCallvirt("SolutionEditorProgramPanel", "ProgramAreaBounds"),
                    instr => instr.MatchStloc(25)))
                throw new Exception("Failed to patch `SolutionEditorProgramPanel.RenderFrame`: Unable to find instruction highlight rendering to patch.");

            cursor.EmitLdarg0();
            cursor.EmitLdloc0();
            cursor.EmitLdloc(5);
            cursor.EmitLdloc(6);
            cursor.EmitCallvirt(m_SolutionEditorProgramPanel_RenderLoopRegionMarkers);

            /*
             * IL_0c8a: ldloc.s 5
             * IL_0c8c: stloc.s 73
             * IL_0c8e: br IL_0dc9
             */
            if (!cursor.TryGotoNext(MoveType.AfterLabel,
                    instr => instr.MatchLdloc(5),
                    instr => instr.MatchStloc(73),
                    instr => instr.MatchBr(out _)))
                throw new Exception("Failed to patch `SolutionEditorProgramPanel.RenderFrame`: Unable to find period highlight rendering to patch.");

            cursor.EmitLdloc0();
            cursor.EmitCall(m_SolutionEditorProgramPanel_UseCustomPeriodRendering);
            cursor.EmitBrfalse(renderPeriodHighlights);

            cursor.EmitLdarg0();
            cursor.EmitLdloc0();
            cursor.EmitLdloc(5);
            cursor.EmitLdloc(6);
            cursor.EmitLdloc(65);
            cursor.EmitCallvirt(m_SolutionEditorProgramPanel_RenderPeriodHighlights);
            cursor.EmitBr(afterRenderPeriodHighlights);

            cursor.MarkLabel(renderPeriodHighlights);

            /*
             * IL_0dbd: ldarg.0
             * IL_0dbe: call valuetype Vector2 InputManager::MousePos()
             * IL_0dc3: callvirt instance valuetype Maybe`1<valuetype InstructionReference> SolutionEditorProgramPanel::GetInstructionAt(valuetype Vector2)
             * IL_0dc8: stloc.s 66
             */
            if (!cursor.TryGotoNext(MoveType.AfterLabel,
                    instr => instr.MatchLdarg0(),
                    instr => instr.MatchCall("InputManager", "MousePos"),
                    instr => instr.MatchCallvirt("SolutionEditorProgramPanel", "GetInstructionAt"),
                    instr => instr.MatchStloc(66)))
                throw new Exception("Failed to patch `SolutionEditorProgramPanel.RenderFrame`: Unable to find assignment to `instructionAt2` local to patch.");

            cursor.MarkLabel(afterRenderPeriodHighlights);

            /*
             * IL_0f47: ldc.r4 2
             * IL_0f4c: ldc.r4 0.0
             * IL_0f51: newobj instance void Vector2::.ctor(float32, float32)
             * IL_0f56: call valuetype Vector2 Vector2::op_Addition(valuetype Vector2, valuetype Vector2)
             * IL_0f5b: call void TextureRenderer::Render(class Texture, valuetype Color, valuetype Vector2)
             */
            if (!cursor.TryGotoNext(MoveType.After,
                    instr => instr.MatchLdcR4(2f),
                    instr => instr.MatchLdcR4(0f),
                    instr => instr.MatchNewobj("Vector2"),
                    instr => instr.MatchCall("Vector2", "op_Addition"),
                    instr => instr.MatchCall("TextureRenderer", "Render")))
                throw new Exception("Failed to patch `SolutionEditorProgramPanel.RenderFrame`: Unable to find call to `TextureRenderer.Render` to patch.");

            cursor.EmitLdarg0();
            cursor.EmitLdloca(66);
            cursor.EmitCall(m_Maybe_InstructionReference_GetValue);
            cursor.EmitLdloc(85);
            cursor.EmitCallvirt(m_SolutionEditorProgramPanel_RenderGhostInstructionIndicator);

            /*
             * IL_1348: ldloc.s 95
             * IL_134a: ldloc.s 6
             * IL_134c: blt IL_11aa
             */
            if (!cursor.TryGotoNext(MoveType.After,
                    instr => instr.MatchLdloc(95),
                    instr => instr.MatchLdloc(6),
                    instr => instr.MatchBlt(out _)))
                throw new Exception("Failed to patch `SolutionEditorProgramPanel.RenderFrame`: Unable to find end of instruction rendering to patch.");

            cursor.EmitLdarg0();
            cursor.EmitLdloc0();
            cursor.EmitLdloc(5);
            cursor.EmitLdloc(6);
            cursor.EmitCallvirt(m_SolutionEditorProgramPanel_RenderLoopRegionMarkers);

            cursor.EmitLdarg0();
            cursor.EmitLdloc(65);
            cursor.EmitCallvirt(m_SolutionEditorProgramPanel_RenderLoopRegionPlacementIndicator);
        });
    }

    private void LoopFromHere_RenderLoopRegionMarkers(PanelWParts panelWParts, int partsStartIndex, int partsEndIndex) {
        for (int i = partsStartIndex; i < partsEndIndex; i++) {
            Part part = panelWParts.parts[i];
            if (!part.TryGetComponent(LoopRegionComponent.Id, out IPartComponent component))
                continue;

            LoopRegionComponent loopRegionComponent = (LoopRegionComponent) component;
            if (loopRegionComponent.StartIndex >= panelWParts.lowerBound) {
                Vector2 loopRegionStartPos = LoopFromHere_GetInstructionEdgePosition(new Index2(loopRegionComponent.StartIndex, i), BoundsPosition.LeftEdge);
                TextureRenderer.Render(LoopRegionTextures.LoopRegionLeft, Color.White.WithAlpha(loopRegionComponent.LoopStartAlpha), loopRegionStartPos - LoopRegionTextures.LoopRegionLeft.GetHalfSize());
            }
            if (loopRegionComponent.EndIndex <= panelWParts.upperBound) {
                Vector2 loopRegionEndPos = LoopFromHere_GetInstructionEdgePosition(new Index2(loopRegionComponent.EndIndex, i), BoundsPosition.RightEdge);
                TextureRenderer.Render(LoopRegionTextures.LoopRegionRight, Color.White.WithAlpha(loopRegionComponent.LoopEndAlpha), loopRegionEndPos - LoopRegionTextures.LoopRegionRight.GetHalfSize());
            }
        }
    }
    private Vector2 LoopFromHere_GetInstructionEdgePosition(Index2 index, BoundsPosition boundsPosition) {
        GetInstructionBounds(index, out _, out Bounds2 relativeBounds);
        return relativeBounds.GetEdgePosition(boundsPosition);
    }

    private static bool LoopFromHere_UseCustomPeriodRendering(PanelWParts panel)
        => panel.parts.Any(p => p.HasComponent(LoopRegionComponent.Id));
    private void LoopFromHere_RenderPeriodHighlights(PanelWParts panelWParts, int partsStartIndex, int partsEndIndex, Dictionary<Part, InstructionsWRef> partInstructions) {
        int defaultPeriod = 1;
        Dictionary<Part, (ProgramHelper.ProgramInfo, Maybe<LoopRegionComponent>)> programInfo = new();
        foreach ((Part part, InstructionsWRef instructions) in partInstructions) {
            ProgramHelper.ProgramInfo info = new(instructions);

            Maybe<LoopRegionComponent> loopRegionComponent = MaybeHelper.empty;
            if (part.TryGetComponent(LoopRegionComponent.Id, out IPartComponent component))
                loopRegionComponent = (LoopRegionComponent) component;
            programInfo.Add(part, (info, loopRegionComponent));

            if (info.HasHalt || loopRegionComponent.HasValue())
                continue;

            int period = info.EndIndex - info.StartIndex + 1;
            defaultPeriod = Math.Max(defaultPeriod, period);
        }

        for (int i = partsStartIndex; i < partsEndIndex; i++)
        {
            Part part = panelWParts.parts[i];
            (ProgramHelper.ProgramInfo info, Maybe<LoopRegionComponent> loopRegionComponent) = programInfo[part];

            if (loopRegionComponent.GetOrDefault(out LoopRegionComponent component)) {
                LoopFromHere_RenderHighlight(i, component.StartIndex, component.EndIndex, Color.FromHex(0xf2c76d));
                if (component.StartIndex > info.StartIndex)
                    LoopFromHere_RenderHighlight(i, info.StartIndex, component.StartIndex - 1, Color.White);
            } else if (!info.IsEmpty) {
                if (info.HasHalt)
                    LoopFromHere_RenderHighlight(i, info.StartIndex, info.EndIndex, Color.White);
                else
                    LoopFromHere_RenderHighlight(i, info.StartIndex, info.StartIndex + defaultPeriod - 1, Color.White);
            }
        }
    }
    private void LoopFromHere_RenderHighlight(int partIndex, int startIndex, int endIndex, Color color) {
        GetInstructionBounds(new Index2(startIndex, partIndex), out _, out Bounds2 highlightBounds);
        highlightBounds.Max.X = highlightBounds.Min.X + instructionSize.X * (endIndex - startIndex + 1f);
        highlightBounds.Min.Y += 1f;
        highlightBounds.Max.Y -= 2f;

        TextureRenderer.Render9Slice(Assets.textures.solution_editor.program_panel.program_length_glow, color.WithAlpha(0.6f), highlightBounds);
    }

    private void LoopFromHere_RenderGhostInstructionIndicator(InstructionReference hoveredInstruction, Bounds2 ghostInstructionBounds) {
        if (!hoveredInstruction.part.TryGetComponent(LoopRegionComponent.Id, out IPartComponent component))
            return;

        LoopRegionComponent loopRegionComponent = (LoopRegionComponent) component;
        if (hoveredInstruction.instructionIndex <= loopRegionComponent.EndIndex)
            return;

        loopRegionComponent.LoopEndAlpha = LoopRegionComponent.GhostAlpha;
        Vector2 loopRegionEndPos = ghostInstructionBounds.GetEdgePosition(BoundsPosition.RightEdge);
        TextureRenderer.Render(LoopRegionTextures.LoopRegionRight, Color.White.WithAlpha(LoopRegionComponent.GhostAlpha), loopRegionEndPos - LoopRegionTextures.LoopRegionRight.GetHalfSize());
    }

    private void LoopFromHere_RenderLoopRegionPlacementIndicator(Dictionary<Part, InstructionsWRef> partInstructions) {
        if (solEditScrn.currentInputMode is not LoopRegionInputMode
            || !GetInstructionAt(InputManager.MousePos()).GetOrDefault(out InstructionReference hoveredInstruction))
            return;

        InstructionsWRef hoveredInstructions = partInstructions[hoveredInstruction.part];
        if (!LoopRegionInputMode.CanPlaceLoopRegion(hoveredInstructions, hoveredInstruction.instructionIndex))
            return;

        Vector2 loopRegionPos = LoopFromHere_GetInstructionEdgePosition(new Index2(hoveredInstruction.instructionIndex, hoveredInstruction.part.programIndex), BoundsPosition.LeftEdge);
        TextureRenderer.Render(LoopRegionTextures.LoopRegionLeft, Color.White.WithAlpha(LoopRegionComponent.GhostAlpha), loopRegionPos - LoopRegionTextures.LoopRegionLeft.GetHalfSize());
    }
}
