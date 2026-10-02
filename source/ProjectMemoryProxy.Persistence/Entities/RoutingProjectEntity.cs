namespace ProjectMemoryProxy.Persistence.Entities;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Represents a link to a basic-memory project.
/// </summary>
[Table("RoutingProjects"), Index(nameof(MemoryProjectId), IsUnique = true), Index(nameof(UpdatedAt))]
internal sealed class RoutingProjectEntity
{
    #region Static Fields

    #endregion

    #region Private Fields

    #endregion

    #region Constructors

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the opaque identifier.
    /// </summary>
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the basic-memory project identifier.
    /// </summary>
    [Required]
    public Guid MemoryProjectId { get; set; }

    /// <summary>
    /// Gets or sets the project context status.
    /// </summary>
    [Required]
    public ProjectContextStatus Status { get; set; } = ProjectContextStatus.Active;

    /// <summary>
    /// Gets or sets when the project was created.
    /// </summary>
    [Required]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets when the project was last updated.
    /// </summary>
    [Required]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the project bindings.
    /// </summary>
    [ForeignKey(nameof(ContextBindingEntity.RoutingProjectId))]
    public ICollection<ContextBindingEntity> Bindings { get; set; } = new List<ContextBindingEntity>();
    
    #endregion

    #region Public Methods

    #endregion

    #region Private Methods

    #endregion

    #region Configuration
    
    #endregion
}
