using System;
using System.Linq;
using Quintessential;
using Quintessential.Components;
using Quintessential.Serialization;

namespace LoopFromHere.Components;

internal class LoopRegionComponent : IPartComponent {
    public static readonly Identifier Id = LoopFromHere.Instance.GetIdentifier("loop_region");
    Identifier IComponentBase<Part>.Id => Id;

    // only save `LoopStartIndex`, as everything should be recalculated based on that
    public static readonly Codec<IPartComponent> Codec = Codec<IPartComponent>.Create(
        Codecs.INT.Seal<IPartComponent>(nameof(LoopStartIndex), component => (component as LoopRegionComponent)!.LoopStartIndex), // cmon c# you can infer the type you can do it
        loopFromIndex => new LoopRegionComponent { LoopStartIndex = loopFromIndex }
    );

    private Part Part;

    public int LoopStartIndex, LoopEndIndex;

    public enum RenderMode {
        None,
        Normal,
        Ghost
    }
    public RenderMode StartRenderMode = RenderMode.Normal, EndRenderMode = RenderMode.Normal;

    public void OnBind(Part self) {
        if (!self.GetType().isProgrammable)
            throw new InvalidOperationException($"{nameof(LoopRegionComponent)} can only be added to a programmable part!");

        Part = self;
    }

    public void OnUnbind(Part self)
        => Part = null;

    // don't clone render properties
    public void OnClone(ref Part cloned, Solution solution)
        => cloned.AddComponent(new LoopRegionComponent {
            LoopStartIndex = LoopStartIndex,
            LoopEndIndex = LoopEndIndex
        });
    public void OnReset() { }

    public void Recalculate(SolutionEditorScreen solEditScrn) {
        Solution solution = solEditScrn.GetSolution();
        InstructionsWRef instructions = Part.program.GetReferenceForPart(Part, solution.GetTrackUnderPart(Part));

        (bool isEmpty, _, int programEndIndex, bool hasHalt) = patch_SolutionEditorProgramPanel.LoopFromHere_GetProgramInfo(instructions);
        if (isEmpty || LoopStartIndex > programEndIndex || hasHalt)
            Part.RemoveComponent(Id);

        LoopEndIndex = programEndIndex;
        StartRenderMode = EndRenderMode = RenderMode.Normal; // hmm
    }

    public static void Register()
        => Part.RegisterComponent(Id, Codec);
}
