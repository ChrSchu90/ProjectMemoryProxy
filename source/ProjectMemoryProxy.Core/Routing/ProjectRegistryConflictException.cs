namespace ProjectMemoryProxy.Core.Routing;

using System;

/// <summary>
/// Represents a concurrent persistence conflict while modifying project registry state.
/// </summary>
public sealed class ProjectRegistryConflictException : Exception
{
    #region Static Fields

    #endregion

    #region Private Fields

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectRegistryConflictException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public ProjectRegistryConflictException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectRegistryConflictException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The exception that caused the persistence conflict.</param>
    public ProjectRegistryConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    #endregion

    #region Properties

    #endregion

    #region Public Methods

    #endregion

    #region Private Methods

    #endregion
}
