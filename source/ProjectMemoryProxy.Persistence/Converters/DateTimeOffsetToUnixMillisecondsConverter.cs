namespace ProjectMemoryProxy.Persistence.Converters;

using System;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

/// <summary>
/// Converts lifecycle timestamps between <see cref="DateTimeOffset"/> and Unix milliseconds.
/// </summary>
internal sealed class DateTimeOffsetToUnixMillisecondsConverter : ValueConverter<DateTimeOffset, long>
{
    #region Static Fields

    #endregion

    #region Private Fields

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new converter.
    /// </summary>
    public DateTimeOffsetToUnixMillisecondsConverter()
        : base(value => value.ToUnixTimeMilliseconds(),
            value => DateTimeOffset.FromUnixTimeMilliseconds(value))
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
