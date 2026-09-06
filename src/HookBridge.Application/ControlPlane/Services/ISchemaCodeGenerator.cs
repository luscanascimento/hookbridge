namespace HookBridge.Application.ControlPlane.Services;

public sealed record SchemaDocumentationResult(
    string MarkdownDocs,
    string TypeScriptSnippet,
    string CSharpSnippet,
    string SamplePayloadJson);

public interface ISchemaCodeGenerator
{
    string GenerateTypeScript(string eventType, string schemaJson);
    string GenerateCSharp(string eventType, string schemaJson);
    string GenerateMarkdownDocs(string eventType, string schemaName, string version, string schemaJson, string? description);
    string GenerateSamplePayload(string schemaJson);
    SchemaDocumentationResult GenerateAll(string eventType, string schemaName, string version, string schemaJson, string? description);
}
