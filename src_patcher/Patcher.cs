using System.Collections.Generic;
using Mono.Cecil;

public static class Patcher
{
    public static IEnumerable<string> TargetDLLs { get; } = new[] { "GameScripts.dll" };

    public static void Patch(AssemblyDefinition asm)
    {
        var module = asm.MainModule;
        module
            .GetType("CardData")
            .Fields.Add(
                new FieldDefinition(
                    "Bene",
                    FieldAttributes.Public,
                    module.ImportReference(typeof(int))
                )
            );
    }
}
