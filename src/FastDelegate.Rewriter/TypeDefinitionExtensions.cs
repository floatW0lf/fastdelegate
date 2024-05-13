using System.Diagnostics;
using Mono.Cecil;
using Mono.Cecil.Rocks;

namespace FastDelegate.Rewriter;

internal static class TypeDefinitionExtensions
{
   /// <summary>
   /// Is childTypeDef a subclass of parentTypeDef. Does not test interface inheritance
   /// </summary>
   /// <param name="childTypeDef"></param>
   /// <param name="parentTypeDef"></param>
   /// <returns></returns>
   public static bool IsSubclassOf(this TypeDefinition childTypeDef, TypeDefinition parentTypeDef) => 
      childTypeDef.MetadataToken 
          != parentTypeDef.MetadataToken 
          && childTypeDef
         .EnumerateBaseClasses()
         .Any(b => b.MetadataToken == parentTypeDef.MetadataToken);

   /// <summary>
   /// Does childType inherit from parentInterface
   /// </summary>
   /// <param name="childType"></param>
   /// <param name="parentInterfaceDef"></param>
   /// <returns></returns>
   public static bool DoesAnySubTypeImplementInterface(this TypeDefinition childType, TypeDefinition parentInterfaceDef)
   {
      Debug.Assert(parentInterfaceDef.IsInterface);
      return childType
     .EnumerateBaseClasses()
     .Any(typeDefinition => typeDefinition.DoesSpecificTypeImplementInterface(parentInterfaceDef));
   }

   /// <summary>
   /// Does the childType directly inherit from parentInterface. Base
   /// classes of childType are not tested
   /// </summary>
   /// <param name="childTypeDef"></param>
   /// <param name="parentInterfaceDef"></param>
   /// <returns></returns>
   public static bool DoesSpecificTypeImplementInterface(this TypeDefinition childTypeDef, TypeDefinition parentInterfaceDef)
   {
      Debug.Assert(parentInterfaceDef.IsInterface);
      return childTypeDef
     .Interfaces
     .Any(imp => DoesSpecificInterfaceImplementInterface(imp.InterfaceType.Resolve(), parentInterfaceDef));
   }

   /// <summary>
   /// Does interface iface0 equal or implement interface iface1
   /// </summary>
   /// <param name="iface0"></param>
   /// <param name="iface1"></param>
   /// <returns></returns>
   public static bool DoesSpecificInterfaceImplementInterface(TypeDefinition iface0, TypeDefinition iface1)
   {
     Debug.Assert(iface1.IsInterface);
     Debug.Assert(iface0.IsInterface);
     return iface0.MetadataToken == iface1.MetadataToken || iface0.DoesAnySubTypeImplementInterface(iface1);
   }

   /// <summary>
   /// Is source type assignable to target type
   /// </summary>
   /// <param name="target"></param>
   /// <param name="source"></param>
   /// <returns></returns>
   public static bool IsAssignableFrom(this TypeDefinition target, TypeDefinition source) 
  => target == source 
     || target.MetadataToken == source.MetadataToken 
     || source.IsSubclassOf(target)
     || target.IsInterface && source.DoesAnySubTypeImplementInterface(target);

   /// <summary>
   /// Enumerate the current type, it's parent and all the way to the top type
   /// </summary>
   /// <param name="klassType"></param>
   /// <returns></returns>
   public static IEnumerable<TypeDefinition> EnumerateBaseClasses(this TypeDefinition klassType)
   {
      for (var typeDefinition = klassType; typeDefinition != null; typeDefinition = typeDefinition.BaseType?.Resolve())
      {
         yield return typeDefinition;
      }
   }
   
   public static MethodReference MakeHostInstanceGeneric(this MethodReference self, GenericInstanceType genericType)
   {
      return MakeHostInstanceGeneric(self, genericType.GenericArguments.ToArray());
   }
   
   public static MethodReference MakeHostInstanceGeneric(this MethodReference self, params TypeReference[] types)
   {
      var reference = new MethodReference(self.Name, self.ReturnType, self.DeclaringType.MakeGenericInstanceType(types))
      {
         HasThis = self.HasThis,
         ExplicitThis = self.ExplicitThis,
         CallingConvention = self.CallingConvention
      };

      foreach (var p in self.Parameters)
         reference.Parameters.Add(new ParameterDefinition(p.ParameterType));
   
      foreach (var gp in self.GenericParameters)
         reference.GenericParameters.Add(new GenericParameter(gp.Name, reference));

      return reference;
   }

   public static TypeReference GetDefinition(this TypeReference typeReference)
   {
      return typeReference is GenericInstanceType ? typeReference.Resolve() : typeReference;
   }
   
   public static MethodReference MakeGenericInstanceMethod(this MethodReference self, params TypeReference[] args)
   {
      var genericInstanceMethod = new GenericInstanceMethod(self);
      foreach (var arg in args)
      {
         genericInstanceMethod.GenericArguments.Add(arg);
      }
      return genericInstanceMethod;
   }
   
   public static void MakeClosureAsStruct(this TypeDefinition def, AssemblyDefinition assemblyDefinition)
   {
      def.Attributes = TypeAttributes.Sealed | TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit | TypeAttributes.SequentialLayout | TypeAttributes.NestedPrivate | TypeAttributes.Public | TypeAttributes.NestedPublic;
      def.BaseType = StaticCache<ValueType,TypeReference>.GetOrAdd(assemblyDefinition, x => x.MainModule.ImportReference(typeof(ValueType)));
   }
   public static bool IsDelegate(this TypeReference typeReference)
   {
      var multicastDelegateDefinition = StaticCache<MulticastDelegate,TypeDefinition>.GetOrAdd(typeReference, ctx => ctx.Module.ImportReference(typeof(MulticastDelegate)).Resolve());
      return typeReference.Resolve()?.BaseType == multicastDelegateDefinition;
   }
}