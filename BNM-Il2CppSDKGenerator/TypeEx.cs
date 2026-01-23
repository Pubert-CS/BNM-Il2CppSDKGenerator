using System.Text;
using Mono.Cecil;

public static class TypeEx
{
    public static string ToCppName(this string self)
    {
        if (string.IsNullOrEmpty(self)) return "_";

        StringBuilder sb = new StringBuilder();
        foreach (char c in self)
        {
            if (c == '`' || c == '.')
            {
                sb.Append('_');
            }
            else if (Config.allowedChars.Contains(c))
            {
                sb.Append(c);
            }
            else
            {
                sb.Append('$');
            }
        }

        string result = sb.ToString();
        if (char.IsDigit(result[0]))
        {
            result = "_" + result;
        }

        return result;
    }

    public static string ClassType(this TypeDefinition self)
    {
        string classType = "class";
        if (self.IsValueType && !self.IsEnum)
            classType = "struct";
        else if (self.IsEnum)
            classType = "enum class";
        return classType;
    }
}