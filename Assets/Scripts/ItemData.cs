using UnityEngine;
namespace GreenHour.Interactions.Items
{
    [CreateAssetMenu(fileName = "ItemData", menuName = "Scriptable Objects/ItemData")]
    public class ItemData : ScriptableObject
    {
        public string itemName;
    }
}