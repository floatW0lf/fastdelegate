using Mono.Cecil;

namespace FastDelegate.Rewriter;

public class MethodReferenceEqualityComparer : IEqualityComparer<MethodReference>
{
    private MethodReferenceEqualityComparer()
    {
    }

    public static MethodReferenceEqualityComparer Instance { get; } = new MethodReferenceEqualityComparer();

    public bool Equals(MethodReference? x, MethodReference? y)
    {
        if (ReferenceEquals(x, null) || ReferenceEquals(y, null)) return false;
        if (ReferenceEquals(x, y)) return true;
        return x.SameSignature(y);
    }

    public int GetHashCode(MethodReference obj)
    {
        return obj.Parameters.Skip(1).Aggregate(obj.Parameters.First().ParameterType.FullName.GetHashCode(), (i, definition) => HashCode.Combine(i, definition.ParameterType.FullName));
    }
}