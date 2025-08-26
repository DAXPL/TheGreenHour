using System.Collections;
using UnityEngine;

namespace GreenHour.Interactions.Items 
{
    public class DigitalCamera : MonoBehaviour
    {
        [SerializeField] private Camera cam;
        [SerializeField] private float framerate = 5f;
        [SerializeField] private float sphereRadius = 1f;
        [SerializeField] private LayerMask detectionMask;
        bool render = false;
        private void Start()
        {
            StartCoroutine(CameraLoop());
        }

        private IEnumerator CameraLoop()
        {
            while (true)
            {
                render = false;

                Collider[] hits = Physics.OverlapSphere(transform.position, sphereRadius, detectionMask);

                foreach (var hit in hits)
                {
                    if (hit.TryGetComponent(out CharacterController character))
                    {
                        render = true;
                        break;
                    }
                }

                if (cam && render) cam.Render();

                yield return new WaitForSeconds(1f / framerate);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (cam != null)
            {
                Gizmos.color = render? Color.green : Color.red;
                Gizmos.DrawWireSphere(transform.position, sphereRadius);
            }
        }
        public void TakePhoto()
        {
            SaveCameraScreenshot(cam.targetTexture, "camera_shot_" + System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
        }
        private void SaveCameraScreenshot(RenderTexture rt, string filename)
        {
            RenderTexture currentRT = RenderTexture.active;
            RenderTexture.active = rt;

            // odczytaj piksele
            Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();

            // przywróæ poprzedni render target
            RenderTexture.active = currentRT;

            // zapisz do pliku
            byte[] bytes = tex.EncodeToPNG();
            string path = Application.persistentDataPath + "/" + filename + ".png";
            System.IO.File.WriteAllBytes(path, bytes);

            Debug.Log("Zapisano screenshot: " + path);
        }
    }
}
