using System.Runtime.InteropServices;

namespace TestLib;

public interface IActionTest<in T>
{
    void Invoke([MarshalAs(UnmanagedType.Bool)]T arg);
}
public static class Example
{
    private struct LabdaTest : IActionTest<int>
    {
        public void Invoke(int arg)
        {
            
        }
    }
    
    public static void FastForeach<T, TLambda>(this IReadOnlyList<T> list, ref TLambda action) where TLambda : struct, IActionTest<T>
    {
        for (int i = 0; i < list.Count; i++)
        {
            action.Invoke(list[i]);
        }
    }
    
    
    
}

