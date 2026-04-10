namespace Simulation.PropertyTypes;

/// <summary>
/// Marker interface for structured property value types.
/// Implement this on any record or class that represents a rich property value
/// (i.e. one that carries more than a plain scalar or vector).
///
/// <c>GetPrintable()</c> returns the single-line string used by the state reporter.
/// Implement it to control exactly how the value appears in console output.
/// </summary>
public interface IPropertyValue
{
    string GetPrintable();
}
