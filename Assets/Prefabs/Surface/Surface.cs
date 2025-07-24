using UnityEngine;
using static GreenHour.PhysicsSurface.SurfaceData;
namespace GreenHour.PhysicsSurface
{
    public class Surface : MonoBehaviour
    {
        
        [SerializeField] private SurfaceData surfaceData;

        public AudioClip GetSound(SoundType type)
        {
            return surfaceData.GetSound(type);
        }

        public AudioClip GetWalkSound()
        {
            if (surfaceData == null) return null;
            return surfaceData.GetWalkSound();
        }
        public AudioClip GetLandingSound()
        {
            if (surfaceData == null) return null;
            return surfaceData.GetLandingSound();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (surfaceData == null) return;
            if (collision.relativeVelocity.magnitude < 0.5f) return;
            AudioClip clip = this.GetLandingSound();
            if(clip != null)
            {
                float volume = Mathf.InverseLerp(1.0f, 10f, collision.relativeVelocity.magnitude)*2.0f;
                AudioSource.PlayClipAtPoint(clip, collision.contacts[0].point, volume);
            }

        }
    }
}