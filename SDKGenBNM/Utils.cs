using System.Text;
using System.Collections.Generic;
using dnlib;
using dnlib.DotNet;
using System.Text.RegularExpressions;

public static class Utils
{
    public static bool StartsWithNumber(string str)
    {
        return char.IsDigit(str[0]);
    }

    public static string Il2CppTypeToCppType(TypeSig type)
    {
        if (type.IsGenericInstanceType)
        {
            return FormatIl2CppGeneric(type);
        }

        string result = type.FullName switch
        {
            "System.Int8" => "int8_t",
            "System.UInt8" => "uint8_t",
            "System.Int16" => "int16_t",
            "System.UInt16" => "uint16_t",
            "System.Int32" => "int32_t",
            "System.UInt32" => "uint32_t",
            "System.Int64" => "int64_t",
            "System.UInt64" => "uint64_t",
            "System.Single" => "float",
            "System.Double" => "double",
            "System.Boolean" => "bool",
            "System.Decimal" => "BNM::Types::decimal",
            "System.Char" => "char",
            "System.Byte" => "BNM::Types::byte",
            "System.SByte" => "BNM::Types::sbyte",
            "System.String" => "BNM::Structures::Mono::String*",
            "UnityEngine.Vector2" => "BNM::Structures::Unity::Vector2",
            "UnityEngine.Vector3" => "BNM::Structures::Unity::Vector3",
            "UnityEngine.Quaternion" => "BNM::Structures::Unity::Quaternion",
            "UnityEngine.Rect" => "BNM::Structures::Unity::Rect",
            "System.Void" => "void",
            _ => "BNM::IL2CPP::Il2CppObject*"
        };

        if (type.FullName.Contains("[]"))
        {
            result = "BNM::Structures::Mono::Array<" + result + ">*";
        }

        return result;
    }

    public static string FormatIl2CppGeneric(TypeSig type)
    {
        string result = "";

        if (type.GetName().StartsWith("List"))
        {
            result = "BNM::Structures::Mono::List<";
        }
        else if (type.GetName().StartsWith("Dictionary"))
        {
            result = "BNM::Structures::Mono::Dictionary<";
        }
        else
        {
            return "void*";
        }
        List<string> args = new List<string>();
        foreach (var arg in type.ToGenericInstSig().GenericArguments)
        {
            if (arg.IsGenericInstanceType)
            {
                args.Add(FormatIl2CppGeneric(arg));
            }
            else args.Add(Il2CppTypeToCppType(arg));
        }
        result += string.Join(", ", args.ToArray());
        result += ">*";
        return result;
    }

    public static bool IsKeyword(string str)
    {
        string[] keywords = [
            "", "alignas", "alignof", "and", "and_eq", "asm", "atomic_cancel", "atomic_commit", "atomic_noexcept",
            "auto", "bitand", "bitor", "bool", "break", "case", "catch", "char", "char8_t", "char16_t", "char32_t",
            "class", "compl", "concept", "const", "consteval", "constexpr", "constinit", "const_cast", "continue",
            "contract_assert", "co_await", "co_return", "co_yield", "decltype", "default", "delete", "do", "double",
            "dynamic_cast", "else", "enum", "explicit", "export", "extern", "false", "float", "for", "friend", "goto",
            "if", "inline", "int", "long", "mutable", "namespace", "new", "noexcept", "not", "not_eq", "nullptr",
            "operator", "or", "or_eq", "private", "protected", "public", "reflexpr", "register", "reinterpret_cast",
            "requires", "return", "short", "signed", "sizeof", "static", "static_assert", "static_cast", "struct",
            "switch", "synchronized", "template", "this", "thread_local", "throw", "true", "try", "typedef", "typeid",
            "typename", "union", "unsigned", "using", "virtual", "void", "volatile", "wchar_t", "while", "xor", "xor_eq",

            "abstract", "add", "as", "base", "byte", "checked", "decimal", "delegate", "event", "explicit", "extern",
            "finally", "fixed", "foreach", "implicit", "in", "interface", "internal", "is", "lock", "null", "object",
            "out", "override", "params", "readonly", "ref", "remove", "sbyte", "sealed", "stackalloc", "string",
            "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using static", "value", "when", "where",
            "yield",

            "INT32_MAX", "INT32_MIN", "UINT32_MAX", "UINT16_MAX", "INT16_MAX", "UINT8_MAX", "INT8_MAX", "INT_MAX",
            "Assert", "NULL", "O",
        ];
        return keywords.Contains(str);
    }

    public static string[] MakeValidParams(string[] paramNames)
    {
        var results = new List<string>();
        var seen = new Dictionary<string, int>();

        foreach (var param in paramNames)
        {
            string cparam = param;
            if (seen.ContainsKey(param))
            {
                seen[param]++;
                cparam = $"{param}_{seen[param]}";
            }
            else
            {
                seen[param] = 0;
            }

            results.Add(cparam);
        }

        return results.ToArray();
    }
        

    public static string FormatInvalidName(string className)
    {
        string str = className.Trim()
            .Replace("<", "$")
            .Replace(">", "$")
            .Replace("|", "$")
            .Replace("-", "$")
            .Replace("`", "$")
            .Replace("=", "$")
            .Replace("@", "$")
            .Trim();
        if (string.IsNullOrEmpty(str))
            return "_";

        if (StartsWithNumber(str))
            str = "_" + str;

        if (IsKeyword(className))
            str = "$" + str;

        return str;
    }

    public static bool IsStruct(this TypeDef type)
    {
        return type.IsValueType && !type.IsEnum;
    }

}