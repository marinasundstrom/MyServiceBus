namespace MyServiceBus;

public static class NamingHelpers2
{
    public static string ToKebabCase(this string input)
    {
        return string.Concat(input.Select((c, i) =>
            i > 0 && char.IsUpper(c) ? "-" + char.ToLower(c) : char.ToLower(c).ToString()));
    }
}