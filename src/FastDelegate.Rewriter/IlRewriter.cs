using FastDelegate.Attributes;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
namespace FastDelegate.Rewriter;
public class ILRewriter : IDisposable
{
    private readonly AssemblyDefinition _assemblyDefinition;
    private readonly TypeReference _inlineAttribute;
    private readonly Dictionary<MethodReference, MethodDefinition> _replaceMap = new();
    private readonly Dictionary<TypeReference, TypeReference> _delegateToInterfaceMap = new(comparer:TypeReferenceComparer.Instance);
    private readonly IDelegateInvokeRewriter[] _chainInvokeRewriter;
    private readonly IDelegateInterfaceGenerator _interfaceGenerator;

    private record ReplaceContext(ParameterDefinition Parameter, int Index, TypeReference Constraint);
    
    public ILRewriter(AssemblyDefinition assemblyDefinition, IDelegateInterfaceGenerator interfaceGenerator, IDelegateInvokeRewriter[] chainInvokeRewriter)
    {
        _chainInvokeRewriter = chainInvokeRewriter;
        _interfaceGenerator = interfaceGenerator;
        _assemblyDefinition = assemblyDefinition;
        _inlineAttribute = _assemblyDefinition.MainModule.ImportReference(typeof(InlineAttribute));
    }

    public void Rewrite(CancellationToken cancellationToken)
    {
        var methodWithInlineAttributes = _assemblyDefinition.MainModule.GetAllTypes().SelectMany(t => t.Methods)
            .Where(m => m.Parameters.Any(p =>
                p.CustomAttributes.Any(c => c.AttributeType.FullName == _inlineAttribute.FullName)));

        var structConstrain = _assemblyDefinition.MainModule.ImportReference(typeof(ValueType));

        foreach (var origin in methodWithInlineAttributes.ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var methodDef = origin.Clone();
            methodDef.CallingConvention = MethodCallingConvention.Generic;
            methodDef.IsReuseSlot = true;

            var genericParameterInfo = methodDef.Parameters
                .Cancelable(cancellationToken)
                .Where(p => p.CustomAttributes.Any(c => c.AttributeType.FullName == _inlineAttribute.FullName))
                .Select((p, index) =>
                {
                    var fastCallDelegateConstrain = _assemblyDefinition.MainModule.ImportReference(FuncToIFunctionConverter(p.ParameterType, origin));

                    fastCallDelegateConstrain = fastCallDelegateConstrain.HasGenericParameters ? fastCallDelegateConstrain.MakeGenericInstanceType(p.ParameterType.As<GenericInstanceType>().GenericArguments.ToArray()) : fastCallDelegateConstrain;
                    
                    var genericParameter = new GenericParameter(methodDef)
                    {
                        Name = $"TLambda_{index}", 
                        HasNotNullableValueTypeConstraint = true,
                        HasDefaultConstructorConstraint = true,
                        Constraints =
                        {
                            new GenericParameterConstraint(structConstrain), 
                            new GenericParameterConstraint(fastCallDelegateConstrain),
                        }
                    };
                    methodDef.GenericParameters.Add(genericParameter);
                    return new ReplaceContext(new ParameterDefinition(genericParameter.MakeByReferenceType()) { Name = p.Name }, p.Index, fastCallDelegateConstrain);
                }).ToArray();

            RewriteBodyCalledDelegate(methodDef, genericParameterInfo, cancellationToken);
            origin.DeclaringType.Methods.Add(methodDef);
            _replaceMap.Add(origin, methodDef);
        }

        var methods = ScanCallMethod(cancellationToken);
        foreach (var method in methods)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RewriteCallFastDelegate(method, cancellationToken);
        }
    }

    private MethodBody[] ScanCallMethod(CancellationToken token)
    {
        return _assemblyDefinition.MainModule.GetAllTypes()
            .Cancelable(token)
            .SelectMany(t => t.Methods)
            .Where(m => m.HasBody)
            .Select(m => m.Body)
            .Where(b => b.Instructions.Any(instruction => (instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt) &&
                instruction.Operand is MethodReference mr && mr.Resolve()?.Parameters.Any(d => d.CustomAttributes.Any(a => a.AttributeType.FullName == _inlineAttribute.FullName)) == true))
            .ToArray();
    }

    private void RewriteCallFastDelegate(MethodBody body, CancellationToken token)
    {
        var completeReplaceCallInstructionMap = new Dictionary<Instruction, MethodCallReplaceInfo>();
        
        var invokeDelegates = body.Instructions.Where(instruction =>
            (instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt) &&
            instruction.Operand is MethodReference mr && mr.Parameters.Any(d =>
                d.CustomAttributes.Any(a => a.AttributeType.FullName == _inlineAttribute.FullName))).ToArray();
        
        var context = new RewriterContext(_assemblyDefinition, _inlineAttribute, invokeDelegates, _delegateToInterfaceMap, _replaceMap, completeReplaceCallInstructionMap);
        foreach (var rewriter in _chainInvokeRewriter)
        {
            if (rewriter.CanHandle(body, context))
            {
                rewriter.Rewrite(body, context, token);
            }
        }
    }
    public void SaveChanges(string assemblyPath)
    {
        _assemblyDefinition.Write(assemblyPath);
    }

    private void RewriteBodyCalledDelegate(MethodDefinition methodDef, ReplaceContext[] replace, CancellationToken token)
    {
        var ilProcessor = methodDef.Body.GetILProcessor();

        foreach (var newParameter in replace)
        {
            token.ThrowIfCancellationRequested();
            var exist = methodDef.Parameters[newParameter.Index];

            methodDef.Parameters[newParameter.Index] = newParameter.Parameter;

            var delegateCall = ilProcessor.Body.Instructions.First(instruction =>
                instruction.OpCode == OpCodes.Callvirt && instruction.Operand is MethodReference r &&
                r.DeclaringType.FullName == exist.ParameterType.FullName);
            
            var genericMethodRef = _assemblyDefinition.MainModule.ImportReference(newParameter.Constraint.Resolve()
                .Methods
                .First());
            
            var genericCall = ilProcessor.Create(OpCodes.Callvirt, newParameter.Constraint is GenericInstanceType instanceType ? genericMethodRef.MakeHostInstanceGeneric(instanceType) : genericMethodRef);

            ilProcessor.ReplaceWithCorrectionLabel(delegateCall, genericCall);

            var constrain =
                ilProcessor.Create(OpCodes.Constrained, newParameter.Parameter.ParameterType.GetElementType());
            ilProcessor.InsertBefore(genericCall, constrain);
        }
    }

    private TypeReference FuncToIFunctionConverter(TypeReference delegateType, MethodReference targetCall)
    {
        delegateType = delegateType.GetDefinition();
        
        if (_delegateToInterfaceMap.TryGetValue(delegateType, out var interfaceType))
        {
            return interfaceType;
        }
        var type = _interfaceGenerator.Create(new [] { delegateType }, targetCall.DeclaringType.Namespace, targetCall.Name);
        _assemblyDefinition.MainModule.Types.Add(type);
        
        _delegateToInterfaceMap.Add(delegateType, type);
        return type;
    }

    public void Dispose()
    {
        _assemblyDefinition.Dispose();
    }
}
