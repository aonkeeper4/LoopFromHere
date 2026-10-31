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

    public Part Part { get; private set; }

    public int StartIndex, EndIndex;

    public const float NormalAlpha = 1f, GhostAlpha = 0.5f;
    public float StartAlpha = NormalAlpha, EndAlpha = NormalAlpha;

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
            StartIndex = StartIndex,
            EndIndex = EndIndex
        });
    public void OnReset() { }

    public Maybe<int> RecalculateEndIndex(SolutionEditorScreen solEditScrn) {
        if (Part is null)
            return MaybeHelper.empty;

        Solution solution = solEditScrn.GetSolution();
        InstructionsWRef instructions = Part.program.GetReferenceForPart(Part, solution.GetTrackUnderPart(Part));

        ProgramHelper.ProgramInfo info = new(instructions);
        if (info.IsEmpty || info.HasHalt || StartIndex > info.EndIndex)
            return MaybeHelper.empty;

        return info.EndIndex;
    }
    public void Recalculate(SolutionEditorScreen solEditScrn) {
        if (RecalculateEndIndex(solEditScrn).GetOrDefault(out int newLoopEndIndex))
            EndIndex = newLoopEndIndex;
        else
            Part.RemoveComponent(Id);
    }

    public static void Register()
        => Part.RegisterComponent(Id, Codec);
}
