using LoopFromHere;
using LoopFromHere.UI;
using MonoMod;

public class patch_NormalInputMode : NormalInputMode {
    [MonoModIgnore]
    public extern Maybe<IInputMode> orig_Process(SolutionEditorScreen solEditScrn);
    public Maybe<IInputMode> Process(SolutionEditorScreen solEditScrn) {
        Maybe<IInputMode> nextInputMode = orig_Process(solEditScrn);
        if (nextInputMode.HasValue())
            return nextInputMode;

        if (solEditScrn.GetSimPlayState() != SimPlayState.Stopped
            || !solEditScrn.ProgramPanelContainsMouse()
            || !LoopFromHereSettings.Instance.EditLoopRegions.Pressed())
            return MaybeHelper.empty;

        return MaybeHelper.Create(LoopRegionInputMode.StartInputMode(solEditScrn));
    }
}

