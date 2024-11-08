using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;
using Formatter = Microsoft.CodeAnalysis.Formatting.Formatter;

var dbContextPath = @"<DbContextPath>";
var outputDir = @"<OutputDirectory>";

var dbContextCode = File.ReadAllText(dbContextPath);
var syntaxTree = CSharpSyntaxTree.ParseText(dbContextCode);
var root = syntaxTree.GetRoot();

var onModelCreatingMethod = root.DescendantNodes()
                                .OfType<MethodDeclarationSyntax>()
                                .FirstOrDefault(m => m.Identifier.Text == "OnModelCreating");

var modelBuilderInvocations = onModelCreatingMethod?.DescendantNodes()
                                                    .OfType<InvocationExpressionSyntax>()
                                                    .Where(i => i.Expression.ToString().Contains("modelBuilder.Entity"))
                                                    .ToList();

foreach (var invocation in modelBuilderInvocations)
{
    string modelName = Regex.Match(invocation.ToString(), @"<([^>]+)>").Groups[1].Value;
    var entityConfigCode = ExtractConfigCode(invocation);

    if (!string.IsNullOrEmpty(entityConfigCode))
    {
        var configFilePath = Path.Combine(outputDir, $"{modelName}Configuration.cs");
        GenerateConfigFile(configFilePath, $"{modelName}Configuration", modelName, entityConfigCode);
    }
}

static string ExtractConfigCode(InvocationExpressionSyntax invocation)
{
    var lambda = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression as SimpleLambdaExpressionSyntax;
    return lambda?.Body is BlockSyntax lambdaBlock
               ? string.Join(Environment.NewLine, lambdaBlock.Statements.Select(s => $"builder.{s.ToString().Trim()};"))
               : string.Empty;
}

static void GenerateConfigFile(string path, string className, string modelName, string content)
{
    var code = $@"
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace YourNamespace
{{
    public class {className} : IEntityTypeConfiguration<{modelName}>
    {{
        public void Configure(EntityTypeBuilder<{modelName}> builder)
        {{
            {content}
        }}
    }}
}}";

    File.WriteAllText(path, FormatCode(code));
    Console.WriteLine($"Generated: {path}");
}

static string FormatCode(string code)
{
    var syntaxTree = CSharpSyntaxTree.ParseText(code);
    var formattedRoot = Formatter.Format(syntaxTree.GetRoot(), new AdhocWorkspace());
    return formattedRoot.ToFullString();
}