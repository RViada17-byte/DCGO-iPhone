using UnityEngine;

public sealed class RecoveredParticleDnaRotator : MonoBehaviour
{
    public float rotateSpeed = 3f;

    void Update()
    {
        transform.Rotate(0f, 0f, rotateSpeed * Time.deltaTime, Space.Self);
    }
}
