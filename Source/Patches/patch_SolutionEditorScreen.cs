using System;
using LoopFromHere.Components;
using Mono.Cecil;
using MonoMod;
using MonoMod.Cil;
using MonoMod.InlineRT;
using MonoMod.Utils;
using Quintessential.Components;

public class patch_SolutionEditorScreen : SolutionEditorScreen {
    [MonoModIgnore]
    public patch_SolutionEditorScreen(Solution solution, MusicTrack musicTrack, Sound fanfare) : base(solution, musicTrack, fanfare) { }

    [MonoModIgnore]
    private bool needsNewSnapshot;

    [MonoModILInject("RenderFrame")]
    public static void Patch_RenderFrame(MethodDefinition method, CustomAttribute attribute) {
        MonoModRule.Modder.Log("[LoopFromHere] Patching `SolutionEditorScreen.RenderFrame` to recalculate loop regions if necessary...");

        MethodDefinition m_SolutionEditorScreen_RecalculateLoopRegions = method.DeclaringType.FindMethod("LoopFromHere_RecalculateLoopRegions")!;

        new ILContext(method).Invoke(il => {
            ILCursor cursor = new(il);

            /*
             * IL_0850: ldarg.0
             * IL_0851: ldfld bool SolutionEditorScreen::needsSaving
             * IL_0856: brfalse.s IL_086a
             */
            if (!cursor.TryGotoNext(MoveType.AfterLabel,
                    instr => instr.MatchLdarg0(),
                    instr => instr.MatchLdfld("SolutionEditorScreen", "needsSaving"),
                    instr => instr.MatchBrfalse(out _)))
                throw new Exception("Failed to patch `SolutionEditorScreen.RenderFrame`: Unable to find check for `SolutionEditorScreen.needsSaving` to patch.");

            cursor.EmitLdarg0();
            cursor.EmitCallvirt(m_SolutionEditorScreen_RecalculateLoopRegions);
        });
    }

    private void LoopFromHere_RecalculateLoopRegions() {
        if (!needsNewSnapshot)
            return;

        foreach (Part part in GetSolution().parts) {
            if (part.TryGetComponent(LoopRegionComponent.Id, out IPartComponent component))
                ((LoopRegionComponent) component).Recalculate(this);
        }
    }
}
