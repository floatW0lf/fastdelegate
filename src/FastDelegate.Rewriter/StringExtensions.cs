namespace FastDelegate.Rewriter;

public static class StringExtensions
{
    public static string SubstringFor(this string str, char symbol)
    {
        var index = str.IndexOf(symbol);
        return index == -1 ? str : str[..index];
    }
}