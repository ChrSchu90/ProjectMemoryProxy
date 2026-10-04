namespace ProjectMemoryProxy.BasicMemory.Projects;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Provides explicit Basic Memory project lifecycle operations used by the ProjectMemoryProxy control plane.
/// </summary>
public interface IBasicMemoryProjectLifecycle
{
    #region Events

    #endregion

    #region Properties

    #endregion

    #region Methods

    /// <summary>
    /// Creates a local Basic Memory project or returns the matching existing project.
    /// </summary>
    /// <param name="memoryProjectName">The Basic Memory project name.</param>
    /// <param name="memoryProjectPath">The absolute local project path.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<BasicMemoryProjectCreationResult> CreateAsync(string memoryProjectName, string memoryProjectPath, CancellationToken cancellationToken = default);

    #endregion
}
