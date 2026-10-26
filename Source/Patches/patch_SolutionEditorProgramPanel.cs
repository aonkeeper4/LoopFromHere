using System;
using System.Collections.Generic;
using System.Linq;
using LoopFromHere.Components;
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

        MethodDefinition m_SolutionEditorProgramPanel_RenderLoopRegionMarkers = method.DeclaringType.FindMethod("LoopFromHere_RenderLoopRegionMarkers")!;
        MethodDefinition m_SolutionEditorProgramPanel_UseCustomPeriodRendering = method.DeclaringType.FindMethod("LoopFromHere_UseCustomPeriodRendering")!;
        MethodDefinition m_SolutionEditorProgramPanel_RenderPeriodHighlights = method.DeclaringType.FindMethod("LoopFromHere_RenderPeriodHighlights")!;
        MethodDefinition m_SolutionEditorProgramPanel_RenderLoopRegionPlacementIndicator = method.DeclaringType.FindMethod("LoopFromHere_RenderLoopRegionPlacementIndicator")!;

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
            cursor.EmitLdarg1();
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
            cursor.EmitLdarg1();
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
            cursor.EmitCallvirt(m_SolutionEditorProgramPanel_RenderLoopRegionMarkers);

            cursor.EmitLdarg0();
            cursor.EmitLdarg1();
            cursor.EmitLdloc0();
            cursor.EmitLdloc(65);
            cursor.EmitCallvirt(m_SolutionEditorProgramPanel_RenderLoopRegionPlacementIndicator);
        });
    }

    private void LoopFromHere_RenderLoopRegionMarkers(float deltaTime, PanelWParts panelWParts, int partsStartIndex, int partsEndIndex) {
        for (int i = partsStartIndex; i < partsEndIndex; i++) {
            Part part = panelWParts.parts[i];
            if (!part.TryGetComponent(LoopRegionComponent.Id, out IPartComponent component))
                continue;

            LoopRegionComponent loopRegionComponent = (LoopRegionComponent) component;
            if (loopRegionComponent.LoopStartIndex >= panelWParts.lowerBound) {
                Vector2 loopRegionStartPos = LoopFromHere_GetIndexEdgeCenter(new Index2(loopRegionComponent.LoopStartIndex, i), rightEdge: false);
                TextureRenderer.Render(LoopRegionTextures.LoopRegionLeft, loopRegionComponent.StartRenderMode.ToColor(), loopRegionStartPos - LoopRegionTextures.LoopRegionLeft.GetHalfSize());
            }
            if (loopRegionComponent.LoopEndIndex <= panelWParts.upperBound) {
                Vector2 loopRegionEndPos = LoopFromHere_GetIndexEdgeCenter(new Index2(loopRegionComponent.LoopEndIndex, i), rightEdge: true);
                TextureRenderer.Render(LoopRegionTextures.LoopRegionRight, loopRegionComponent.EndRenderMode.ToColor(), loopRegionEndPos - LoopRegionTextures.LoopRegionRight.GetHalfSize());
            }
        }
    }

    private static bool LoopFromHere_UseCustomPeriodRendering(PanelWParts panel)
        => panel.parts.Any(p => p.HasComponent(LoopRegionComponent.Id));
    private void LoopFromHere_RenderPeriodHighlights(float deltaTime, PanelWParts panelWParts, int partsStartIndex, int partsEndIndex, Dictionary<Part, InstructionsWRef> partInstructions) {
        int defaultPeriod = 0;
        Dictionary<Part, (bool, int, int, bool, Maybe<LoopRegionComponent>)> programInfo = new();
        foreach ((Part part, InstructionsWRef instructions) in partInstructions) {
            (bool isEmpty, int programStartIndex, int programEndIndex, bool hasHalt) = LoopFromHere_GetProgramInfo(instructions);

            Maybe<LoopRegionComponent> loopRegionComponent = MaybeHelper.empty;
            if (part.TryGetComponent(LoopRegionComponent.Id, out IPartComponent component))
                loopRegionComponent = (LoopRegionComponent) component;
            programInfo.Add(part, (isEmpty, programStartIndex, programEndIndex, hasHalt, loopRegionComponent));

            if (!hasHalt && !loopRegionComponent.HasValue()) {
                int period = programEndIndex - programStartIndex + 1;
                defaultPeriod = Math.Max(defaultPeriod, period);
            }
        }

        for (int i = partsStartIndex; i < partsEndIndex; i++)
        {
            Part part = panelWParts.parts[i];
            (bool isEmpty, int programStartIndex, int programEndIndex, bool hasHalt, Maybe<LoopRegionComponent> loopRegionComponent) = programInfo[part];

            if (loopRegionComponent.GetOrDefault(out LoopRegionComponent component)) {
                LoopFromHere_RenderHighlight(i, component.LoopStartIndex, component.LoopEndIndex, Color.FromHex(0xf2c76d));
                if (!isEmpty && component.LoopStartIndex > programStartIndex)
                    LoopFromHere_RenderHighlight(i, programStartIndex, component.LoopStartIndex - 1, Color.White);
            } else if (!isEmpty) {
                if (hasHalt)
                    LoopFromHere_RenderHighlight(i, programStartIndex, programEndIndex, Color.White);
                else
                    LoopFromHere_RenderHighlight(i, programStartIndex, programStartIndex + defaultPeriod - 1, Color.White);
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

    private void LoopFromHere_RenderLoopRegionPlacementIndicator(float deltaTime, PanelWParts panelWParts, Dictionary<Part, InstructionsWRef> partInstructions) {
        if (solEditScrn.currentInputMode is not LoopRegionInputMode
            || !LoopFromHere_GetIndexAt(InputManager.MousePos()).GetOrDefault(out Index2 hoveredIndex))
            return;

        InstructionsWRef hoveredInstructions = partInstructions[panelWParts.parts[hoveredIndex.Y]];
        if (!LoopFromHere_CanPlaceLoopRegion(hoveredInstructions, hoveredIndex.X))
            return;

        Vector2 loopRegionPos = LoopFromHere_GetIndexEdgeCenter(hoveredIndex, rightEdge: false);
        TextureRenderer.Render(LoopRegionTextures.LoopRegionLeft, Color.White.WithAlpha(0.5f), loopRegionPos - LoopRegionTextures.LoopRegionLeft.GetHalfSize());
    }

    #endregion

    #region Utils

    internal static (bool, int, int, bool) LoopFromHere_GetProgramInfo(InstructionsWRef instructions) {
        if (instructions.program.Count <= 0)
            return (true, -1, -1, false);

        int startIndex = instructions.program.Keys.Min();
        int endIndex = instructions.program.Keys.Max();
        endIndex += instructions.program[endIndex].expansion.Length - 1;
        bool hasHalt = instructions.program.Values.Any(i => i.instruction.category == InstructionCategory.Halt);
        return (false, startIndex, endIndex, hasHalt);
    }

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

    internal static bool LoopFromHere_CanPlaceLoopRegion(InstructionsWRef instructions, int index) {
        (bool isEmpty, _, int programEndIndex, bool hasHalt) = LoopFromHere_GetProgramInfo(instructions);
        return !isEmpty && !hasHalt && index <= programEndIndex;
    }

    #endregion
}
