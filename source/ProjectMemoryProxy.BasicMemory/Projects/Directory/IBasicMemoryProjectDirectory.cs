namespace ProjectMemoryProxy.BasicMemory.Projects.Directory;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Provides read-only discovery and validation of local Basic Memory projects.
/// </summary>
public interface IBasicMemoryProjectDirectory
{
    #region Events

    #endregion

    #region Properties

    #endregion

    #region Methods

    /// <summary>
    /// Lists the local projects currently known to Basic Memory.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<BasicMemoryProjectInfo>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that a Basic Memory project UUID and name identify the same current local project.
    /// </summary>
    /// <param name="memoryProjectId">The expected Basic Memory external project UUID.</param>
    /// <param name="memoryProjectName">The expected Basic Memory project name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<BasicMemoryProjectValidationResult> ValidateAsync(Guid memoryProjectId, string memoryProjectName, CancellationToken cancellationToken = default);

    #endregion
}
