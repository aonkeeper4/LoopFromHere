using System;
using System.Linq;
using LoopFromHere.Components;
using Mono.Cecil;
using MonoMod;
using MonoMod.Cil;
using MonoMod.InlineRT;

public class patch_SolutionEditorProgramPanel : SolutionEditorProgramPanel {
    [MonoModIgnore]
    public patch_SolutionEditorProgramPanel(SolutionEditorScreen solEditScrn) : base(solEditScrn) { }

    [MonoModILInject("RenderFrame")]
    public static void Patch_RenderFrame(MethodDefinition method, CustomAttribute attribute) {
        MonoModRule.Modder.Log("[LoopFromHere] Patching `SolutionEditorProgramPanel.RenderFrame` to add UI for custom looping behaviour...");

        MethodDefinition m_SolutionEditorProgramPanel_UseCustomPeriodRendering = method.DeclaringType.Methods.First(m => m.Name == "LoopFromHere_UseCustomPeriodRendering");
        MethodDefinition m_SolutionEditorProgramPanel_RenderPeriodHighlights = method.DeclaringType.Methods.First(m => m.Name == "LoopFromHere_RenderPeriodHighlights");
        MethodDefinition m_SolutionEditorProgramPanel_RenderLoopRegionMarkers = method.DeclaringType.Methods.First(m => m.Name == "LoopFromHere_RenderLoopRegionMarkers");

        // todo: reduce patch area? do we need to completely replace the vanilla period rendering?
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

            cursor.EmitLdarg0();
            cursor.EmitCallvirt(m_SolutionEditorProgramPanel_UseCustomPeriodRendering);
            cursor.EmitBrfalse(normalBehaviour);

            cursor.EmitLdarg0();
            cursor.EmitLdarg1();
            cursor.EmitLdloc0();
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
             * IL_1174: ldloc.s 5
             * IL_1176: stloc.s 95
             * IL_1178: br IL_131b
             */
            if (!cursor.TryGotoNext(MoveType.AfterLabel,
                    instr => instr.MatchLdloc(5),
                    instr => instr.MatchStloc(95),
                    instr => instr.MatchBr(out _)))
                throw new Exception("Failed to patch `SolutionEditorProgramPanel.RenderFrame`: Unable to find instruction highlight rendering to patch.");

            cursor.EmitLdarg0();
            cursor.EmitLdarg1();
            cursor.EmitLdloc0();
            cursor.EmitCallvirt(m_SolutionEditorProgramPanel_RenderLoopRegionMarkers);
        });
    }

    private bool LoopFromHere_UseCustomPeriodRendering()
        => solEditScrn.GetSolution().parts.Any(p => p.HasComponent(LoopFromHereComponent.Id));
    private void LoopFromHere_RenderPeriodHighlights(float deltaTime, PanelWParts panelWParts) {
        // todo
    }

    private void LoopFromHere_RenderLoopRegionMarkers(float deltaTime, PanelWParts panelWParts) {
        // todo
    }
}
