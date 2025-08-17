using UnityEngine;
using static GreenHour.PhysicsSurface.Surface;
namespace GreenHour.PhysicsSurface
{
    [CreateAssetMenu(fileName = "SurfaceData", menuName = "Scriptable Objects/SurfaceData")]
    public class SurfaceData : ScriptableObject
    {
        public enum SoundType
        {
            Walk,
            Landing
        }

        [SerializeField] private string surfaceName = "Default Surface";
        [SerializeField] private AudioClip[] walkSound;
        [SerializeField] private AudioClip[] landingSound;

        public AudioClip GetWalkSound()
        {
            if (walkSound == null || walkSound.Length == 0) return null;
            return walkSound[Random.Range(0, walkSound.Length)];
        }

        public AudioClip GetLandingSound()
        {
            if (landingSound == null || landingSound.Length == 0) return null;
            return landingSound[Random.Range(0, landingSound.Length)];
        }
        public AudioClip GetSound(SoundType type)
        {
            switch (type)
            {
                case SoundType.Walk:
                    return GetWalkSound();
                case SoundType.Landing:
                    return GetLandingSound();
                default:
                    return null;
            }
        }
        public string SurfaceName
        {
            get => surfaceName;
            set => surfaceName = value;
        }

        public void PlayHitSound(float volume, Vector3 position)
        {
            AudioClip clip = GetLandingSound();
            if (clip == null) return;
            AudioSource.PlayClipAtPoint(clip, position, volume);
        }
    }
}