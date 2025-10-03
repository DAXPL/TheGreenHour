using UnityEngine;
namespace GreenHour.Editor
{
    public class Randomizer : MonoBehaviour
    {
        [SerializeField] private GameObject[] objects;

        void Start() 
        {
            int id = Random.Range(0, objects.Length);
            for (int i = 0; i<objects.Length; i++) objects[i].SetActive(i==id);
        }
    }
}