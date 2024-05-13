using Mono.Cecil;

namespace FastDelegate.Rewriter;

public class ParameterEqualityComparer : IEqualityComparer<ParameterReference>
{
    private ParameterEqualityComparer()
    {
    }

    public static IEqualityComparer<ParameterReference> Instance { get; } = new ParameterEqualityComparer();

    public bool Equals(ParameterReference? x, ParameterReference? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (ReferenceEquals(x, null)) return false;
        if (ReferenceEquals(y, null)) return false;
            
        return x.ParameterType.FullName == y.ParameterType.FullName;
    }

    public int GetHashCode(ParameterReference obj)
    {
        return HashCode.Combine(obj.ParameterType.FullName);
    }
}