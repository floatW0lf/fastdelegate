using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace FastDelegate.Rewriter;

public record CreationContext(MethodReference TargetMethod, AssemblyDefinition Assembly)
{
    public bool IsClass { get; set; }
}

public record CreationContextImplementedInterface(MethodReference TargetMethod, AssemblyDefinition Assembly, TypeDefinition ImplementInterface, TypeReference DelegateType) : CreationContext(TargetMethod, Assembly);

public static class TypeFactory
{
    public const string CallWrapperTemplate = "CallWrapper_";
    public static TypeDefinition CreateCallWrapperAndAttach(TypeDefinition parent, TypeReference closure, CreationContext creationContext)
    {
        var attributes = TypeAttributes.Sealed | TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit |
                         TypeAttributes.SequentialLayout | TypeAttributes.NestedPrivate;

        if (creationContext.IsClass)
        {
            attributes |= TypeAttributes.Class;
        }

        var index = parent.NestedTypes.Count(x => x.FullName.Contains(CallWrapperTemplate));
        var wrapper = new TypeDefinition("", $"{CallWrapperTemplate}{index}",
            attributes, creationContext.IsClass ? creationContext.Assembly.MainModule.TypeSystem.Object : creationContext.Assembly.MainModule.ImportReference(typeof(ValueType)));

        if (creationContext.IsClass)    
        {
            var wrapperCtor = new MethodDefinition(".ctor", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.RTSpecialName | MethodAttributes.SpecialName, creationContext.Assembly.MainModule.TypeSystem.Void);
            wrapper.Methods.Add(wrapperCtor);
            var ilProcessor = wrapperCtor.Body.GetILProcessor();
            ilProcessor.Emit(OpCodes.Ldarg_0);
            ilProcessor.Emit(OpCodes.Call, creationContext.Assembly.MainModule.ImportReference(creationContext.Assembly.MainModule.TypeSystem.Object.Resolve().GetConstructors().Single(x => !x.IsStatic)));
            ilProcessor.Emit(OpCodes.Ret);
        }
        
        var copyTarget = creationContext.TargetMethod.Resolve().Clone();
        copyTarget.Body = new MethodBody(copyTarget);
        copyTarget.ImplAttributes = MethodImplAttributes.AggressiveInlining;

        var closureField = new FieldDefinition("ClosurePointer", FieldAttributes.Public, closure.MakePointerType());
        wrapper.Fields.Add(closureField);
        wrapper.Methods.Add(copyTarget);

        if (creationContext is CreationContextImplementedInterface specCtx)
        {
            copyTarget.MakeMethodImplementInterface(MakeGenericOrSimple(specCtx.ImplementInterface, specCtx.DelegateType, creationContext.TargetMethod));
        }

        var processor = copyTarget.Body.GetILProcessor();
        processor.Emit(OpCodes.Ldarg_0);
        processor.Emit(OpCodes.Ldfld, closureField);
        for (int i = 0; i < creationContext.TargetMethod.Parameters.Count; i++)
        {
            var methodArg = creationContext.TargetMethod.Parameters[i];
            processor.Emit(methodArg.ParameterType.IsByReference ? OpCodes.Ldarga : OpCodes.Ldarg, i + 1);
        }
        processor.Emit(OpCodes.Call, creationContext.TargetMethod);
        processor.Emit(OpCodes.Ret);
        
        parent.NestedTypes.Add(wrapper);
        
        return wrapper;
    }

    private static TypeReference MakeGenericOrSimple(TypeReference interfaceType, TypeReference delegateType, MethodReference genericArgSource)
    {
        if (delegateType is GenericInstanceType genericDelegate && genericArgSource is GenericInstanceMethod instanceMethod)
        {
            return interfaceType.MakeGenericInstanceType(genericDelegate.GenericArguments.Select(x =>
            {
                if (x is GenericParameter gp)
                {
                    return instanceMethod.GenericArguments[gp.Position];
                }
                return x;
            }).ToArray());
        }
        return interfaceType;
    }
}