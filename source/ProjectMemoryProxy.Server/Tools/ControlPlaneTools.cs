namespace ProjectMemoryProxy.Server.Tools;

using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using ProjectMemoryProxy.BasicMemory.Projects;
using ProjectMemoryProxy.BasicMemory.Projects.ControlPlane;
using ProjectMemoryProxy.Core.Routing;

/// <summary>
/// Provides MCP tools for explicit ProjectMemoryProxy control-plane operations.
/// </summary>
[McpServerToolType]
internal sealed class ControlPlaneTools
{
    #region Static Fields

    #endregion

    #region Private Fields

    private readonly ProjectRegistryManager _projectRegistryManager;
    private readonly IProjectRegistry _projectRegistry;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ControlPlaneTools"/> class.
    /// </summary>
    /// <param name="projectRegistryManager">The validated project-registry manager.</param>
    /// <param name="projectRegistry">The routing registry.</param>
    public ControlPlaneTools(ProjectRegistryManager projectRegistryManager, IProjectRegistry projectRegistry)
    {
        _projectRegistryManager = projectRegistryManager ?? throw new ArgumentNullException(nameof(projectRegistryManager));
        _projectRegistry = projectRegistry ?? throw new ArgumentNullException(nameof(projectRegistry));
    }

    #endregion

    #region Properties

    #endregion

    #region Public Methods

    /// <summary>
    /// Lists all registered project routings.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The current project-routing inventory.</returns>
    [McpServerTool(Name = "list_projects", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Lists all ProjectMemoryProxy project routings with their exact Basic Memory project identifiers, names, and statuses.")]
    public async Task<ListProjectsResult> ListProjectsAsync(CancellationToken cancellationToken = default)
    {
        var projects = await _projectRegistry.ListProjectsAsync(cancellationToken).ConfigureAwait(false);
        return new ListProjectsResult(projects.Select(ToProjectRoutingInfo).ToArray());
    }

    /// <summary>
    /// Lists all registered exact technical context bindings.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The current context-binding inventory.</returns>
    [McpServerTool(Name = "list_context_bindings", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Lists all exact ProjectMemoryProxy context bindings with their target Basic Memory project identifiers and statuses.")]
    public async Task<ListContextBindingsResult> ListContextBindingsAsync(CancellationToken cancellationToken = default)
    {
        var bindings = await _projectRegistry.ListBindingsAsync(cancellationToken).ConfigureAwait(false);
        return new ListContextBindingsResult(bindings.Select(ToContextBindingInfo).ToArray());
    }

    /// <summary>
    /// Creates a local Basic Memory project and registers its validated ProjectMemoryProxy routing.
    /// </summary>
    /// <param name="project_name">The exact Basic Memory project name.</param>
    /// <param name="project_path">The fully qualified local filesystem path for the Basic Memory project.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The project creation result.</returns>
    [McpServerTool(Name = "create_project", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Creates a local Basic Memory project at an explicit absolute path and registers its validated ProjectMemoryProxy routing.")]
    public async Task<ProjectControlPlaneResult> CreateProjectAsync(
        [Description("Exact Basic Memory project name to create.")] string project_name,
        [Description("Fully qualified local filesystem path where Basic Memory should create the project.")] string project_path,
        CancellationToken cancellationToken = default)
    {
        var result = await _projectRegistryManager.CreateProjectAsync(project_name, project_path, cancellationToken).ConfigureAwait(false);
        return new ProjectControlPlaneResult(GetExternalStatus(result.Status), result.Routing == null ? null : ToProjectRoutingInfo(result.Routing));
    }

    /// <summary>
    /// Deletes a Basic Memory project and then removes its ProjectMemoryProxy routing and dependent context bindings.
    /// </summary>
    /// <param name="project_id">The exact Basic Memory external project identifier.</param>
    /// <param name="delete_notes">Whether Basic Memory should also delete the project's note files.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The project deletion result.</returns>
    [McpServerTool(Name = "delete_project", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Deletes an explicitly identified Basic Memory project and removes its ProjectMemoryProxy routing after absence is proven. Dependent context bindings are removed with the routing.")]
    public async Task<ProjectControlPlaneResult> DeleteProjectAsync(
        [Description("Exact Basic Memory project UUID to delete.")] Guid project_id,
        [Description("Whether Basic Memory should also delete the project's note files.")] bool delete_notes,
        CancellationToken cancellationToken = default)
    {
        var result = await _projectRegistryManager.DeleteProjectAsync(project_id, delete_notes, cancellationToken).ConfigureAwait(false);
        return new ProjectControlPlaneResult(GetExternalStatus(result.Status), null);
    }

    /// <summary>
    /// Binds an exact technical context identifier to an active registered project routing.
    /// </summary>
    /// <param name="context_id">The canonical technical context identifier.</param>
    /// <param name="project_id">The exact Basic Memory external project identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The context-binding result.</returns>
    [McpServerTool(Name = "bind_context", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Binds an explicit canonical technical context identifier to an explicit active Basic Memory project routing.")]
    public async Task<ContextBindingControlPlaneResult> BindContextAsync(
        [Description("Canonical technical context identifier, for example 'git:github.com/ChrSchu90/ProjectMemoryProxy'.")] string context_id,
        [Description("Exact Basic Memory project UUID to bind the context to.")] Guid project_id,
        CancellationToken cancellationToken = default)
    {
        if (!ContextId.TryParse(context_id, out var parsedContextId))
            return new ContextBindingControlPlaneResult("invalid_context", null);

        var result = await _projectRegistryManager.BindContextAsync(parsedContextId, project_id, cancellationToken).ConfigureAwait(false);
        return new ContextBindingControlPlaneResult(GetExternalStatus(result.Status), result.Binding == null ? null : ToContextBindingInfo(result.Binding));
    }

    /// <summary>
    /// Removes an exact technical context binding without changing its target project routing.
    /// </summary>
    /// <param name="context_id">The canonical technical context identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The context-binding removal result.</returns>
    [McpServerTool(Name = "unbind_context", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Removes an explicit exact ProjectMemoryProxy context binding without changing the target Basic Memory project routing.")]
    public async Task<ContextBindingControlPlaneResult> UnbindContextAsync(
        [Description("Canonical technical context identifier to unbind.")] string context_id,
        CancellationToken cancellationToken = default)
    {
        if (!ContextId.TryParse(context_id, out var parsedContextId))
            return new ContextBindingControlPlaneResult("invalid_context", null);

        var result = await _projectRegistryManager.UnbindContextAsync(parsedContextId, cancellationToken).ConfigureAwait(false);
        return new ContextBindingControlPlaneResult(GetExternalStatus(result.Status), null);
    }

    /// <summary>
    /// Deactivates a registered project routing so it can no longer be used for context resolution.
    /// </summary>
    /// <param name="project_id">The exact Basic Memory external project identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The project-routing status-change result.</returns>
    //[McpServerTool(Name = "deactivate_project", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)] // ToDo: Active if tool is required via MCP
    [Description("Deactivates an explicitly identified ProjectMemoryProxy project routing without deleting the Basic Memory project.")]
    public async Task<ProjectControlPlaneResult> DeactivateProjectAsync(
        [Description("Exact Basic Memory project UUID whose ProjectMemoryProxy routing should be deactivated.")] Guid project_id,
        CancellationToken cancellationToken = default)
    {
        var result = await _projectRegistryManager.DeactivateProjectAsync(project_id, cancellationToken).ConfigureAwait(false);
        return new ProjectControlPlaneResult(GetExternalStatus(result.Status), result.Routing == null ? null : ToProjectRoutingInfo(result.Routing));
    }

    /// <summary>
    /// Reactivates a registered project routing after validating the exact live Basic Memory project identity.
    /// </summary>
    /// <param name="project_id">The exact Basic Memory external project identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The project-routing status-change result.</returns>
    //[McpServerTool(Name = "reactivate_project", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)] // ToDo: Active if tool is required via MCP
    [Description("Reactivates an explicitly identified ProjectMemoryProxy project routing after exact Basic Memory identity validation.")]
    public async Task<ProjectControlPlaneResult> ReactivateProjectAsync(
        [Description("Exact Basic Memory project UUID whose ProjectMemoryProxy routing should be reactivated.")] Guid project_id,
        CancellationToken cancellationToken = default)
    {
        var result = await _projectRegistryManager.ReactivateProjectAsync(project_id, cancellationToken).ConfigureAwait(false);
        return new ProjectControlPlaneResult(GetExternalStatus(result.Status), result.Routing == null ? null : ToProjectRoutingInfo(result.Routing));
    }

    /// <summary>
    /// Deactivates an exact technical context binding without changing its target project routing.
    /// </summary>
    /// <param name="context_id">The canonical technical context identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The context-binding status-change result.</returns>
    //[McpServerTool(Name = "deactivate_context", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)] // ToDo: Active if tool is required via MCP
    [Description("Deactivates an explicit exact ProjectMemoryProxy context binding without changing its target project routing.")]
    public async Task<ContextBindingControlPlaneResult> DeactivateContextAsync(
        [Description("Canonical technical context identifier whose binding should be deactivated.")] string context_id,
        CancellationToken cancellationToken = default)
    {
        if (!ContextId.TryParse(context_id, out var parsedContextId))
            return new ContextBindingControlPlaneResult("invalid_context", null);

        var result = await _projectRegistryManager.DeactivateContextAsync(parsedContextId, cancellationToken).ConfigureAwait(false);
        return new ContextBindingControlPlaneResult(GetExternalStatus(result.Status), result.Binding == null ? null : ToContextBindingInfo(result.Binding));
    }

    /// <summary>
    /// Reactivates an exact technical context binding when its target project routing is active.
    /// </summary>
    /// <param name="context_id">The canonical technical context identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The context-binding status-change result.</returns>
    //[McpServerTool(Name = "reactivate_context", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)] // ToDo: Active if tool is required via MCP
    [Description("Reactivates an explicit exact ProjectMemoryProxy context binding when its target project routing is active.")]
    public async Task<ContextBindingControlPlaneResult> ReactivateContextAsync(
        [Description("Canonical technical context identifier whose binding should be reactivated.")] string context_id,
        CancellationToken cancellationToken = default)
    {
        if (!ContextId.TryParse(context_id, out var parsedContextId))
            return new ContextBindingControlPlaneResult("invalid_context", null);

        var result = await _projectRegistryManager.ReactivateContextAsync(parsedContextId, cancellationToken).ConfigureAwait(false);
        return new ContextBindingControlPlaneResult(GetExternalStatus(result.Status), result.Binding == null ? null : ToContextBindingInfo(result.Binding));
    }

    #endregion

    #region Private Methods

    private static ProjectRoutingInfo ToProjectRoutingInfo(ProjectRouting routing)
    {
        return new ProjectRoutingInfo(routing.MemoryProjectId, routing.MemoryProjectName, GetExternalStatus(routing.Status));
    }

    private static ContextBindingInfo ToContextBindingInfo(ContextBinding binding)
    {
        return new ContextBindingInfo(binding.ContextId.ToString(), binding.MemoryProjectId, GetExternalStatus(binding.Status));
    }

    private static string GetExternalStatus(Status status)
    {
        return status switch
        {
            Status.Active => "active",
            Status.Inactive => "inactive",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };
    }

    private static string GetExternalStatus(ProjectCreationStatus status)
    {
        return status switch
        {
            ProjectCreationStatus.Created => "created",
            ProjectCreationStatus.Recovered => "recovered",
            ProjectCreationStatus.AlreadyRegistered => "already_registered",
            ProjectCreationStatus.BasicMemoryProjectNameMismatch => "basic_memory_project_name_mismatch",
            ProjectCreationStatus.BasicMemoryProjectPathMismatch => "basic_memory_project_path_mismatch",
            ProjectCreationStatus.BasicMemoryProjectNotFound => "basic_memory_project_not_found",
            ProjectCreationStatus.BasicMemoryProjectIdMismatch => "basic_memory_project_id_mismatch",
            ProjectCreationStatus.BasicMemoryProjectIdentityConflict => "basic_memory_project_identity_conflict",
            ProjectCreationStatus.BasicMemoryValidationFailed => "basic_memory_validation_failed",
            ProjectCreationStatus.RegistryProjectNameMismatch => "registry_project_name_mismatch",
            ProjectCreationStatus.RegistryProjectIdMismatch => "registry_project_id_mismatch",
            ProjectCreationStatus.RegistryProjectIdentityConflict => "registry_project_identity_conflict",
            ProjectCreationStatus.RegistryWriteConflict => "registry_write_conflict",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };
    }

    private static string GetExternalStatus(ProjectDeletionStatus status)
    {
        return status switch
        {
            ProjectDeletionStatus.Deleted => "deleted",
            ProjectDeletionStatus.Recovered => "recovered",
            ProjectDeletionStatus.AlreadyDeleted => "already_deleted",
            ProjectDeletionStatus.BasicMemoryProjectNameMismatch => "basic_memory_project_name_mismatch",
            ProjectDeletionStatus.BasicMemoryProjectIdMismatch => "basic_memory_project_id_mismatch",
            ProjectDeletionStatus.BasicMemoryProjectIdentityConflict => "basic_memory_project_identity_conflict",
            ProjectDeletionStatus.BasicMemoryDeleteNotCompleted => "basic_memory_delete_not_completed",
            ProjectDeletionStatus.BasicMemoryValidationFailed => "basic_memory_validation_failed",
            ProjectDeletionStatus.RegistryWriteConflict => "registry_write_conflict",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };
    }

    private static string GetExternalStatus(ProjectRoutingStatusChangeStatus status)
    {
        return status switch
        {
            ProjectRoutingStatusChangeStatus.Updated => "updated",
            ProjectRoutingStatusChangeStatus.AlreadyInRequestedState => "already_in_requested_state",
            ProjectRoutingStatusChangeStatus.ProjectRoutingNotFound => "project_routing_not_found",
            ProjectRoutingStatusChangeStatus.BasicMemoryProjectNotFound => "basic_memory_project_not_found",
            ProjectRoutingStatusChangeStatus.BasicMemoryProjectNameMismatch => "basic_memory_project_name_mismatch",
            ProjectRoutingStatusChangeStatus.BasicMemoryProjectIdMismatch => "basic_memory_project_id_mismatch",
            ProjectRoutingStatusChangeStatus.BasicMemoryProjectIdentityConflict => "basic_memory_project_identity_conflict",
            ProjectRoutingStatusChangeStatus.BasicMemoryValidationFailed => "basic_memory_validation_failed",
            ProjectRoutingStatusChangeStatus.RegistryWriteConflict => "registry_write_conflict",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };
    }

    private static string GetExternalStatus(ContextBindingRegistrationStatus status)
    {
        return status switch
        {
            ContextBindingRegistrationStatus.Bound => "bound",
            ContextBindingRegistrationStatus.AlreadyBound => "already_bound",
            ContextBindingRegistrationStatus.ProjectRoutingNotFound => "project_routing_not_found",
            ContextBindingRegistrationStatus.ProjectRoutingInactive => "project_routing_inactive",
            ContextBindingRegistrationStatus.ContextAlreadyBoundToDifferentProject => "context_already_bound_to_different_project",
            ContextBindingRegistrationStatus.RegistryWriteConflict => "registry_write_conflict",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };
    }

    private static string GetExternalStatus(ContextBindingRemovalStatus status)
    {
        return status switch
        {
            ContextBindingRemovalStatus.Unbound => "unbound",
            ContextBindingRemovalStatus.AlreadyUnbound => "already_unbound",
            ContextBindingRemovalStatus.RegistryWriteConflict => "registry_write_conflict",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };
    }

    private static string GetExternalStatus(ContextBindingStatusChangeStatus status)
    {
        return status switch
        {
            ContextBindingStatusChangeStatus.Updated => "updated",
            ContextBindingStatusChangeStatus.AlreadyInRequestedState => "already_in_requested_state",
            ContextBindingStatusChangeStatus.ContextBindingNotFound => "context_binding_not_found",
            ContextBindingStatusChangeStatus.ProjectRoutingNotFound => "project_routing_not_found",
            ContextBindingStatusChangeStatus.ProjectRoutingInactive => "project_routing_inactive",
            ContextBindingStatusChangeStatus.RegistryWriteConflict => "registry_write_conflict",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };
    }

    #endregion
}
