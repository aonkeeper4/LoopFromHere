using System.Collections.Generic;
using System.Linq;

namespace LoopFromHere.Helpers;

internal static class Extensions {
    extension(IEnumerable<int> source) {
        public int MinOrDefault(int @default)
            => source.Count() > 0 ? source.Min() : default;
        public int MaxOrDefault(int @default)
            => source.Count() > 0 ? source.Max() : default;
    }
}
