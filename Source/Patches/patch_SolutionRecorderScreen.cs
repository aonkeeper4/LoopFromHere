using System.Linq;
using LoopFromHere.Components;
using MonoMod;

public class patch_SolutionRecorderScreen : SolutionRecorderScreen {
    [MonoModIgnore]
    private SimpleSolutionEditor solutionEditor;

    [MonoModIgnore]
    public patch_SolutionRecorderScreen(Solution solution) : base(solution) { }

    [MonoModIgnore]
    private extern bool orig_WaitingForAnyPartToHalt();
    private bool WaitingForAnyPartToHalt() {
        // return true if we are waiting for any parts to finish non-looping behaviour (either waiting for them to halt or waiting for them to loop)

        if (solutionEditor.GetSolution().parts.All(p => !p.HasComponent(LoopFromHereComponent.Id)))
            return orig_WaitingForAnyPartToHalt();

        int cycle = solutionEditor.GetSimulation().GetCycle();
        CompiledProgramGrid programGrid = solutionEditor.GetSimulation().GetProgram();
        foreach (Part part in solutionEditor.GetSolution().parts) {
            int startIndex = programGrid.GetProgramForPart(part).startIndex;
            if (LoopFromHereComponent.LoopFromIndexForPart(part).GetOrDefault(out int loopFromIndex) && cycle < startIndex + loopFromIndex)
                return true; // this part hasn't started looping yet
        }

        return orig_WaitingForAnyPartToHalt();
    }
}
