using Mono.Cecil;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics.CodeAnalysis;

public class Program
{
    public static string outputDir => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output");
    public static string includeFolder => Path.Combine(outputDir, "include");
    
    public class DependencyContext
    {
        public HashSet<string> IncludeFiles = new HashSet<string>();
        public HashSet<TypeDefinition> ForwardDeclTypes = new HashSet<TypeDefinition>();
    }

    public static string[] ctorNames = new string[2] { ".ctor", "_ctor" };
    
    static string[] opNames =
    {
        "op_Implicit", "op_Explicit", "op_Assign", "op_AdditionAssignment",
        "op_SubtractionAssignment", "op_MultiplicationAssignment", "op_DivisionAssignment",
        "op_ModulusAssignment", "op_BitwiseAndAssignment", "op_BitwiseOrAssignment",
        "op_ExclusiveOrAssignment", "op_LeftShiftAssignment", "op_RightShiftAssignment",
        "op_Increment", "op_Decrement", "op_UnaryPlus", "op_UnaryNegation",
        "op_Addition", "op_Subtraction", "op_Multiply", "op_Division",
        "op_Modulus", "op_OnesComplement", "op_BitwiseAnd", "op_BitwiseOr",
        "op_ExclusiveOr", "op_LeftShift", "op_RightShift", "op_LogicalNot",
        "op_LogicalAnd", "op_LogicalOr", "op_Equality", "op_Inequality",
        "op_LessThan", "op_GreaterThan", "op_LessThanOrEqual", "op_GreaterThanOrEqual",
        "op_Comma", "op_True", "op_False"
    };

    public static string GetFullCppPath(TypeDefinition type)
    {
        if (type.IsNested)
            return GetFullCppPath(type.DeclaringType) + "::" + type.Name.ToCppName();
        
        string ns = string.IsNullOrEmpty(type.Namespace) ? "GlobalNamespace" : type.Namespace.Replace(".", "::");
        return "::" + ns + "::" + type.Name.ToCppName();
    }

    public static string GetCppTypeName(TypeReference type, DependencyContext context)
    {
        if (type.IsByReference) return GetCppTypeName(type.GetElementType(), context) + "&";
        if (type.IsPointer) return GetCppTypeName(type.GetElementType(), context) + "*";
        if (type.IsArray) return $"BNM::Structures::Mono::Array<{GetCppTypeName(type.GetElementType(), context)}>*";
        if (type.IsGenericParameter) return type.Name;

        if (type is GenericInstanceType git)
        {
            string baseName = git.ElementType.FullName.Split('<')[0];
            string args = string.Join(", ", git.GenericArguments.Select(a => GetCppTypeName(a, context)));

            if (baseName == "System.Collections.Generic.List`1")
            {
                context.IncludeFiles.Add("BNM/BasicMonoStructures.hpp");
                return $"BNM::Structures::Mono::List<{args}>*";
            }
            if (baseName == "System.Collections.Generic.Dictionary`2")
            {
                context.IncludeFiles.Add("BNM/ComplexMonoStructures.hpp");
                return $"BNM::Structures::Mono::Dictionary<{args}>*";
            }
            
            var resBase = git.ElementType.Resolve();
            
            if (resBase != null && resBase.IsValueType && !resBase.IsEnum)
                return "void*";

            if (resBase != null)
            {
                if (resBase.IsNested)
                {
                    TypeDefinition topLevelBase = resBase;
                    while (topLevelBase.IsNested) topLevelBase = topLevelBase.DeclaringType;
                    string[] nsPartsBase = string.IsNullOrEmpty(topLevelBase.Namespace) ? new[] { "GlobalNamespace" } : topLevelBase.Namespace.Split('.');
                    context.IncludeFiles.Add(string.Join("/", nsPartsBase) + "/" + topLevelBase.Name.ToCppName() + ".h");
                }
                else
                {
                    context.ForwardDeclTypes.Add(resBase);
                }
            }
            
            string baseCppName = resBase != null ? GetFullCppPath(resBase) : git.ElementType.Name.ToCppName();
            return $"{baseCppName}<{args}>*";
        }

        if (Config.DefaultTypeMap.TryGetValue(type.FullName, out Tuple<string, string>? mapped))
        {
            context.IncludeFiles.Add(mapped.Item2);
            return mapped.Item1;
        }

        var resolved = type.Resolve();
        if (resolved == null)
        {
            context.IncludeFiles.Add("BNM/Il2CppHeaders.hpp");
            return "::BNM::IL2CPP::Il2CppObject*";
        }

        if (resolved.IsValueType && !resolved.IsEnum)
        {
            return "void*";
        }

        if (resolved.IsNested)
        {
            TypeDefinition topLevel = resolved;
            while (topLevel.IsNested) topLevel = topLevel.DeclaringType;
            string[] nsParts = string.IsNullOrEmpty(topLevel.Namespace) ? new[] { "GlobalNamespace" } : topLevel.Namespace.Split('.');
            context.IncludeFiles.Add(string.Join("/", nsParts) + "/" + topLevel.Name.ToCppName() + ".h");
            return GetFullCppPath(resolved) + "*";
        }

        context.ForwardDeclTypes.Add(resolved);

        string fullPath = GetFullCppPath(resolved);
        return $"{fullPath}*";
    }

    public static string GetClassGetter(TypeDefinition type, string icn = "")
    {
        var defTree = new List<TypeDefinition>();
        var current = type;
        while (current != null)
        {
            defTree.Add(current);
            current = current.DeclaringType;
        }
        defTree.Reverse();
        var builder = new StringBuilder();
        var firstType = defTree[0];
        if (string.IsNullOrEmpty(icn))
            builder.Append($"::BNM::Class(\"{firstType.Namespace}\", \"{firstType.Name}\")");
        else
            builder.Append($"::BNM::Class {icn} = ::BNM::Class(\"{firstType.Namespace}\", \"{firstType.Name}\")");
        if (defTree.Count > 1)
        {
            for (int i = 1; i < defTree.Count; ++i)
                builder.Append($".GetInnerClass(\"{defTree[i].Name}\")");
        }
        return builder.ToString();
    }

    public static void ParseFields(TypeDefinition type, CppCodeWriter writer, DependencyContext context)
    {
        if (!type.HasFields) return;
        
        if (type.IsEnum)
        {
            for (int i = 0; i < type.Fields.Count; ++i)
            {
                var field = type.Fields[i];
                if (field.IsStatic && field.HasConstant)
                    writer.WriteLine($"{field.Name.ToCppName()} = {field.Constant}{(i != type.Fields.Count - 1 ? "," : "")}");
            }
            return;
        }

        foreach (var field in type.Fields)
        {
            string fieldType = GetCppTypeName(field.FieldType, context);
            string fieldName = field.Name.ToCppName();
            if (fieldType == "void*")
            {
                writer.WriteLine("template <typename T = void*>");
                fieldType = "T";
            }
            
            if (field.IsStatic) writer.Write("static ");
            writer.Write($"{fieldType} dyn_{fieldName}() ");
            writer.OpenBracket();
            string icn = "_class_internal_";
            string ifn = "_field_internal_";
            writer.WriteLine($"static {GetClassGetter(type!, icn)};");
            writer.WriteLine($"static ::BNM::Field<{fieldType}> {ifn} = {icn}.GetField(\"{field.Name}\");");
            if (!field.IsStatic) writer.WriteLine($"{ifn}.SetInstance(reinterpret_cast<::BNM::IL2CPP::Il2CppObject*>(this));");
            writer.WriteLine($"return {ifn}.Get();");
            writer.CloseBracket();

            fieldType = GetCppTypeName(field.FieldType, context); 
            if (fieldType == "void*")
            {
                writer.WriteLine("template <typename T = void*>");
                fieldType = "T";
            }
            if (field.IsStatic) writer.Write("static ");
            writer.WriteLine($"void _set_{fieldName}({fieldType} value) ");
            writer.OpenBracket();
            writer.WriteLine($"static {GetClassGetter(type!, icn)};");
            writer.WriteLine($"static ::BNM::Field<{fieldType}> {ifn} = {icn}.GetField(\"{field.Name}\");");
            if (!field.IsStatic) writer.WriteLine($"{ifn}.SetInstance(reinterpret_cast<::BNM::IL2CPP::Il2CppObject*>(this));");
            writer.WriteLine($"{ifn}.Set(value);");
            writer.CloseBracket();
        }
    }

    public static void ParseMethods(TypeDefinition type, CppCodeWriter writer, DependencyContext context)
    {
        if (!type.HasMethods) return;
        
        HashSet<string> processedSignatures = new HashSet<string>();

        foreach (var method in type.Methods)
        {
            if (opNames.Contains(method.Name)) continue;
            
            string icn = "_class_internal_";
            string imn = "_method_call_";
            
            if (method.IsConstructor)
            {
                if (method.IsStatic) continue;
                
                List<string> ctorTemplates = new List<string>();
                var ctorParamTypes = new List<string>();
                
                for (int i = 0; i < method.Parameters.Count; i++)
                {
                    string pType = GetCppTypeName(method.Parameters[i].ParameterType, context);
                    if (pType == "void*")
                    {
                        string tName = $"T{i}";
                        ctorTemplates.Add($"typename {tName} = void*");
                        pType = tName;
                    }
                    ctorParamTypes.Add(pType);
                }
                
                string sigKey = $"New_ctor<{ctorTemplates.Count}>({string.Join(",", ctorParamTypes)})";
                if (!processedSignatures.Add(sigKey)) continue;
                
                string paramsDecl = string.Join(", ", method.Parameters.Select((x, i) => $"{ctorParamTypes[i]} {x.Name.ToCppName()}"));
                string typeNameInternal = type.Name.ToCppName();
                if (type.HasGenericParameters) typeNameInternal += $"<{string.Join(", ", type.GenericParameters.Select(p => p.Name))}>";
                string ptr = type.IsValueType ? "" : "*";
                string paramsCall = string.Join(", ", method.Parameters.Select(x => x.Name.ToCppName()));
                
                if (ctorTemplates.Count > 0) writer.WriteLine($"template <{string.Join(", ", ctorTemplates)}>");
                writer.Write($"static {typeNameInternal}{ptr} New_ctor({paramsDecl}) ");
                writer.OpenBracket();
                writer.WriteLine($"static {GetClassGetter(type!, icn)};");
                writer.WriteLine($"return ({typeNameInternal}{ptr}){icn}.CreateNewObjectParameters({paramsCall});");
                writer.CloseBracket();
                continue;
            }

            List<string> methodTemplates = new List<string>();
            if (method.HasGenericParameters) methodTemplates.AddRange(method.GenericParameters.Select(p => $"typename {p.Name}"));
            
            string typeName = GetCppTypeName(method.ReturnType, context);
            if (typeName == "void*")
            {
                methodTemplates.Add("typename TRet = void*");
                typeName = "TRet";
            }
            
            List<string> finalParamTypes = new List<string>();
            for (int i = 0; i < method.Parameters.Count; i++)
            {
                string pType = GetCppTypeName(method.Parameters[i].ParameterType, context);
                if (pType == "void*")
                {
                    string tName = $"TP{i}";
                    methodTemplates.Add($"typename {tName} = void*");
                    pType = tName;
                }
                finalParamTypes.Add(pType);
            }
            
            string methodSigKey = $"{method.Name.ToCppName()}<{methodTemplates.Count}>({string.Join(",", finalParamTypes)})";
            if (!processedSignatures.Add(methodSigKey)) continue;

            if (methodTemplates.Count > 0) writer.WriteLine($"template <{string.Join(", ", methodTemplates)}>");
            writer.Write($"{(method.IsStatic ? "static " : "")}{typeName} {method.Name.ToCppName()}(");
            if (method.HasParameters) writer.Write(string.Join(", ", method.Parameters.Select((x, i) => $"{finalParamTypes[i]} {x.Name.ToCppName()}")));
            writer.Write(") ");
            writer.OpenBracket();
            
            context.IncludeFiles.Add("BNM/MethodBase.hpp");
            context.IncludeFiles.Add("BNM/Method.hpp");
            writer.WriteLine($"static {GetClassGetter(type!, icn)};");
            string append = method.HasParameters ? $", {{{string.Join(", ", method.Parameters.Select(x => $"\"{x.Name}\""))}}}" : ", 0";
            
            writer.WriteLine($"::BNM::Method<{typeName}> {imn} = {icn}.GetMethod(\"{method.Name}\"{append});");
            
            if (!method.IsStatic)
            {
                context.IncludeFiles.Add("BNM/Il2CppHeaders.hpp");
                writer.WriteLine($"{imn}.SetInstance(reinterpret_cast<::BNM::IL2CPP::Il2CppObject*>(this));");
            }
            if (typeName != "void") writer.Write("return ");
            writer.WriteLine($"{imn}.Call({string.Join(", ", method.Parameters.Select(x => (x.ParameterType.IsByReference ? "&" : "") + x.Name.ToCppName()))});");
            writer.CloseBracket();
        }
    }

    public static void ParseType(TypeDefinition type, CppCodeWriter? writer, bool isNested = false)
    {
        if (type.IsValueType && !type.IsEnum) return;
        if (type.Name == "<Module>" || type.Name.StartsWith("<PrivateImplementationDetails>")) return;

        DependencyContext context = new DependencyContext();
        
        TypeReference? baseTypeRef = type.BaseType;
        TypeDefinition? baseTypeDef = null;
        if (!type.IsValueType && baseTypeRef != null)
        {
             baseTypeDef = baseTypeRef.Resolve();
             if (baseTypeRef.FullName == "System.Object")
             {
                 context.IncludeFiles.Add("BNM/Il2CppHeaders.hpp");
             }
             else if (baseTypeDef != null)
             {
                 TypeDefinition topLevelBase = baseTypeDef;
                 while (topLevelBase.IsNested) topLevelBase = topLevelBase.DeclaringType;
                 string[] nsPartsBase = string.IsNullOrEmpty(topLevelBase.Namespace) ? new[] { "GlobalNamespace" } : topLevelBase.Namespace.Split('.');
                 string path = string.Join("/", nsPartsBase) + "/" + topLevelBase.Name.ToCppName() + ".h";
                 context.IncludeFiles.Add(path);
             }
        }
        
        if (type.HasFields) 
            foreach (var f in type.Fields) GetCppTypeName(f.FieldType, context);
        if (type.HasMethods) 
            foreach (var m in type.Methods) 
            {
                if (opNames.Contains(m.Name)) continue;
                GetCppTypeName(m.ReturnType, context);
                foreach(var p in m.Parameters) GetCppTypeName(p.ParameterType, context);
            }

        string[] namespaceSplit = string.IsNullOrEmpty(type.Namespace) ? new[] { "GlobalNamespace" } : type.Namespace.Split('.');
        
        if (writer == null)
        {
            string fileFolder = includeFolder;
            foreach (var part in namespaceSplit) fileFolder = Path.Combine(fileFolder, part);
            if (!Directory.Exists(fileFolder)) Directory.CreateDirectory(fileFolder);
            writer = new CppCodeWriter(Path.Combine(fileFolder, type.Name.ToCppName() + ".h"));
        }

        if (!isNested)
        {
            foreach (var inc in context.IncludeFiles) writer.WriteInclude(inc);
            writer.WriteLine();

            context.ForwardDeclTypes.RemoveWhere(t => t.FullName == type.FullName || (t.DeclaringType != null && t.DeclaringType.FullName == type.FullName));
            
            foreach (var fwd in context.ForwardDeclTypes)
            {
                TypeDefinition root = fwd;
                while (root.IsNested) root = root.DeclaringType;
                
                string ns = string.IsNullOrEmpty(root.Namespace) ? "GlobalNamespace" : root.Namespace.Replace(".", "::");
                string cppName = root.Name.ToCppName();
                
                if (root.HasGenericParameters) {
                    string templateArgs = string.Join(", ", Enumerable.Repeat("typename", root.GenericParameters.Count));
                    writer.WriteLine($"namespace {ns} {{ template <{templateArgs}> class {cppName}; }}");
                } else {
                    writer.WriteLine($"namespace {ns} {{ class {cppName}; }}");
                }
            }
            writer.WriteLine();

            foreach (string ns in namespaceSplit)
            {
                writer.WriteLine($"namespace {ns}");
                writer.OpenBracket();
            }
        }

        if (type.HasGenericParameters)
        {
            string genericArgs = string.Join(", ", type.GenericParameters.Select(p => $"typename {p.Name}"));
            writer.WriteLine($"template<{genericArgs}>");
        }

        writer.Write($"{type.ClassType()} {type.Name.ToCppName()} ");
        
        if (!type.IsValueType && baseTypeRef != null)
        {
            if (baseTypeRef.FullName == "System.Object")
                writer.Write(": public ::BNM::IL2CPP::Il2CppObject ");
            else if (baseTypeDef != null)
                writer.Write($": public {GetFullCppPath(baseTypeDef)} ");
        }

        writer.OpenBracket();
        if (!type.IsEnum) writer.WriteLine("public:");
        
        if (type.HasNestedTypes)
        {
            foreach (var nestedType in type.NestedTypes) ParseType(nestedType, writer, true);
        }

        writer.WriteLine();
        ParseFields(type, writer, context);
        writer.WriteLine();
        ParseMethods(type, writer, context);

        writer.CloseBracket(";");

        if (!isNested)
        {
            foreach (string _ in namespaceSplit.Reverse()) writer.CloseBracket();
            writer.WriteLine();
            
            writer.WriteInclude("BNM/Defaults.hpp");

            if (!type.HasGenericParameters)
            {
                string resolvedTypeName = "::" + (string.IsNullOrEmpty(type.Namespace) ? "GlobalNamespace" : type.Namespace.Replace(".", "::")) + "::" + type.Name.ToCppName() + (type.IsValueType ? "" : "*");
                writer.WriteLine("template <>");
                writer.Write("BNM::Defaults::DefaultTypeRef ");
                writer.Write($"BNM::Defaults::Get<{resolvedTypeName}>() ");
                writer.OpenBracket();
                writer.WriteLine($"static BNM::Defaults::Internal::ClassType classCache = nullptr;");
                writer.WriteLine($"if (!classCache) classCache = {GetClassGetter(type)}._data;");
                writer.WriteLine($"return ::BNM::Defaults::DefaultTypeRef {{ &classCache }};");
                writer.CloseBracket();
            }

            if (type.HasNestedTypes)
            {
                foreach (var nt in type.NestedTypes)
                {
                    if (nt.IsValueType && !nt.IsEnum) continue;
                    if (nt.HasGenericParameters) continue;
                    string resNtName = "::" + (string.IsNullOrEmpty(type.Namespace) ? "GlobalNamespace" : type.Namespace.Replace(".", "::")) + "::" + type.Name.ToCppName() + "::" + nt.Name.ToCppName() + (nt.IsValueType ? "" : "*");
                    writer.WriteLine("template <>");
                    writer.Write("BNM::Defaults::DefaultTypeRef ");
                    writer.Write($"BNM::Defaults::Get<{resNtName}>() ");
                    writer.OpenBracket();
                    writer.WriteLine($"static BNM::Defaults::Internal::ClassType classCache = nullptr;");
                    writer.WriteLine($"if (!classCache) classCache = {GetClassGetter(nt)}._data;");
                    writer.WriteLine($"return BNM::Defaults::DefaultTypeRef {{ &classCache }};");
                    writer.CloseBracket();
                }
            }
            writer.Save();
        }
    }

    public static void Main(string[] args)
    {
        string? dummyDllPath = "/home/pubert/Downloads/Il2CppDumper-GT-LATEST/DummyDll";
        if (!Directory.Exists(dummyDllPath))
        {
            Console.WriteLine("Path not found, enter manually:");
            dummyDllPath = Console.ReadLine();
        }
        
        if (string.IsNullOrEmpty(dummyDllPath) || !Directory.Exists(dummyDllPath)) return;
        
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(dummyDllPath);
        var readerParams = new ReaderParameters { AssemblyResolver = resolver };
        
        if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true);
        Directory.CreateDirectory(outputDir);
        
        foreach (string file in Directory.GetFiles(dummyDllPath, "*.dll"))
        {
            try
            {
                var def = AssemblyDefinition.ReadAssembly(file, readerParams);
                foreach (var type in def.MainModule.Types) ParseType(type, null);
            }
            catch (Exception ex) { Console.WriteLine($"Error processing {Path.GetFileName(file)}: {ex.Message}"); }
        }
    }
}