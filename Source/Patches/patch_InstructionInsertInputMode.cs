using System;
using System.Linq;
using LoopFromHere.Components;
using Mono.Cecil;
using MonoMod;
using MonoMod.Cil;
using MonoMod.InlineRT;

// todo:
// while holding the edit loop regions keybind, make editing a program affect its loop region (including making it show in the held instructions if necessary)
// make removing all instructions from a program delete its loop region
// make adding a halt instruction to a program delete its loop region
// after all other edits to a program have been processed this frame, if a loop region has no instructions to its right, delete it
//
// loop regions are solid on the left, sticky on the right
public class patch_InstructionInsertInputMode : InstructionInsertInputMode {
    [MonoModILInject("IInputMode InstructionInsertInputMode::StartInputMode(SolutionEditorScreen,Vector2)")]
    public static void Patch_StartInputMode(MethodDefinition method, CustomAttribute attribute) {
        MonoModRule.Modder.Log("[LoopFromHere] Patching `InstructionInsertInputMode.StartInputMode(SolutionEditorScreen solEditScrn, Vector2 startMousePos)` to remove period override instruction limit...");

        MethodDefinition m_InstructionInsertInputMode_UncapPeriodOverrideLimit = method.DeclaringType.Methods.First(m => m.Name == "LoopFromHere_RemovePeriodOverrideLimit");

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
            cursor.EmitCall(m_InstructionInsertInputMode_UncapPeriodOverrideLimit);
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
