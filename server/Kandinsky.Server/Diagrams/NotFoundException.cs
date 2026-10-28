namespace Kandinsky.Server.Diagrams;

/// <summary>A requested diagram, shape or edge does not exist. The message is safe to return to clients.</summary>
public sealed class NotFoundException(string message) : Exception(message);
