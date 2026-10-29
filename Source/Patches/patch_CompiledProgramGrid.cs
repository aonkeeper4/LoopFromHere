using System;
using System.Collections.Generic;
using System.Linq;
using LoopFromHere.Components;
using LoopFromHere.Helpers;
using Mono.Cecil;
using MonoMod;
using MonoMod.Cil;
using MonoMod.InlineRT;
using MonoMod.Utils;
using Quintessential.Components;

// can't inherit from `CompiledProgramGrid` bc the base ctor is private :(
public class patch_CompiledProgramGrid {
    [MonoModIgnore]
    private readonly Dictionary<Part, CompiledProgram> programDict;

    [MonoModILInject("GetInstructionForPart")]
    public static void Patch_GetInstructionForPart(MethodDefinition method, CustomAttribute attribute) {
        MonoModRule.Modder.Log("[LoopFromHere] Patching `CompiledProgramGrid.GetInstructionForPart` to add support for custom looping behaviour...");

        MethodDefinition m_CompiledProgramGrid_ModifyInstructionIndex = method.DeclaringType.FindMethod("LoopFromHere_ModifyInstructionIndex")!;

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
        if (!part.TryGetComponent(LoopRegionComponent.Id, out IPartComponent component))
            return orig;

        LoopRegionComponent loopRegionComponent = (LoopRegionComponent) component;
        int loopStartIndex = loopRegionComponent.StartIndex - program.startIndex,
            loopEndIndex = loopRegionComponent.EndIndex - program.startIndex;

        int cyclesSinceStart = cycle - program.startIndex, cyclesSinceLooping = cycle - loopRegionComponent.StartIndex;
        if (cyclesSinceLooping < 0)
            return cyclesSinceStart;

        int period = loopEndIndex - loopStartIndex + 1;
        int positionInLoop = cyclesSinceLooping % period;
        return positionInLoop + loopStartIndex;
    }

    [MonoModILInject("GetLoopedCycle")]
    public static void Patch_GetLoopedCycle(MethodDefinition method, CustomAttribute attribute) {
        MonoModRule.Modder.Log("[LoopFromHere] Patching `CompiledProgramGrid.GetLoopedCycle` to add support for custom looping behaviour...");

        MethodDefinition m_CompiledProgramGrid_ModifyTotalPeriod = method.DeclaringType.FindMethod("LoopFromHere_ModifyTotalPeriod")!;

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
            cursor.EmitCall(m_CompiledProgramGrid_ModifyTotalPeriod);
        });
    }

    private static int LoopFromHere_ModifyTotalPeriod(int orig, patch_CompiledProgramGrid programGrid) {
        if (programGrid.programDict.Keys.All(p => !p.HasComponent(LoopRegionComponent.Id)))
            return orig;

        int totalPeriod = 1;
        foreach ((Part part, CompiledProgram program) in programGrid.programDict) {
            if (program.isOutOfCycle || program.instructions.Length <= 0)
                continue;

            int period;
            if (part.TryGetComponent(LoopRegionComponent.Id, out IPartComponent component)) {
                LoopRegionComponent loopRegionComponent = (LoopRegionComponent) component;
                int loopStartIndex = loopRegionComponent.StartIndex - program.startIndex,
                    loopEndIndex = loopRegionComponent.EndIndex - program.startIndex;
                period = loopEndIndex - loopStartIndex + 1;
            } else
                period = program.instructions.Length;

            totalPeriod = MathHelper.Lcm(totalPeriod, period);
        }

        return totalPeriod;
    }

    [MonoModILInject("FromSolution")]
    public static void Patch_FromSolution(MethodDefinition method, CustomAttribute attribute) {
        MonoModRule.Modder.Log("[LoopFromHere] Patching `CompiledProgramGrid.FromSolution` to add support for custom looping behaviour...");

        MethodDefinition m_CompiledProgramGrid_UseCustomCompilation = method.DeclaringType.FindMethod("LoopFromHere_UseCustomCompilation")!;
        MethodDefinition m_CompiledProgramGrid_CompileProgramGrid = method.DeclaringType.FindMethod("LoopFromHere_CompileProgramGrid")!;

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
            cursor.EmitCall(m_CompiledProgramGrid_UseCustomCompilation);
            cursor.EmitBrfalse(normalBehaviour);

            cursor.EmitLdloc0();
            cursor.EmitCall(m_CompiledProgramGrid_CompileProgramGrid);
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
        => solution.parts.Any(p => p.HasComponent(LoopRegionComponent.Id));
    private static Dictionary<Part, CompiledProgram> LoopFromHere_CompileProgramGrid(Dictionary<Part, SortedDictionary<int, CompiledInstruction>> partInstructions) {
        int defaultTapeLength = 1;
        Dictionary<Part, (ProgramHelper.ProgramInfo, bool)> programInfo = new();
        foreach ((Part part, SortedDictionary<int, CompiledInstruction> instructions) in partInstructions) {
            ProgramHelper.ProgramInfo info = new(instructions);
            bool hasLoopRegion = part.HasComponent(LoopRegionComponent.Id);
            programInfo.Add(part, (info, hasLoopRegion));

            if (info.IsEmpty || info.HasHalt || hasLoopRegion)
                continue;

            int period = info.EndIndex - info.StartIndex + 1;
            defaultTapeLength = Math.Max(defaultTapeLength, period);
        }

        Dictionary<Part, CompiledProgram> compiledProgramGrid = new();
        foreach ((Part part, SortedDictionary<int, CompiledInstruction> instructions) in partInstructions) {
            (ProgramHelper.ProgramInfo info, bool hasLoopRegion) = programInfo[part];
            int tapeLength = info.HasHalt || hasLoopRegion
                ? info.EndIndex - info.StartIndex + 1 // in vanilla, halt arms have their instruction tape padded to be at least the period of the other arms. i think it's ok to not do that here
                : defaultTapeLength;

            CompiledProgram partProgram = new() {
                startIndex = info.StartIndex,
                instructions = new CompiledInstruction[tapeLength],
                isOutOfCycle = info.HasHalt
            };

            bool isExpansion = false;
            for (int i = 0; i < tapeLength; i++)
            {
                Maybe<CompiledInstruction> instruction = instructions.GetOrNone(info.StartIndex + i);

                partProgram.instructions[i] = instruction.GetOrDefault(new CompiledInstruction {
                    type = InstructionTypes.idle,
                    isExpansion = isExpansion
                });
                isExpansion = !instruction.HasValue();
            }

            compiledProgramGrid[part] = partProgram;
        }

        return compiledProgramGrid;
    }
}
