using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using dnlib.DotNet;
using dnlib.IO;
using dnlib.Utils;
using System.Text.RegularExpressions;
using System.Collections;

namespace Il2CppSDK
{
    class Program
    {
        static Dictionary<string, int> m_DuplicateMethodTable = new Dictionary<string, int>();
        static string OUTPUT_DIR = "SDK";
        static ModuleDefMD currentModule = null;
        static StreamWriter currentFile = null;

        public static string fileIncludes = "", fileContent = "";

        static int indentLevel = 0;

        static void ParseFields(TypeDef clazz)
        {
            if (clazz.IsStruct())
            {
                foreach (var rid in currentModule.Metadata.GetFieldRidList(clazz.Rid))
                {
                    var field = currentModule.ResolveField(rid);

                    if (field == null)
                    {
                        continue;
                    }

                    var fieldName = field.Name.Replace("::", "_").Replace("<", "$").Replace(">", "$").Replace("k__BackingField", "").Replace(".", "_").Replace("`", "_");

                    if (fieldName.Equals("auto") || fieldName.Equals("register"))
                        fieldName += "_";

                    var fieldType = Utils.Il2CppTypeToCppType(field.FieldType.ToTypeDefOrRef().ResolveTypeDef());

                    Write($"{(field.IsStatic ? "static " : "")}{fieldType} {Utils.FormatInvalidName(fieldName)};");
                }
                return;
            }

            foreach (var rid in currentModule.Metadata.GetFieldRidList(clazz.Rid))
            {
                var field = currentModule.ResolveField(rid);

                if (field == null)
                {
                    continue;
                }

                var fieldName = field.Name.Replace("::", "_").Replace("<", "$").Replace(">", "$").Replace("k__BackingField", "").Replace(".", "_").Replace("`", "_");

                if (fieldName.Equals("auto") || fieldName.Equals("register"))
                    fieldName += "_";

                var fieldType = Utils.Il2CppTypeToCppType(field.FieldType.ToTypeDefOrRef().ResolveTypeDef());

                //get
                Write(string.Format(" {0}{1} {2}() {{", (field.IsStatic ? "static " : ""), fieldType, Utils.FormatInvalidName(fieldName)), false, true);
                if (field.IsStatic)
                {
                    Write(string.Format("\tstatic BNM::Field<{0}> __bnm__field__ = StaticClass().GetField(\"{1}\");", fieldType, fieldName));
                    Write("\treturn __bnm__field__();");
                }
                else
                {
                    Write(string.Format("\tstatic BNM::Field<{0}> __bnm__field__ = StaticClass().GetField(\"{1}\");", fieldType, fieldName));
                    Write("\t__bnm__field__.SetInstance((BNM::IL2CPP::Il2CppObject*)this);");
                    Write("\treturn __bnm__field__();");
                }
                Write("}");

                // set
                Write(string.Format("{0}{1} set_{2}({3}) {{", (field.IsStatic ? "static " : ""), "void", Utils.FormatInvalidName(fieldName), fieldType + " value"), false, true);
                if (field.IsStatic)
                {
                    Write(string.Format("\tstatic BNM::Field<{0}> __bnm__field__ = StaticClass().GetField(\"{1}\");", fieldType, fieldName));
                    Write("\t__bnm__field__.Set(value);");
                }
                else
                {
                    Write(string.Format("\tstatic BNM::Field<{0}> __bnm__field__ = StaticClass().GetField(\"{1}\");", fieldType, fieldName));
                    Write("\t__bnm__field__.SetInstance((BNM::IL2CPP::Il2CppObject*)this);");
                    Write("\t__bnm__field__.Set(value);");
                }
                Write("}");
            }
        }
        static void ParseMethods(TypeDef clazz)
        {
            if (clazz.IsStruct())
            {
                return;
            }

            foreach (var rid in currentModule.Metadata.GetMethodRidList(clazz.Rid))
            {
                var method = currentModule.ResolveMethod(rid);

                if (method == null || method.IsConstructor || method.IsStaticConstructor)
                {
                    continue;
                }

                var methodName = method.Name.Replace("::", "_").Replace("<", "").Replace(">", "").Replace(".", "_").Replace("`", "_");

                if (methodName.Equals("auto") || methodName.Equals("register"))
                    methodName += "_";

                var methodType = Utils.Il2CppTypeToCppType(method.ReturnType.ToTypeDefOrRef().ResolveTypeDef());

                string methodKey = clazz.Namespace + clazz.FullName + method.Name;

                if (m_DuplicateMethodTable.ContainsKey(methodKey))
                {
                    methodName += "_" + m_DuplicateMethodTable[methodKey]++;
                }
                else
                {
                    m_DuplicateMethodTable.Add(methodKey, 1);
                }

                List<string> methodParams = new List<string>();
                List<string> paramTypes = new List<string>();
                List<string> paramNames = new List<string>();

                foreach (var param in method.Parameters)
                {
                    if (param.IsNormalMethodParameter)
                    {
                        var paramType = Utils.Il2CppTypeToCppType(param.Type.ToTypeDefOrRef().ResolveTypeDef());

                        if (param.HasParamDef && param.ParamDef.IsOut)
                            paramType += "*";

                        var originalName = param.Name;
                        if (originalName == "auto" || originalName == "register")
                            originalName += "_";

                        paramTypes.Add(paramType);
                        paramNames.Add(originalName);;
                    }
                }

                paramNames = Utils.MakeValidParams(paramNames.ToArray()).ToList();

                for (int i = 0; i < paramNames.Count; i++)
                {
                    methodParams.Add(paramTypes[i] + " " + Utils.FormatInvalidName(paramNames[i]));
                }

                if (method.HasGenericParameters)
                {
                    Write(string.Format("template <typename T>"), false, true);
                    methodType = "T";
                }

                Write(string.Format(" {0}{1} {2}({3}) {{", (method.IsStatic ? "static " : ""), methodType, Utils.FormatInvalidName(methodName), string.Join(", ", methodParams)), false, true);
                if (!method.IsStatic)
                {
                    Write(string.Format("\tstatic BNM::Method<{0}> __bnm__method__ = StaticClass().GetMethod(\"{1}\", {2});", method.HasGenericParameters ? "T*" : methodType, method.Name, methodParams.Count));

                    Write("\treturn __bnm__method__[(BNM::IL2CPP::Il2CppObject*)this](", true);
                    Write(string.Join(", ", paramNames.Select(x => Utils.FormatInvalidName(x))), true, false);
                    Write(");", false, false);
                }
                else
                {
                    Write(string.Format("\tstatic BNM::Method<{0}> __bnm__method__ = StaticClass().GetMethod(\"{1}\", {2});", method.HasGenericParameters ? "T*" : methodType, method.Name, methodParams.Count));

                    Write("\treturn __bnm__method__(", true);
                    Write(string.Join(", ", paramNames.Select(x => Utils.FormatInvalidName(x))), true, false);
                    Write(");", false, false);
                }
                Write("}");

            }
        }
        
        static void Write(string line, bool isWrite = false, bool indent = true)
        {
            if (isWrite)
                fileContent += (indent ? new string('\t', indentLevel) : "") + line;
            else
                fileContent += (indent ? new string('\t', indentLevel) : "") + line + "\n";
        }

        static void ParseClass(TypeDef clazz)
        {
            var module = clazz.Module;
            string namespaze = clazz.Namespace;
            string className = clazz.Name;
            string classFilename = string.Concat(className.Split(Path.GetInvalidFileNameChars()));
            string validClassname = Utils.FormatInvalidName(className);

            Write("", false, false);
            indentLevel = 0;

            Write("namespace " + clazz.Module.Assembly.Name.Replace(".dll", "").Replace(".", "").Replace("-", "_") + " {");
            indentLevel++;

            string[] nameSpaceSplit = namespaze.Split('.');
            if (nameSpaceSplit.Length == 0 || (nameSpaceSplit.Length == 1 && nameSpaceSplit[0] == ""))
            {
                Write("namespace GlobalNamespace {");
                indentLevel++;
            }
            else
            {
                foreach (var part in nameSpaceSplit)
                {
                    Write("namespace " + part + " {");
                    indentLevel++;
                }
            }

            if (clazz.IsEnum)
            {
                string baseType = "int";
                
                var firstField = clazz.Fields.FirstOrDefault(f => !f.IsStatic);
                if (firstField != null)
                    baseType = Utils.Il2CppTypeToCppType(firstField.FieldType.ToTypeDefOrRef().ResolveTypeDef());

                Write("enum class " + validClassname + " : " + baseType);
                Write("{");
                indentLevel++;

                foreach (var field in clazz.Fields)
                {
                    if (!field.IsStatic) continue; 
                    string name = Utils.FormatInvalidName(field.Name);
                    object constant = field.HasConstant ? field.Constant.Value : 0;
                    Write(name + " = " + constant + ",");
                }

                indentLevel--;
                Write("};");
            }
            else
            {
                if (!clazz.IsStruct())
                {
                    Write("class " + validClassname, true, true);
                    string interfaceString = "";

                    if (clazz.BaseType != null)
                    {
                        if (clazz.BaseType.FullName == "System.Object")
                            interfaceString += " : public BNM::IL2CPP::Il2CppObject";
                        else
                        {
                            interfaceString += (interfaceString.Contains(":") ? ", public " : " : public ") + clazz.GetBaseType().DefinitionAssembly.Name.Replace(".dll", "").Replace(".", "").Replace("-", "_") + "::" +  clazz.BaseType.FullName.Replace(".", "::");

                            string asmName = clazz.BaseType.Module.Assembly.Name;
                            string ns = clazz.BaseType.Namespace;
                            string name = clazz.BaseType.Name;
                            string baseInclude = $"#include <SDK/{clazz.GetBaseType().DefinitionAssembly.Name}.dll/Includes/{ns}/{name}.h> // gen waf";
                            if (!fileIncludes.Contains(baseInclude))
                            {
                                fileIncludes += baseInclude + "\n";
                            }
                        }
                       
                    }

                if (clazz.HasInterfaces)
                {
                    foreach (var iface in clazz.Interfaces)
                    {
                        string ifaceAsm = (iface.Interface is TypeRef tref)
                            ? tref.DefinitionAssembly?.Name ?? tref.Scope?.ScopeName ?? "UnknownAssembly"
                            : iface.Interface.Module.Assembly.Name;

                        ifaceAsm = ifaceAsm.Replace(".dll", "").Replace(".", "").Replace("-", "_");
                        string ifaceName = iface.Interface.FullName.Replace(".", "::");

                        if (!interfaceString.Contains(":"))
                            interfaceString += " : public " + ifaceAsm + "::" + ifaceName;
                        else
                            interfaceString += ", public " + ifaceAsm + "::" + ifaceName;

                        string ns = iface.Interface.Namespace;
                        string name = iface.Interface.Name;
                        string include = $"#include <SDK/{ifaceAsm}.dll/Includes/{ns}/{name}.h>\n";

                        if (!fileIncludes.Contains(include))
                            fileIncludes += include;
                    }
                }


                    Write(interfaceString, true, false);
                }
                else
                {
                    Write("struct " + validClassname, true, true);
                }

                Write("", false, true);
                Write("{");
                indentLevel++;

                if (!clazz.IsStruct()) Write("public:");

                if (!clazz.IsStruct())
                {
                    Write("static BNM::Class StaticClass() {");
                    Write($"\treturn BNM::Class(\"{namespaze}\", \"{className}\", BNM::Image(\"{module.Name}\"));");
                    Write("}");
                    Write("");
                }

                ParseFields(clazz);
                Write("");
                ParseMethods(clazz);
                Write("");

                indentLevel--;
                Write("};");
            }

            for (int i = 0; i < nameSpaceSplit.Length; i++)
            {
                indentLevel--;
                Write("}");
            }

            Write("}");

            currentFile.WriteLine(fileIncludes);
            currentFile.WriteLine(fileContent);
        }

        static void ParseClasses()
        {
            if (currentModule == null)
                return;

            foreach (var rid in currentModule.Metadata.GetTypeDefRidList())
            {
                var type = currentModule.ResolveTypeDef(rid);

                if (type == null)
                    continue;

                var module = type.Module;
                var namespaze = type.Namespace.Replace("<", "").Replace(">", "");
                var className = (string)type.Name.Replace("<", "").Replace(">", "");
                var classFilename = string.Concat(className.Split(Path.GetInvalidFileNameChars()));
                var validClassname = Utils.FormatInvalidName(className);

                string outputPath = OUTPUT_DIR;
                outputPath += "/" + module.Name;

                if (!Directory.Exists(outputPath))
                    Directory.CreateDirectory(outputPath);

                if (namespaze.Length > 0)
                {
                    File.AppendAllText(outputPath + "/" + namespaze + ".h", string.Format("#include \"Includes/{0}/{1}.h\"\r\n", namespaze, classFilename));
                }
                else
                {
                    File.AppendAllText(outputPath + "/-.h", string.Format("#include \"Includes/{0}.h\"\r\n", classFilename));
                }

                outputPath += "/Includes";

                if (namespaze.Length > 0)
                {
                    outputPath += "/" + namespaze;
                }

                if (!Directory.Exists(outputPath))
                    Directory.CreateDirectory(outputPath);

                outputPath += "/" + classFilename + ".h";

                currentFile = new StreamWriter(outputPath);

                fileContent = "";
                fileIncludes = "#pragma once\n#include <BNMIncludes.hpp>\n";

                ParseClass(type);
                currentFile.Close();
                

            }
        }
        static void ParseModule(string moduleFile)
        {
            Console.WriteLine("Generating SDK for {0}...", Path.GetFileName(moduleFile));

            ModuleContext modCtx = ModuleDef.CreateModuleContext();
            currentModule = ModuleDefMD.Load(moduleFile, modCtx);

            string moduleOutput = OUTPUT_DIR + "/" + currentModule.Name;

            if (!Directory.Exists(moduleOutput))
                Directory.CreateDirectory(moduleOutput);

            ParseClasses();
        }
        static void Main(string[] args)
        {
            if(args.Length < 1)
            {
                Console.WriteLine("Invalid Arguments!");
                return;
            }

            if (Directory.Exists(OUTPUT_DIR))
                Directory.Delete(OUTPUT_DIR, true);

            if (Directory.Exists(args[0]))
            {
                foreach(var file in Directory.GetFiles(args[0]))
                {
                    ParseModule(file);
                }
            }
            else
            {
                ParseModule(args[0]);
            }
        }
    }
}
;