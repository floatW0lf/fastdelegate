using FastDelegate.Rewriter;
using Mono.Cecil;
using TestLib;

namespace FastDelegate.Tests;

public class RewriterTests
{
    [Test]
    public void SimpleTest()
    {
        var assembly = AssemblyDefinition.ReadAssembly(typeof(Some).Assembly.Location, DefaultReaderParameters.Value);
        var rewriter = new ILRewriter(assembly, new DefaultDelegateInterfaceGenerator(), new IDelegateInvokeRewriter[]{new CaptureClosureDelegateInvokeRewriter(), new NonCaptureDelegateInvokeRewriter(), new CaptureNonFastDelegateRewriter()});
        rewriter.Rewrite(default);
        rewriter.SaveChanges(typeof(Some).Assembly.Location.Replace("TestLib", "TestLibWeaved"));
    }
}