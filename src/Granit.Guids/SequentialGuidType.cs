namespace Granit.Guids;

/// <summary>
/// Describes the sequential GUID type according to the target database engine.
/// </summary>
public enum SequentialGuidType
{
    /// <summary>
    /// The GUID is sequential in its string representation (<see cref="Guid.ToString()"/>).
    /// Used by PostgreSQL and MySQL.
    /// </summary>
    SequentialAsString,

    /// <summary>
    /// The GUID is sequential in its binary representation (<see cref="Guid.ToByteArray()"/>).
    /// Used by Oracle.
    /// </summary>
    SequentialAsBinary,

    /// <summary>
    /// The sequential portion is placed at the end of the Data4 block.
    /// Used by SQL Server.
    /// </summary>
    SequentialAtEnd
}
