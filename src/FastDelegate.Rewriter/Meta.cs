using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Mono.Cecil;

namespace FastDelegate.Rewriter;

struct PrivateCorlibFixerMixin
    {
        const string SystemPrivateCoreLib = "System.Private.CoreLib";
        AssemblyNameReference _runtimeLib;

        public PrivateCorlibFixerMixin(ModuleDefinition module)
        {
            _runtimeLib = AssemblyNameReference.Parse("System.Runtime, Version=7.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            if (!module.AssemblyReferences.Contains(_runtimeLib))
                module.AssemblyReferences.Add(_runtimeLib);
        }

        internal bool TryMapAssemblyName(string candidateAssemblyName, [NotNullWhen(true)] out AssemblyNameReference? correctCorlibReference)
        {
            correctCorlibReference = null;
            if (_runtimeLib == null || candidateAssemblyName != SystemPrivateCoreLib)
                return false;

            correctCorlibReference = _runtimeLib;
            return true;
        }
    }
    
    public class SystemPrivateCoreLibFixerMetadataImporterProvider : IMetadataImporterProvider
    {
        public IMetadataImporter GetMetadataImporter(ModuleDefinition module) => new SystemPrivateCoreLibFixerMetadataImporter(module);
    }

    internal class SystemPrivateCoreLibFixerMetadataImporter : DefaultMetadataImporter
    {
        private PrivateCorlibFixerMixin importerMixin;
        
        public SystemPrivateCoreLibFixerMetadataImporter(ModuleDefinition module) : base(module)
        {
            importerMixin = new PrivateCorlibFixerMixin(module);
        }

        public override AssemblyNameReference ImportReference (AssemblyNameReference name)
        {
            if (importerMixin.TryMapAssemblyName(name.Name, out var correctCorlibReference))
                return correctCorlibReference;

            return base.ImportReference(name);
        }
    }
    
    public class SystemPrivateCoreLibFixerReflectionProvider : IReflectionImporterProvider
    {
        public IReflectionImporter GetReflectionImporter(ModuleDefinition module)
        {
            return new SystemPrivateCoreLibFixerReflectionImporter(module);
        }
    }

    internal class SystemPrivateCoreLibFixerReflectionImporter : DefaultReflectionImporter
    {
        private PrivateCorlibFixerMixin importerMixin;
        
        public SystemPrivateCoreLibFixerReflectionImporter(ModuleDefinition module) : base(module)
        {
            importerMixin = new PrivateCorlibFixerMixin(module);
        }

        public override AssemblyNameReference ImportReference(AssemblyName reference)
        {
            if (importerMixin.TryMapAssemblyName(reference.Name, out var correctCorlibReference))
                return correctCorlibReference;

            return base.ImportReference(reference);
        }
    }