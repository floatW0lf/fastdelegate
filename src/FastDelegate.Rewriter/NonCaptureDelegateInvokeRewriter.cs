using Mono.Cecil;
using Mono.Cecil.Cil;

namespace FastDelegate.Rewriter;

public class NonCaptureDelegateInvokeRewriter : IDelegateInvokeRewriter
{
    public bool CanHandle(MethodBody body, RewriterContext context)
    {
        return context.InvokeDelegates.Any(x => FindStaticClosureClass(body, x) != default || (context.CompleteReplaceCallInstructionMap.TryGetValue(x, out var exist) && FindStaticClosureClass(body, exist.NewCallInstruction) != default));
    }

    private const string StructLambdaNameTemplate = "NonCaptureLambda_";

    public void Rewrite(MethodBody body, RewriterContext context, CancellationToken token)
    {
        foreach (var invokeDelegate in context.InvokeDelegates)
        {
            token.ThrowIfCancellationRequested();

            var methodWithLambda = (MethodReference)invokeDelegate.Operand;

            var startIndex = body.Instructions.IndexOf(invokeDelegate);
            MethodCallReplaceInfo? existRewriteCallInfo = null;
            if (NotFound(startIndex) && context.CompleteReplaceCallInstructionMap.TryGetValue(invokeDelegate, out existRewriteCallInfo))
            {
                startIndex = body.Instructions.IndexOf(existRewriteCallInfo.NewCallInstruction);
            }
            if (NotFound(startIndex)) continue;
            
            var paramsInfo = new List<MethodParameterInfo>();
            foreach (var inlineLambda in methodWithLambda.GetInlineDelegates(context.InlineAttribute).Reverse())
            {
                token.ThrowIfCancellationRequested();
                
                var closureMethod = body.FindClosureMethod(startIndex);
                if (closureMethod == null) continue;
                
                var stub = Instruction.Create(OpCodes.Nop);
                
                if (!body.TryReplaceDelegateArgument(ref startIndex, new ReplaceContext(stub, inlineLambda.ParameterType, methodWithLambda, false))) continue;

                var lambda = CreateStruct(body.WrapperCounter(), closureMethod.Resolve(), context.CurrentAssembly);
                
                var interfaceDelegate = context.DelegateToInterfaceMap[inlineLambda.ParameterType.GetDefinition()];
                lambda.Methods.First().MakeMethodImplementInterface(interfaceDelegate.Resolve());
        
                body.Method.DeclaringType.NestedTypes.Add(lambda);
                var lambdaVar = new VariableDefinition(lambda);
                body.Variables.Add(lambdaVar);
                body.GetILProcessor().ReplaceWithCorrectionLabel(stub, Instruction.Create(OpCodes.Ldloca_S, lambdaVar));
                paramsInfo.Add(new MethodParameterInfo(inlineLambda.Index, lambda));
            }

            if (existRewriteCallInfo != null)   
            {
                existRewriteCallInfo.ParameterOrderMap.AddRange(paramsInfo);
                
                var genericArgs = existRewriteCallInfo.NewCallInstruction.Operand.As<GenericInstanceMethod>().GenericArguments;
                var fastCallArguments = genericArgs.Where(closure =>
                {
                    if (closure is GenericParameter gp)
                    {
                        return gp.Constraints.Select(x => x.ConstraintType.Resolve())
                            .Intersect(context.DelegateToInterfaceMap.Values.Select(x => x.Resolve())).Any();
                    }
                    return context.DelegateToInterfaceMap.Values.Any(fastInterface =>
                            closure.Resolve().DoesSpecificTypeImplementInterface(fastInterface.Resolve()));
                }).ToArray();
                
                genericArgs.RemoveRange(fastCallArguments);
                genericArgs.AddRange(existRewriteCallInfo.ParameterOrderMap.OrderBy(x => x.Index).Select(x => x.Type));
                
                continue;
            }
            
            paramsInfo.Reverse();
            var genericCall = Instruction.Create(invokeDelegate.OpCode, context.MethodReplaceMap[methodWithLambda].MakeGenericInstanceMethod(paramsInfo.Select(x => x.Type).ToArray()));
            body.GetILProcessor().ReplaceWithCorrectionLabel(invokeDelegate, genericCall);
            context.CompleteReplaceCallInstructionMap.Add(invokeDelegate, new MethodCallReplaceInfo(genericCall, paramsInfo));
        }
    }

    private bool NotFound(int index)
    {
        return index <= -1;
    }
    private static TypeReference? FindStaticClosureClass(MethodBody body, Instruction lambdaInvoke)
    {
        var index = body.Instructions.IndexOf(lambdaInvoke);
        for (int i = index - 1; i >= 0; i--)
        {
            var instruction = body.Instructions[i];
            if (instruction.OpCode == OpCodes.Stsfld 
                && instruction.Operand is FieldReference fieldReference 
                && fieldReference.DeclaringType.Name == "<>c")
            {
                return fieldReference.DeclaringType;
            }
        }
        return default;
    }

    private TypeDefinition CreateStruct(int index, MethodDefinition lambdaBody, AssemblyDefinition assembly)
    {
        var staticLambda = new TypeDefinition("", $"{StructLambdaNameTemplate}{index}",
            TypeAttributes.Sealed | TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit |
            TypeAttributes.SequentialLayout | TypeAttributes.NestedPrivate,
            assembly.MainModule.ImportReference(typeof(ValueType)));

        staticLambda.Methods.Add(lambdaBody.Clone());
        return staticLambda;
    }
}