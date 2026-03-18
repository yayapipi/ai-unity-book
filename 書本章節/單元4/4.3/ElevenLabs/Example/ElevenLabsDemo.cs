using UnityEngine;
using UnityEngine.UI;
using ElevenLab;

public class ElevenLabsDemo : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ElevenLabsCore eleven;
    [SerializeField] private AudioSource audioSource;

    [Header("UI")]
    [SerializeField] private Button speakButton;
    [SerializeField] private Slider progressSlider;

    [Header("Text Input")]
    [TextArea(3, 6)]
    [SerializeField] private string text =
        "The first move is what sets everything in motion.";

    private void Awake()
    {
        if (speakButton != null)
            speakButton.onClick.AddListener(Speak);
    }

    private void OnDestroy()
    {
        if (speakButton != null)
            speakButton.onClick.RemoveListener(Speak);
    }

    private void Start()
    {
        if (progressSlider != null)
        {
            progressSlider.value = 0f;
            progressSlider.gameObject.SetActive(false);
        }
    }

    [ContextMenu("Speak")]
    public void Speak()
    {
        if (eleven == null)
        {
            Debug.LogError("ElevenLabsCore is not assigned!");
            return;
        }

        if (speakButton != null)
            speakButton.interactable = false;

        if (progressSlider != null)
        {
            progressSlider.value = 0f;
            progressSlider.gameObject.SetActive(true);
        }

        var req = new ElevenLabsRequest
        {
            text = text,
            model_id = "eleven_multilingual_v2"
        };

        eleven.Speak(
            req,
            clip =>
            {
                if (audioSource != null)
                {
                    audioSource.clip = clip;
                    audioSource.Play();
                }
                Debug.Log("ElevenLabs speaking");
                
                if (speakButton != null)
                    speakButton.interactable = true;
                    
                if (progressSlider != null)
                {
                    progressSlider.value = 1f;
                    // 可選：播放完成後隱藏進度條
                    // progressSlider.gameObject.SetActive(false);
                }
            },
            err =>
            {
                Debug.LogError("ElevenLabs error: " + err);
                
                if (speakButton != null)
                    speakButton.interactable = true;
                    
                if (progressSlider != null)
                    progressSlider.gameObject.SetActive(false);
            },
            progress =>
            {
                if (progressSlider != null)
                    progressSlider.value = progress;
            }
        );
    }
}
