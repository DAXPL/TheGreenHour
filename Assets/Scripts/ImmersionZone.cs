using UnityEngine;
using GreenHour.Electonics;

namespace GreenHour.WordBuilding
{
    public class ImmersionZone : MonoBehaviour
    {
        [SerializeField] private ImmersionZoneController.Smells smell;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.TryGetComponent(out CharacterController cc)) return;
            if (ImmersionZoneController.Instance == null) return;
            ImmersionZoneController.Instance.SendData(smell);
        }
    }
}

