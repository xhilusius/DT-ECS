namespace Simulation.StateManager;

using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// MVP Visualization Mapper - bridges simulation state to Godot HTTP server.
/// 
/// ARCHITECTURE:
/// - PRIMARY: HTTP/REST endpoint for Godot visualization server
///   Sends BallUpdate JSON via POST to http://127.0.0.1:8080/balls/update
///   This is the MVP mechanism for visualization communication
/// - OPTIONAL: IExternalSourceReceiver - local in-process visualization interface
///   If set, will also forward updates to local receivers (for testing/debugging)
///   If null, only HTTP requests are sent (typical production setup)
/// 
/// DEPENDENCIES (Explicitly documented for future changes):
/// - REQUIRED: HttpClient for HTTP/REST communication
/// - OPTIONAL: IExternalSourceReceiver for in-process receivers
/// 
/// ERROR HANDLING:
/// - HTTP failures don't crash simulation (graceful degradation)
/// - Network timeouts: 5 seconds per request
/// - Invalid JSON or serialization errors logged but don't stop execution
/// 
/// FUTURE EXTENSIBILITY:
/// - Configurable endpoint URL
/// - Batch updates instead of individual POST requests
/// - Connection pooling and retry strategies
/// - Support for multiple visualization backends simultaneously
/// </summary>
public class VisualizationMapper
{
    /// <summary>
    /// HTTP client for communicating with visualization server.
    /// Static/lazy to reuse connections across multiple mappers.
    /// </summary>
    private static readonly Lazy<HttpClient> _httpClient = new(() => 
    {
        var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(5); // 5s timeout for POST requests
        return client;
    });

    /// <summary>
    /// Endpoint URL for Godot visualization server.
    /// MVP: Configurable via constructor, defaults to localhost.
    /// Example: "http://127.0.0.1:8080/balls/update"
    /// </summary>
    private readonly string _visualizationEndpoint;
    private readonly string _visualizationClearEndpoint;
    private readonly string _visualizationRemoveEndpoint;

    /// <summary>
    /// Optional external receiver for visualization updates.
    /// When null, only HTTP requests are sent (typical for production with Godot).
    /// When set, also forwards updates to local receivers (for testing/multiple backends).
    /// </summary>
    private IExternalSourceReceiver? _externalReceiver;

    /// <summary>
    /// Initialize mapper with HTTP endpoint configuration.
    /// </summary>
    /// <param name="visualizationEndpoint">HTTP endpoint URL for Godot visualization server</param>
    /// <param name="externalReceiver">Optional local visualization receiver (can be null)</param>
    public VisualizationMapper(
        string visualizationEndpoint = "http://127.0.0.1:8080/balls/update",
        IExternalSourceReceiver? externalReceiver = null)
    {
        _visualizationEndpoint = visualizationEndpoint ?? "http://127.0.0.1:8080/balls/update";
        _visualizationClearEndpoint = ResolveEndpoint(_visualizationEndpoint, "/balls/clear");
        _visualizationRemoveEndpoint = ResolveEndpoint(_visualizationEndpoint, "/balls/remove");
        _externalReceiver = externalReceiver;
    }

    /// <summary>
    /// Set or replace the external receiver.
    /// Allows runtime connection/disconnection of local visualization receivers.
    /// </summary>
    public void SetExternalReceiver(IExternalSourceReceiver? receiver)
    {
        _externalReceiver = receiver;
    }

    /// <summary>
    /// Tests the connection to the visualization server.
    /// Sends a POST request with a heartbeat/health message to verify the server is running.
    /// 
    /// USAGE: Call this before starting the simulation to verify Godot is ready.
    /// 
    /// RETURNS: (isConnected, statusMessage)
    /// - isConnected: true if server responded successfully
    /// - statusMessage: Details about the connection attempt
    /// 
    /// Godot Implementation:
    /// - Your existing POST /balls/update handler will receive this heartbeat
    /// - You can identify it by checking if the array is empty: entities.length == 0
    /// - Example (GDScript):
    ///   if not entities.is_empty():
    ///       # Process ball updates
    ///   else:
    ///       # This is a health check heartbeat - just respond OK
    /// </summary>
    public async Task<(bool isConnected, string message)> TestConnectionAsync()
    {
        try
        {
            // Send an empty ball update as a heartbeat/health check
            var heartbeatData = new List<object>(); // Empty array signals health check
            var json = JsonSerializer.Serialize(heartbeatData, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.Value.PostAsync(_visualizationEndpoint, content);

            if (response.IsSuccessStatusCode)
            {
                return (true, $"✅ Connected to visualization server at {_visualizationEndpoint}");
            }
            else
            {
                return (false, $"❌ Server responded with {response.StatusCode}: {response.ReasonPhrase}");
            }
        }
        catch (HttpRequestException ex)
        {
            return (false, $"❌ Cannot connect to visualization server: {ex.Message}");
        }
        catch (TaskCanceledException ex)
        {
            return (false, $"❌ Connection timeout (5s): {ex.Message}");
        }
        catch (Exception ex)
        {
            return (false, $"❌ Unexpected error: {ex.Message}");
        }
    }

    /// <summary>
    /// Notify visualization system that new entities have been created.
    /// Called by StateManager when entities are first registered.
    /// 
    /// FLOW:
    /// 1. Convert entity data to BallUpdate structures
    /// 2. Send HTTP POST request to Godot server (async, fire-and-forget)
    /// 3. Also notify local external receiver if configured (for testing)
    /// 
    /// DEPENDENCY: Entity data must include Position and Radius for visualization.
    /// Will not crash simulation if HTTP request fails (graceful degradation).
    /// </summary>
    public void NotifyEntitiesCreated(IEnumerable<EntityVisualizationData> entities)
    {
        var entityList = entities.ToList();
        if (entityList.Count == 0)
            return;

        Console.WriteLine($"   📊 Sending {entityList.Count} newly created entities to visualization...");

        var ballUpdates = entityList.Select(ConvertToBallUpdate).ToList();

        // Send HTTP request to visualization server (fire-and-forget)
        _ = SendVisualizationUpdateAsync(ballUpdates);

        // Also notify local external receiver if configured
        if (_externalReceiver != null)
        {
            _externalReceiver.HandleBallUpdates(ballUpdates);
        }
    }

    /// <summary>
    /// Notify visualization system of entity state updates.
    /// Called by StateManager after each simulation step.
    /// 
    /// FLOW:
    /// 1. Convert entity data to BallUpdate structures
    /// 2. Send HTTP POST request to Godot server (async, fire-and-forget)
    /// 3. Also notify local external receiver if configured (for testing)
    /// 
    /// HOT PATH: Called frequently during simulation - async to avoid blocking execution.
    /// DEPENDENCY: Entity data must include Position (required) and Radius (strongly recommended).
    /// </summary>
    public void NotifyStateUpdated(IEnumerable<EntityVisualizationData> entities)
    {
        var entityList = entities.ToList();
        if (entityList.Count == 0)
            return;

        var ballUpdates = entityList.Select(ConvertToBallUpdate).ToList();

        // Send HTTP request to visualization server (fire-and-forget to avoid blocking)
        _ = SendVisualizationUpdateAsync(ballUpdates);

        // Also notify local external receiver if configured (for testing/debugging)
        if (_externalReceiver != null)
        {
            _externalReceiver.HandleBallUpdates(ballUpdates);
        }
    }

    /// <summary>
    /// Clear all entities from the visualization.
    /// Used after a test case completes so the visualization can be reused.
    /// </summary>
    public async Task ClearAllEntitiesAsync()
    {
        // Notify HTTP visualization server
        await SendClearVisualizationAsync();

        // Also notify local external receiver if configured
        if (_externalReceiver != null)
        {
            _externalReceiver.ClearAllEntities();
        }
    }

    /// <summary>
    /// Remove a single entity from the visualization.
    /// </summary>
    public async Task RemoveEntityAsync(string entityId)
    {
        if (string.IsNullOrWhiteSpace(entityId))
            return;

        await SendRemoveVisualizationAsync(entityId);

        if (_externalReceiver != null)
        {
            _externalReceiver.RemoveEntity(entityId);
        }
    }

    /// <summary>
    /// Send BallUpdate batch to visualization server via HTTP POST.
    /// Serializes to JSON and sends to Godot HTTP endpoint.
    /// 
    /// MVP DESIGN:
    /// - Fire-and-forget async pattern to avoid blocking simulation
    /// - Graceful error handling - failures don't crash simulation
    /// - Could be enhanced with: retries, batching, connection pooling
    /// 
    /// EXPECTED ENDPOINT:
    /// - URL: http://127.0.0.1:8080/balls/update (configurable)
    /// - Method: POST
    /// - Content-Type: application/json
    /// - Body: Array of BallUpdate structures with camelCase property names
    /// </summary>
    private async Task SendVisualizationUpdateAsync(List<BallUpdate> updates)
    {
        try
        {
            // Serialize BallUpdate list to JSON with camelCase property names
            var json = JsonSerializer.Serialize(updates, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Send POST request to Godot visualization server
            var response = await _httpClient.Value.PostAsync(_visualizationEndpoint, content);

            // MVP: Just log response status. Future could add retry logic.
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"⚠ Visualization server returned {response.StatusCode}: {response.ReasonPhrase}");
            }
            else
            {
                // Only log successful responses for entity creation
                if (json!="[]")
                {
                    Console.WriteLine($"   ✓ Sent {updates.Count} ball update(s) to visualization server");
                }
            }
        }
        catch (HttpRequestException ex)
        {
            // Network/connection error - log warning but don't crash simulation
            Console.WriteLine($"⚠ Failed to contact visualization server at {_visualizationEndpoint}: {ex.Message}");
        }
        catch (Exception ex)
        {
            // Other serialization or unexpected errors
            Console.WriteLine($"⚠ Error sending visualization update: {ex.Message}");
        }
    }

    /// <summary>
    /// Send a clear-all request to the visualization server.
    /// </summary>
    private async Task SendClearVisualizationAsync()
    {
        try
        {
            // Clear endpoint accepts empty (or any) body
            var content = new StringContent(string.Empty, Encoding.UTF8, "application/json");
            var response = await _httpClient.Value.PostAsync(_visualizationClearEndpoint, content);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"⚠ Visualization server returned {response.StatusCode}: {response.ReasonPhrase}");
            }
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"⚠ Failed to contact visualization server at {_visualizationEndpoint}: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠ Error sending clear request: {ex.Message}");
        }
    }

    /// <summary>
    /// Send a remove-entity request to the visualization server.
    /// </summary>
    private async Task SendRemoveVisualizationAsync(string entityId)
    {
        try
        {
            var json = JsonSerializer.Serialize(new { entityId }, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.Value.PostAsync(_visualizationRemoveEndpoint, content);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"⚠ Visualization server returned {response.StatusCode}: {response.ReasonPhrase}");
            }
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"⚠ Failed to contact visualization server at {_visualizationRemoveEndpoint}: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠ Error sending remove request: {ex.Message}");
        }
    }

    private static string ResolveEndpoint(string baseEndpoint, string newPath)
    {
        if (string.IsNullOrWhiteSpace(baseEndpoint))
        {
            return "http://127.0.0.1:8080" + newPath;
        }

        if (Uri.TryCreate(baseEndpoint, UriKind.Absolute, out var baseUri))
        {
            var builder = new UriBuilder(baseUri)
            {
                Path = newPath
            };
            return builder.Uri.ToString().TrimEnd('/');
        }

        return "http://127.0.0.1:8080" + newPath;
    }

    /// <summary>
    /// Converts simulation entity data to visualization BallUpdate structure.
    /// 
    /// DEPENDENCY MAPPING:
    /// - EntityId: Required, must be valid string identifier
    /// - Position: Required, Vector3 in simulation coordinates
    /// - Radius: Required for accurate visualization, defaults to 0.1m
    /// - Color: Optional, defaults to Blue if not provided
    /// </summary>
    private BallUpdate ConvertToBallUpdate(EntityVisualizationData entity)
    {
        // Default color if not provided (MVP: hardcoded, future: configurable)
        var color = entity.Color ?? System.Drawing.Color.Blue;

        return new BallUpdate(
            entity.EntityId,
            entity.Position,
            entity.Radius ?? 0.1f, // Default radius if not provided
            color
        );
    }
}

/// <summary>
/// Data contract for entity visualization information.
/// Extracted from simulation state for visualization purposes.
/// 
/// DESIGN NOTE: Separate struct to decouple simulation entity representation
/// from visualization requirements. Allows future changes to either system
/// without requiring synchronized updates.
/// </summary>
public struct EntityVisualizationData
{
    /// <summary>Entity identifier (must match simulation entity ID for tracking)</summary>
    public string EntityId { get; set; }

    /// <summary>Current 3D position (required)</summary>
    public Vector3 Position { get; set; }

    /// <summary>Sphere radius for visualization (optional, defaults to 0.1m)</summary>
    public float? Radius { get; set; }

    /// <summary>Visual color (optional, defaults to Blue)</summary>
    public System.Drawing.Color? Color { get; set; }

    public EntityVisualizationData(string entityId, Vector3 position, float? radius = null, System.Drawing.Color? color = null)
    {
        EntityId = entityId;
        Position = position;
        Radius = radius;
        Color = color;
    }
}
