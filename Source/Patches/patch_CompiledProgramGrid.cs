using System;
using System.Collections.Generic;
using System.Linq;
using LoopFromHere.Components;
using LoopFromHere.Helpers;
using Mono.Cecil;
using MonoMod;
using MonoMod.Cil;
using MonoMod.InlineRT;

// can't inherit from `CompiledProgramGrid` bc the base ctor is private :(
public class patch_CompiledProgramGrid {
    [MonoModIgnore]
    private readonly Dictionary<Part, CompiledProgram> programDict;

    [MonoModILInject("GetInstructionForPart")]
    public static void Patch_GetInstructionForPart(MethodDefinition method, CustomAttribute attribute) {
        MonoModRule.Modder.Log("[LoopFromHere] Patching `CompiledProgramGrid.GetInstructionForPart` to add support for custom looping behaviour...");

        MethodReference m_CompiledProgramGrid_ModifyInstructionIndex = method.DeclaringType.Methods.First(m => m.Name == "LoopFromHere_ModifyInstructionIndex");

        new ILContext(method).Invoke(il => {
            ILCursor cursor = new(il);

            /*
             * IL_0048: ldfld class CompiledInstruction[] CompiledProgram::instructions
             * IL_004d: ldlen
             * IL_004e: conv.i4
             * IL_004f: rem
             */
            if (!cursor.TryGotoNext(MoveType.After,
                    instr => instr.MatchLdfld("CompiledProgram", "instructions"),
                    instr => instr.MatchLdlen(),
                    instr => instr.MatchConvI4(),
                    instr => instr.MatchRem()))
                throw new Exception("Failed to patch `CompiledProgramGrid.GetInstructionForPart`: Unable to find assignment to `num` local to patch.");

            cursor.EmitLdarg1();
            cursor.EmitLdarg2();
            cursor.EmitLdloc0();
            cursor.EmitCall(m_CompiledProgramGrid_ModifyInstructionIndex);
        });
    }

    private static int LoopFromHere_ModifyInstructionIndex(int orig, int cycle, Part part, CompiledProgram program) {
        if (!LoopFromHereComponent.LoopFromIndexForPart(part).GetOrDefault(out int loopFromIndex))
            return orig;

        int cyclesSinceStart = cycle - program.startIndex;
        int cyclesSinceLooping = cyclesSinceStart - loopFromIndex;
        if (cyclesSinceLooping < 0)
            return cyclesSinceStart;

        int period = program.instructions.Length - loopFromIndex;
        int positionInLoop = cyclesSinceLooping % period;
        return positionInLoop + loopFromIndex;
    }

    [MonoModILInject("GetLoopedCycle")]
    public static void Patch_GetLoopedCycle(MethodDefinition method, CustomAttribute attribute) {
        MonoModRule.Modder.Log("[LoopFromHere] Patching `CompiledProgramGrid.GetLoopedCycle` to add support for custom looping behaviour...");

        MethodReference m_CompiledProgramGrid_ModifyGifPeriod = method.DeclaringType.Methods.First(m => m.Name == "LoopFromHere_ModifyGifPeriod");

        new ILContext(method).Invoke(il => {
            ILCursor cursor = new(il);

            /*
             * IL_0036: ldfld class CompiledInstruction[] CompiledProgram::instructions
             * IL_003b: ldlen
             * IL_003c: conv.i4
             */
            if (!cursor.TryGotoNext(MoveType.After,
                    instr => instr.MatchLdfld("CompiledProgram", "instructions"),
                    instr => instr.MatchLdlen(),
                    instr => instr.MatchConvI4()))
                throw new Exception("Failed to patch `CompiledProgramGrid.GetLoopedCycle`: Unable to find assignment to `val` local to patch.");

            cursor.EmitLdarg0();
            cursor.EmitCallvirt(m_CompiledProgramGrid_ModifyGifPeriod);
        });
    }

    private static int LoopFromHere_ModifyGifPeriod(int orig, CompiledProgramGrid grid) {
        patch_CompiledProgramGrid programGrid = (patch_CompiledProgramGrid) (object) grid;

        int normalPeriod = 0, loopFromHerePeriod = 0;
        foreach ((Part part, CompiledProgram program) in programGrid.programDict) {
            if (program.isOutOfCycle)
                continue;

            if (LoopFromHereComponent.LoopFromIndexForPart(part).GetOrDefault(out int loopFromIndex)) {
                int period = program.instructions.Length - loopFromIndex;
                loopFromHerePeriod = MathHelper.Lcm(loopFromHerePeriod, period);
            } else
                normalPeriod = program.instructions.Length;
        }

        return MathHelper.Lcm(normalPeriod, loopFromHerePeriod);
    }

    [MonoModILInject("FromSolution")]
    public static void Patch_FromSolution(MethodDefinition method, CustomAttribute attribute) {
        MonoModRule.Modder.Log("[LoopFromHere] Patching `CompiledProgramGrid.FromSolution` to add support for custom looping behaviour...");

        MethodReference m_CompiledProgramGrid_ShouldSkipVanillaCompilation = method.DeclaringType.Methods.First(m => m.Name == "LoopFromHere_UseCustomCompilation");
        MethodReference m_CompiledProgramGrid_CompileProgramGrid = method.DeclaringType.Methods.First(m => m.Name == "LoopFromHere_CompileProgramGrid");

        new ILContext(method).Invoke(il => {
            ILCursor cursor = new(il);
            ILLabel normalBehaviour = cursor.DefineLabel(), afterNormalBehaviour = cursor.DefineLabel();

            /*
             * IL_010b: ldc.i4.0
             * IL_010c: stloc.1
             * IL_010d: ldloc.0
             * IL_010e: callvirt instance valuetype [mscorlib]System.Collections.Generic.Dictionary`2/Enumerator<!0, !1> class [mscorlib]System.Collections.Generic.Dictionary`2<class Part, class [System]System.Collections.Generic.SortedDictionary`2<int32, class CompiledInstruction>>::GetEnumerator()
             * IL_0113: stloc.s 12
             */
            if (!cursor.TryGotoNext(MoveType.AfterLabel,
                    instr => instr.MatchLdcI4(0),
                    instr => instr.MatchStloc1(),
                    instr => instr.MatchLdloc0(),
                    instr => instr.MatchCallvirt(out MethodReference m_Dictionary_GetEnumerator)
                        && m_Dictionary_GetEnumerator.DeclaringType.Name == "Dictionary`2"
                        && m_Dictionary_GetEnumerator.Name == "GetEnumerator",
                    instr => instr.MatchStloc(12)))
                throw new Exception("Failed to patch `CompiledProgramGrid.FromSolution`: Unable to find assignment to `num` local to patch.");

            cursor.EmitLdarg0();
            cursor.EmitCall(m_CompiledProgramGrid_ShouldSkipVanillaCompilation);
            cursor.EmitBrfalse(normalBehaviour);

            cursor.EmitLdloc0();
            cursor.EmitCallvirt(m_CompiledProgramGrid_CompileProgramGrid);
            cursor.EmitStloc2();
            cursor.EmitBr(afterNormalBehaviour);

            cursor.MarkLabel(normalBehaviour);

            /*
             * IL_0300: ldarg.0
             * IL_0301: ldfld class [mscorlib]System.Collections.Generic.List`1<class Part> Solution::parts
             * IL_0306: callvirt instance int32 class [mscorlib]System.Collections.Generic.List`1<class Part>::get_Count()
             */
            if (!cursor.TryGotoNext(MoveType.AfterLabel,
                    instr => instr.MatchLdarg0(),
                    instr => instr.MatchLdfld("Solution", "parts"),
                    instr => instr.MatchCallvirt(out MethodReference m_List_get_Count)
                        && m_List_get_Count.DeclaringType.Name == "List`1"
                        && m_List_get_Count.Name == "get_Count"))
                throw new Exception("Failed to patch `CompiledProgramGrid.FromSolution`: Unable to find call to `Utils.AssertOrThrow` to patch.");

            cursor.MarkLabel(afterNormalBehaviour);
        });
    }

    private static bool LoopFromHere_UseCustomCompilation(Solution solution)
        => solution.parts.Any(p => p.HasComponent(LoopFromHereComponent.Id));

    private static Dictionary<Part, CompiledProgram> LoopFromHere_CompileProgramGrid(Dictionary<Part, SortedDictionary<int, CompiledInstruction>> partInstructions) {
        // todo
        throw new NotImplementedException();
    }
}
