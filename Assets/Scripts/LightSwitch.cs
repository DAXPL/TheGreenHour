using UnityEngine;
namespace GreenHour.Interactions
{
    public class LightSwitch : MonoBehaviour
    {
        [SerializeField] private Light[] lights;
        bool state = false;

        private void Start()
        {
            ToggleLights(Breakers.breakersOn);
        }

        public void ToggleLights(bool newState)
        {
            state = newState;
            bool electricity = Breakers.breakersOn;
            foreach (var light in lights)
            {
                light.enabled = state && electricity;
            }
        }
        public void ToggleLights()
        {
            state = !state;
            bool electricity = Breakers.breakersOn;
            foreach (var light in lights)
            {
                light.enabled = state && electricity;
            }
        }

    }
}