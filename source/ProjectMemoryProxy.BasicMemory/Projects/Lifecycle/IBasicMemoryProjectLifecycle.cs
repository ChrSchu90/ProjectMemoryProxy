namespace ProjectMemoryProxy.BasicMemory.Projects.Lifecycle;

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

    /// <summary>
    /// Deletes a local Basic Memory project.
    /// </summary>
    /// <param name="memoryProjectName">The Basic Memory project name.</param>
    /// <param name="deleteNotes">Whether Basic Memory should also delete the project's note files.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task DeleteAsync(string memoryProjectName, bool deleteNotes, CancellationToken cancellationToken = default);

    #endregion
}
