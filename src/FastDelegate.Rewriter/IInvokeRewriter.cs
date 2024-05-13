using Mono.Cecil;
using Mono.Cecil.Cil;

namespace FastDelegate.Rewriter;

public record struct MethodParameterInfo(int Index, TypeReference Type);
public record MethodCallReplaceInfo(Instruction NewCallInstruction, List<MethodParameterInfo> ParameterOrderMap);
public record RewriterContext(
    AssemblyDefinition CurrentAssembly,
    TypeReference InlineAttribute,
    IReadOnlyList<Instruction> InvokeDelegates,
    IReadOnlyDictionary<TypeReference, TypeReference> DelegateToInterfaceMap,
    IReadOnlyDictionary<MethodReference, MethodDefinition> MethodReplaceMap,
    Dictionary<Instruction,MethodCallReplaceInfo> CompleteReplaceCallInstructionMap);

public interface IDelegateInvokeRewriter
{
    bool CanHandle(MethodBody body, RewriterContext context);
    void Rewrite(MethodBody body, RewriterContext context, CancellationToken token);
}