namespace ProjectMemoryProxy.BasicMemory.Projects;

using ProjectMemoryProxy.BasicMemory.Projects.ControlPlane;
using ProjectMemoryProxy.BasicMemory.Projects.Directory;

using ProjectMemoryProxy.Core.Routing;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Reconciles the ProjectMemoryProxy routing inventory with the current local Basic Memory projects.
/// </summary>
public sealed class ProjectRegistryReconciler
{
    #region Static Fields

    #endregion

    #region Private Fields

    private readonly ProjectRegistryManager _projectRegistryManager;
    private readonly IBasicMemoryProjectDirectory _projectDirectory;
    private readonly IProjectRegistry _projectRegistry;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectRegistryReconciler"/> class.
    /// </summary>
    /// <param name="projectDirectory">The Basic Memory project directory.</param>
    /// <param name="projectRegistry">The ProjectMemoryProxy routing registry.</param>
    /// <param name="projectRegistryManager">The project registry lifecycle manager.</param>
    public ProjectRegistryReconciler(IBasicMemoryProjectDirectory projectDirectory, IProjectRegistry projectRegistry, ProjectRegistryManager projectRegistryManager)
    {
        _projectDirectory = projectDirectory ?? throw new ArgumentNullException(nameof(projectDirectory));
        _projectRegistry = projectRegistry ?? throw new ArgumentNullException(nameof(projectRegistry));
        _projectRegistryManager = projectRegistryManager ?? throw new ArgumentNullException(nameof(projectRegistryManager));
    }

    #endregion

    #region Properties

    #endregion

    #region Public Methods

    /// <summary>
    /// Reconciles registered project routings with the projects currently exposed by Basic Memory.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <remarks>
    /// Missing local routings are registered as active. Active local routings whose Basic Memory
    /// project is absent are deactivated. Existing inactive routings are never reactivated automatically.
    /// Identity conflicts fail before any reconciliation mutation is attempted.
    /// </remarks>
    public async Task ReconcileAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var basicMemoryProjects = await _projectDirectory.ListAsync(cancellationToken).ConfigureAwait(false);
        var registeredProjects = await _projectRegistry.ListProjectsAsync(cancellationToken).ConfigureAwait(false);
        var basicMemoryById = BuildBasicMemoryProjectIndex(basicMemoryProjects);
        var basicMemoryByName = BuildBasicMemoryProjectNameIndex(basicMemoryProjects);
        var registeredById = BuildRegisteredProjectIndex(registeredProjects);
        var registeredByName = BuildRegisteredProjectNameIndex(registeredProjects);
        ValidateIdentityConsistency(basicMemoryProjects, registeredById, registeredByName);
        ValidateIdentityConsistency(registeredProjects, basicMemoryById, basicMemoryByName);

        // Disable stale active routes first. This is the safer partial state if a later persistence operation fails.
        foreach (var routing in registeredProjects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (routing.Status != Status.Active || basicMemoryById.ContainsKey(routing.MemoryProjectId))
                continue;

            var result = await _projectRegistryManager.DeactivateProjectAsync(routing.MemoryProjectId, cancellationToken).ConfigureAwait(false);
            if (result.Status is not ProjectRoutingStatusChangeStatus.Updated and not ProjectRoutingStatusChangeStatus.AlreadyInRequestedState and not ProjectRoutingStatusChangeStatus.ProjectRoutingNotFound)
                throw new InvalidOperationException($"Could not deactivate missing Basic Memory project routing '{routing.MemoryProjectId:D}' ('{routing.MemoryProjectName}'). Result: {result.Status}.");
        }

        foreach (var project in basicMemoryProjects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (registeredById.ContainsKey(project.MemoryProjectId))
                continue;

            var result = await _projectRegistryManager.RegisterValidatedProjectAsync(project.MemoryProjectId, project.MemoryProjectName, cancellationToken).ConfigureAwait(false);
            if (result.Status is not ProjectRegistrationStatus.Registered and not ProjectRegistrationStatus.AlreadyRegistered)
                throw new InvalidOperationException($"Could not register Basic Memory project '{project.MemoryProjectId:D}' ('{project.MemoryProjectName}'). Result: {result.Status}.");
        }
    }

    #endregion

    #region Private Methods

    private static Dictionary<Guid, BasicMemoryProjectInfo> BuildBasicMemoryProjectIndex(IReadOnlyList<BasicMemoryProjectInfo> projects)
    {
        var byId = new Dictionary<Guid, BasicMemoryProjectInfo>();
        foreach (var project in projects)
        {
            if (project.MemoryProjectId == Guid.Empty)
                throw new InvalidOperationException("Basic Memory returned a project with an empty external identifier.");

            if (string.IsNullOrWhiteSpace(project.MemoryProjectName))
                throw new InvalidOperationException("Basic Memory returned a project without a valid name.");

            if (!byId.TryAdd(project.MemoryProjectId, project))
                throw new InvalidOperationException($"Basic Memory returned duplicate project external identifier '{project.MemoryProjectId:D}'.");
        }

        return byId;
    }

    private static Dictionary<string, BasicMemoryProjectInfo> BuildBasicMemoryProjectNameIndex(IReadOnlyList<BasicMemoryProjectInfo> projects)
    {
        var byName = new Dictionary<string, BasicMemoryProjectInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var project in projects)
        {
            if (!byName.TryAdd(project.MemoryProjectName, project))
            {
                throw new InvalidOperationException($"Basic Memory returned duplicate project name '{project.MemoryProjectName}'.");
            }
        }

        return byName;
    }

    private static Dictionary<Guid, ProjectRouting> BuildRegisteredProjectIndex(IReadOnlyList<ProjectRouting> projects)
    {
        var byId = new Dictionary<Guid, ProjectRouting>();

        foreach (var project in projects)
        {
            if (project.MemoryProjectId == Guid.Empty)
                throw new InvalidOperationException("The project registry contains a routing with an empty Basic Memory project identifier.");

            if (string.IsNullOrWhiteSpace(project.MemoryProjectName))
                throw new InvalidOperationException($"The project registry contains routing '{project.MemoryProjectId:D}' without a valid Basic Memory project name.");

            if (project.Status is not Status.Active and not Status.Inactive)
                throw new InvalidOperationException($"The project registry contains unsupported status '{project.Status}' for '{project.MemoryProjectId:D}'.");

            if (!byId.TryAdd(project.MemoryProjectId, project))
                throw new InvalidOperationException($"The project registry contains duplicate Basic Memory project identifier '{project.MemoryProjectId:D}'.");
        }

        return byId;
    }

    private static Dictionary<string, ProjectRouting> BuildRegisteredProjectNameIndex(IReadOnlyList<ProjectRouting> projects)
    {
        var byName = new Dictionary<string, ProjectRouting>(StringComparer.OrdinalIgnoreCase);
        foreach (var project in projects)
        {
            if (!byName.TryAdd(project.MemoryProjectName, project))
            {
                throw new InvalidOperationException($"The project registry contains duplicate Basic Memory project name '{project.MemoryProjectName}'.");
            }
        }

        return byName;
    }

    private static void ValidateIdentityConsistency(IReadOnlyList<BasicMemoryProjectInfo> projects, IReadOnlyDictionary<Guid, ProjectRouting> registeredById, IReadOnlyDictionary<string, ProjectRouting> registeredByName)
    {
        foreach (var project in projects)
        {
            registeredById.TryGetValue(project.MemoryProjectId, out var registeredByProjectId);
            registeredByName.TryGetValue(project.MemoryProjectName, out var registeredByProjectName);
            if (registeredByProjectId != null && !string.Equals(registeredByProjectId.MemoryProjectName, project.MemoryProjectName, StringComparison.Ordinal))
                throw new InvalidOperationException($"Basic Memory project identifier '{project.MemoryProjectId:D}' is registered under name '{registeredByProjectId.MemoryProjectName}' instead of '{project.MemoryProjectName}'.");

            if (registeredByProjectName != null && registeredByProjectName.MemoryProjectId != project.MemoryProjectId)
                throw new InvalidOperationException($"Basic Memory project name '{project.MemoryProjectName}' is registered with identifier '{registeredByProjectName.MemoryProjectId:D}' instead of '{project.MemoryProjectId:D}'.");
        }
    }

    private static void ValidateIdentityConsistency(IReadOnlyList<ProjectRouting> projects, IReadOnlyDictionary<Guid, BasicMemoryProjectInfo> basicMemoryById, IReadOnlyDictionary<string, BasicMemoryProjectInfo> basicMemoryByName)
    {
        foreach (var project in projects)
        {
            basicMemoryById.TryGetValue(project.MemoryProjectId, out var basicMemoryByProjectId);
            basicMemoryByName.TryGetValue(project.MemoryProjectName, out var basicMemoryByProjectName);
            if (basicMemoryByProjectId != null && !string.Equals(basicMemoryByProjectId.MemoryProjectName, project.MemoryProjectName, StringComparison.Ordinal))
                throw new InvalidOperationException($"Registered project identifier '{project.MemoryProjectId:D}' exists in Basic Memory under name '{basicMemoryByProjectId.MemoryProjectName}' instead of '{project.MemoryProjectName}'.");

            if (basicMemoryByProjectName != null && basicMemoryByProjectName.MemoryProjectId != project.MemoryProjectId)
                throw new InvalidOperationException($"Registered project name '{project.MemoryProjectName}' exists in Basic Memory with identifier '{basicMemoryByProjectName.MemoryProjectId:D}' instead of '{project.MemoryProjectId:D}'.");
        }
    }

    #endregion
}
