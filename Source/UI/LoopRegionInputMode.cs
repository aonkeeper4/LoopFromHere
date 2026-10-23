using SDL2;

namespace LoopFromHere.UI;

internal class LoopRegionInputMode : IInputMode {
    public Maybe<IInputMode> Process(SolutionEditorScreen solEditScrn) {
        if (!EditingLoopRegions())
            return new NormalInputMode();

        bool leftClickPressed = InputManager.IsClickPressed(MouseButtonType.LeftClick),
            rightClickPressed = InputManager.IsClickPressed(MouseButtonType.RightClick);
        bool rightClickConsumed = false;

        // todo

        if (rightClickPressed && !rightClickConsumed
            && solEditScrn.programPanel.ProgramScrollAreaBounds().Contains(InputManager.MousePos()))
            return new PanningInputMode(solEditScrn, solEditScrn.programPanel);

        return MaybeHelper.empty;
    }
    public void ProcessAfterFrame(SolutionEditorScreen solEditScrn) { }

    public void method_2(SolutionEditorScreen solEditScrn) { }

    public static IInputMode StartInputMode(SolutionEditorScreen solEditScrn) {
        solEditScrn.method_2120(); // clear instruction selection
        return new LoopRegionInputMode();
    }

    // no idea why i have to use `WhileKeyHeld`
    public static bool EditingLoopRegions()
        => InputManager.WhileKeyHeld(SDL.SDL_GetKeyFromName(LoopFromHereSettings.Instance.EditLoopRegions.Key));
}
