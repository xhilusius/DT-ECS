namespace DataStorage.ArchetypeMapper;

/// <summary>
/// Manages archetypes for EPS (Entity-Property-Service) systems.
/// Keeps track of which property combinations are needed by services
/// and helps identify which entities match specific archetypes.
/// </summary>
public class ArchetypeMapper
{
    private readonly Dictionary<string, Archetype> _archetypes;
    private readonly Dictionary<string, HashSet<string>> _serviceArchetypes; // Maps service name to archetype IDs
    private readonly object _lockObject = new();

    public ArchetypeMapper()
    {
        _archetypes = new Dictionary<string, Archetype>();
        _serviceArchetypes = new Dictionary<string, HashSet<string>>();
    }

    /// <summary>
    /// Registers a new archetype
    /// </summary>
    public void RegisterArchetype(Archetype archetype)
    {
        if (archetype == null)
        {
            throw new ArgumentNullException(nameof(archetype));
        }

        if (string.IsNullOrWhiteSpace(archetype.Id))
        {
            throw new ArgumentException("Archetype ID cannot be null or empty", nameof(archetype.Id));
        }

        if (archetype.PropertyTypes == null || archetype.PropertyTypes.Count == 0)
        {
            throw new ArgumentException("Archetype must contain at least one property type");
        }

        lock (_lockObject)
        {
            if (_archetypes.ContainsKey(archetype.Id))
            {
                throw new InvalidOperationException($"Archetype with ID '{archetype.Id}' already exists");
            }

            _archetypes[archetype.Id] = archetype;
        }
    }

    /// <summary>
    /// Registers a service and its required archetype
    /// </summary>
    public void RegisterServiceArchetype(string serviceName, string archetypeId)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            throw new ArgumentException("Service name cannot be null or empty", nameof(serviceName));
        }

        if (string.IsNullOrWhiteSpace(archetypeId))
        {
            throw new ArgumentException("Archetype ID cannot be null or empty", nameof(archetypeId));
        }

        lock (_lockObject)
        {
            if (!_archetypes.ContainsKey(archetypeId))
            {
                throw new InvalidOperationException($"Archetype with ID '{archetypeId}' not found");
            }

            if (!_serviceArchetypes.ContainsKey(serviceName))
            {
                _serviceArchetypes[serviceName] = new HashSet<string>();
            }

            _serviceArchetypes[serviceName].Add(archetypeId);
        }
    }

    /// <summary>
    /// Gets an archetype by ID
    /// </summary>
    public Archetype? GetArchetype(string archetypeId)
    {
        if (string.IsNullOrWhiteSpace(archetypeId))
        {
            throw new ArgumentException("Archetype ID cannot be null or empty", nameof(archetypeId));
        }

        lock (_lockObject)
        {
            return _archetypes.TryGetValue(archetypeId, out var archetype) ? archetype : null;
        }
    }

    /// <summary>
    /// Gets all archetypes required by a service
    /// </summary>
    public List<Archetype> GetServiceArchetypes(string serviceName)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            throw new ArgumentException("Service name cannot be null or empty", nameof(serviceName));
        }

        lock (_lockObject)
        {
            if (!_serviceArchetypes.TryGetValue(serviceName, out var archetypeIds))
            {
                return new List<Archetype>();
            }

            return archetypeIds
                .Where(id => _archetypes.ContainsKey(id))
                .Select(id => _archetypes[id])
                .ToList();
        }
    }

    /// <summary>
    /// Gets all registered archetypes
    /// </summary>
    public List<Archetype> GetAllArchetypes()
    {
        lock (_lockObject)
        {
            return new List<Archetype>(_archetypes.Values);
        }
    }

    /// <summary>
    /// Finds archetypes that match a set of property types
    /// </summary>
    public List<Archetype> FindMatchingArchetypes(IEnumerable<string> propertyTypes)
    {
        if (propertyTypes == null)
        {
            throw new ArgumentNullException(nameof(propertyTypes));
        }

        var types = new HashSet<string>(propertyTypes);

        lock (_lockObject)
        {
            return _archetypes.Values
                .Where(a => a.Matches(types))
                .ToList();
        }
    }

    /// <summary>
    /// Finds archetypes that contain all specified property types
    /// </summary>
    public List<Archetype> FindArchetypesContaining(IEnumerable<string> requiredProperties)
    {
        if (requiredProperties == null)
        {
            throw new ArgumentNullException(nameof(requiredProperties));
        }

        var required = requiredProperties.ToList();
        if (required.Count == 0)
        {
            return new List<Archetype>();
        }

        lock (_lockObject)
        {
            return _archetypes.Values
                .Where(a => a.ContainsAllProperties(required))
                .ToList();
        }
    }

    /// <summary>
    /// Removes an archetype by ID
    /// </summary>
    public bool RemoveArchetype(string archetypeId)
    {
        if (string.IsNullOrWhiteSpace(archetypeId))
        {
            throw new ArgumentException("Archetype ID cannot be null or empty", nameof(archetypeId));
        }

        lock (_lockObject)
        {
            // Remove from services that reference it
            foreach (var serviceArchetypes in _serviceArchetypes.Values)
            {
                serviceArchetypes.Remove(archetypeId);
            }

            return _archetypes.Remove(archetypeId);
        }
    }

    /// <summary>
    /// Gets the number of registered archetypes
    /// </summary>
    public int ArchetypeCount
    {
        get
        {
            lock (_lockObject)
            {
                return _archetypes.Count;
            }
        }
    }

    /// <summary>
    /// Clears all archetypes and service mappings
    /// </summary>
    public void Clear()
    {
        lock (_lockObject)
        {
            _archetypes.Clear();
            _serviceArchetypes.Clear();
        }
    }
}
