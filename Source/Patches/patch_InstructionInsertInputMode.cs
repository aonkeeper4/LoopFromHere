using System;
using System.Linq;
using LoopFromHere.Components;
using Mono.Cecil;
using MonoMod;
using MonoMod.Cil;
using MonoMod.InlineRT;
using MonoMod.Utils;

// todo:
// make editing a program affect its loop region
// * only edits made while holding the edit loop regions keybind can affect the start of a loop region
// * all edits can affect the end of a loop region
// * if an edit will affect a loop region, make it render appropriately
//
// the start of a loop region is solid on the left and sticky on the right
public class patch_InstructionInsertInputMode : InstructionInsertInputMode {
    [MonoModILInject("IInputMode InstructionInsertInputMode::StartInputMode(SolutionEditorScreen,Vector2)")]
    public static void Patch_StartInputMode(MethodDefinition method, CustomAttribute attribute) {
        MonoModRule.Modder.Log("[LoopFromHere] Patching `InstructionInsertInputMode.StartInputMode(SolutionEditorScreen solEditScrn, Vector2 startMousePos)` to remove period override instruction limit...");

        MethodDefinition m_InstructionInsertInputMode_RemovePeriodOverrideLimit = method.DeclaringType.FindMethod("LoopFromHere_RemovePeriodOverrideLimit");

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
        => solEditScrn.GetSolution().parts.Any(p => p.HasComponent(LoopRegionComponent.Id)) || orig;

    [MonoModILInject("Process")]
    public static void Patch_Process(MethodDefinition method, CustomAttribute attribute) {
        MonoModRule.Modder.Log("[LoopFromHere] Patching `InstructionInsertInputMode.Process` to update loop regions when editing instructions...");

        // todo
    }
}
