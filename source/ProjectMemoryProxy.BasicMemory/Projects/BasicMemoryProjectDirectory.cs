namespace ProjectMemoryProxy.BasicMemory.Projects;

using ModelContextProtocol.Protocol;
using ProjectMemoryProxy.BasicMemory.Discovery;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Provides read-only discovery and identity validation for local Basic Memory projects.
/// </summary>
internal sealed class BasicMemoryProjectDirectory : IBasicMemoryProjectDirectory
{
    #region Static Fields

    private const string ListMemoryProjectsToolName = "list_memory_projects";
    private const string LocalProjectSource = "local";

    #endregion

    #region Private Fields

    private readonly BasicMemoryToolCatalog _toolCatalog;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="BasicMemoryProjectDirectory"/> class.
    /// </summary>
    /// <param name="toolCatalog">The discovered Basic Memory tool catalog.</param>
    public BasicMemoryProjectDirectory(BasicMemoryToolCatalog toolCatalog)
    {
        _toolCatalog = toolCatalog ?? throw new ArgumentNullException(nameof(toolCatalog));
    }

    #endregion

    #region Properties

    #endregion

    #region Public Methods

    /// <inheritdoc />
    public async Task<IReadOnlyList<BasicMemoryProjectInfo>> ListAsync(CancellationToken cancellationToken = default)
    {
        await _toolCatalog.InitializeAsync(cancellationToken).ConfigureAwait(false);

        if (!_toolCatalog.TryGetTool(ListMemoryProjectsToolName, out var tool))
            throw new InvalidOperationException($"Basic Memory does not expose the required '{ListMemoryProjectsToolName}' project lifecycle tool.");

        var result = await tool.CallAsync(new Dictionary<string, object?> { ["output_format"] = "json" }, cancellationToken: cancellationToken).ConfigureAwait(false);
        var payload = GetResultPayload(result);
        if (!payload.TryGetProperty("projects", out var projectsElement) || projectsElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException($"Basic Memory '{ListMemoryProjectsToolName}' returned structured JSON without a projects array.");
        
        var projectIds = new HashSet<Guid>();
        var projectNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var projects = new List<BasicMemoryProjectInfo>();
        foreach (var projectElement in projectsElement.EnumerateArray())
        {
            if (projectElement.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException($"Basic Memory '{ListMemoryProjectsToolName}' returned a non-object project entry.");

            var projectName = GetRequiredString(projectElement, "name");
            var externalIdValue = GetRequiredString(projectElement, "external_id");
            var source = GetRequiredString(projectElement, "source");
            if (!string.Equals(source, LocalProjectSource, StringComparison.Ordinal))
                throw new InvalidOperationException($"Basic Memory returned project '{projectName}' from unsupported source '{source}'. ProjectMemoryProxy currently supports local projects only.");

            if (!Guid.TryParse(externalIdValue, out var projectId) || projectId == Guid.Empty)
                throw new InvalidOperationException($"Basic Memory returned project '{projectName}' with invalid external_id '{externalIdValue}'.");

            if (!projectIds.Add(projectId))
                throw new InvalidOperationException($"Basic Memory returned duplicate project external_id '{projectId:D}'.");

            if (!projectNames.Add(projectName))
                throw new InvalidOperationException($"Basic Memory returned duplicate project name '{projectName}'.");

            projects.Add(new BasicMemoryProjectInfo(projectId, projectName));
        }

        return projects.AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<BasicMemoryProjectValidationResult> ValidateAsync(Guid memoryProjectId, string memoryProjectName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(memoryProjectName);
        if (memoryProjectId == Guid.Empty)
            throw new ArgumentException("The Basic Memory project identifier must not be empty.", nameof(memoryProjectId));

        var projects = await ListAsync(cancellationToken).ConfigureAwait(false);
        BasicMemoryProjectInfo? projectById = null;
        BasicMemoryProjectInfo? projectByName = null;
        foreach (var project in projects)
        {
            if (project.MemoryProjectId == memoryProjectId)
                projectById = project;

            if (string.Equals(project.MemoryProjectName, memoryProjectName, StringComparison.Ordinal))
                projectByName = project;
        }

        var status = GetValidationStatus(projectById, projectByName);
        return new BasicMemoryProjectValidationResult(status, projectById, projectByName);
    }

    #endregion

    #region Private Methods

    private static BasicMemoryProjectValidationStatus GetValidationStatus(BasicMemoryProjectInfo? projectById, BasicMemoryProjectInfo? projectByName)
    {
        if (projectById != null && projectByName != null)
        {
            return projectById.MemoryProjectId == projectByName.MemoryProjectId ?
                       BasicMemoryProjectValidationStatus.ExactMatch :
                       BasicMemoryProjectValidationStatus.IdentityConflict;
        }

        if (projectById != null)
            return BasicMemoryProjectValidationStatus.NameMismatch;

        if (projectByName != null)
            return BasicMemoryProjectValidationStatus.IdMismatch;

        return BasicMemoryProjectValidationStatus.NotFound;
    }

    private static JsonElement GetResultPayload(CallToolResult result)
    {
        if (result.IsError is true)
        {
            var errorText = string.Join(Environment.NewLine, result.Content.OfType<TextContentBlock>().Select(block => block.Text));
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(errorText) ?
                                                    $"Basic Memory '{ListMemoryProjectsToolName}' returned an MCP tool error." :
                                                    $"Basic Memory '{ListMemoryProjectsToolName}' returned an MCP tool error: {errorText}");
        }

        if (result.StructuredContent is not { } structuredContent)
            throw new InvalidOperationException($"Basic Memory '{ListMemoryProjectsToolName}' returned no structured content.");

        var payload = structuredContent;
        if (payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("result", out var wrappedResult))
        {
            payload = wrappedResult;
        }

        if (payload.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException($"Basic Memory '{ListMemoryProjectsToolName}' returned an unsupported structured result.");

        return payload;
    }

    private static string GetRequiredString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(property.GetString()))
            throw new InvalidOperationException($"Basic Memory '{ListMemoryProjectsToolName}' returned a project without a valid '{propertyName}' value.");

        return property.GetString()!;
    }

    #endregion
}
