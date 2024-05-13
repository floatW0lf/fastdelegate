namespace FastDelegate.Rewriter;

public static class CollectionExtensions
{
    public static int RemoveWhere<T>(this IList<T> collection, Func<T, bool> filter)
    {
        var removeCount = 0;
        for (var i = 0; i < collection.Count; i++)
        {
            var item = collection[i];
            if (filter(item) && collection.Remove(item))
            {
                removeCount++;
            }
        }
        return removeCount;
    }

    public static void RemoveRange<T>(this ICollection<T> collection, IEnumerable<T> other)
    {
        foreach (var item in other)
        {
            collection.Remove(item);
        }
    }

    public static void AddRange<T>(this ICollection<T> collection, IEnumerable<T> other)
    {
        foreach (var item in other)
        {
            collection.Add(item);
        }
    }
}