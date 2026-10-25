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

        TypeDefinition t_SolutionEditorProgramPanel = method.DeclaringType;
        MethodDefinition m_SolutionEditorProgramPanel_RenderLoopRegionMarkers = t_SolutionEditorProgramPanel.FindMethod("LoopFromHere_RenderLoopRegionMarkers")!;
        MethodDefinition m_SolutionEditorProgramPanel_UseCustomPeriodRendering = t_SolutionEditorProgramPanel.FindMethod("LoopFromHere_UseCustomPeriodRendering")!;
        MethodDefinition m_SolutionEditorProgramPanel_RenderPeriodHighlights = t_SolutionEditorProgramPanel.FindMethod("LoopFromHere_RenderPeriodHighlights")!;
        MethodDefinition m_SolutionEditorProgramPanel_RenderLoopRegionPlacementIndicator = t_SolutionEditorProgramPanel.FindMethod("LoopFromHere_RenderLoopRegionPlacementIndicator")!;
        MethodDefinition m_SolutionEditorProgramPanel_GetProgramEndIndicesFromProgramGrid = t_SolutionEditorProgramPanel.FindMethod("LoopFromHere_GetProgramEndIndicesFromProgramGrid")!;
        MethodDefinition m_SolutionEditorProgramPanel_GetProgramEndIndicesFromInstructions = t_SolutionEditorProgramPanel.FindMethod("LoopFromHere_GetProgramEndIndicesFromInstructions")!;

        TypeDefinition t_InstructionHighlighter = t_SolutionEditorProgramPanel.NestedTypes.First(t => t.Name == "InstructionHighlighter");
        FieldDefinition f_InstructionHighlighter_programGrid = t_InstructionHighlighter.FindField("programGrid")!;

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
            cursor.EmitLdloc(40);
            cursor.EmitLdfld(f_InstructionHighlighter_programGrid);
            cursor.EmitCall(m_SolutionEditorProgramPanel_GetProgramEndIndicesFromProgramGrid);
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
            cursor.EmitLdloc(65);
            cursor.EmitCall(m_SolutionEditorProgramPanel_GetProgramEndIndicesFromInstructions);
            cursor.EmitCallvirt(m_SolutionEditorProgramPanel_RenderLoopRegionMarkers);

            cursor.EmitLdarg0();
            cursor.EmitLdarg1();
            cursor.EmitLdloc0();
            cursor.EmitLdloc(65);
            cursor.EmitCallvirt(m_SolutionEditorProgramPanel_RenderLoopRegionPlacementIndicator);
        });
    }

    private void LoopFromHere_RenderLoopRegionMarkers(float deltaTime, PanelWParts panelWParts, int partsStartIndex, int partsEndIndex, Dictionary<Part, int> partProgramEndIndices) {
        for (int i = partsStartIndex; i < partsEndIndex; i++) {
            Part part = panelWParts.parts[i];
            if (!part.TryGetComponent(LoopRegionComponent.Id, out IPartComponent component)
                || !partProgramEndIndices.TryGetValue(part, out int loopRegionEndIndex))
                continue;

            int loopRegionStartIndex = ((LoopRegionComponent) component).LoopStartCycle;
            if (loopRegionStartIndex >= panelWParts.lowerBound) {
                Vector2 loopRegionStartPos = LoopFromHere_GetIndexEdgeCenter(new Index2(loopRegionStartIndex, i), rightEdge: false);
                TextureRenderer.Render(LoopRegionTextures.LoopRegionLeft, loopRegionStartPos - LoopRegionTextures.LoopRegionLeft.GetHalfSize());
            }
            if (loopRegionEndIndex <= panelWParts.upperBound) {
                Vector2 loopRegionEndPos = LoopFromHere_GetIndexEdgeCenter(new Index2(loopRegionEndIndex, i), rightEdge: true);
                TextureRenderer.Render(LoopRegionTextures.LoopRegionRight, loopRegionEndPos - LoopRegionTextures.LoopRegionRight.GetHalfSize());
            }
        }
    }

    private static bool LoopFromHere_UseCustomPeriodRendering(PanelWParts panel)
        => panel.parts.Any(p => p.HasComponent(LoopRegionComponent.Id));
    private void LoopFromHere_RenderPeriodHighlights(float deltaTime, PanelWParts panelWParts, int partsStartIndex, int partsEndIndex, Dictionary<Part, InstructionsWRef> partInstructions) {
        int defaultPeriod = 0;
        Dictionary<Part, (int, int, bool, Maybe<LoopRegionComponent>)> partInfo = new();
        foreach ((Part part, InstructionsWRef instructions) in partInstructions) {
            if (!LoopFromHere_TryGetProgramInfo(instructions, out int programStartIndex, out int programEndIndex, out bool hasHalt))
                continue;

            Maybe<LoopRegionComponent> loopRegionComponent = MaybeHelper.empty;
            if (part.TryGetComponent(LoopRegionComponent.Id, out IPartComponent component))
                loopRegionComponent = (LoopRegionComponent) component;
            partInfo.Add(part, (programStartIndex, programEndIndex, hasHalt, loopRegionComponent));
            if (hasHalt || loopRegionComponent.HasValue())
                continue;

            int period = programEndIndex - programStartIndex + 1;
            defaultPeriod = Math.Max(defaultPeriod, period);
        }

        for (int i = partsStartIndex; i < partsEndIndex; i++)
        {
            Part part = panelWParts.parts[i];
            if (!partInfo.TryGetValue(part, out (int programStartIndex, int programEndIndex, bool hasHalt, Maybe<LoopRegionComponent> loopRegionComponent) info))
                continue;

            if (info.loopRegionComponent.GetOrDefault(out LoopRegionComponent component)) {
                if (component.LoopStartCycle > info.programStartIndex)
                    LoopFromHere_RenderHighlight(i, info.programStartIndex, component.LoopStartCycle - 1, Color.White);
                LoopFromHere_RenderHighlight(i, component.LoopStartCycle, info.programEndIndex, Color.FromHex(0xf2c76d));
            } else if (info.hasHalt)
                LoopFromHere_RenderHighlight(i, info.programStartIndex, info.programEndIndex, Color.White);
            else
                LoopFromHere_RenderHighlight(i, info.programStartIndex, info.programStartIndex + defaultPeriod - 1, Color.White);
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
        if (!LoopFromHere_CanPlaceLoopRegion(hoveredIndex, hoveredInstructions))
            return;

        Vector2 loopRegionPos = LoopFromHere_GetIndexEdgeCenter(hoveredIndex, rightEdge: false);
        TextureRenderer.Render(LoopRegionTextures.LoopRegionLeft, Color.White.WithAlpha(0.5f), loopRegionPos - LoopRegionTextures.LoopRegionLeft.GetHalfSize());
    }

    #endregion

    #region Utils

    internal static bool LoopFromHere_TryGetProgramInfo(InstructionsWRef instructions, out int startIndex, out int endIndex, out bool hasHalt) {
        startIndex = endIndex = -1;
        hasHalt = false;
        if (instructions.program.Count <= 0)
            return false;

        startIndex = instructions.program.Keys.Min();
        endIndex = instructions.program.Keys.Max();
        endIndex += instructions.program[endIndex].expansion.Length - 1;
        hasHalt = instructions.program.Values.Any(i => i.instruction.category == InstructionCategory.Halt);
        return true;
    }

    internal static Dictionary<Part, int> LoopFromHere_GetProgramEndIndicesFromProgramGrid(CompiledProgramGrid programGrid) {
        Dictionary<Part, int> programEndIndices = new();
        foreach ((Part part, CompiledProgram program) in ((patch_CompiledProgramGrid) (object) programGrid).programDict) {
            if (program.instructions.Length <= 0)
                continue;

            programEndIndices[part] = program.startIndex + program.instructions.Length - 1;
        }

        return programEndIndices;
    }
    internal static Dictionary<Part, int> LoopFromHere_GetProgramEndIndicesFromInstructions(Dictionary<Part, InstructionsWRef> instructions) {
        Dictionary<Part, int> programEndIndices = new();
        foreach ((Part part, InstructionsWRef partInstructions) in instructions) {
            if (LoopFromHere_TryGetProgramInfo(partInstructions, out int _, out int programEndIndex, out bool _))
                programEndIndices[part] = programEndIndex;
        }

        return programEndIndices;
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

    internal static bool LoopFromHere_CanPlaceLoopRegion(Index2 index, InstructionsWRef instructions)
        => LoopFromHere_TryGetProgramInfo(instructions, out int _, out int programEndIndex, out bool hasHalt)
            && !hasHalt
            && index.X <= programEndIndex;

    #endregion
}
