using UnityEngine;
namespace GreenHour.Interactions.Items
{
    public class Bottle : MonoBehaviour
    {
        public float impulseThreshold = 50f;
        public GameObject destructionEffectPrefab;

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.impulse.magnitude < impulseThreshold) return;
            if (destructionEffectPrefab != null)
                Instantiate(destructionEffectPrefab, transform.position, transform.rotation);
            Destroy(gameObject);
        }
    }
}