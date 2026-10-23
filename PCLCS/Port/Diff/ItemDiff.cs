using System;
using System.Collections.Generic;
using System.Linq;

namespace PCLCS.Port.Diff;

/// <summary>
/// 列表差异计算。移植自 PCL-CE-dev 的 PCL.Core/Utils/Diff/ItemDiff.cs。
/// 给定旧/新两个集合与键选择器，返回新增/删除/未变三部分。
/// </summary>
public class ItemDiff
{
    public static ItemDiffResult<T> ComputeDiff<T, TKey>(
        IEnumerable<T> oldItems,
        IEnumerable<T> newItems,
        Func<T, TKey> keySelector,
        IEqualityComparer<TKey>? keyComparer = null)
        where TKey : notnull
    {
        if (oldItems == null) throw new ArgumentNullException(nameof(oldItems));
        if (newItems == null) throw new ArgumentNullException(nameof(newItems));
        if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

        var comparer = keyComparer ?? EqualityComparer<TKey>.Default;

        var oldDict = oldItems.ToDictionary(keySelector, comparer);
        var newDict = newItems.ToDictionary(keySelector, comparer);

        var oldKeys = new HashSet<TKey>(oldDict.Keys, comparer);
        var newKeys = new HashSet<TKey>(newDict.Keys, comparer);

        var addedKeys = new HashSet<TKey>(newKeys.Except(oldKeys, comparer), comparer);
        var removedKeys = new HashSet<TKey>(oldKeys.Except(newKeys, comparer), comparer);
        var commonKeys = new HashSet<TKey>(oldKeys.Intersect(newKeys, comparer), comparer);

        var added = addedKeys.Select(k => newDict[k]).ToList();
        var removed = removedKeys.Select(k => oldDict[k]).ToList();
        var unchanged = commonKeys.Select(k => newDict[k]).ToList();

        return new ItemDiffResult<T>(added, removed, unchanged);
    }
}
