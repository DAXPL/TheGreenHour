using System.Collections;
using UnityEditor.Rendering;
using UnityEngine;
namespace GreenHour.Gameplay.Surveillance
{
    public class SurveillanceSystem : MonoBehaviour
    {
        [SerializeField] private SurveillanceCamera[] cameras;
        [SerializeField] private MeshRenderer outputImage;
        [SerializeField] private float framerate = 8;
        private int currentCamera = -1;
        private RenderTexture surveillanceTexture;
        private void Start()
        {
            surveillanceTexture = new RenderTexture(400, 300, 32);
            if (outputImage != null)
            {
                outputImage.material.mainTexture = surveillanceTexture;
            }
            StartCoroutine(SurveillanceLoop());
        }

        public void ChangeCamera(int a)
        {
            currentCamera = (currentCamera + a)%cameras.Length;
        }
        [ContextMenu("NextCamera")]
        public void NextCamera()
        {
            ChangeCamera(1);
        }
        [ContextMenu("PreviousCamera")]
        public void PreviousCamera()
        {
            ChangeCamera(-1);
        }

        private IEnumerator SurveillanceLoop()
        {
            while (true) 
            {
                if (currentCamera >= 0 && currentCamera < cameras.Length && cameras[currentCamera] != null) cameras[currentCamera].RenderCamera(surveillanceTexture);
                yield return new WaitForSeconds(1.0f/framerate);
            }
        }
    }
}