using Mono.Cecil;
using Mono.Cecil.Rocks;

namespace FastDelegate.Rewriter;

public static class MethodDefinitionExtensions
{
    public static MethodReference GetDefinition(this MethodReference methodReference)
    {
        return methodReference is GenericInstanceMethod ? methodReference.Resolve() : methodReference;
    }
    public static void MakeMethodImplementInterface(this MethodDefinition method, TypeReference implInterface)
    {
        var type = method.DeclaringType.Resolve();

        type.Interfaces.Add(new InterfaceImplementation(implInterface.MakeGenericInstanceFromSource(method)));

        method.Name = implInterface.Resolve().Methods.First().Name;
        method.IsVirtual = true;
        method.IsNewSlot = true;
        method.IsFinal = true;
        method.IsHideBySig = true;
        method.IsPublic = true;
    }

    public static TypeReference MakeGenericInstanceFromSource(this TypeReference type, MethodReference genericArgSource)
    {
        if (type.HasGenericParameters)
        {
            var typeParams = genericArgSource.Parameters.Select(x => x.ParameterType);
            if (genericArgSource.ReturnType.FullName != typeof(void).FullName)
            {
                typeParams = typeParams.Append(genericArgSource.ReturnType);
            }

            return type.MakeGenericInstanceType(typeParams.ToArray());
        }
        return type;
    }

    public static bool SameSignature(this MethodReference a, MethodReference b)
    {
        if (a.Parameters.Count != b.Parameters.Count 
            || a.ReturnType.FullName != b.ReturnType.FullName) return false;

        return a.Parameters.SequenceEqual(b.Parameters, ParameterEqualityComparer.Instance);
    }

}