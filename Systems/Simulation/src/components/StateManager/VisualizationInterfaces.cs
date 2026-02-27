namespace Simulation.StateManager;

using System.Collections.Generic;
using System.Numerics;
using System.Drawing;

/// <summary>
/// Simple position DTO that serializes properly to JSON
/// Now using double precision to match simulation's internal representation
/// </summary>
public class PositionDto
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }

    public PositionDto(double[] position)
    {
        if (position == null || position.Length != 3)
            throw new ArgumentException("Position must be a double array with 3 elements", nameof(position));
        X = position[0];
        Y = position[1];
        Z = position[2];
    }
}

/// <summary>
/// Simple color DTO (RGBA) that serializes properly to JSON
/// </summary>
public class ColorDto
{
    public int R { get; set; }
    public int G { get; set; }
    public int B { get; set; }
    public int A { get; set; }

    public ColorDto(Color color)
    {
        R = color.R;
        G = color.G;
        B = color.B;
        A = color.A;
    }
}

/// <summary>
/// MVP: Ball update data structure for visualization.
/// Represents snapshot of a ball's state at a point in time.
/// 
/// LOCATION: Placed in Simulation project to avoid circular dependencies.
/// Used by both Simulation (StateManager) and Visualization (external tools).
/// </summary>
public class BallUpdate
{
    public string EntityId { get; set; }
    public PositionDto Position { get; set; }
    public float Radius { get; set; }
    public ColorDto Color { get; set; }

    public BallUpdate(string entityId, double[] position, float radius, Color color)
    {
        EntityId = entityId;
        Position = new PositionDto(position);
        Radius = radius;
        Color = new ColorDto(color);
    }
}

/// <summary>
/// MVP: Interface for external visualization tool receivers.
/// Implemented by visualization backends (e.g., Unity, 3D rendering engines).
/// 
/// RESPONSIBILITY:
/// - Receive ball/entity update batches from StateManager
/// - Render/display entities in external tool
/// - Clean up when entities are removed or simulation clears
/// 
/// LOCATION: Placed in Simulation project to avoid circular project dependencies.
/// Visualization project implements this interface.
/// 
/// FUTURE CHANGES:
/// - Could add error handling/callbacks for failure scenarios
/// - Could add filtering or update coalescing
/// - Could support multiple concurrent receivers
/// </summary>
public interface IExternalSourceReceiver
{
    /// <summary>
    /// Handle batch update of ball/entity positions and properties.
    /// Called frequently during simulation (every state update).
    /// Should be efficient - implementation should minimize allocations.
    /// </summary>
    /// <param name="updates">List of BallUpdate structs with current entity state</param>
    void HandleBallUpdates(IList<BallUpdate> updates);

    /// <summary>
    /// Remove a specific entity from visualization.
    /// Called when entity is deleted from simulation.
    /// </summary>
    /// <param name="entityId">ID of entity to remove</param>
    void RemoveEntity(string entityId);

    /// <summary>
    /// Clear all entities from visualization.
    /// Called when simulation is reset/stopped.
    /// </summary>
    void ClearAllEntities();
}
