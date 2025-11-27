namespace Anvil.Http.Exceptions;

/// <summary>
/// Base exception for HTTP parsing errors.
/// </summary>
public class HttpParsingException : Exception
{
        /// <summary>
        /// Initializes a new instance of the <see cref="HttpParsingException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public HttpParsingException(string message) : base(message) { }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="HttpParsingException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public HttpParsingException(string message, Exception innerException)
            : base(message, innerException) { }
}
