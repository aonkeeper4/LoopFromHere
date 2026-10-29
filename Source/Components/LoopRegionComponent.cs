using System;
using LoopFromHere.Helpers;
using Quintessential;
using Quintessential.Components;
using Quintessential.Serialization;

namespace LoopFromHere.Components;

internal class LoopRegionComponent : IPartComponent {
    public static readonly Identifier Id = LoopFromHere.Instance.GetIdentifier("loop_region");
    Identifier IComponentBase<Part>.Id => Id;

    // only save `LoopStartIndex`, as everything should be recalculated based on that
    public static readonly Codec<IPartComponent> Codec = Codec<IPartComponent>.Create(
        Codecs.INT.Seal<IPartComponent>(nameof(StartIndex), component => (component as LoopRegionComponent)!.StartIndex), // cmon c# you can infer the type you can do it
        loopFromIndex => new LoopRegionComponent { StartIndex = loopFromIndex }
    );

    private Part part;

    public int StartIndex, EndIndex;

    public const float NormalAlpha = 1f, GhostAlpha = 0.5f;
    public float LoopStartAlpha = NormalAlpha, LoopEndAlpha = NormalAlpha;

    public void OnBind(Part self) {
        if (!self.GetType().isProgrammable)
            throw new InvalidOperationException($"{nameof(LoopRegionComponent)} can only be added to a programmable part!");

        part = self;
    }
    public void OnUnbind(Part self)
        => part = null;

    // don't clone render properties
    public void OnClone(ref Part cloned, Solution solution)
        => cloned.AddComponent(new LoopRegionComponent {
            StartIndex = StartIndex,
            EndIndex = EndIndex
        });
    public void OnReset() { }

    public Maybe<int> RecalculateEndIndex(SolutionEditorScreen solEditScrn) {
        Solution solution = solEditScrn.GetSolution();
        InstructionsWRef instructions = part.program.GetReferenceForPart(part, solution.GetTrackUnderPart(part));

        ProgramHelper.ProgramInfo info = new(instructions);
        if (info.IsEmpty || info.HasHalt || StartIndex > info.EndIndex)
            return MaybeHelper.empty;

        return info.EndIndex;
    }

    public static void Register()
        => Part.RegisterComponent(Id, Codec);
}
