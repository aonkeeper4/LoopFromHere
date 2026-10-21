using MonoMod;

public class patch_SolutionRecorderScreen : SolutionRecorderScreen {
    [MonoModIgnore]
    public patch_SolutionRecorderScreen(Solution solution) : base(solution) { }

    // todo
}
