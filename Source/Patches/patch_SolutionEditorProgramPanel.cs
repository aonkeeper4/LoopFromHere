using System;
using System.Collections.Generic;
using System.Linq;
using LoopFromHere.Components;
using LoopFromHere.UI;
using Mono.Cecil;
using MonoMod;
using MonoMod.Cil;
using MonoMod.InlineRT;
using Quintessential.Components;

public class patch_SolutionEditorProgramPanel : SolutionEditorProgramPanel {
    [MonoModIgnore]
    public patch_SolutionEditorProgramPanel(SolutionEditorScreen solEditScrn) : base(solEditScrn) { }

    #region Exposed members

    [MonoModIgnore]
    private Vector2 programOffset;

    [MonoModIgnore]
    private static readonly Vector2 programAreaOffset;
    [MonoModIgnore]
    private static readonly int programAreaHeight;

    [MonoModIgnore]
    private extern Vector2 GetScreenPosition(Index2 instructionPos);

    #endregion

    #region Patches

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
        MonoModRule.Modder.Log("[LoopFromHere] Patching `SolutionEditorProgramPanel.RenderFrame` to add UI for custom looping behaviour...");

        MethodDefinition m_SolutionEditorProgramPanel_UseCustomPeriodRendering = method.DeclaringType.Methods.First(m => m.Name == "LoopFromHere_UseCustomPeriodRendering");
        MethodDefinition m_SolutionEditorProgramPanel_RenderPeriodHighlights = method.DeclaringType.Methods.First(m => m.Name == "LoopFromHere_RenderPeriodHighlights");
        MethodDefinition m_SolutionEditorProgramPanel_RenderLoopRegionMarkers = method.DeclaringType.Methods.First(m => m.Name == "LoopFromHere_RenderLoopRegionMarkers");

        // todo:
        // reduce patch area? do we need to completely replace the vanilla period rendering?
        // if a ghost instruction is about to be inserted past the end of the current tape, display a ghost loop region end marker
        new ILContext(method).Invoke(il => {
            ILCursor cursor = new(il);
            ILLabel normalBehaviour = cursor.DefineLabel(), afterNormalBehaviour = cursor.DefineLabel();

            // todo: this match is flimsy
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
            cursor.EmitBrfalse(normalBehaviour);

            cursor.EmitLdarg0();
            cursor.EmitLdarg1();
            cursor.EmitLdloc0();
            cursor.EmitLdloc(5);
            cursor.EmitLdloc(6);
            cursor.EmitCallvirt(m_SolutionEditorProgramPanel_RenderPeriodHighlights);
            cursor.EmitBr(afterNormalBehaviour);

            cursor.MarkLabel(normalBehaviour);

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

            cursor.MarkLabel(afterNormalBehaviour);

            // todo: this match is flimsy
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
            cursor.EmitLdarg1();
            cursor.EmitLdloc0();
            cursor.EmitLdloc(5);
            cursor.EmitLdloc(6);
            cursor.EmitLdloc(65);
            cursor.EmitCallvirt(m_SolutionEditorProgramPanel_RenderLoopRegionMarkers);
        });
    }

    private static bool LoopFromHere_UseCustomPeriodRendering(PanelWParts panel)
        => panel.parts.Any(p => p.HasComponent(LoopRegionComponent.Id));
    private void LoopFromHere_RenderPeriodHighlights(float deltaTime, PanelWParts panelWParts, int partsStartIndex, int partsEndIndex) {
        // todo
    }

    private void LoopFromHere_RenderLoopRegionMarkers(float deltaTime, PanelWParts panelWParts, int partsStartIndex, int partsEndIndex, Dictionary<Part, InstructionsWRef> partInstructions) {
        // render loop region markers
        // todo: remember loop region positions so we can keep rendering them while e.g. instructions are being moved
        for (int i = partsStartIndex; i < partsEndIndex; i++) {
            Part part = panelWParts.parts[i];
            InstructionsWRef instructions = partInstructions[part];
            if (!part.TryGetComponent(LoopRegionComponent.Id, out IPartComponent component)
                || instructions.program.Count <= 0)
                continue;

            int loopRegionStartIndex = ((LoopRegionComponent) component).LoopStartCycle;
            int loopRegionEndIndex = instructions.program.Keys.Max();
            loopRegionEndIndex += instructions.program[loopRegionEndIndex].expansion.Length - 1;
            if (loopRegionStartIndex >= panelWParts.lowerBound) {
                Vector2 loopRegionStartPos = LoopFromHere_GetIndexEdgeCenter(new Index2(loopRegionStartIndex, i), rightEdge: false);
                TextureRenderer.Render(LoopRegionTextures.LoopRegionLeft, loopRegionStartPos - LoopRegionTextures.LoopRegionLeft.GetHalfSize());
            }
            if (loopRegionEndIndex <= panelWParts.upperBound) {
                Vector2 loopRegionEndPos = LoopFromHere_GetIndexEdgeCenter(new Index2(loopRegionEndIndex, i), rightEdge: true);
                TextureRenderer.Render(LoopRegionTextures.LoopRegionRight, loopRegionEndPos - LoopRegionTextures.LoopRegionRight.GetHalfSize());
            }
        }

        // render loop region placement indicator
        if (solEditScrn.currentInputMode is LoopRegionInputMode
            && LoopFromHere_GetIndexAt(InputManager.MousePos()).GetOrDefault(out Index2 hoveredIndex)) {
            Part hoveredPart = panelWParts.parts[hoveredIndex.Y];
            InstructionsWRef hoveredInstructions = partInstructions[hoveredPart];
            if (hoveredInstructions.program.Count <= 0)
                return;

            int hoveredProgramEndIndex = hoveredInstructions.program.Keys.Max();
            hoveredProgramEndIndex += hoveredInstructions.program[hoveredProgramEndIndex].expansion.Length - 1;
            if (hoveredInstructions.program.Values.Any(i => i.instruction.category == InstructionCategory.Halt)
                || hoveredIndex.X > hoveredProgramEndIndex)
                return;

            Vector2 loopRegionPos = LoopFromHere_GetIndexEdgeCenter(hoveredIndex, rightEdge: false);
            TextureRenderer.Render(LoopRegionTextures.LoopRegionLeft, Color.White.WithAlpha(0.5f), loopRegionPos - LoopRegionTextures.LoopRegionLeft.GetHalfSize());
        }
    }

    #endregion

    #region Utils

    internal Maybe<Index2> LoopFromHere_GetIndexAt(Vector2 screenPos) {
        List<Part> parts = solEditScrn.GetSolution().GetOrderedProgrammables();
        Vector2 vector = screenPos - programAreaOffset - programOffset;
        if (vector.X <= 0f || vector.Y >= programAreaHeight)
            return MaybeHelper.empty;

        Index2 index = new((int) MathF.Floor(vector.X / instructionSize.X + 0.5f), (int) MathF.Floor((programAreaHeight - vector.Y) / instructionSize.Y));
        if (index.X >= 0 && index.Y >= 0 && index.Y < parts.Count)
            return index;

        return MaybeHelper.empty;
    }

    internal Vector2 LoopFromHere_GetIndexEdgeCenter(Index2 index, bool rightEdge = false) {
        Vector2 relativePosition = GetScreenPosition(index) - programAreaOffset;
        return relativePosition + new Vector2(rightEdge ? instructionSize.X : 0f, instructionSize.Y / 2f);
    }

    #endregion
}
