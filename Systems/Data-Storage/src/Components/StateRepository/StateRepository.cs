namespace DataStorage.StateRepository;

/// <summary>
/// In-memory repository for storing EPS (Entity-Property-Service) property lists.
/// This is a dumb storage implementation that stores properties organized by property type.
/// Each property type has its own list of values. The repository does not understand entities or archetypes.
/// </summary>
public class StateRepository
{
    /// <summary>
    /// Stores properties: propertyType -> List of property values
    /// </summary>
    private readonly Dictionary<string, List<object>> _propertiesByType;
    private readonly object _lockObject = new();

    public StateRepository()
    {
        _propertiesByType = new Dictionary<string, List<object>>();
    }

    #region Property Type Operations

    /// <summary>
    /// Gets all property values for a specific property type.
    /// Returns the complete list for the property type, or null if the property type doesn't exist.
    /// </summary>
    public List<object>? GetPropertiesByType(string propertyType)
    {
        if (string.IsNullOrWhiteSpace(propertyType))
        {
            throw new ArgumentException("Property type cannot be null or empty", nameof(propertyType));
        }

        lock (_lockObject)
        {
            if (_propertiesByType.TryGetValue(propertyType, out var properties))
            {
                return new List<object>(properties);
            }
            return null;
        }
    }

    /// <summary>
    /// Adds a property value to the list for a specific property type.
    /// Creates the property type list if it doesn't exist.
    /// Returns the index where the property was added.
    /// </summary>
    public int AddProperty(string propertyType, object propertyValue)
    {
        if (string.IsNullOrWhiteSpace(propertyType))
        {
            throw new ArgumentException("Property type cannot be null or empty", nameof(propertyType));
        }

        if (propertyValue == null)
        {
            throw new ArgumentNullException(nameof(propertyValue));
        }

        lock (_lockObject)
        {
            if (!_propertiesByType.ContainsKey(propertyType))
            {
                _propertiesByType[propertyType] = new List<object>();
            }

            _propertiesByType[propertyType].Add(propertyValue);
            return _propertiesByType[propertyType].Count - 1; // Return the index
        }
    }

    /// <summary>
    /// Replaces the entire list of properties for a property type.
    /// </summary>
    public void SetPropertiesForType(string propertyType, List<object> propertyValues)
    {
        if (string.IsNullOrWhiteSpace(propertyType))
        {
            throw new ArgumentException("Property type cannot be null or empty", nameof(propertyType));
        }

        if (propertyValues == null)
        {
            throw new ArgumentNullException(nameof(propertyValues));
        }

        lock (_lockObject)
        {
            _propertiesByType[propertyType] = new List<object>(propertyValues);
        }
    }

    /// <summary>
    /// Deletes all properties of a specific property type.
    /// Returns the number of properties that were deleted.
    /// </summary>
    public int DeletePropertiesByType(string propertyType)
    {
        if (string.IsNullOrWhiteSpace(propertyType))
        {
            throw new ArgumentException("Property type cannot be null or empty", nameof(propertyType));
        }

        lock (_lockObject)
        {
            if (_propertiesByType.TryGetValue(propertyType, out var properties))
            {
                int count = properties.Count;
                _propertiesByType.Remove(propertyType);
                return count;
            }
            return 0;
        }
    }

    /// <summary>
    /// Checks if a property type exists in the repository.
    /// </summary>
    public bool PropertyTypeExists(string propertyType)
    {
        if (string.IsNullOrWhiteSpace(propertyType))
        {
            throw new ArgumentException("Property type cannot be null or empty", nameof(propertyType));
        }

        lock (_lockObject)
        {
            return _propertiesByType.ContainsKey(propertyType);
        }
    }

    #endregion

    #region Multiple Property Types

    /// <summary>
    /// Gets all properties for multiple property types.
    /// Returns a dictionary mapping property type to its list of values.
    /// </summary>
    public Dictionary<string, List<object>> GetPropertiesForTypes(IEnumerable<string> propertyTypes)
    {
        if (propertyTypes == null)
        {
            throw new ArgumentNullException(nameof(propertyTypes));
        }

        lock (_lockObject)
        {
            var result = new Dictionary<string, List<object>>();
            foreach (var propertyType in propertyTypes)
            {
                if (_propertiesByType.TryGetValue(propertyType, out var properties))
                {
                    result[propertyType] = new List<object>(properties);
                }
            }
            return result;
        }
    }

    /// <summary>
    /// Gets all property types currently stored in the repository.
    /// </summary>
    public List<string> GetAllPropertyTypes()
    {
        lock (_lockObject)
        {
            return new List<string>(_propertiesByType.Keys);
        }
    }

    /// <summary>
    /// Gets all properties organized by property type.
    /// </summary>
    public Dictionary<string, List<object>> GetAllProperties()
    {
        lock (_lockObject)
        {
            var result = new Dictionary<string, List<object>>();
            foreach (var kvp in _propertiesByType)
            {
                result[kvp.Key] = new List<object>(kvp.Value);
            }
            return result;
        }
    }

    #endregion

    #region Utility

    /// <summary>
    /// Gets the total number of property types.
    /// </summary>
    public int PropertyTypeCount
    {
        get
        {
            lock (_lockObject)
            {
                return _propertiesByType.Count;
            }
        }
    }

    /// <summary>
    /// Gets the total count of all properties across all types.
    /// </summary>
    public int TotalPropertyCount
    {
        get
        {
            lock (_lockObject)
            {
                return _propertiesByType.Values.Sum(list => list.Count);
            }
        }
    }

    /// <summary>
    /// Clears all properties from the repository.
    /// </summary>
    public void Clear()
    {
        lock (_lockObject)
        {
            _propertiesByType.Clear();
        }
    }

    #endregion
}


