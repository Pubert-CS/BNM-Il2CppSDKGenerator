using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Mono.Cecil;

public class CppCodeWriter
{
    private string _path;
    private List<string> includes = new();
    private List<string> typesAlreadyDeclared = new();
    private Dictionary<string, List<string>> forwardDecls = new();
    private StringBuilder bodyBuilder = new();
    private int _indent = 0;
    private bool _needsIndent = true;

    public CppCodeWriter(string path) => _path = path;

    public void WriteInclude(string include)
    {
        if (string.IsNullOrEmpty(include)) return;
        string formatted = $"#include <{include}>";
        if (!includes.Contains(formatted)) includes.Add(formatted);
    }

    public void WriteForwardDecl(TypeDefinition type)
    {
        if (type.IsNested) return;

        string fixedNamespace = Program.FixNamespace(type.Namespace);
        
        if (!forwardDecls.ContainsKey(fixedNamespace))
            forwardDecls[fixedNamespace] = new List<string>();

        if (!typesAlreadyDeclared.Contains(type.FullName))
        {
            typesAlreadyDeclared.Add(type.FullName);
            
            StringBuilder decl = new StringBuilder();
            if (type.HasGenericParameters)
            {
                string genericParams = string.Join(", ", type.GenericParameters.Select(x => "typename " + x.Name));
                decl.Append($"template <{genericParams}> ");
            }

            if (type.IsEnum)
            {
                var enumValueField = type.Fields.FirstOrDefault(f => f.Name == "value__");
                string underlyingType = "int";
                if (enumValueField != null && Config.DefaultTypeMap.TryGetValue(enumValueField.FieldType.FullName, out var mapped))
                    underlyingType = mapped.Item1;
                
                decl.Append($"enum class {type.Name.ToCppName()} : {underlyingType}");
            }
            else
            {
                decl.Append($"{type.ClassType()} {type.Name.ToCppName()}");
            }

            decl.Append(";");
            forwardDecls[fixedNamespace].Add(decl.ToString());
        }
    }

    public void WriteLine(string line = "")
    {
        if (_needsIndent) bodyBuilder.Append(new string('\t', _indent));
        bodyBuilder.AppendLine(line);
        _needsIndent = true;
    }

    public void Write(string line)
    {
        if (_needsIndent) bodyBuilder.Append(new string('\t', _indent));
        bodyBuilder.Append(line);
        _needsIndent = false;
    }

    public void OpenBracket()
    {
        if (!_needsIndent) bodyBuilder.Append(" ");
        else bodyBuilder.Append(new string('\t', _indent));
        bodyBuilder.AppendLine("{");
        _indent++;
        _needsIndent = true;
    }

    public void CloseBracket(string suffix = "")
    {
        _indent--;
        if (_indent < 0) _indent = 0;
        bodyBuilder.AppendLine(new string('\t', _indent) + "}" + suffix);
        _needsIndent = true;
    }

    public void Save()
    {
        using StreamWriter writer = new(_path);
        writer.WriteLine("#pragma once");
        writer.WriteLine("// Generated with BNM-Il2CppSdkGenerator by Pubert-CS");
        if (includes.Any())
        {
            writer.WriteLine("// Includes");
            foreach (var inc in includes.OrderBy(x => x)) writer.WriteLine(inc);
            writer.WriteLine();
        }
        if (forwardDecls.Any())
        {
            writer.WriteLine("// Forward Declarations");
            foreach (var kvp in forwardDecls.OrderBy(x => x.Key))
            {
                writer.WriteLine($"namespace {kvp.Key} {{");
                foreach (string decl in kvp.Value) writer.WriteLine($"\t{decl}");
                writer.WriteLine("}");
            }
            writer.WriteLine();
        }
        writer.WriteLine("// Header Generation");
        writer.Write(bodyBuilder.ToString());
    }
}