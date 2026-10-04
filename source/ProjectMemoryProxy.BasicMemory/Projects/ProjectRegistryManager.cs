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

    #endregion
}
