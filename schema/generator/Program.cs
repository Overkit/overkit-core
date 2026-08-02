// Générateur de types C# du State Bus (EXG-020) : le JSON Schema est la seule
// source ; ce programme produit host/Overkit.Contracts/StateBus.g.cs.
// Usage : dotnet run --project schema/generator   (depuis la racine du repo)
using NJsonSchema;
using NJsonSchema.CodeGeneration.CSharp;

var schemaPath = args.Length > 0 ? args[0] : Path.Combine("schema", "state-bus.v1.schema.json");
var outputPath = args.Length > 1 ? args[1] : Path.Combine("host", "Overkit.Contracts", "StateBus.g.cs");

if (!File.Exists(schemaPath))
{
    Console.Error.WriteLine($"Schéma introuvable : {Path.GetFullPath(schemaPath)} — lancer depuis la racine du repo.");
    return 1;
}

var schema = await JsonSchema.FromFileAsync(schemaPath);

var settings = new CSharpGeneratorSettings
{
    Namespace = "Overkit.Contracts",
    JsonLibrary = CSharpJsonLibrary.SystemTextJson,
    ClassStyle = CSharpClassStyle.Poco,
    GenerateNullableReferenceTypes = true,
    GenerateOptionalPropertiesAsNullable = true,
    GenerateDataAnnotations = false,
};

// Le schéma est une enveloppe oneOf : on génère une classe par définition
// (le host aiguille sur le champ `type` avant de désérialiser).
var resolver = new CSharpTypeResolver(settings);
resolver.RegisterSchemaDefinitions(schema.Definitions);
var generator = new CSharpGenerator(schema, settings, resolver);
var code = generator.GenerateFile();

var header = """
    //------------------------------------------------------------------------------
    // GÉNÉRÉ — NE PAS ÉDITER À LA MAIN.
    // Source : schema/state-bus.v1.schema.json
    // Regénération : dotnet run --project schema/generator
    //------------------------------------------------------------------------------

    """;

File.WriteAllText(outputPath, header + code);
Console.WriteLine($"Généré : {outputPath} ({new FileInfo(outputPath).Length:N0} octets)");
return 0;
