using UnityEditor;
using UnityEngine;

/// <summary>
///     A simple class to intentionally cause an error for testing purposes.
/// </summary>
public class TestError : MonoBehaviour
{
    /// <summary>
    ///     Throws a NullReferenceException to be used as a test case.
    /// </summary>
    [MenuItem("Tools/Ariko Tests/Throw NullReferenceException")]
    public static void ThrowError()
    {
        GameObject obj = null;
        Debug.Log(obj.name);
    }
}
