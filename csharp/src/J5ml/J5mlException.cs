namespace J5ml;

/// <summary>
/// A J5ML parse or serialization failure.
/// </summary>
public sealed class J5mlException : Exception
{
	/// <summary>Builds an exception with a message.</summary>
	public J5mlException(string message) : base(message) { }

	/// <summary>Builds an exception wrapping the failure underneath it.</summary>
	public J5mlException(string message, Exception inner) : base(message, inner) { }
}
