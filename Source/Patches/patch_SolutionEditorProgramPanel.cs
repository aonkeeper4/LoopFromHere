using MonoMod;

public class patch_SolutionEditorProgramPanel : SolutionEditorProgramPanel {
    [MonoModIgnore]
    public patch_SolutionEditorProgramPanel(SolutionEditorScreen solEditScrn) : base(solEditScrn) { }

    // todo
}
