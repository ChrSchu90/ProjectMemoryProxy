namespace ProjectMemoryProxy.BasicMemory.Projects;

using ProjectMemoryProxy.Core.Routing;
using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Coordinates validated Basic Memory projects with the ProjectMemoryProxy routing registry.
/// </summary>
public sealed class ProjectRegistryManager
{
    #region Static Fields

    #endregion

    #region Private Fields

    private readonly IBasicMemoryProjectDirectory _projectDirectory;
    private readonly IProjectRegistry _projectRegistry;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectRegistryManager"/> class.
    /// </summary>
    /// <param name="projectDirectory">The Basic Memory project directory.</param>
    /// <param name="projectRegistry">The ProjectMemoryProxy routing registry.</param>
    public ProjectRegistryManager(IBasicMemoryProjectDirectory projectDirectory, IProjectRegistry projectRegistry)
    {
        _projectDirectory = projectDirectory ?? throw new ArgumentNullException(nameof(projectDirectory));
        _projectRegistry = projectRegistry ?? throw new ArgumentNullException(nameof(projectRegistry));
    }

    #endregion

    #region Properties

    #endregion

    #region Public Methods

    /// <summary>
    /// Registers an existing Basic Memory project after validating its exact UUID/name identity.
    /// </summary>
    public async Task<ProjectRegistrationResult> RegisterExistingProjectAsync(Guid memoryProjectId, string memoryProjectName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(memoryProjectName);
        if (memoryProjectId == Guid.Empty)
            throw new ArgumentException("The Basic Memory project identifier must not be empty.", nameof(memoryProjectId));

        var validation = await _projectDirectory
            .ValidateAsync(memoryProjectId, memoryProjectName, cancellationToken)
            .ConfigureAwait(false);

        if (validation.Status != BasicMemoryProjectValidationStatus.ExactMatch)
            return new ProjectRegistrationResult(MapValidationStatus(validation.Status), null);

        var existing = await GetExistingRegistrationResultAsync(memoryProjectId, memoryProjectName, cancellationToken).ConfigureAwait(false);
        if (existing != null)
            return existing;

        try
        {
            var routing = await _projectRegistry
                .CreateAsync(memoryProjectId, memoryProjectName, Status.Active, cancellationToken)
                .ConfigureAwait(false);

            return new ProjectRegistrationResult(ProjectRegistrationStatus.Registered, routing);
        }
        catch (ProjectRegistryConflictException)
        {
            // Another writer may have inserted a matching or conflicting routing after the initial reads.
            // Re-read persisted state rather than relying on process-local synchronization.
            return await GetExistingRegistrationResultAsync(memoryProjectId, memoryProjectName, cancellationToken).ConfigureAwait(false)
                ?? new ProjectRegistrationResult(ProjectRegistrationStatus.RegistryWriteConflict, null);
        }
    }

    /// <summary>
    /// Binds an exact technical context to an active registered project routing.
    /// </summary>
    public async Task<ContextBindingRegistrationResult> BindContextAsync(ContextId contextId, Guid memoryProjectId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contextId);

        if (memoryProjectId == Guid.Empty)
            throw new ArgumentException("The Basic Memory project identifier must not be empty.", nameof(memoryProjectId));

        var routing = await _projectRegistry.FindByMemoryProjectIdAsync(memoryProjectId, cancellationToken).ConfigureAwait(false);
        if (routing == null)
            return new ContextBindingRegistrationResult(ContextBindingRegistrationStatus.ProjectRoutingNotFound, null);

        if (routing.Status != Status.Active)
            return new ContextBindingRegistrationResult(ContextBindingRegistrationStatus.ProjectRoutingInactive, null);

        var existing = await GetExistingBindingResultAsync(contextId, memoryProjectId, cancellationToken).ConfigureAwait(false);
        if (existing != null)
            return existing;

        try
        {
            var binding = await _projectRegistry
                .CreateBindingAsync(contextId, memoryProjectId, Status.Active, cancellationToken)
                .ConfigureAwait(false);

            return new ContextBindingRegistrationResult(ContextBindingRegistrationStatus.Bound, binding);
        }
        catch (ProjectRegistryConflictException)
        {
            return await GetExistingBindingResultAsync(contextId, memoryProjectId, cancellationToken).ConfigureAwait(false) ??
                   new ContextBindingRegistrationResult(ContextBindingRegistrationStatus.RegistryWriteConflict, null);
        }
    }

    /// <summary>
    /// Deactivates a registered project routing so it can no longer be used for context resolution.
    /// </summary>
    /// <param name="memoryProjectId">The Basic Memory external project identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the project-routing status change.</returns>
    public Task<ProjectRoutingStatusChangeResult> DeactivateProjectAsync(Guid memoryProjectId, CancellationToken cancellationToken = default)
    {
        return ChangeProjectStatusAsync(memoryProjectId, Status.Inactive, validateBasicMemory: false, cancellationToken);
    }

    /// <summary>
    /// Reactivates a registered project routing after validating its current Basic Memory project identity.
    /// </summary>
    /// <param name="memoryProjectId">The Basic Memory external project identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the project-routing status change.</returns>
    public Task<ProjectRoutingStatusChangeResult> ReactivateProjectAsync(Guid memoryProjectId, CancellationToken cancellationToken = default)
    {
        return ChangeProjectStatusAsync(memoryProjectId, Status.Active, validateBasicMemory: true, cancellationToken);
    }

    /// <summary>
    /// Deactivates an exact technical context binding without changing its target project routing.
    /// </summary>
    /// <param name="contextId">The exact technical context identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the context-binding status change.</returns>
    public Task<ContextBindingStatusChangeResult> DeactivateContextAsync(ContextId contextId, CancellationToken cancellationToken = default)
    {
        return ChangeBindingStatusAsync(contextId, Status.Inactive, requireActiveProject: false, cancellationToken);
    }

    /// <summary>
    /// Reactivates an exact technical context binding when its target project routing is active.
    /// </summary>
    /// <param name="contextId">The exact technical context identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the context-binding status change.</returns>
    public Task<ContextBindingStatusChangeResult> ReactivateContextAsync(ContextId contextId, CancellationToken cancellationToken = default)
    {
        return ChangeBindingStatusAsync(contextId, Status.Active, requireActiveProject: true, cancellationToken);
    }

    #endregion

    #region Private Methods

    private async Task<ProjectRegistrationResult?> GetExistingRegistrationResultAsync(Guid memoryProjectId, string memoryProjectName, CancellationToken cancellationToken = default)
    {
        var projectById = await _projectRegistry.FindByMemoryProjectIdAsync(memoryProjectId, cancellationToken).ConfigureAwait(false);
        var projectByName = await _projectRegistry.FindByMemoryProjectNameAsync(memoryProjectName, cancellationToken).ConfigureAwait(false);
        if (projectById == null && projectByName == null)
            return null;

        if (projectById != null && projectByName != null)
        {
            if (projectById.MemoryProjectId == memoryProjectId &&
                string.Equals(projectById.MemoryProjectName, memoryProjectName, StringComparison.Ordinal) &&
                projectByName.MemoryProjectId == memoryProjectId &&
                string.Equals(projectByName.MemoryProjectName, memoryProjectName, StringComparison.Ordinal))
            {
                return new ProjectRegistrationResult(ProjectRegistrationStatus.AlreadyRegistered, projectById);
            }

            return new ProjectRegistrationResult(ProjectRegistrationStatus.RegistryProjectIdentityConflict, null);
        }

        return projectById != null ?
                   new ProjectRegistrationResult(ProjectRegistrationStatus.RegistryProjectNameMismatch, null) :
                   new ProjectRegistrationResult(ProjectRegistrationStatus.RegistryProjectIdMismatch, null);
    }

    private static ProjectRegistrationStatus MapValidationStatus(BasicMemoryProjectValidationStatus status)
    {
        return status switch
        {
            BasicMemoryProjectValidationStatus.NotFound => ProjectRegistrationStatus.BasicMemoryProjectNotFound,
            BasicMemoryProjectValidationStatus.NameMismatch => ProjectRegistrationStatus.BasicMemoryProjectNameMismatch,
            BasicMemoryProjectValidationStatus.IdMismatch => ProjectRegistrationStatus.BasicMemoryProjectIdMismatch,
            BasicMemoryProjectValidationStatus.IdentityConflict => ProjectRegistrationStatus.BasicMemoryProjectIdentityConflict,
            _ => ProjectRegistrationStatus.BasicMemoryValidationFailed
        };
    }

    private async Task<ContextBindingRegistrationResult?> GetExistingBindingResultAsync(ContextId contextId, Guid memoryProjectId, CancellationToken cancellationToken)
    {
        var binding = await _projectRegistry.FindBindingAsync(contextId, cancellationToken).ConfigureAwait(false);
        if (binding == null)
            return null;

        if (binding.MemoryProjectId == memoryProjectId)
            return new ContextBindingRegistrationResult(ContextBindingRegistrationStatus.AlreadyBound, binding);

        return new ContextBindingRegistrationResult(ContextBindingRegistrationStatus.ContextAlreadyBoundToDifferentProject, null);
    }

    private async Task<ProjectRoutingStatusChangeResult> ChangeProjectStatusAsync(Guid memoryProjectId, Status targetStatus, bool validateBasicMemory, CancellationToken cancellationToken)
    {
        if (memoryProjectId == Guid.Empty)
            throw new ArgumentException("The Basic Memory project identifier must not be empty.", nameof(memoryProjectId));

        var routing = await _projectRegistry.FindByMemoryProjectIdAsync(memoryProjectId, cancellationToken).ConfigureAwait(false);
        if (routing == null)
            return new ProjectRoutingStatusChangeResult(ProjectRoutingStatusChangeStatus.ProjectRoutingNotFound, null);

        if (routing.Status == targetStatus)
            return new ProjectRoutingStatusChangeResult(ProjectRoutingStatusChangeStatus.AlreadyInRequestedState, routing);

        if (validateBasicMemory)
        {
            var validation = await _projectDirectory.ValidateAsync(routing.MemoryProjectId, routing.MemoryProjectName, cancellationToken).ConfigureAwait(false);
            if (validation.Status != BasicMemoryProjectValidationStatus.ExactMatch)
                return new ProjectRoutingStatusChangeResult(MapProjectStatusValidationStatus(validation.Status), null);
        }

        var updated = await _projectRegistry.TryUpdateProjectStatusAsync(memoryProjectId, routing.Status, targetStatus, cancellationToken).ConfigureAwait(false);
        if (updated)
            return new ProjectRoutingStatusChangeResult(ProjectRoutingStatusChangeStatus.Updated, routing with { Status = targetStatus });

        var current = await _projectRegistry.FindByMemoryProjectIdAsync(memoryProjectId, cancellationToken).ConfigureAwait(false);
        if (current == null)
            return new ProjectRoutingStatusChangeResult(ProjectRoutingStatusChangeStatus.ProjectRoutingNotFound, null);

        if (current.Status == targetStatus)
            return new ProjectRoutingStatusChangeResult(ProjectRoutingStatusChangeStatus.AlreadyInRequestedState, current);

        return new ProjectRoutingStatusChangeResult(ProjectRoutingStatusChangeStatus.RegistryWriteConflict, null);
    }

    private async Task<ContextBindingStatusChangeResult> ChangeBindingStatusAsync(ContextId contextId, Status targetStatus, bool requireActiveProject, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contextId);

        var binding = await _projectRegistry.FindBindingAsync(contextId, cancellationToken).ConfigureAwait(false);
        if (binding == null)
            return new ContextBindingStatusChangeResult(ContextBindingStatusChangeStatus.ContextBindingNotFound, null);

        if (binding.Status == targetStatus)
            return new ContextBindingStatusChangeResult(ContextBindingStatusChangeStatus.AlreadyInRequestedState, binding);

        if (requireActiveProject)
        {
            var routing = await _projectRegistry.FindByMemoryProjectIdAsync(binding.MemoryProjectId, cancellationToken).ConfigureAwait(false);
            if (routing == null)
                return new ContextBindingStatusChangeResult(ContextBindingStatusChangeStatus.ProjectRoutingNotFound, null);

            if (routing.Status != Status.Active)
                return new ContextBindingStatusChangeResult(ContextBindingStatusChangeStatus.ProjectRoutingInactive, null);
        }

        var updated = await _projectRegistry.TryUpdateBindingStatusAsync(contextId, binding.Status, targetStatus, cancellationToken).ConfigureAwait(false);
        if (updated)
            return new ContextBindingStatusChangeResult(ContextBindingStatusChangeStatus.Updated, binding with { Status = targetStatus });

        var current = await _projectRegistry.FindBindingAsync(contextId, cancellationToken).ConfigureAwait(false);
        if (current == null)
            return new ContextBindingStatusChangeResult(ContextBindingStatusChangeStatus.ContextBindingNotFound, null);

        if (current.Status == targetStatus)
            return new ContextBindingStatusChangeResult(ContextBindingStatusChangeStatus.AlreadyInRequestedState, current);

        return new ContextBindingStatusChangeResult(ContextBindingStatusChangeStatus.RegistryWriteConflict, null);
    }

    private static ProjectRoutingStatusChangeStatus MapProjectStatusValidationStatus(BasicMemoryProjectValidationStatus status)
    {
        return status switch
        {
            BasicMemoryProjectValidationStatus.NotFound => ProjectRoutingStatusChangeStatus.BasicMemoryProjectNotFound,
            BasicMemoryProjectValidationStatus.NameMismatch => ProjectRoutingStatusChangeStatus.BasicMemoryProjectNameMismatch,
            BasicMemoryProjectValidationStatus.IdMismatch => ProjectRoutingStatusChangeStatus.BasicMemoryProjectIdMismatch,
            BasicMemoryProjectValidationStatus.IdentityConflict => ProjectRoutingStatusChangeStatus.BasicMemoryProjectIdentityConflict,
            _ => ProjectRoutingStatusChangeStatus.BasicMemoryValidationFailed
        };
    }

    #endregion
}
