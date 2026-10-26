using System.Collections.Generic;
using System.Linq;

namespace LoopFromHere.Helpers;

internal static class ProgramHelper {
    public struct ProgramInfo {
        public readonly bool IsEmpty;
        public readonly int StartIndex, EndIndex;

        public readonly bool HasHalt;

        public ProgramInfo(SortedDictionary<int, CompiledInstruction> program) {
            if (program.Count <= 0) {
                IsEmpty = true;
                StartIndex = EndIndex = 0;
                HasHalt = false;
                return;
            }

            StartIndex = program.Keys.Min();
            EndIndex = program.Keys.Max();
            HasHalt = program.Values.Any(i => i.type.category == InstructionCategory.Halt);
        }

        public ProgramInfo(InstructionsWRef program) {
            if (program.program.Count <= 0) {
                IsEmpty = true;
                StartIndex = EndIndex = 0;
                HasHalt = false;
                return;
            }

            StartIndex = program.program.Keys.Min();
            EndIndex = program.program.Keys.Max();
            EndIndex += program.program[EndIndex].expansion.Length - 1;
            HasHalt = program.program.Values.Any(i => i.instruction.category == InstructionCategory.Halt);
        }
    }
}
