using FastDelegate;
using FastDelegate.Attributes;

namespace TestLib;
public class Some
{
    public float Simple([Inline] Func<int,int> act1, string some)
    {
        return act1(1);
    }
    
    public float Complex(string text, [Inline] Func<int,float> act, int value)
    {
        if (text == "some")
        {
            return act(value + 1);
        }
        return 0;
    }

    public int ManyDelegates([Inline] Action<int> first, [Inline] Func<int, int> second)
    {
        first(1);
        return second(10);
    }
    
    public int MixDelegates([Inline] Func<int,int> first)
    {
        return first(1);
    }
    
    public int OriginalDelegates(Func<int, int> second)
    {
        return second(10);
    }
}

public record ComplexVar
{
    public int A;
    public float B;
}

public class Usage
{
    public float UseSimple(int a, string text)
    {
        var complex = new ComplexVar();
        var c = complex.A + 1;

        float result = 0;
        for (int i = 0; i < 2; i++)
        {
            result += new Some().Simple(x => a + x, c == 9 ? "text" : "empty");
        }

        return result;
    }

    public int ManyDelegates()
    {
        var i = 0;
        var v = new Some();
        return v.ManyDelegates(x => i = 1, y => i + y);
    }
    
    public int ManyDifferentTypeDelegates()
    {
        var i = 0;
        var v = new Some();
        v.ManyDelegates(x => i = 1, y => y + 10);
        return v.ManyDelegates(x => { Console.WriteLine("Hello"); }, y => i + 10);
    }

    public float StaticSimple()
    {
        var v = new Some();
        var list = new List<int>() { 1, 2, 3, 4, 5, 6, 7 };
        var text = "";
        foreach (var item in list)
        {
            text += item.ToString();
        }
        return v.Simple(x => x + 1, text); 
    }
    
    public float StaticSimpleSameCall()
    {
        var s = new Some();
        var result = s.Simple(x => x + 2, "some");
        result += s.Simple(x => x + 42, "o");

        return result;
    }

    public int MixDelegateUsage(int a)
    {
        var s = new Some();
        s.MixDelegates(i => i + a);
        return s.OriginalDelegates(y => y + a);
    }
    
    public float ClosureUse()
    {
        var a = 1;
        var complex = new ComplexVar();
        var c = complex.A + 1;

        float result = 0;
        for (int i = 0; i < 2; i++)
        {
            result += new Some().Simple(x => a + 1, c == 9 ? "text" : "empty");

        }
        var s = new Some();
        result += s.Simple(x => a + 2, "1");
        result += s.Simple(x => a + 3, "2");
        
        return result;
    }

    public int ExtensionUsage()
    {
        var result = 0;
        new [] {1, 2, 3, 4, 5}.FastForeach(i => result += i, 0);
        new [] {1}.FastForeach(i => result += i, 0);
        return result;
    }
}