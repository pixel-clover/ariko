using UnityEngine;

public class PlayerTest : MonoBehaviour
{
    private void Update()
    {
        // Ariko should find this specific line as an issue.
        var rigidBody = GetComponent<Rigidbody>();

        Debug.Log("Hello, Ariko!");
    }
}
