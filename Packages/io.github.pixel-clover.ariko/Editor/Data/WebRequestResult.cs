/// <summary>
///     Defines the type of error that occurred during a web request.
/// </summary>
public enum ErrorType
{
    /// <summary>
    ///     No error occurred.
    /// </summary>
    None,

    /// <summary>
    ///     A network error occurred (e.g., no internet connection).
    /// </summary>
    Network,

    /// <summary>
    ///     An HTTP protocol error occurred (e.g., 404 Not Found, 500 Server Error).
    /// </summary>
    Http,

    /// <summary>
    ///     An error occurred while parsing the response from the server.
    /// </summary>
    Parsing,

    /// <summary>
    ///     The request was cancelled.
    /// </summary>
    Cancellation,

    /// <summary>
    ///     An authentication error occurred (e.g., missing or invalid API key).
    /// </summary>
    Auth,

    /// <summary>
    ///     An unknown or uncategorized error occurred.
    /// </summary>
    Unknown
}

/// <summary>
///     A generic struct that represents the result of a web request.
///     It can either hold the successful data or information about an error.
/// </summary>
/// <typeparam name="T">The type of data expected from a successful request.</typeparam>
public struct WebRequestResult<T>
{
    /// <summary>
    ///     Gets the data from a successful request.
    /// </summary>
    public T Data { get; }

    /// <summary>
    ///     Gets the error message from a failed request.
    /// </summary>
    public string Error { get; }

    /// <summary>
    ///     Gets the type of error that occurred.
    /// </summary>
    public ErrorType ErrorType { get; }

    /// <summary>
    ///     Gets a value indicating whether the web request was successful.
    /// </summary>
    public bool IsSuccess => ErrorType == ErrorType.None;

    /// <summary>
    ///     Initializes a new instance of the <see cref="WebRequestResult{T}" /> struct.
    /// </summary>
    /// <param name="data">The data from a successful request.</param>
    /// <param name="error">The error message from a failed request.</param>
    /// <param name="errorType">The type of error that occurred.</param>
    public WebRequestResult(T data, string error, ErrorType errorType)
    {
        Data = data;
        Error = error;
        ErrorType = errorType;
    }

    /// <summary>
    ///     Creates a successful WebRequestResult.
    /// </summary>
    /// <param name="data">The data to wrap in the result.</param>
    /// <returns>A new WebRequestResult instance representing success.</returns>
    public static WebRequestResult<T> Success(T data)
    {
        return new WebRequestResult<T>(data, null, ErrorType.None);
    }

    /// <summary>
    ///     Creates a failed WebRequestResult.
    /// </summary>
    /// <param name="error">The error message.</param>
    /// <param name="errorType">The type of the error.</param>
    /// <returns>A new WebRequestResult instance representing failure.</returns>
    public static WebRequestResult<T> Fail(string error, ErrorType errorType)
    {
        return new WebRequestResult<T>(default, error, errorType);
    }
}
