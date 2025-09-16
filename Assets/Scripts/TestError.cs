using UnityEditor;
using UnityEngine;

public class TestError : MonoBehaviour
{
    [MenuItem("Tools/Ariko Tests/Throw NullReferenceException")]
    public static void ThrowError()
    {
        GameObject obj = null;
        Debug.Log(obj.name);
    }
}
