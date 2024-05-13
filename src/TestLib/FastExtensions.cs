using FastDelegate.Attributes;

namespace TestLib;

public static class FastExtensions
{
    public static void FastForeach<TItem,TOther>(this IReadOnlyList<TItem> list, [Inline] Action<TItem> action, TOther value)
    {
        for (int i = 0; i < list.Count; i++)
        {
            action(list[i]);
        }
    }
}