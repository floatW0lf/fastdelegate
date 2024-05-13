using FastDelegate.Rewriter;
using Mono.Cecil;

namespace FastDelegate.Tests;

public delegate string SomeDelegate(string a, int b);

public class DefaultDelegateInterfaceGeneratorTest
{
    [Test]
    public void CreateTest()
    {
        var def = new AssemblyNameDefinition("Test", new Version(1, 1, 1));
        var assembly = AssemblyDefinition.CreateAssembly(def, "test", new ModuleParameters()
        {
            AssemblyResolver = new DefaultAssemblyResolver(),
            MetadataImporterProvider = new SystemPrivateCoreLibFixerMetadataImporterProvider(),
            ReflectionImporterProvider = new SystemPrivateCoreLibFixerReflectionProvider()
        });
        var generator = new DefaultDelegateInterfaceGenerator();
        var typeDefinition = generator.Create(new[] {assembly.MainModule.ImportReference(typeof(Func<int,float>)), assembly.MainModule.ImportReference(typeof(SomeDelegate)) }, "test", "Foo");
        
        Assert.Multiple(() =>
        {
            Assert.That(typeDefinition.Methods[0].Name, Is.EqualTo("Invoke_0"));
            Assert.That(typeDefinition.Methods[0].Parameters[0].ParameterType.FullName, Is.EqualTo("System.Int32"));
            Assert.That(typeDefinition.Methods[0].ReturnType.FullName, Is.EqualTo("System.Single"));
            
            Assert.That(typeDefinition.Methods[1].Name, Is.EqualTo("Invoke_1"));
            Assert.That(typeDefinition.Methods[1].Parameters[0].ParameterType.FullName, Is.EqualTo("System.String"));
            Assert.That(typeDefinition.Methods[1].Parameters[1].ParameterType.FullName, Is.EqualTo("System.Int32"));
            Assert.That(typeDefinition.Methods[1].ReturnType.FullName, Is.EqualTo("System.String"));
            
        });
    }
}