using Mono.Cecil;

namespace FastDelegate.Rewriter;

public interface IDelegateInterfaceGenerator
{
    public TypeDefinition Create(IReadOnlyList<TypeReference> delegateTypes, string typeNamespace, string typeName);
}