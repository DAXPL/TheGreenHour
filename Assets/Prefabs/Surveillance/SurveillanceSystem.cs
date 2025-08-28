using GreenHour.Interactions;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
namespace GreenHour.Gameplay.Surveillance
{
    public class SurveillanceSystem : MonoBehaviour
    {
        [SerializeField] private SurveillanceCamera[] cameras;
        [SerializeField] private MeshRenderer outputImage;
        [SerializeField] private float framerate = 8;
        [Space]
        [SerializeField] private SurveillanceEffect[] effects;
        private int currentCamera = 0;
        private RenderTexture surveillanceTexture;
        private bool renderCameras = false;
        private bool nvrActive = false;
        [Space]
        [SerializeField] private UnityEvent onFix;
        [SerializeField] private UnityEvent onBroke;
       
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
                if (renderCameras && nvrActive && currentCamera >= 0 && currentCamera < cameras.Length && cameras[currentCamera] != null) cameras[currentCamera].RenderCamera(surveillanceTexture);
                yield return new WaitForSeconds(1.0f/framerate);
            }
        }
        public void ShowImage(Entity entity)
        {
            foreach(var s in effects)
            {
                if(s.entity != entity) continue;
                s.onShow.Invoke();
                return;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                renderCameras = true;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                renderCameras = false;
            }
        }
       
        [ContextMenu("Fix")]
        public void Fix()
        {
            if(Breakers.breakersOn == false)
            {
                Debug.Log("No electricity!");
                return;
            }
            nvrActive = true;
            onFix.Invoke();
        }

        [ContextMenu("Broke")]
        public void Broke()
        {
            nvrActive = false;
            onBroke.Invoke();
        }



        [System.Serializable]
        private class SurveillanceEffect
        {
            public Entity entity;
            public UnityEvent onShow;
        }
    }
}