using Mono.Cecil;
using Mono.Cecil.Cil;

namespace FastDelegate.Rewriter;

public static class MethodBodyExtensions
{
    private static readonly Dictionary<OpCode, int> MapLdlocToVarPos = new()
    {
        [OpCodes.Ldloc_0] = 0,
        [OpCodes.Ldloc_1] = 1,
        [OpCodes.Ldloc_2] = 2,
        [OpCodes.Ldloc_3] = 3
    };

    private static readonly HashSet<OpCode> LoadVarInstr = new HashSet<OpCode>()
    {
        OpCodes.Ldloc,
        OpCodes.Ldloc_0,
        OpCodes.Ldloc_1,
        OpCodes.Ldloc_2,
        OpCodes.Ldloc_3,
        OpCodes.Ldloc_S
    };

    public static VariableDefinition? GetVarFromLoadLocalVariable(this MethodBody body, Instruction instruction)
    {
        if (MapLdlocToVarPos.TryGetValue(instruction.OpCode, out var varIndex))
        {
            return body.Variables[varIndex];
        }

        return instruction.Operand as VariableDefinition;
    }

    public static void ReplaceWithCorrectionLabel(this ILProcessor processor, Instruction target,
        Instruction instruction)
    {
        var labels = processor.Body.Instructions.Where(x =>
            x.OpCode.FlowControl is FlowControl.Branch or FlowControl.Cond_Branch && x.Operand == target).ToArray();
        processor.Replace(target, instruction);
        foreach (var label in labels)
        {
            label.Operand = instruction;
        }
    }

    public static void InsertBlockAfter(this ILProcessor processor, Instruction target,
        IReadOnlyList<Instruction> block)
    {
        var targetIndex = processor.Body.Instructions.IndexOf(target);
        for (int i = 0; i < block.Count; i++)
        {
            var instr = block[i];
            processor.InsertAfter(targetIndex + i, instr);
        }
    }

    public static bool IsLoadVariableInstruction(this Instruction target)
    {
        return LoadVarInstr.Contains(target.OpCode);
    }

    public static int WrapperCounter(this MethodBody body)
    {
        return body.Method.DeclaringType.NestedTypes.Count(x => x.FullName.Contains(TypeFactory.CallWrapperTemplate));
    }

    public static MethodReference? FindClosureMethod(this MethodBody body, int startIndex)
    {
        for (int i = startIndex - 1; i >= 0; i--)
        {
            var instr = body.Instructions[i];
            if (instr.OpCode == OpCodes.Ldftn && instr.Operand is MethodReference methodReference)
            {
                return methodReference;
            }
        }

        return null;
    }
    
    public static TypeReference? FindClosureWithCaptureType(this MethodBody body, Instruction lambdaInvoke, bool structClosure = false)
    {
        var index = body.Instructions.IndexOf(lambdaInvoke);
        for (int i = index - 1; i >= 0; i--)
        {
            var instruction = body.Instructions[i];
            if (structClosure && instruction.OpCode == OpCodes.Initobj && instruction.Operand is TypeReference tr 
                && tr.DeclaringType.FullName.Contains("<>c__DisplayClass"))
            {
                return tr;
            }
            if (instruction.OpCode == OpCodes.Newobj && 
                                instruction.Operand is MethodReference methodReference && 
                                methodReference.DeclaringType.FullName.Contains("<>c__DisplayClass"))
            {
                return methodReference.DeclaringType;
            }

            if (instruction.OpCode == OpCodes.Stfld 
                && instruction.Operand is FieldReference fieldReference 
                && fieldReference.DeclaringType.FullName.Contains("<>c__DisplayClass"))
            {
                return fieldReference.DeclaringType;
            }
        }
        return default;
    }
}
 