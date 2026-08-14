using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TrelamiumIII.Generators;

[Generator(LanguageNames.CSharp)]
public sealed class GenerateAttributeGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static ctx => ctx.AddSource(
            "GenerateAttribute.g.cs",
            """
            namespace TrelamiumIII;

            [System.AttributeUsage(System.AttributeTargets.Class)]
            internal sealed class GenerateAttribute : System.Attribute { }
            """
        ));

        var classDeclarations = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "TrelamiumIII.GenerateAttribute",
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, _) => (ClassDeclarationSyntax)ctx.TargetNode
            );

        var compilation = context.CompilationProvider.Combine(classDeclarations.Collect());

        context.RegisterSourceOutput(compilation, static (ctx, source) =>
        {
            var (compilation, classes) = source;
            foreach (var cls in classes)
                Generate(ctx, compilation, cls);
        });
    }

    private static void Generate(
        SourceProductionContext ctx,
        Compilation compilation,
        ClassDeclarationSyntax cls)
    {
        var model = compilation.GetSemanticModel(cls.SyntaxTree);
        var symbol = model.GetDeclaredSymbol(cls);

        if (symbol is null)
            return;

        var source = $$"""
            namespace {{symbol.ContainingNamespace}};

            partial class {{symbol.Name}}
            {
                // generated code here
            }
            """;

        ctx.AddSource($"{symbol.Name}.g.cs", source);
    }
}
