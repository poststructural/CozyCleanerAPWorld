using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

// BepInEx 5 preloader patcher. Cozy Cleaner ships a stripped UnityEngine.CoreModule, and BepInEx's chainloader
// calls members that were stripped (SystemInfo.graphicsDeviceID in its headless check). Mono throws
// MissingMethodException when it JITs a method that references them, which aborts the chainloader inside
// Application's static constructor. This patcher adds small managed stubs for those members before the
// chainloader runs. It never touches members that still exist.
public static class CozyCleanerStripFix
{
    public static IEnumerable<string> TargetDLLs { get; } = new[] { "UnityEngine.CoreModule.dll" };

    public static void Patch(AssemblyDefinition assembly)
    {
        var module = assembly.MainModule;
        var systemInfo = module.GetType("UnityEngine.SystemInfo");
        if (systemInfo != null)
            AddStaticGetter(module, systemInfo, "graphicsDeviceID", module.TypeSystem.Int32, 1);
        var application = module.GetType("UnityEngine.Application");
        if (application != null)
            AddStaticGetter(module, application, "isBatchMode", module.TypeSystem.Boolean, 0);
    }

    static void AddStaticGetter(ModuleDefinition module, TypeDefinition type, string name, TypeReference ret, int value)
    {
        string getterName = "get_" + name;
        if (type.Methods.Any(m => m.Name == getterName))
            return;
        var getter = new MethodDefinition(getterName,
            MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.SpecialName | MethodAttributes.HideBySig, ret);
        var il = getter.Body.GetILProcessor();
        il.Emit(OpCodes.Ldc_I4, value);
        il.Emit(OpCodes.Ret);
        type.Methods.Add(getter);
        if (!type.Properties.Any(p => p.Name == name))
            type.Properties.Add(new PropertyDefinition(name, PropertyAttributes.None, ret) { GetMethod = getter });
    }
}
