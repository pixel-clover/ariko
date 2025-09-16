public enum ErrorType
{
    None,
    Network,
    Http,
    Parsing,
    Cancellation,
    Auth,
    Unknown
}

public struct WebRequestResult<T>
{
    public T Data { get; }
    public string Error { get; }
    public ErrorType ErrorType { get; }
    public bool IsSuccess => ErrorType == ErrorType.None;

    public WebRequestResult(T data, string error, ErrorType errorType)
    {
        Data = data;
        Error = error;
        ErrorType = errorType;
    }

    // Static constructors for convenience
    public static WebRequestResult<T> Success(T data)
    {
        return new WebRequestResult<T>(data, null, ErrorType.None);
    }

    public static WebRequestResult<T> Fail(string error, ErrorType errorType)
    {
        return new WebRequestResult<T>(default, error, errorType);
    }
}
