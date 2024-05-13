namespace FastDelegate.Rewriter;

public static class LinqExtensions
{
    public static IEnumerable<T> Cancelable<T>(this IEnumerable<T> collection, CancellationToken cancellationToken)
    {
        return collection.Select(x =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return x;
        });
    }

    public static T As<T>(this object input) 
    {
        return (T)input;
    }
}