using GreenHour.Interactions.Items;
using UnityEngine;
namespace GreenHour.Gameplay
{
    public class WasteBin : Action
    {
        [SerializeField] private Rigidbody trapdoor;
        [SerializeField] private int itemsPerPoint;
        [SerializeField] private ItemData[] waste;
        [SerializeField] private int strength = 1000;
        private int wasteCount = 0;
        public override bool GetPenalty(Entity entity, out int presencePenalty, out int safetyPenalty)
        {
            presencePenalty = 0;
            safetyPenalty = 0;
            wasteCount = 0;
            int waste = wasteCount / itemsPerPoint;
            foreach (var result in results)
            {
                if (result == null) continue;
                if (result.entity.EntityName == entity.EntityName)
                {
                    presencePenalty = result.presencePenalty * waste;
                    safetyPenalty = result.safetyPenalty * waste;
                    result.action.Invoke();
                    return true;
                }
            }
            return false;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.gameObject.TryGetComponent(out Item item)) return;
            foreach(ItemData i  in waste)
            {
                if (i == item.GetData())
                {
                    wasteCount++;
                    Destroy(item.gameObject);
                }
            }
        }

        public void Open()
        {
            if (trapdoor == null) return;
            trapdoor.AddForce(trapdoor.transform.up * strength);
            trapdoor.AddTorque(trapdoor.transform.right * -strength);
        }
    }
}