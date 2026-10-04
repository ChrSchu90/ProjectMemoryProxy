namespace ProjectMemoryProxy.BasicMemory.Projects;

using System;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using ProjectMemoryProxy.BasicMemory.Projects.ControlPlane;
using ProjectMemoryProxy.BasicMemory.Projects.Directory;
using ProjectMemoryProxy.BasicMemory.Projects.Lifecycle;
using ProjectMemoryProxy.Core.Routing;

/// <summary>
/// Coordinates validated Basic Memory projects with the ProjectMemoryProxy routing registry.
/// </summary>
public sealed class ProjectRegistryManager
{
    #region Static Fields

    #endregion

    #region Private Fields

    private readonly IBasicMemoryProjectDirectory _projectDirectory;
    private readonly IBasicMemoryProjectLifecycle _projectLifecycle;
    private readonly IProjectRegistry _projectRegistry;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectRegistryManager" /> class.
    /// </summary>
    /// <param name="projectDirectory">The Basic Memory project directory.</param>
    /// <param name="projectLifecycle">The project lifecycle.</param>
    /// <param name="projectRegistry">The ProjectMemoryProxy routing registry.</param>
    public ProjectRegistryManager(IBasicMemoryProjectDirectory projectDirectory, IBasicMemoryProjectLifecycle projectLifecycle, IProjectRegistry projectRegistry)
    {
        _projectDirectory = projectDirectory ?? throw new ArgumentNullException(nameof(projectDirectory));
        _projectLifecycle = projectLifecycle ?? throw new ArgumentNullException(nameof(projectLifecycle));
        _projectRegistry = projectRegistry ?? throw new ArgumentNullException(nameof(projectRegistry));
    }

    #endregion

    #region Properties

    #endregion

    #region Public Methods

    /// <summary>
    /// Creates a local Basic Memory project and registers its validated routing.
    /// </summary>
    /// <param name="memoryProjectName">The Basic Memory project name.</param>
    /// <param name="memoryProjectPath">The absolute local Basic Memory project path.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The project creation and registration result.</returns>
    public async Task<ProjectCreationResult> CreateProjectAsync(string memoryProjectName, string memoryProjectPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(memoryProjectName);
        ArgumentException.ThrowIfNullOrWhiteSpace(memoryProjectPath);
        if (!Path.IsPathFullyQualified(memoryProjectPath))
            throw new ArgumentException("The Basic Memory project path must be fully qualified.", nameof(memoryProjectPath));

        var normalizedProjectPath = NormalizeProjectPath(memoryProjectPath);
        var existingRouting = await _projectRegistry.FindByMemoryProjectNameAsync(memoryProjectName, cancellationToken).ConfigureAwait(false);
        if (existingRouting != null)
        {
            var existingValidation = await _projectDirectory.ValidateAsync(existingRouting.MemoryProjectId, existingRouting.MemoryProjectName, cancellationToken).ConfigureAwait(false);
            if (existingValidation.Status == BasicMemoryProjectValidationStatus.ExactMatch)
                return new ProjectCreationResult(ProjectCreationStatus.AlreadyRegistered, existingRouting);

            return new ProjectCreationResult(MapCreationValidationStatus(existingValidation.Status), null);
        }

        var creation = await _projectLifecycle.CreateAsync(memoryProjectName, normalizedProjectPath, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(creation.Project.MemoryProjectName, memoryProjectName, StringComparison.Ordinal))
            return new ProjectCreationResult(ProjectCreationStatus.BasicMemoryProjectNameMismatch, null);

        if (creation.Status == BasicMemoryProjectCreationStatus.AlreadyExists && !ProjectPathsEqual(normalizedProjectPath, creation.ProjectPath))
            return new ProjectCreationResult(ProjectCreationStatus.BasicMemoryProjectPathMismatch, null);

        var validation = await _projectDirectory.ValidateAsync(creation.Project.MemoryProjectId, creation.Project.MemoryProjectName, cancellationToken).ConfigureAwait(false);
        if (validation.Status != BasicMemoryProjectValidationStatus.ExactMatch)
            return new ProjectCreationResult(MapCreationValidationStatus(validation.Status), null);

        var registration = await RegisterValidatedProjectAsync(creation.Project.MemoryProjectId, creation.Project.MemoryProjectName, cancellationToken).ConfigureAwait(false);
        return MapCreationRegistrationResult(creation.Status, registration);
    }

    /// <summary>
    /// Deletes a registered Basic Memory project before removing its ProjectMemoryProxy routing and dependent context bindings.
    /// </summary>
    /// <param name="memoryProjectId">The Basic Memory external project identifier.</param>
    /// <param name="deleteNotes">Whether Basic Memory should also delete the project's note files.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The project deletion result.</returns>
    public async Task<ProjectDeletionResult> DeleteProjectAsync(Guid memoryProjectId, bool deleteNotes, CancellationToken cancellationToken = default)
    {
        if (memoryProjectId == Guid.Empty)
            throw new ArgumentException("The Basic Memory project identifier must not be empty.", nameof(memoryProjectId));

        var routing = await _projectRegistry.FindByMemoryProjectIdAsync(memoryProjectId, cancellationToken).ConfigureAwait(false);
        if (routing == null)
            return new ProjectDeletionResult(ProjectDeletionStatus.AlreadyDeleted);

        var validation = await _projectDirectory.ValidateAsync(memoryProjectId, routing.MemoryProjectName, cancellationToken).ConfigureAwait(false);
        if (validation.Status == BasicMemoryProjectValidationStatus.NotFound)
        {
            return await RemoveDeletedProjectRoutingAsync(memoryProjectId, ProjectDeletionStatus.Recovered, cancellationToken).ConfigureAwait(false);
        }

        if (validation.Status != BasicMemoryProjectValidationStatus.ExactMatch)
            return new ProjectDeletionResult(MapDeletionValidationStatus(validation.Status));

        Exception? deleteException = null;

        try
        {
            await _projectLifecycle.DeleteAsync(routing.MemoryProjectName, deleteNotes, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            // The remote delete may have committed before the MCP call failed.
            // Re-read Basic Memory before deciding whether the operation failed.
            deleteException = exception;
        }

        var postDeleteValidation = await _projectDirectory.ValidateAsync(memoryProjectId, routing.MemoryProjectName, cancellationToken).ConfigureAwait(false);
        if (postDeleteValidation.Status == BasicMemoryProjectValidationStatus.NotFound)
            return await RemoveDeletedProjectRoutingAsync(memoryProjectId, ProjectDeletionStatus.Deleted, cancellationToken).ConfigureAwait(false);

        if (postDeleteValidation.Status != BasicMemoryProjectValidationStatus.ExactMatch)
            return new ProjectDeletionResult(MapDeletionValidationStatus(postDeleteValidation.Status));

        if (deleteException != null)
            ExceptionDispatchInfo.Capture(deleteException).Throw();

        return new ProjectDeletionResult(ProjectDeletionStatus.BasicMemoryDeleteNotCompleted);
    }

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

        return await RegisterValidatedProjectAsync(memoryProjectId, memoryProjectName, cancellationToken).ConfigureAwait(false);
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

    /// <summary>
    /// Removes an exact technical context binding without changing its target project routing.
    /// </summary>
    /// <param name="contextId">The exact technical context identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the context-binding removal.</returns>
    public async Task<ContextBindingRemovalResult> UnbindContextAsync(ContextId contextId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contextId);

        var removed = await _projectRegistry.TryRemoveBindingAsync(contextId, cancellationToken).ConfigureAwait(false);
        if (removed)
            return new ContextBindingRemovalResult(ContextBindingRemovalStatus.Unbound);

        var current = await _projectRegistry.FindBindingAsync(contextId, cancellationToken).ConfigureAwait(false);
        return current == null ?
                   new ContextBindingRemovalResult(ContextBindingRemovalStatus.AlreadyUnbound) :
                   new ContextBindingRemovalResult(ContextBindingRemovalStatus.RegistryWriteConflict);
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

    private async Task<ProjectRegistrationResult> RegisterValidatedProjectAsync(Guid memoryProjectId, string memoryProjectName, CancellationToken cancellationToken)
    {
        var existing = await GetExistingRegistrationResultAsync(memoryProjectId, memoryProjectName, cancellationToken).ConfigureAwait(false);
        if (existing != null)
            return existing;

        try
        {
            var routing = await _projectRegistry.CreateAsync(memoryProjectId, memoryProjectName, Status.Active, cancellationToken).ConfigureAwait(false);
            return new ProjectRegistrationResult(ProjectRegistrationStatus.Registered, routing);
        }
        catch (ProjectRegistryConflictException)
        {
            return await GetExistingRegistrationResultAsync(memoryProjectId, memoryProjectName, cancellationToken).ConfigureAwait(false) ??
                   new ProjectRegistrationResult(ProjectRegistrationStatus.RegistryWriteConflict, null);
        }
    }

    private static ProjectCreationStatus MapCreationValidationStatus(BasicMemoryProjectValidationStatus status)
    {
        return status switch
        {
            BasicMemoryProjectValidationStatus.NotFound => ProjectCreationStatus.BasicMemoryProjectNotFound,
            BasicMemoryProjectValidationStatus.NameMismatch => ProjectCreationStatus.BasicMemoryProjectNameMismatch,
            BasicMemoryProjectValidationStatus.IdMismatch => ProjectCreationStatus.BasicMemoryProjectIdMismatch,
            BasicMemoryProjectValidationStatus.IdentityConflict => ProjectCreationStatus.BasicMemoryProjectIdentityConflict,
            _ => ProjectCreationStatus.BasicMemoryValidationFailed
        };
    }

    private static ProjectCreationResult MapCreationRegistrationResult(BasicMemoryProjectCreationStatus creationStatus, ProjectRegistrationResult registration)
    {
        return registration.Status switch
        {
            ProjectRegistrationStatus.Registered => new ProjectCreationResult(creationStatus == BasicMemoryProjectCreationStatus.Created ? ProjectCreationStatus.Created : ProjectCreationStatus.Recovered, registration.Routing),
            ProjectRegistrationStatus.AlreadyRegistered => new ProjectCreationResult(ProjectCreationStatus.AlreadyRegistered, registration.Routing),
            ProjectRegistrationStatus.RegistryProjectNameMismatch => new ProjectCreationResult(ProjectCreationStatus.RegistryProjectNameMismatch, null),
            ProjectRegistrationStatus.RegistryProjectIdMismatch => new ProjectCreationResult(ProjectCreationStatus.RegistryProjectIdMismatch, null),
            ProjectRegistrationStatus.RegistryProjectIdentityConflict => new ProjectCreationResult(ProjectCreationStatus.RegistryProjectIdentityConflict, null),
            ProjectRegistrationStatus.RegistryWriteConflict => new ProjectCreationResult(ProjectCreationStatus.RegistryWriteConflict, null),
            _ => throw new InvalidOperationException($"Unexpected validated project registration status '{registration.Status}'.")
        };
    }

    private static string NormalizeProjectPath(string projectPath)
    {
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(projectPath));
    }

    private static bool ProjectPathsEqual(string expectedProjectPath, string actualProjectPath)
    {
        if (!Path.IsPathFullyQualified(actualProjectPath))
            return false;

        var normalizedActualPath = NormalizeProjectPath(actualProjectPath);
        return string.Equals(expectedProjectPath, normalizedActualPath, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private async Task<ProjectDeletionResult> RemoveDeletedProjectRoutingAsync(Guid memoryProjectId, ProjectDeletionStatus successStatus, CancellationToken cancellationToken)
    {
        var removed = await _projectRegistry.TryRemoveProjectAsync(memoryProjectId, cancellationToken).ConfigureAwait(false);
        if (removed)
            return new ProjectDeletionResult(successStatus);

        var current = await _projectRegistry.FindByMemoryProjectIdAsync(memoryProjectId, cancellationToken).ConfigureAwait(false);
        return current == null ? new ProjectDeletionResult(successStatus) : new ProjectDeletionResult(ProjectDeletionStatus.RegistryWriteConflict);
    }

    private static ProjectDeletionStatus MapDeletionValidationStatus(BasicMemoryProjectValidationStatus status)
    {
        return status switch
            {
                BasicMemoryProjectValidationStatus.NameMismatch => ProjectDeletionStatus.BasicMemoryProjectNameMismatch,
                BasicMemoryProjectValidationStatus.IdMismatch => ProjectDeletionStatus.BasicMemoryProjectIdMismatch,
                BasicMemoryProjectValidationStatus.IdentityConflict => ProjectDeletionStatus.BasicMemoryProjectIdentityConflict,
                _ => ProjectDeletionStatus.BasicMemoryValidationFailed
            };
    }

    #endregion
}
