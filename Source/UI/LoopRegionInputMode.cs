using System.Linq;
using LoopFromHere.Components;
using Quintessential.Components;
using SDL2;

namespace LoopFromHere.UI;

internal class LoopRegionInputMode : IInputMode {
    public Maybe<IInputMode> Process(SolutionEditorScreen solEditScrn) {
        if (!EditingLoopRegions())
            return new NormalInputMode();

        Vector2 mousePos = InputManager.MousePos();
        bool leftClickPressed = InputManager.IsClickPressed(MouseButtonType.LeftClick),
            rightClickPressed = InputManager.IsClickPressed(MouseButtonType.RightClick);

        patch_SolutionEditorProgramPanel programPanel = (patch_SolutionEditorProgramPanel) solEditScrn.programPanel;
        bool editMade = false;
        if (programPanel.LoopFromHere_GetIndexAt(mousePos).GetOrDefault(out Index2 hoveredIndex)) {
            Part hoveredPart = solEditScrn.GetSolution().GetOrderedProgrammables()[hoveredIndex.Y];
            InstructionsWRef hoveredInstructions = hoveredPart.program.GetReferenceForPart(hoveredPart, solEditScrn.GetSolution().GetTrackUnderPart(hoveredPart));

            if (leftClickPressed && patch_SolutionEditorProgramPanel.LoopFromHere_CanPlaceLoopRegion(hoveredInstructions, hoveredIndex.X)) {
                // place loop region
                if (hoveredPart.TryGetComponent(LoopRegionComponent.Id, out IPartComponent component))
                    ((LoopRegionComponent) component).LoopStartIndex = hoveredIndex.X;
                else
                    hoveredPart.AddComponent(new LoopRegionComponent { LoopStartIndex = hoveredIndex.X });
                Assets.sounds.instruction_place.PlaySound(1f);

                leftClickPressed = false;
                editMade = true;
            } else if (rightClickPressed
                && hoveredPart.TryGetComponent(LoopRegionComponent.Id, out IPartComponent component)
                && ((LoopRegionComponent) component).LoopStartIndex == hoveredIndex.X) {
                // remove loop region
                hoveredPart.RemoveComponent(LoopRegionComponent.Id);
                Assets.sounds.instruction_remove.PlaySound(1f);

                rightClickPressed = false;
                editMade = true;
            }
        }
        if (editMade)
            solEditScrn.MarkNeedsNewSnapshot();

        if (rightClickPressed && solEditScrn.programPanel.ProgramScrollAreaBounds().Contains(InputManager.MousePos()))
            return new PanningInputMode(solEditScrn, solEditScrn.programPanel);

        return MaybeHelper.empty;
    }
    public void ProcessAfterFrame(SolutionEditorScreen solEditScrn) { }

    public void RenderOverlay(SolutionEditorScreen solEditScrn) { }

    public static IInputMode StartInputMode(SolutionEditorScreen solEditScrn) {
        solEditScrn.ClearInstrSelection();
        return new LoopRegionInputMode();
    }

    // no idea why i have to use `WhileKeyHeld`
    public static bool EditingLoopRegions()
        => InputManager.WhileKeyHeld(SDL.SDL_GetKeyFromName(LoopFromHereSettings.Instance.EditLoopRegions.Key));
}
