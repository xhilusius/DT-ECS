namespace DataStorage.RepositoryManager;

/// <summary>
/// Result for archetype queries including property arrays and entity-to-index mappings.
/// Provides the TransformExecutor with the index grouping needed to correlate property arrays.
/// </summary>
public class ArchetypeQueryResult
{
    /// <summary>
    /// Property arrays indexed by property type.
    /// </summary>
    public Dictionary<string, List<object>> Arrays { get; set; } = new();

    /// <summary>
    /// Valid entity IDs that have all properties required by the archetype.
    /// </summary>
    public List<int> ValidEntityIds { get; set; } = new();

    /// <summary>
    /// Maps each valid entity to its index in each property array.
    /// </summary>
    public Dictionary<int, Dictionary<string, int>> EntityToPropertyIndices { get; set; } = new();
}
