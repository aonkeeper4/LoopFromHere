using System;
using System.Linq;
using Mono.Cecil;
using MonoMod;
using MonoMod.Cil;
using MonoMod.InlineRT;
using MonoMod.Utils;

public class patch_PanningInputMode : PanningInputMode {
    private IInputMode LoopFromHere_previousInputMode;

    [MonoModIgnore]
    public patch_PanningInputMode(SolutionEditorScreen solEditScrn, SolutionEditorPanel pannedItem) : base(solEditScrn, pannedItem) { }

    // `orig_`-style patching for constructors doesn't work yet
    // neither does calling patch methods `patch_Method` weh
    [MonoModILInject(".ctor")]
    public static void Patch_ctor(MethodDefinition method, CustomAttribute attribute) {
        MonoModRule.Modder.Log("[LoopFromHere] Patching `PanningInputMode..ctor` to remember previous input mode...");

        FieldDefinition f_SolutionEditorScreen_currentInputMode = MonoModRule.Modder.FindType("SolutionEditorScreen").Resolve().FindField("currentInputMode");
        FieldDefinition f_PanningInputMode_previousInputMode = method.DeclaringType.FindField("LoopFromHere_previousInputMode");

        new ILContext(method).Invoke(il => {
            ILCursor cursor = new(il);

            if (!cursor.TryGotoNext(MoveType.AfterLabel, instr => instr.MatchRet()))
                throw new Exception("Failed to patch `PanningInputMode..ctor`: Unable to find return to patch.");

            cursor.EmitLdarg0();
            cursor.EmitLdarg1();
            cursor.EmitLdfld(f_SolutionEditorScreen_currentInputMode);
            cursor.EmitStfld(f_PanningInputMode_previousInputMode);
        });
    }

    [MonoModILInject("Process")]
    public static void Patch_Process(MethodDefinition method, CustomAttribute attribute) {
        MonoModRule.Modder.Log("[LoopFromHere] Patching `PanningInputMode.Process` to remember previous input mode...");

        FieldDefinition f_PanningInputMode_previousInputMode = method.DeclaringType.FindField("LoopFromHere_previousInputMode");

        new ILContext(method).Invoke(il => {
            ILCursor cursor = new(il);

            if (!cursor.TryGotoNext(MoveType.After, instr => instr.MatchNewobj("NormalInputMode")))
                throw new Exception("Failed to patch `PanningInputMode.Process`: Unable to find `NormalInputMode` construction to patch.");

            cursor.EmitPop(); // hmm
            cursor.EmitLdarg0();
            cursor.EmitLdfld(f_PanningInputMode_previousInputMode);
        });
    }
}
