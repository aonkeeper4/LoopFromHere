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
        MonoModRule.Modder.Log("[LoopFromHere] Patching `SolutionEditorScreen.RenderFrame` to update loop regions...");

        MethodDefinition m_SolutionEditorScreen_UpdateLoopRegions = method.DeclaringType.FindMethod("LoopFromHere_UpdateLoopRegions")!;

        new ILContext(method).Invoke(il => {
            ILCursor cursor = new(il);

            /*
             * IL_07e7: ldarg.0
             * IL_07e8: ldfld class IInputMode SolutionEditorScreen::currentInputMode
             * IL_07ed: ldarg.0
             * IL_07ee: callvirt instance void IInputMode::ProcessAfterFrame(class SolutionEditorScreen)
             */
            if (!cursor.TryGotoNext(MoveType.After,
                    instr => instr.MatchLdarg0(),
                    instr => instr.MatchLdfld("SolutionEditorScreen", "currentInputMode"),
                    instr => instr.MatchLdarg0(),
                    instr => instr.MatchCallvirt("IInputMode", "ProcessAfterFrame")))
                throw new Exception("Failed to patch `SolutionEditorScreen.RenderFrame`: Unable to find call to `IInputMode.ProcessAfterFrame` to patch.");

            cursor.EmitLdarg0();
            cursor.EmitCallvirt(m_SolutionEditorScreen_UpdateLoopRegions);
        });
    }

    private void LoopFromHere_UpdateLoopRegions() {
        foreach (Part part in GetSolution().parts) {
            if (!part.TryGetComponent(LoopRegionComponent.Id, out IPartComponent component))
                continue;

            LoopRegionComponent loopRegionComponent = (LoopRegionComponent) component;
            if (loopRegionComponent.RecalculateEndIndex(this).GetOrDefault(out int newLoopEndIndex))
                loopRegionComponent.LoopEndIndex = newLoopEndIndex;
            else if (currentInputMode is not InstructionInsertInputMode insertInputMode
                || !((patch_InstructionInsertInputMode) insertInputMode).LoopFromHere_editedLoopRegions.Contains(loopRegionComponent))
                part.RemoveComponent(LoopRegionComponent.Id);
            loopRegionComponent.LoopStartAlpha = loopRegionComponent.LoopEndAlpha = LoopRegionComponent.NormalAlpha;
        }
    }
}
