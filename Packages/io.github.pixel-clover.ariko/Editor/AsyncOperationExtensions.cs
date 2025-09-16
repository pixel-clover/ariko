using System.Threading.Tasks;
using UnityEngine;

public static class AsyncOperationExtensions
{
    public static Task AsTask(this AsyncOperation op)
    {
        var tcs = new TaskCompletionSource<object>();
        op.completed += _ => { tcs.TrySetResult(null); };
        return tcs.Task;
    }
}
