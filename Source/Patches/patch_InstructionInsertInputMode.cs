using System;
using System.Linq;
using LoopFromHere.Components;
using Mono.Cecil;
using MonoMod;
using MonoMod.Cil;
using MonoMod.InlineRT;

public class patch_InstructionInsertInputMode : InstructionInsertInputMode {
    [MonoModILInject("IInputMode InstructionInsertInputMode::StartInputMode(SolutionEditorScreen,Vector2)")]
    public static void Patch_StartInputMode(MethodDefinition method, CustomAttribute attribute) {
        MonoModRule.Modder.Log("[LoopFromHere] Patching `InstructionInsertInputMode.StartInputMode(SolutionEditorScreen solEditScrn, Vector2 startMousePos)` to remove period override instruction limit...");

        MethodDefinition m_InstructionInsertInputMode_UncapPeriodOverrideLimit = method.DeclaringType.Methods.First(m => m.Name == "LoopFromHere_UncapPeriodOverrideLimit");

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

    private static bool LoopFromHere_UncapPeriodOverrideLimit(bool orig, SolutionEditorScreen solEditScrn)
        => orig && solEditScrn.GetSolution().parts.All(p => !p.HasComponent(LoopRegionComponent.Id));

    // todo: make instruction editing affect loop regions only while holding the keybind
}
