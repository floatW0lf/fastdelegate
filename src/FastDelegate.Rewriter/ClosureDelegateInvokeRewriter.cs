using Mono.Cecil;
using Mono.Cecil.Cil;

namespace FastDelegate.Rewriter;
public class CaptureClosureDelegateInvokeRewriter : IDelegateInvokeRewriter
{
    public bool CanHandle(MethodBody body, RewriterContext context)
    {
        return context.InvokeDelegates.Any(x => body.FindClosureWithCaptureType(x) != null);
    }

    public void Rewrite(MethodBody body, RewriterContext context, CancellationToken token)
    {
        var processor = body.GetILProcessor();
        var closureType = body.FindClosureWithCaptureType(context.InvokeDelegates[0]);
        var closureTypeDefinition = closureType!.Resolve();
        closureTypeDefinition.MakeClosureAsStruct(context.CurrentAssembly);
        
        ReplaceClosureNewobjToInitobj(processor, closureTypeDefinition);
        
        var closureVar = body.Variables.First(x => x.VariableType.FullName == closureType.FullName);
        var sameMethods = new HashSet<MethodReference>(MethodReferenceEqualityComparer.Instance);

        foreach (var lambdaInvoke in context.InvokeDelegates)
        {
            token.ThrowIfCancellationRequested();
            var methodWithLambda = (MethodReference)lambdaInvoke.Operand;
            var genericCallMethod = context.MethodReplaceMap[methodWithLambda.GetDefinition()];

            var loadAddressClosure = Instruction.Create(OpCodes.Ldloca_S, body.Variables.First());

            var startIndex = body.Instructions.IndexOf(lambdaInvoke);

            var genLambdas = new List<MethodParameterInfo>();

            foreach (var inlineDelegate in methodWithLambda.GetInlineDelegates(context.InlineAttribute).Reverse())
            {
                token.ThrowIfCancellationRequested();
                var method = body.FindClosureMethod(startIndex);
                if (method == null) continue;
                
                var invokeMethod = method.Resolve();
                
                
                var fastCallInterface = context.DelegateToInterfaceMap[inlineDelegate.ParameterType.GetDefinition()].Resolve();
                
                if (!body.TryReplaceDelegateArgument(ref startIndex, new ReplaceContext(loadAddressClosure, inlineDelegate.ParameterType, methodWithLambda, true))) continue;
                

                if (sameMethods.Add(method))
                {
                    invokeMethod.MakeMethodImplementInterface(fastCallInterface);
                    genLambdas.Add(new MethodParameterInfo(inlineDelegate.Index, closureType));
                }
                else
                {
                    var wrapper = TypeFactory.CreateCallWrapperAndAttach(body.Method.DeclaringType, closureType, new CreationContextImplementedInterface(method, context.CurrentAssembly, fastCallInterface, inlineDelegate.ParameterType));
                    var wrapperVar = new VariableDefinition(wrapper);
                    body.Variables.Add(wrapperVar);

                    var wrapperBlockCode = new[]
                    {
                        Instruction.Create(OpCodes.Ldloca_S, wrapperVar),
                        Instruction.Create(OpCodes.Ldloca_S, closureVar),
                        Instruction.Create(OpCodes.Conv_U),
                        Instruction.Create(OpCodes.Stfld, wrapper.Fields.First()),
                        Instruction.Create(OpCodes.Ldloca_S, wrapperVar)
                    };
                    processor.InsertBlockAfter(loadAddressClosure, wrapperBlockCode);
                    processor.Remove(loadAddressClosure);
                    genLambdas.Add(new MethodParameterInfo(inlineDelegate.Index, wrapper));
                }
            }

            genLambdas.Reverse();
            
            var genericCall = Instruction.Create(lambdaInvoke.OpCode, genericCallMethod.MakeGenericInstanceMethod(GetGenericArguments(methodWithLambda, genLambdas.Select(x => x.Type).ToArray())));
            processor.ReplaceWithCorrectionLabel(lambdaInvoke, genericCall);
            context.CompleteReplaceCallInstructionMap.Add(lambdaInvoke, new MethodCallReplaceInfo(genericCall, genLambdas));
        }
        ReplaceByValueToByRef(processor, closureType);
        closureTypeDefinition.Methods.RemoveWhere(x => x.IsConstructor);
    }

    private TypeReference[] GetGenericArguments(MethodReference originLambdaMethod, IReadOnlyList<TypeReference> structClosureTypes)
    {
        if (originLambdaMethod is GenericInstanceMethod gn)
        {
            return gn.GenericArguments.Concat(structClosureTypes).ToArray();
        }
        return structClosureTypes.ToArray();
    }
    
    /*
     newobj ctorCall
     stloc.*
     
     ldloca.S
     initobj ...            
    */
    private void ReplaceClosureNewobjToInitobj(ILProcessor processor, TypeDefinition closureType)
    {
        var body = processor.Body;
        var newClosure = body.Instructions.First(x =>
            x.OpCode == OpCodes.Newobj && x.Operand is MethodReference mr &&
            mr.DeclaringType.FullName == closureType.FullName);

        var init = processor.Create(OpCodes.Initobj, closureType);
        processor.ReplaceWithCorrectionLabel(newClosure, init);
        processor.InsertBefore(init, LoadLocalVarAddressToStack(processor, closureType));
        processor.Remove(init.Next);
    }
    private Instruction LoadLocalVarAddressToStack(ILProcessor processor, TypeReference closureVarType)
    {
        var vars = processor.Body.Variables;
        for (int i = 0; i < vars.Count; i++)
        {
            var variable = vars[i];
            if (variable.VariableType.FullName == closureVarType.FullName)
            {
                return processor.Create(OpCodes.Ldloca_S, variable);
            }
        }
        throw new NotSupportedException();
    }
    private void ReplaceByValueToByRef(ILProcessor processor, TypeReference closureType)
    {
        var instructions = processor.Body.Instructions;
        for (int i = 0; i < instructions.Count; i++)
        {
            var item = instructions[i];
            if (item.OpCode == OpCodes.Stfld && 
                item.Operand is FieldReference stReference && 
                closureType.FullName == stReference.DeclaringType.FullName)
            {
                var firstArgInstruction = item.Previous.Previous;
                processor.ReplaceWithCorrectionLabel(firstArgInstruction, processor.Create(OpCodes.Ldloca_S, processor.Body.GetVarFromLoadLocalVariable(firstArgInstruction)));
            }

            if (item.OpCode == OpCodes.Ldfld && 
                item.Operand is FieldReference ldReference &&
                closureType.FullName == ldReference.DeclaringType.FullName)
            {
                processor.ReplaceWithCorrectionLabel(item.Previous, processor.Create(OpCodes.Ldloca_S, processor.Body.GetVarFromLoadLocalVariable(item.Previous)));
            }
        }
    }

}