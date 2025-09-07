using GreenHour.Gameplay;
using GreenHour.Gameplay.Events;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;
namespace GreenHour.UI
{
    public class CameraOverlayResultController : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TextMeshProUGUI safetyDesc;
        [SerializeField] private TextMeshProUGUI presenceDesc;
        [SerializeField] private GameObject nextButton;
        [SerializeField] private GameEvent enableMovement;
        [SerializeField] private GameEvent disableMovement;
        private IEnumerator StartSequence()
        {
            group.alpha = 0.0f;
            safetyDesc.text = "";
            presenceDesc.text = "";
            nextButton.SetActive(false);
            disableMovement.Raise();

            if ((GameManager.Instance)) GameManager.Instance.SetCursor(true);
            string safetyText = GameManager.Instance != null ? GameManager.Instance.GetSafetyDesc() : "Missing no.";
            string presenceText = GameManager.Instance != null ? GameManager.Instance.GetPresenceDesc() : "Missing no.";

            float f = 0;
            while (f <= 1.1)
            {
                group.alpha = f;
                f += Time.deltaTime;
                yield return null;
            }
            yield return Typewrite(safetyText, safetyDesc,20);
            yield return new WaitForSeconds(1.0f);
            yield return Typewrite(presenceText, presenceDesc,20);
            yield return new WaitForSeconds(1.0f);
            nextButton.SetActive(true);
        }

        private IEnumerator EndSequence()
        {
            if ((GameManager.Instance)) GameManager.Instance.SetCursor(false);
            group.alpha = 1.0f;
            nextButton.SetActive(false);
            enableMovement.Raise();
            float f = 1;
            while (f >= -0.1f)
            {
                group.alpha = f;
                f -= Time.deltaTime;
                yield return null;
            }
            yield return null;
            this.gameObject.SetActive(false);
        }

        private IEnumerator Typewrite(string text, TextMeshProUGUI output, float speed = 1.0f)
        {
            if(output == null) yield break;
            if (speed == 0.0f) 
            {
                output.text = text;
                yield break;
            }
            output.maxVisibleCharacters = 0;
            output.text = text;
            foreach (char c in text)
            {
                output.maxVisibleCharacters += 1;
                yield return new WaitForSeconds(1.0f/speed);
            }
            
        }
        public void OnEnable()
        {
            StartCoroutine(StartSequence());
        }
        public void Disable()
        {
            StartCoroutine(EndSequence());
        }
    }
}