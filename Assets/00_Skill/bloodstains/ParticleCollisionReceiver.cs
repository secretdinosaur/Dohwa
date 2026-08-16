using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(ParticleSystem))]
public class ParticleCollisionReceiver : MonoBehaviour
{
    private ParticleSystem part;
    private List<ParticleCollisionEvent> collisionEvents;

    private void Awake()
    {
        part = GetComponent<ParticleSystem>();
        collisionEvents = new List<ParticleCollisionEvent>();
    }

    private void OnParticleCollision(GameObject other)
    {
        int numCollisionEvents = part.GetCollisionEvents(other, collisionEvents);

        for (int i = 0; i < numCollisionEvents; i++)
        {
            Vector3 hitPoint = collisionEvents[i].intersection;
            Vector3 hitNormal = collisionEvents[i].normal;
            Vector3 velocity = collisionEvents[i].velocity;

            if (MapDecalManager.Instance != null)
            {
                MapDecalManager.Instance.SpawnBloodDecal(hitPoint, hitNormal, velocity);
            }
        }
    }
}