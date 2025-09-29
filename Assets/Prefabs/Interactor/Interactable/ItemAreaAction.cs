using GreenHour.Interactions.Items;
using UnityEngine;
namespace GreenHour.Gameplay
{
    public class ItemAreaAction : Action
    {
        [SerializeField] private int wantedCount = 1;
        [SerializeField] private ItemData[] wantedItems;

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
            int wantedCounter = 0;
            foreach (Collider hit in hits)
            {
                if (hit == collider) continue;
                if (!hit.TryGetComponent(out Item item)) continue;

                ItemData itemData = item.GetData();
                foreach (ItemData wanted in wantedItems)
                {
                    if (wanted == itemData)
                    {
                        wantedCounter ++;
                        if(wantedCounter < wantedCount) continue;
                        foreach (var result in results)
                        {
                            if (result == null) continue;
                            if (result.entity.EntityName == entity.EntityName)
                            {
                                Debug.Log($"Ok: {hit.gameObject.name}");
                                presencePenalty = result.presencePenalty;
                                safetyPenalty = result.safetyPenalty;
                                result.action.Invoke();
                                Destroy(hit.gameObject);
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }
    }
}