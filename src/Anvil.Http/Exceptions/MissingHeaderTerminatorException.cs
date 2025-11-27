namespace Anvil.Http.Exceptions;

/// <summary>
/// Exception thrown when the HTTP request is missing the header terminator (\r\n\r\n).
/// </summary>
public class MissingHeaderTerminatorException : HttpParsingException
{        
        /// <summary>
        /// Initializes a new instance of the <see cref="MissingHeaderTerminatorException"/> class with a default message.
        /// </summary>
        public MissingHeaderTerminatorException()
            : base("Invalid HTTP request: missing header terminator") { }
}
