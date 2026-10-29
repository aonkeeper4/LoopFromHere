using LoopFromHere.Components;
using LoopFromHere.Helpers;
using Quintessential.Components;
using SDL2;

namespace LoopFromHere.UI;

internal class LoopRegionInputMode : IInputMode {
    private Vector2 leftClickDragStartPosition, rightClickDragStartPosition;

    public Maybe<IInputMode> Process(SolutionEditorScreen solEditScrn) {
        if (!EditingLoopRegions())
            return new NormalInputMode();

        Vector2 mousePos = InputManager.MousePos();
        if (InputManager.IsClickPressed(MouseButtonType.LeftClick))
            leftClickDragStartPosition = mousePos;
        if (InputManager.IsClickPressed(MouseButtonType.RightClick))
            rightClickDragStartPosition = mousePos;

        bool interpretedLeftClick = InputManager.IsClickReleased(MouseButtonType.LeftClick) && Vector2.Distance(leftClickDragStartPosition, mousePos) < Editor.inputModeSwitchDistance,
            interpretedRightClick = InputManager.IsClickReleased(MouseButtonType.RightClick) && Vector2.Distance(rightClickDragStartPosition, mousePos) < Editor.inputModeSwitchDistance;
        bool leftClickDragging = InputManager.IsClickHeld(MouseButtonType.LeftClick) && Vector2.Distance(leftClickDragStartPosition, mousePos) >= Editor.inputModeSwitchDistance,
            rightClickDragging = InputManager.IsClickHeld(MouseButtonType.RightClick) && Vector2.Distance(rightClickDragStartPosition, mousePos) >= Editor.inputModeSwitchDistance;

        // handle placing/deleting loop regions
        patch_SolutionEditorProgramPanel programPanel = (patch_SolutionEditorProgramPanel) solEditScrn.programPanel;
        bool editMade = false;
        if (programPanel.GetInstructionAt(mousePos).GetOrDefault(out InstructionReference hoveredInstruction)) {
            Part hoveredPart = hoveredInstruction.part;
            InstructionsWRef hoveredInstructions = hoveredPart.program.GetReferenceForPart(hoveredPart, solEditScrn.GetSolution().GetTrackUnderPart(hoveredPart));

            if (interpretedLeftClick && CanPlaceLoopRegion(hoveredInstructions, hoveredInstruction.instructionIndex)) {
                // place loop region
                LoopRegionComponent loopRegionComponent;
                if (hoveredPart.TryGetComponent(LoopRegionComponent.Id, out IPartComponent component)) {
                    loopRegionComponent = (LoopRegionComponent) component;
                    loopRegionComponent.StartIndex = hoveredInstruction.instructionIndex;
                }
                else
                    hoveredPart.AddComponent(loopRegionComponent = new LoopRegionComponent { StartIndex = hoveredInstruction.instructionIndex });

                loopRegionComponent.EndIndex = loopRegionComponent.RecalculateEndIndex(solEditScrn).GetValue(); // will never throw since we know the placement is valid
                Assets.sounds.instruction_place.PlaySound(1f);

                editMade = true;
            } else if (interpretedRightClick
                && hoveredPart.TryGetComponent(LoopRegionComponent.Id, out IPartComponent component)
                && ((LoopRegionComponent) component).StartIndex == hoveredInstruction.instructionIndex) {
                // delete loop region
                hoveredPart.RemoveComponent(LoopRegionComponent.Id);
                Assets.sounds.instruction_remove.PlaySound(1f);

                editMade = true;
            }
        }
        if (editMade)
            solEditScrn.MarkNeedsNewSnapshot();

        // handle instruction selection
        if (solEditScrn.programPanel.ProgramScrollAreaBounds().Contains(mousePos)) {
            if (leftClickDragging) {
                solEditScrn.programPanel.ClearPartSelection();

                if (!solEditScrn.programPanel.GetInstructionAtPos(leftClickDragStartPosition).GetOrDefault(out InstructionReference draggedInstruction)
                    || !solEditScrn.instructionSelection.HasMatching(draggedInstruction)
                    || draggedInstruction.part.GetIsFixed())
                    // start selecting instructions
                    return MaybeHelper.Create(AreaSelectInstructionsInputMode.StartInputMode(solEditScrn));

                // drag selected instructions
                Assets.sounds.instruction_pickup.PlaySound(1f);
                return MaybeHelper.Create(InstructionInsertInputMode.StartInputMode(solEditScrn, leftClickDragStartPosition));
            }
            if (rightClickDragging)
                // start panning
                return new PanningInputMode(solEditScrn, solEditScrn.programPanel);
        }

        return MaybeHelper.empty;
    }
    public void ProcessAfterFrame(SolutionEditorScreen solEditScrn) { }

    public void RenderOverlay(SolutionEditorScreen solEditScrn) { }

    // no idea why i have to use `WhileKeyHeld`
    public static bool EditingLoopRegions()
        => InputManager.WhileKeyHeld(SDL.SDL_GetKeyFromName(LoopFromHereSettings.Instance.EditLoopRegions.Key))
            && !SolutionEditorProgramPanel.IsFastForwardHeld();

    public static bool CanPlaceLoopRegion(InstructionsWRef instructions, int index) {
        ProgramHelper.ProgramInfo info = new(instructions);
        return !info.IsEmpty && !info.HasHalt && index <= info.EndIndex;
    }
}
