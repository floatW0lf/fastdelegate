using Mono.Cecil;

namespace FastDelegate.Rewriter;

public class TypeReferenceComparer : IEqualityComparer<TypeReference>
{
    private TypeReferenceComparer()
    {
    }

    public static IEqualityComparer<TypeReference> Instance { get; } = new TypeReferenceComparer();

    public bool Equals(TypeReference? x, TypeReference? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (ReferenceEquals(x, null)) return false;
        if (ReferenceEquals(y, null)) return false;
        return x.FullName == y.FullName;
    }

    public int GetHashCode(TypeReference obj)
    {
        return (obj.FullName != null ? obj.FullName.GetHashCode() : 0);
    }
}