using System.Threading.Tasks;
using UnityEngine;

/// <summary>
///     Provides extension methods for Unity's <see cref="AsyncOperation" /> class.
/// </summary>
public static class AsyncOperationExtensions
{
    /// <summary>
    ///     Allows awaiting an <see cref="AsyncOperation" /> using the Task-based Asynchronous Pattern (TAP).
    /// </summary>
    /// <param name="op">The async operation to convert to a task.</param>
    /// <returns>A <see cref="Task" /> that completes when the async operation finishes.</returns>
    public static Task AsTask(this AsyncOperation op)
    {
        var tcs = new TaskCompletionSource<object>();
        op.completed += _ => { tcs.TrySetResult(null); };
        return tcs.Task;
    }
}
