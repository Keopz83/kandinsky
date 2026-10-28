namespace Kandinsky.Server.Diagrams;

/// <summary>Invalid input. The message is safe to return to clients.</summary>
public sealed class DiagramValidationException(string message) : Exception(message);
