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
    }
}