using System.Collections.Generic;
using Mono.Cecil;

public static class Patcher
{
    public static IEnumerable<string> TargetDLLs { get; } = new[] { "GameScripts.dll" };

#if RUN
    const FieldAttributes VISIBILITY = FieldAttributes.Public;
#else
    const FieldAttributes VISIBILITY = FieldAttributes.Private;
#endif

    public static void Patch(AssemblyDefinition asm)
    {
        var module = asm.MainModule;

        var gameCardType = module.GetType("GameCard");
        gameCardType.Fields.Add(new FieldDefinition("BeneLastChild", VISIBILITY, gameCardType));
        gameCardType.Fields.Add(new FieldDefinition("BeneRoot", VISIBILITY, gameCardType));
        gameCardType.Fields.Add(
            new FieldDefinition("BeneFrameMod", VISIBILITY, module.ImportReference(typeof(int)))
        );

        var equipableType = module.GetType("Equipable");
        equipableType.Fields.Add(
            new FieldDefinition("BeneUpdatedOnce", VISIBILITY, module.ImportReference(typeof(bool)))
        );
    }

#if RUN
    public static void Main(string[] args)
    {
        var managedPath = @"G:\Steam\steamapps\common\Stacklands\Stacklands_Data\Managed";
        var inPath = managedPath + @"\publicized_assemblies\GameScripts_publicized.dll";
        var outPath = managedPath + @"\publicized_assemblies\GameScripts_patched.dll";

        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(managedPath);

        var asm = AssemblyDefinition.ReadAssembly(
            inPath,
            new ReaderParameters { AssemblyResolver = resolver }
        );
        Patch(asm);
        asm.Write(outPath);
    }
#endif
}
