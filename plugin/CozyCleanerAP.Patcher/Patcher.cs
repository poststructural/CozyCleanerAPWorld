using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;

// BepInEx 5 preloader patcher. Cozy Cleaner ships a stripped UnityEngine.CoreModule, and BepInEx's chainloader and
// our plugin call members that were stripped. Which members are gone differs per platform build (Windows lost
// SystemInfo.graphicsDeviceID and Application.isBatchMode; macOS also lost Application.unityVersion). Mono throws
// MissingMethodException when it JITs a method that references them, which aborts the chainloader inside
// Application's static constructor and leaves the game stuck loading.
//
// So before the chainloader runs, this patcher reads every UnityEngine.CoreModule member that BepInEx/core and
// BepInEx/plugins assemblies reference, and adds a small managed stub for each one the game removed. Members that
// still exist are never touched. Each stub is logged.
public static class CozyCleanerStripFix
{
    public static IEnumerable<string> TargetDLLs { get; } = new[] { "UnityEngine.CoreModule.dll" };

    const string UnityVersion = "2022.3.9f1";
    static readonly ManualLogSource Log = Logger.CreateLogSource("CozyStripFix");

    public static void Patch(AssemblyDefinition assembly)
    {
        var module = assembly.MainModule;
        int added = 0;
        foreach (var path in ReferencingAssemblies())
        {
            ModuleDefinition refModule;
            try { refModule = ModuleDefinition.ReadModule(path); }
            catch { continue; }
            using (refModule)
            {
                foreach (var mr in refModule.GetMemberReferences())
                {
                    try
                    {
                        if (mr is MethodReference m && !(mr is MethodDefinition)) added += StubMethod(module, m, path);
                    }
                    catch (Exception e) { Log.LogWarning("could not stub " + mr.FullName + ": " + e.Message); }
                }
            }
        }
        Log.LogInfo("added " + added + " stub(s) for stripped engine members");
    }

    static IEnumerable<string> ReferencingAssemblies()
    {
        var dirs = new[] { Paths.BepInExAssemblyDirectory, Paths.PluginPath };
        foreach (var d in dirs)
        {
            if (string.IsNullOrEmpty(d) || !Directory.Exists(d)) continue;
            foreach (var f in Directory.GetFiles(d, "*.dll", SearchOption.AllDirectories)) yield return f;
        }
    }

    static int StubMethod(ModuleDefinition module, MethodReference m, string source)
    {
        if (m.HasGenericParameters || m.DeclaringType.IsGenericInstance || m.DeclaringType.HasGenericParameters) return 0;
        var type = module.GetType(m.DeclaringType.FullName);
        if (type == null && m.DeclaringType.IsNested && m.DeclaringType.DeclaringType != null)
        {
            var outer = module.GetType(m.DeclaringType.DeclaringType.FullName);
            type = outer?.NestedTypes.FirstOrDefault(t => t.Name == m.DeclaringType.Name);
        }
        if (type == null || type.IsInterface) return 0;   // type lives in another module, or can't hold a body
        if (type.Methods.Any(d => Same(d, m))) return 0;

        var ret = module.ImportReference(m.ReturnType);
        var attrs = MethodAttributes.Public | MethodAttributes.HideBySig;
        if (!m.HasThis) attrs |= MethodAttributes.Static;
        if (m.Name.StartsWith("get_") || m.Name.StartsWith("set_") || m.Name == ".ctor"
            || m.Name.StartsWith("add_") || m.Name.StartsWith("remove_"))
            attrs |= MethodAttributes.SpecialName;
        if (m.Name == ".ctor") attrs |= MethodAttributes.RTSpecialName;

        var stub = new MethodDefinition(m.Name, attrs, ret);
        foreach (var p in m.Parameters)
            stub.Parameters.Add(new ParameterDefinition(p.Name, p.Attributes, module.ImportReference(p.ParameterType)));
        EmitDefaultBody(module, stub, m.Name);
        type.Methods.Add(stub);

        AttachProperty(type, stub);
        Log.LogInfo("stubbed " + type.FullName + "::" + m.Name + " (used by " + Path.GetFileName(source) + ")");
        return 1;
    }

    static void EmitDefaultBody(ModuleDefinition module, MethodDefinition stub, string name)
    {
        var il = stub.Body.GetILProcessor();
        var rt = stub.ReturnType;
        if (rt.FullName == "System.Void") { il.Emit(OpCodes.Ret); return; }
        if (name == "get_unityVersion" && rt.FullName == "System.String")
        {
            il.Emit(OpCodes.Ldstr, UnityVersion);
            il.Emit(OpCodes.Ret);
            return;
        }
        if (name == "get_graphicsDeviceID" && rt.FullName == "System.Int32")
        {
            il.Emit(OpCodes.Ldc_I4_1);   // non-zero: not a headless/batch run
            il.Emit(OpCodes.Ret);
            return;
        }
        if (rt.IsValueType || rt.IsGenericParameter)
        {
            var v = new VariableDefinition(rt);
            stub.Body.Variables.Add(v);
            stub.Body.InitLocals = true;
            il.Emit(OpCodes.Ldloca_S, v);
            il.Emit(OpCodes.Initobj, rt);
            il.Emit(OpCodes.Ldloc, v);
            il.Emit(OpCodes.Ret);
            return;
        }
        il.Emit(OpCodes.Ldnull);
        il.Emit(OpCodes.Ret);
    }

    static void AttachProperty(TypeDefinition type, MethodDefinition stub)
    {
        if (!stub.Name.StartsWith("get_") && !stub.Name.StartsWith("set_")) return;
        string name = stub.Name.Substring(4);
        var prop = type.Properties.FirstOrDefault(p => p.Name == name);
        if (prop == null)
        {
            var ptype = stub.Name.StartsWith("get_") ? stub.ReturnType : stub.Parameters.Last().ParameterType;
            prop = new PropertyDefinition(name, PropertyAttributes.None, ptype);
            type.Properties.Add(prop);
        }
        if (stub.Name.StartsWith("get_") && prop.GetMethod == null) prop.GetMethod = stub;
        if (stub.Name.StartsWith("set_") && prop.SetMethod == null) prop.SetMethod = stub;
    }

    static bool Same(MethodDefinition d, MethodReference r)
    {
        if (d.Name != r.Name || d.Parameters.Count != r.Parameters.Count || d.HasThis != r.HasThis) return false;
        if (d.GenericParameters.Count != r.GenericParameters.Count) return false;
        for (int i = 0; i < d.Parameters.Count; i++)
            if (d.Parameters[i].ParameterType.FullName != r.Parameters[i].ParameterType.FullName) return false;
        return d.ReturnType.FullName == r.ReturnType.FullName || d.HasGenericParameters;
    }
}
