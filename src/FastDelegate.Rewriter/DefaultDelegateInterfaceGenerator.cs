using Mono.Cecil;

namespace FastDelegate.Rewriter;

public class DefaultDelegateInterfaceGenerator : IDelegateInterfaceGenerator
{
    public TypeDefinition Create(IReadOnlyList<TypeReference> delegateTypes, string typeNamespace, string typeName)
    {
        var interfaceType = new TypeDefinition(typeNamespace, delegateTypes.Count == 1 ? $"IFast{delegateTypes[0].Name.SubstringFor('`')}" : $"IFast{typeName}Combined", TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract | TypeAttributes.AnsiClass | TypeAttributes.AutoLayout | TypeAttributes.BeforeFieldInit);
        
        for (var index = 0; index < delegateTypes.Count; index++)
        {
            var delegateType = delegateTypes[index];
            var definition = delegateType.Resolve();
            var methodDefinition = definition.Methods.First(m => m.Name == "Invoke");

            if (definition.HasGenericParameters)
            {
                interfaceType.GenericParameters.AddRange(definition.GenericParameters.Select(x => x.Clone(interfaceType)));
            }

            AddMethodToInterface(methodDefinition, delegateTypes.Count == 1 ? null : index, interfaceType);
        }

        return interfaceType;
    }

    private MethodDefinition ConvertGenericMethodToNonGeneric(MethodReference methodReference)
    {
        var genType = (GenericInstanceType)methodReference.DeclaringType;
        var returnType = methodReference.ReturnType;
        if (methodReference.ReturnType is GenericParameter genReturn)
        {
            returnType = genType.GenericArguments[genReturn.Position];
        }
        
        var methodDef = new MethodDefinition("_", MethodAttributes.Abstract | MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot, returnType);
        foreach (var parameter in methodReference.Parameters)
        {
            var methodParameter = parameter;
            if (parameter.ParameterType is GenericParameter gen)
            {
                methodParameter = new ParameterDefinition(parameter.Name, parameter.Attributes, genType.GenericArguments[gen.Position]);
            }
            methodDef.Parameters.Add(methodParameter);
        }

        return methodDef;
    }

    private static void AddMethodToInterface(MethodDefinition methodDefinition, int? counter, TypeDefinition interfaceType)
    {
        var copy = methodDefinition.Clone();
        copy.CustomAttributes.Clear(); // need import
        copy.ImplAttributes = MethodImplAttributes.Managed;
        copy.Attributes = MethodAttributes.Abstract | MethodAttributes.Public | MethodAttributes.Virtual |
                          MethodAttributes.HideBySig | MethodAttributes.NewSlot;

        copy.Name = counter.HasValue ? $"Invoke_{counter}" : "Invoke";
        copy.Body = null;
        interfaceType.Methods.Add(copy);
    }
}