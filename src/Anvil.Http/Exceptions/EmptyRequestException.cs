namespace Anvil.Http.Exceptions;

/// <summary>
/// Exception thrown when an empty HTTP request is received.
/// </summary>
public class EmptyRequestException : HttpParsingException
{
        /// <summary>
        /// Initializes a new instance of the <see cref="EmptyRequestException"/> class with a default message.
        /// </summary>
        public EmptyRequestException() : base("Empty HTTP request received") { }
}
