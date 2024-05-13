using System.Reflection;
using TestLib;

namespace CallRewroteDll;

public class Tests
{
    [Test]
    public void NestedTypes()
    {
        Assert.That(typeof(Usage).GetNestedTypes(BindingFlags.NonPublic).Length, Is.EqualTo(10));
    }
    
    [Test]
    public void CallGeneric()
    {
        var c = new Usage();
        var result = c.UseSimple(10,"");
        Assert.That(result, Is.EqualTo(22));
    }

    [Test]
    public void ManyCall()
    {
        var c = new Usage();
        Assert.That(c.ClosureUse(), Is.EqualTo(11));
    }

    [Test]
    public void ExtUsage()
    {
        var c = new Usage();
        Assert.That(c.ExtensionUsage(), Is.EqualTo(16));
    }
}