using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace GreenHour.Gameplay
{
    public class BoardController : MonoBehaviour
    {
        [Header("Photos")]
        [SerializeField] private Image entityImageOutput;
        [SerializeField] private Image footprintImageOutput;
        [Header("Sound")]
        [SerializeField] private TextMeshProUGUI soundDescriptionOutput;
        public void LoadEntityPhoto()
        {
            Debug.Log("LoadEntityPhoto");
            if (entityImageOutput && GameManager.Instance) 
            {
                Sprite img = GameManager.Instance?.GetCurrentEntity()?.GetRecordedImage(0);
                entityImageOutput.gameObject.SetActive(img != null);
                entityImageOutput.sprite = img;
            }
        }
        public void LoadEntitySound(string desc)
        {
            Debug.Log("LoadEntitySound");
            if (soundDescriptionOutput) soundDescriptionOutput.SetText(desc);
        }
        public void LoadEntityFootprint()
        {
            Debug.Log("LoadEntityFootprint");
            if (footprintImageOutput && GameManager.Instance)
            {
                Sprite img = GameManager.Instance?.GetCurrentEntity()?.GetRecordedImage(1);
                footprintImageOutput.gameObject.SetActive(img != null);
                footprintImageOutput.sprite = img;
            }
        }
    }
}