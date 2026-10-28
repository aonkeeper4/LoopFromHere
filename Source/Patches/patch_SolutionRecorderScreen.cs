using System.Linq;
using LoopFromHere.Components;
using MonoMod;
using Quintessential.Components;

public class patch_SolutionRecorderScreen : SolutionRecorderScreen {
    [MonoModIgnore]
    public patch_SolutionRecorderScreen(Solution solution) : base(solution) { }

    [MonoModIgnore]
    private SimpleSolutionEditor solutionEditor;

    [MonoModIgnore]
    private extern bool orig_WaitingForAnyPartToHalt();
    private bool WaitingForAnyPartToHalt() {
        // return true if we are waiting for any parts to finish non-looping behaviour (either waiting for them to halt or waiting for them to loop)

        if (solutionEditor.GetSolution().parts.All(p => !p.HasComponent(LoopRegionComponent.Id)))
            return orig_WaitingForAnyPartToHalt();

        int cycle = solutionEditor.GetSimulation().GetCycle();
        foreach (Part part in solutionEditor.GetSolution().parts) {
            if (!part.TryGetComponent(LoopRegionComponent.Id, out IPartComponent component))
                continue;

            if (cycle < ((LoopRegionComponent) component).LoopStartIndex)
                return true;
        }

        return orig_WaitingForAnyPartToHalt();
    }
}
