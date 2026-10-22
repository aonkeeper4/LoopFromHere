namespace LoopFromHere.UI;

// todo: patch instruction insertion modes to move loop region start accordingly
public class LoopRegionInputMode : IInputMode {
    public Maybe<IInputMode> Process(SolutionEditorScreen solEditScrn) {
        bool leftClickPressed = InputManager.IsClickPressed(MouseButtonType.LeftClick),
            rightClickPressed = InputManager.IsClickPressed(MouseButtonType.RightClick);

        if ((!solEditScrn.ProgramPanelContainsMouse() && (leftClickPressed || rightClickPressed)) // "lose focus" when clicking somewhere else. todo: don't interrupt dragging?
            || LoopFromHereSettings.Instance.EditLoopRegions.Pressed()) // exit normally
            return new NormalInputMode();

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
}
