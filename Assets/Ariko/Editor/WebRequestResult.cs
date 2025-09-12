public struct WebRequestResult<T>
{
    public T Data { get; }
    public string Error { get; }
    public bool IsSuccess => Error == null;

    public WebRequestResult(T data, string error)
    {
        Data = data;
        Error = error;
    }
}
