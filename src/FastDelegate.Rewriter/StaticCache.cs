namespace FastDelegate.Rewriter;

internal static class StaticCache<TKey, TValue> where TValue : class
{
    private static TValue? _element;
    public static TValue GetOrAdd<TContext>(TContext ctx, Func<TContext,TValue> factory)
    {
        if (Volatile.Read(ref _element) != null) return _element!;
      
        var value = factory(ctx);
        Interlocked.CompareExchange(ref _element, value, null);
        return value;
    }
}