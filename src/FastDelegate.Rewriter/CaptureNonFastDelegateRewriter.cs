using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace FastDelegate.Rewriter;

public class CaptureNonFastDelegateRewriter : IDelegateInvokeRewriter
{
    public bool CanHandle(MethodBody body, RewriterContext context)
    {
        return body.Instructions.Any(x => NonInlineDelegate(context, x));
    }

    private static bool NonInlineDelegate(RewriterContext context, Instruction x)
    {
        return (x.OpCode == OpCodes.Callvirt || x.OpCode == OpCodes.Call) 
               && x.Operand is MethodReference mr && 
               mr.Parameters.Any(definition => definition.ParameterType.IsDelegate()
                                               && NonInline(context, definition));
    }

    private static bool NonInline(RewriterContext context, ParameterDefinition definition)
    {
        return (!definition.CustomAttributes.Any() || definition.CustomAttributes.All(a => a.AttributeType.FullName != context.InlineAttribute.FullName));
    }

    public void Rewrite(MethodBody body, RewriterContext context, CancellationToken token)
    {
        var processor = body.GetILProcessor();
        foreach (var callInstruction in body.Instructions.Where(x => NonInlineDelegate(context, x)).ToArray())
        {
            var withCaptureClosure = body.FindClosureWithCaptureType(callInstruction, true);
            if (withCaptureClosure == null) continue;
            var closureVar = body.Variables.First(x => x.VariableType.FullName == withCaptureClosure.FullName);

            var instructionPosition = body.Instructions.IndexOf(callInstruction) - 1;
            
            foreach (var parameter in callInstruction.Operand.As<MethodReference>().Parameters.Reverse())
            {
                if (parameter.ParameterType.IsDelegate() && NonInline(context, parameter))
                {
                    var method = body.FindClosureMethod(instructionPosition);
                    var wrapper = TypeFactory.CreateCallWrapperAndAttach(body.Method.DeclaringType, withCaptureClosure, new CreationContext(method, context.CurrentAssembly){IsClass = true});
                    var wrapperVar = new VariableDefinition(wrapper);
                    body.Variables.Add(wrapperVar);
                    ReplaceLdftnArg(body, ref instructionPosition, method, wrapper.Methods.First(x => !x.IsConstructor));
                    
                    var wrapperBlockCode = new[]
                    {
                        Instruction.Create(OpCodes.Newobj, wrapper.GetConstructors().Single(x => !x.IsStatic)),
                        Instruction.Create(OpCodes.Dup),
                        Instruction.Create(OpCodes.Stloc_S, wrapperVar),
                        Instruction.Create(OpCodes.Ldloca_S, closureVar),
                        Instruction.Create(OpCodes.Conv_U),
                        Instruction.Create(OpCodes.Stfld, wrapper.Fields.First()),
                        Instruction.Create(OpCodes.Ldloc_S, wrapperVar)
                    };
                    var loadClosure = body.Instructions[instructionPosition];
                    processor.InsertBlockAfter(loadClosure, wrapperBlockCode);
                    processor.Remove(loadClosure);
                }
            }
            
        }
    }

    private void ReplaceLdftnArg(MethodBody body, ref int startPosition, MethodReference origin, MethodReference wrapper)
    {
        for (int i = startPosition; i >= 0; i--)
        {
            var instruction = body.Instructions[i];
            if (instruction.OpCode == OpCodes.Ldftn && instruction.Operand == origin)
            {
                instruction.Operand = wrapper;
                startPosition = i - 1;
                return;
            }
        }
    }
}