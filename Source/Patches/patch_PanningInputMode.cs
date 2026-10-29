using System;
using Mono.Cecil;
using MonoMod;
using MonoMod.Cil;
using MonoMod.InlineRT;
using MonoMod.Utils;

public class patch_PanningInputMode : PanningInputMode {
    [MonoModIgnore]
    public patch_PanningInputMode(SolutionEditorScreen solEditScrn, SolutionEditorPanel pannedItem) : base(solEditScrn, pannedItem) { }

    private IInputMode LoopFromHere_previousInputMode;

    // `orig_`-style patching for constructors doesn't work yet
    [MonoModILInject(".ctor")]
    public static void Patch_ctor(MethodDefinition method, CustomAttribute attribute) {
        MonoModRule.Modder.Log("[LoopFromHere] Patching `PanningInputMode..ctor` to remember previous input mode...");

        FieldDefinition f_SolutionEditorScreen_currentInputMode = MonoModRule.Modder.FindType("SolutionEditorScreen").Resolve().FindField("currentInputMode")!;
        FieldDefinition f_PanningInputMode_previousInputMode = method.DeclaringType.FindField("LoopFromHere_previousInputMode")!;

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

    [MonoModIgnore]
    public extern Maybe<IInputMode> orig_Process(SolutionEditorScreen solEditScrn);
    // i think this is fine
    public Maybe<IInputMode> Process(SolutionEditorScreen solEditScrn)
        => orig_Process(solEditScrn).HasValue()
            ? MaybeHelper.Create(LoopFromHere_previousInputMode)
            : MaybeHelper.empty;
}
