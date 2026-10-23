using System;
using System.Collections.Generic;

namespace PCLCS.Port.Diff;

/// <summary>列表差异结果。移植自 PCL-CE-dev 的 PCL.Core/Utils/Diff/ItemDiffResult.cs。</summary>
public class ItemDiffResult<T>
{
    public IReadOnlyList<T> Added { get; }
    public IReadOnlyList<T> Removed { get; }
    public IReadOnlyList<T> Unchanged { get; }

    public ItemDiffResult(IReadOnlyList<T> added, IReadOnlyList<T> removed, IReadOnlyList<T> unchanged)
    {
        Added = added ?? throw new ArgumentNullException(nameof(added));
        Removed = removed ?? throw new ArgumentNullException(nameof(removed));
        Unchanged = unchanged ?? throw new ArgumentNullException(nameof(unchanged));
    }
}
