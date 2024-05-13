using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Mono.Collections.Generic;

namespace FastDelegate.Rewriter;

public record ReplaceContext(Instruction NewInstruction, TypeReference DelegateType, MethodReference Invoked,
    bool Capture);

public static class ReplaceExtensions
{
    public static bool TryReplaceDelegateArgument(this MethodBody caller, ref int searchIndex,
        ReplaceContext replaceContext)
    {
        var result = FindDelegateCreation(caller, searchIndex,
            MakeGenericFromOwnerOrSkip(replaceContext.DelegateType, replaceContext.Invoked));
        if (result == default)
        {
            return false;
        }

        var (instruction, deleteCount, captured) = result;

        if (captured != replaceContext.Capture)
        {
            searchIndex -= deleteCount;
            return false;
        }

        caller.Instructions.RemoveUpRange(instruction, deleteCount);
        caller.GetILProcessor().ReplaceWithCorrectionLabel(instruction, replaceContext.NewInstruction);
        searchIndex = caller.Instructions.IndexOf(replaceContext.NewInstruction);
        return true;
    }

    public static IReadOnlyList<ParameterDefinition> GetInlineDelegates(this MethodReference methodReference,
        TypeReference inlineAttribute)
    {
        return methodReference.Parameters
            .Where(d => d.CustomAttributes.Any(a => a.AttributeType.FullName == inlineAttribute.FullName))
            .ToArray();
    }

    public static TypeReference MakeGenericFromOwnerOrSkip(this TypeReference type, MethodReference owner)
    {
        if (type is GenericInstanceType gt && gt.GenericArguments.Any(t => t is GenericParameter))
        {
            var def = type.Resolve();
            var args = gt.GenericArguments.Select(x =>
            {
                if (x is GenericParameter gp)
                {
                    return gp.Type switch
                    {
                        GenericParameterType.Type => owner.DeclaringType.As<GenericInstanceType>()
                            .GenericArguments[gp.Position],
                        GenericParameterType.Method => owner.As<GenericInstanceMethod>().GenericArguments[gp.Position],
                        _ => throw new ArgumentOutOfRangeException(nameof(gp.Type))
                    };
                }

                return x;
            });
            return def.MakeGenericInstanceType(args.ToArray());
        }

        return type;
    }

    private static void RemoveUpRange(this Collection<Instruction> instructions, Instruction current, int count)
    {
        var start = instructions.IndexOf(current);
        for (int i = start - 1, deleted = 0; i >= 0 && deleted < count; i--, deleted++)
        {
            instructions.RemoveAt(i);
        }
    }

    private static (Instruction current, int deleteCount, bool captured) FindDelegateCreation(MethodBody body,
        int start, TypeReference delegateType)
    {
        var instructions = body.Instructions;
        for (int i = start - 1; i >= 0; i--)
        {
            var instruction = instructions[i];

            if (CaptureClosureLoadFromVar(body, delegateType, instruction))
            {
                return (instruction, 12, true);
            }

            if (CaptureClosureWithCache(delegateType, instruction))
            {
                return (instruction, 10, true);
            }

            if (NonCaptureClosure(delegateType, instruction))
            {
                return (instruction, 8, false);
            }

            if (CaptureClosureWithoutCache(delegateType, instruction))
            {
                return (instruction, 2, true);
            }
        }

        return default;
    }

    private static bool CaptureClosureLoadFromVar(MethodBody body, TypeReference delegateType, Instruction instruction)
    {
        return instruction.IsLoadVariableInstruction()
               && body.GetVarFromLoadLocalVariable(instruction) is { } variableDefinition
               && variableDefinition.VariableType.FullName == delegateType.FullName;
    }

    private static bool CaptureClosureWithCache(TypeReference delegateType, Instruction instruction)
    {
        return instruction.OpCode == OpCodes.Stfld && instruction.Operand is FieldReference instanceField &&
               instanceField.FieldType.FullName == delegateType.FullName;
    }

    private static bool NonCaptureClosure(TypeReference delegateType, Instruction instruction)
    {
        return instruction.OpCode == OpCodes.Stsfld && instruction.Operand is FieldReference staticField &&
               staticField.FieldType.FullName == delegateType.FullName;
    }

    private static bool CaptureClosureWithoutCache(TypeReference delegateType, Instruction instruction)
    {
        return instruction.OpCode == OpCodes.Newobj && instruction.Operand is MethodReference ctor &&
               ctor.DeclaringType.FullName == delegateType.FullName;
    }
}