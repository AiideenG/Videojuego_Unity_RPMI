using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class CreditsController : MonoBehaviour
{
    [Header("UI Toolkit")]
    [SerializeField] private UIDocument uiDocument;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;

    [Header("Icons")]
    [SerializeField] private Texture2D playIcon;
    [SerializeField] private Texture2D pauseIcon;

    private VisualElement root;
    private VisualElement audioIcon;
    private Button backToMenuButton;

    // Barra de progreso
    private VisualElement audioProgress;

    private bool isPlaying;

    private void Awake()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        root = uiDocument.rootVisualElement;

        backToMenuButton = root.Q<Button>("BackToMenu");
        if (backToMenuButton != null)
            backToMenuButton.clicked += () => SceneManager.LoadScene("Menu");

        audioIcon = root.Q<VisualElement>("AudioIcon");
        if (audioIcon != null)
            audioIcon.RegisterCallback<ClickEvent>(_ => ToggleAudio());

        audioProgress = root.Q<VisualElement>("AudioProgress");

        isPlaying = audioSource != null && audioSource.isPlaying;
        UpdateAudioIcon();
        UpdateProgressBar(forceReset: true);
    }

    private void Update()
    {
        UpdateProgressBar();
    }

    private void ToggleAudio()
    {
        if (audioSource == null || audioSource.clip == null)
        {
            Debug.LogWarning("CreditsController: AudioSource o clip no asignado");
            return;
        }

        if (audioSource.isPlaying)
        {
            audioSource.Pause();
            isPlaying = false;
        }
        else
        {
            audioSource.Play();
            isPlaying = true;
        }

        UpdateAudioIcon();
        UpdateProgressBar();
    }

    private void UpdateAudioIcon()
    {
        if (audioIcon == null) return;

        Texture2D icon = isPlaying ? pauseIcon : playIcon;
        if (icon == null) return;

        audioIcon.style.backgroundImage = new StyleBackground(icon);
    }

    private void UpdateProgressBar(bool forceReset = false)
    {
        if (audioProgress == null) return;

        if (audioSource == null || audioSource.clip == null)
        {
            audioProgress.style.width = Length.Percent(0);
            return;
        }

        if (forceReset && !audioSource.isPlaying && audioSource.time <= 0.01f)
        {
            audioProgress.style.width = Length.Percent(0);
            return;
        }

        float t = audioSource.clip.length > 0f ? (audioSource.time / audioSource.clip.length) : 0f;
        t = Mathf.Clamp01(t);

        audioProgress.style.width = Length.Percent(t * 100f);

        // Si llega al final (por ejemplo si no está en loop), resetea icono
        if (!audioSource.loop && t >= 0.999f)
        {
            isPlaying = false;
            UpdateAudioIcon();
        }
    }
}
