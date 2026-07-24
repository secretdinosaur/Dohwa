using UnityEngine;

public class PositionFollower : MonoBehaviour
{
    public Transform target;
    void Update() => transform.position = target.position;
}