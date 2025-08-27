using UnityEngine;
namespace GreenHour.Gameplay.Surveillance
{
    public class SurveillanceCamera : MonoBehaviour
    {
        [SerializeField] private Camera camera;

        public void RenderCamera(RenderTexture targetTexture)
        {
            if (camera == null) return;
            camera.targetTexture = targetTexture;
            camera.Render();
        }
    }
}