using UnityEngine;

namespace GreenHour.Gameplay
{
    public class BirdFeeder : Action
    {
        [SerializeField] private string nameTag;
        private BoxCollider collider;

        public override void Start()
        {
            base.Start();
            collider = GetComponent<BoxCollider>();
        }
        public override bool GetPenalty(Entity entity, out int presencePenalty, out int safetyPenalty)
        {
            presencePenalty = 0;
            safetyPenalty = 0;

            if (collider == null) return false;

            Vector3 center = collider.transform.TransformPoint(collider.center);
            Vector3 halfExtents = Vector3.Scale(collider.size * 0.5f, collider.transform.lossyScale);

            Collider[] hits = Physics.OverlapBox(center, halfExtents, collider.transform.rotation);

            foreach (Collider hit in hits)
            {
                Debug.Log("Found: " + hit.name);
                if (hit == collider) continue;
                if (!hit.name.Contains(nameTag)) continue;
                Debug.Log($"Ok {hit.name}");
                Destroy(hit.gameObject);
                foreach (var result in results)
                {
                    if (result == null) continue;
                    if (result.entity.name == entity.name)
                    {
                        presencePenalty = result.presencePenalty;
                        safetyPenalty = result.safetyPenalty;
                        return true;
                    }
                }

            }

            return false;
        }
    }
}