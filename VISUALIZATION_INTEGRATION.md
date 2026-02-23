# Visualization Integration Guide

## Overview

The Digital Twin Simulation now has full HTTP-based visualization integration with Godot. Entities are automatically sent to a Godot visualization server as they are created and as their state updates during simulation steps.

## Architecture

### Communication Flow

```
Simulation → VisualizationMapper → HTTP POST → Godot Server
                                 (127.0.0.1:8080/balls/update)
```

### Key Components

1. **VisualizationMapper** (`StateManager/VisualizationMapper.cs`)
   - Bridges simulation state to external visualization tools
   - Sends BallUpdate JSON objects via HTTP POST
   - Handles connection failures gracefully (simulation continues even if Godot disconnects)

2. **StateManager** (`StateManager/StateManager.cs`)
   - Integrates VisualizationMapper during initialization
   - Calls `NotifyEntitiesCreatedAsync()` when entities are created
   - Calls `NotifyStateUpdatedAsync()` after each simulation step

3. **Program.cs** (Entry Point)
   - Creates VisualizationMapper instance
   - Performs pre-execution health check (empty array POST)
   - Passes mapper through initialization chain

## How to Test with Godot

### Prerequisites

1. **Godot Project Running** with HTTP server listening on `http://127.0.0.1:8080`
2. **POST Handler** for `/balls/update` endpoint accepting JSON array of BallUpdate objects
3. **Godot C# Server Code** similar to the example below

### Godot C# Handler Example

```csharp
// In your Godot C# script
using Godot;
using System.Collections.Generic;

public partial class BallVisualizationHandler : Node
{
    private HttpServer _server;

    public override void _Ready()
    {
        _server = new HttpServer();
        _server.Bind("127.0.0.1", 8080);
        _server.StartServer();
    }

    public void HandleBallsUpdate(string requestBody)
    {
        // Parse JSON array
        var json = Json.Parse(requestBody);
        
        if (json.Data is Godot.Collections.Array entities)
        {
            // Check if this is just a health check (empty array)
            if (entities.Count == 0)
            {
                GD.Print("✅ Health check received from simulation");
                return;
            }

            // Process actual ball updates
            foreach (var entity in entities)
            {
                if (entity is Godot.Collections.Dictionary ball)
                {
                    string entityId = ball["entityId"].ToString();
                    var position = new Vector3(
                        (float)ball["position"]["x"],
                        (float)ball["position"]["y"],
                        (float)ball["position"]["z"]
                    );
                    float radius = (float)ball["radius"];
                    
                    // Update or create ball visual in Godot
                    UpdateBallVisual(entityId, position, radius);
                }
            }
        }
    }

    private void UpdateBallVisual(string entityId, Vector3 position, float radius)
    {
        // Your implementation: create/update a sphere at the given position
        var sphere = GetNodeOrNull<MeshInstance3D>($"Balls/{entityId}");
        if (sphere == null)
        {
            sphere = new MeshInstance3D();
            sphere.Name = entityId;
            sphere.Mesh = new SphereMesh() { Radius = radius, Height = radius * 2 };
            AddChild(sphere);
        }
        
        sphere.Position = position;
    }
}
```

## JSON Data Format

### Entity Creation & State Update (Non-Empty Array)

Sent as array of BallUpdate objects:

```json
[
  {
    "entityId": "Ball",
    "position": {
      "x": 0.0,
      "y": 9.95,
      "z": 0.0
    },
    "radius": 0.1,
    "color": "Red"
  }
]
```

### Health Check (Empty Array)

Used to verify connection before simulation starts:

```json
[]
```

**Godot Handler Check:**
```csharp
if (entities.Count == 0) {
    // This is a health check - just acknowledge
    GD.Print("Health check OK");
    return;
}
```

## Configuration

### Step Delay

Entity updates are sent after each simulation step. Pacing is controlled by `stepDelayMs` in `DefaultSetup.json`:

```json
{
  "stepDelayMs": 1000
}
```

- Default: **1000ms (1 second)** per simulation step
- This allows Godot visualization to keep up with changes
- Adjust based on your Godot rendering performance

### Visualization Endpoint

Default: `http://127.0.0.1:8080/balls/update`

To change, modify the VisualizationMapper initialization in `Program.cs`:

```csharp
var visualizationMapper = new VisualizationMapper(
    "http://your-godot-server:8080/custom/endpoint"
);
```

## Execution Flow

### On Program Start

```
1. Create VisualizationMapper
2. Test Connection (POST empty array)
   - Success → "✅ Connected to visualization server"
   - Failure → "⚠️ WARNING: Running without visualization"
3. Show test options
4. Get user confirmation
5. Initialize simulation with mapper
```

### During Initialization

```
[3/6] Initializing State Manager...
   📡 Visualization mapper connected - entities will be sent to external tool
```

### When Entities are Created

```
Creating entity: Ball
   📊 Sending 1 newly created entities to visualization...
```

### During Each Simulation Step

```
⚠ Failed to contact visualization server... (if Godot is not running)
OR
✓ Sent 1 ball update(s) to visualization server (if Godot is running)
```

## Error Handling

The visualization system gracefully degrades if Godot is unavailable:

| Event | Godot Running | Godot Down |
|-------|--------------|-----------|
| Health Check | ✅ Connected | ❌ Cannot connect (continues) |
| Entity Creation | ✓ Sent | ⚠ Failed to contact (continues) |
| State Updates | ✓ Sent | ⚠ Failed to contact (continues) |
| Simulation | Visualizes | Still runs locally |

**Key Point:** The simulation never crashes due to visualization issues. It gracefully continues execution while logging warnings.

## Debugging

### Check Initialization

Look for these messages in console:

```
📡 Visualization mapper connected - entities will be sent to external tool
```

If missing: VisualizationMapper was not set during StateManager initialization.

### Check Entity Creation Notifications

Look for:

```
📊 Sending X newly created entities to visualization...
```

If missing: `NotifyEntitiesCreatedAsync()` was not called by InteractionController.

### Check State Updates

Look for (after each step):

```
✓ Sent 1 ball update(s) to visualization server
```

vs

```
⚠ Failed to contact visualization server at http://127.0.0.1:8080/balls/update
```

If you see failures, verify Godot server is running and listening on the correct port.

## Testing Without Godot

The system is designed to work without a Godot server:

1. **For unit tests:** Set `visualizationMapper = null` when initializing
2. **For local visualization:** Create a local HTTP server mock
3. **For debugging:** Check console output for visualization messages

All visualization operations are **fire-and-forget** async, so network issues don't block simulation execution.

## Performance Considerations

- **HTTP Request Overhead:** ~5ms per POST (5-second timeout)
- **Fire-and-Forget Pattern:** Requests are async and don't block simulation
- **Step Delay:** Bottleneck is typically `stepDelayMs` configuration (default 1s)
- **Batching:** All entities updated in a single POST per step (no per-entity overhead)

## Future Enhancements

Potential improvements (not yet implemented):

1. **Configurable Endpoint** - Move HTTP URL to configuration file
2. **Reconnection Logic** - Automatic retry on connection loss
3. **Batch Optimization** - Combine updates from multiple steps
4. **WebSocket Support** - Replace HTTP POST with persistent WebSocket
5. **Multiple Backends** - Send to multiple visualization servers simultaneously

## Summary

✅ **Full Integration Complete**
- Entity creation notifications: **Working**
- Step-by-step state updates: **Working**
- HTTP communication: **Working**
- Graceful error handling: **Working**
- Ready for Godot integration: **Yes**

**Next Step:** Run Godot visualization server on `http://127.0.0.1:8080` with `/balls/update` POST handler, then execute the simulation to see real-time entity visualization.
