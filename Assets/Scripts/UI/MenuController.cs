using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class MenuController : MonoBehaviour
{
    [Header("UI Toolkit (optional)")]
    [SerializeField] private UIDocument uiDocument;

    private VisualElement root;

    // Main menu group
    private VisualElement menuLayout;
    private VisualElement contentRoot;

    // Accessibility panel
    private VisualElement accessibilityPanel;

    // Accessibility controls
    private Button btnAminus;
    private Button btnAplus;
    private Button btnToggleContrast;
    private Button btnCloseAccess;

    // Menu buttons (pueden ser Button o VisualElement según UXML)
    private VisualElement veNewGame;
    private VisualElement veCredits;
    private VisualElement veExit;

    // Accesibilidad puede ser botón o icono
    private VisualElement veAccessibility;

    // State
    private bool highContrastEnabled = false;

    private Label previewText;


    // Text scaling (solo textos del menú principal, NO panel accesibilidad, NO títulos, NO textos dentro de botones)
    [Header("Text Size Settings")]
    [SerializeField] private float baseFontSize = 18f;
    [SerializeField] private float fontStep = 2f;
    [SerializeField] private float minFont = 14f;
    [SerializeField] private float maxFont = 30f;

    private float currentFontSize;
    private readonly List<TextElement> scalableTexts = new List<TextElement>();

    // Class name for contrast (defínela en tu USS)
    private const string HIGH_CONTRAST_CLASS = "high-contrast";
    private const string START_OVERLAY_NAME = "StartOverlay";

    private void Awake()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
        root = uiDocument.rootVisualElement;

        CacheReferences();
        WireEvents();
        InitialState();
    }

    private void CacheReferences()
    {
        contentRoot = root.Q<VisualElement>("ContentRoot") ?? root.Q<VisualElement>("content-root");

        menuLayout =
            root.Q<VisualElement>("Layout") ??
            root.Q<VisualElement>("layout") ??
            root.Q<VisualElement>("MenuLayout") ??
            root.Q<VisualElement>("ButtonsLayout");

        accessibilityPanel = root.Q<VisualElement>("AccessibilityPanel");

        // Accesibilidad (IDs)
        btnAminus         = root.Q<Button>("TextDown");       // A-
        btnAplus          = root.Q<Button>("TextUp");         // A+
        btnToggleContrast = root.Q<Button>("ToggleContrast"); // botón grande "High Contrast: OFF"
        btnCloseAccess    = root.Q<Button>("CloseAccess");

        // Botón/icono para abrir accesibilidad
        veAccessibility =
            (VisualElement)root.Q<Button>("Accessibility") ??
            root.Q<VisualElement>("Accessibility") ??
            root.Q<VisualElement>("IconAccessibility") ??
            root.Q<VisualElement>("IconAccesibility");

        // --- NEW GAME: lo intentamos por Name, y si no, por texto visible ---
        veNewGame = FindMenuActionByNameOrText(
            possibleNames: new[] { "NewGame", "StartGame", "BtnNewGame", "ButtonNewGame" },
            possibleTexts: new[] { "NEW GAME", "INICIAR", "START" }
        );

        // Credits / Exit: primero por name, luego por texto por si acaso
        veCredits = FindMenuActionByNameOrText(
            possibleNames: new[] { "Credits", "BtnCredits", "ButtonCredits" },
            possibleTexts: new[] { "CREDITS", "CRÉDITOS" }
        );

        veExit = FindMenuActionByNameOrText(
            possibleNames: new[] { "Exit", "BtnExit", "ButtonExit" },
            possibleTexts: new[] { "EXIT", "SALIR" }
        );

        // Logs útiles
        Debug.Log($"[MenuController] menuLayout: {(menuLayout != null ? menuLayout.name : "NULL")}");
        Debug.Log($"[MenuController] NEW GAME found: {(veNewGame != null ? veNewGame.name + " (" + veNewGame.GetType().Name + ")" : "NULL")}");

        previewText = root.Q<Label>("PreviewText");

    }

    private VisualElement FindMenuActionByNameOrText(string[] possibleNames, string[] possibleTexts)
    {
        // 1) Por Name (Button)
        foreach (var n in possibleNames)
        {
            var b = root.Q<Button>(n);
            if (b != null) return b;
        }

        // 2) Por Name (VisualElement)
        foreach (var n in possibleNames)
        {
            var ve = root.Q<VisualElement>(n);
            if (ve != null) return ve;
        }

        // 3) Por texto visible (Button dentro del menuLayout)
        if (menuLayout != null)
        {
            var buttons = menuLayout.Query<Button>().ToList();
            foreach (var b in buttons)
            {
                var txt = (b.text ?? "").Trim().ToUpperInvariant();
                if (possibleTexts.Any(t => txt.Contains(t)))
                    return b;
            }
        }

        // 4) Por texto de Label hijo dentro del menuLayout (para UIs “custom”)
        if (menuLayout != null)
        {
            var containers = menuLayout.Query<VisualElement>().ToList();
            foreach (var ve in containers)
            {
                // si no puede recibir eventos, lo saltamos (pero ojo: luego lo ponemos a Position al registrar)
                var label = ve.Q<Label>();
                if (label == null) continue;

                var txt = (label.text ?? "").Trim().ToUpperInvariant();
                if (possibleTexts.Any(t => txt.Contains(t)))
                    return ve;
            }
        }

        return null;
    }

    private void WireEvents()
    {
        // Accesibilidad
        btnAminus?.RegisterCallback<ClickEvent>(_ => ChangeTextSize(-fontStep));
        btnAplus?.RegisterCallback<ClickEvent>(_ => ChangeTextSize(+fontStep));
        btnToggleContrast?.RegisterCallback<ClickEvent>(_ => ToggleHighContrast());
        btnCloseAccess?.RegisterCallback<ClickEvent>(_ => CloseAccessibility());

        // Abrir accesibilidad
        RegisterClick(veAccessibility, OpenAccessibility);

        // NEW GAME (arreglado)
        RegisterClick(veNewGame, StartGameAction);

        // Otros
        RegisterClick(veCredits, () => SceneManager.LoadScene("Credits"));
        RegisterClick(veExit, ExitGame);
    }

    private void RegisterClick(VisualElement ve, System.Action action)
    {
        if (ve == null) return;

        // Asegura que reciba clicks
        ve.pickingMode = PickingMode.Position;

        ve.RegisterCallback<ClickEvent>(_ => action?.Invoke());
    }

    private void InitialState()
    {
        // Panel de accesibilidad oculto al iniciar
        if (accessibilityPanel != null)
            accessibilityPanel.style.display = DisplayStyle.None;

        // Contraste OFF al inicio
        highContrastEnabled = false;
        root.RemoveFromClassList(HIGH_CONTRAST_CLASS);
        UpdateContrastButtonText();

        // Texto base
        currentFontSize = baseFontSize;

        // Textos escalables del menú principal
        CacheScalableTexts();
        ApplyFontSizeToTexts();

        // Si existía overlay de antes
        var overlay = root.Q<VisualElement>(START_OVERLAY_NAME);
        if (overlay != null) overlay.style.display = DisplayStyle.None;

        if (previewText != null)
    previewText.style.fontSize = baseFontSize + 6f;

    }

    private void CacheScalableTexts()
    {
        scalableTexts.Clear();

        var allTexts = root.Query<TextElement>().ToList();
        foreach (var t in allTexts)
        {
            if (t == null) continue;

            // No tocar títulos grandes
            if (t.name == "TitleShadow" || t.name == "TitleLeap" || t.name == "Title")
                continue;

            // No tocar nada dentro del panel de accesibilidad (para que no “baile”)
            if (accessibilityPanel != null && accessibilityPanel.Contains(t))
                continue;

            // No tocar textos dentro de botones (evita que cambie el tamaño del botón/layout)
            if (IsInsideButton(t))
                continue;

            if (t.ClassListContains("no-scale"))
                continue;

            scalableTexts.Add(t);
        }
    }

    private bool IsInsideButton(VisualElement element)
    {
        var p = element.parent;
        while (p != null)
        {
            if (p is Button) return true;
            p = p.parent;
        }
        return false;
    }

    private void OpenAccessibility()
    {
        if (accessibilityPanel != null)
            accessibilityPanel.style.display = DisplayStyle.Flex;

        if (menuLayout != null)
            menuLayout.style.display = DisplayStyle.None;
    }

    private void CloseAccessibility()
    {
        if (accessibilityPanel != null)
            accessibilityPanel.style.display = DisplayStyle.None;

        if (menuLayout != null)
            menuLayout.style.display = DisplayStyle.Flex;
    }

    private void ToggleHighContrast()
    {
        highContrastEnabled = !highContrastEnabled;

        if (highContrastEnabled) root.AddToClassList(HIGH_CONTRAST_CLASS);
        else root.RemoveFromClassList(HIGH_CONTRAST_CLASS);

        UpdateContrastButtonText();
    }

    private void UpdateContrastButtonText()
    {
        if (btnToggleContrast == null) return;
        btnToggleContrast.text = highContrastEnabled ? "High Contrast: ON" : "High Contrast: OFF";
    }

    private void ChangeTextSize(float delta)
{
    currentFontSize = Mathf.Clamp(currentFontSize + delta, minFont, maxFont);

    ApplyFontSizeToTexts();

    // Aplicar también al preview
    if (previewText != null)
        previewText.style.fontSize = currentFontSize + 6f; 
        // +6 para que se note más visualmente
}


    private void ApplyFontSizeToTexts()
    {
        foreach (var t in scalableTexts)
        {
            if (t == null) continue;
            t.style.fontSize = currentFontSize;
        }
    }

    private void StartGameAction()
    {
        Debug.Log("Iniciando juego...");

        // Acción visible: ocultar menú + panel accesibilidad
        if (menuLayout != null) menuLayout.style.display = DisplayStyle.None;
        if (accessibilityPanel != null) accessibilityPanel.style.display = DisplayStyle.None;

        // Pantalla azul (overlay)
        ShowBlueOverlay();
    }

    private void ShowBlueOverlay()
    {
        var overlay = root.Q<VisualElement>(START_OVERLAY_NAME);
        if (overlay == null)
        {
            overlay = new VisualElement { name = START_OVERLAY_NAME };
            overlay.style.position = Position.Absolute;
            overlay.style.left = 0;
            overlay.style.right = 0;
            overlay.style.top = 0;
            overlay.style.bottom = 0;
            overlay.style.backgroundColor = new Color(0.1f, 0.25f, 0.9f, 1f);
            overlay.style.justifyContent = Justify.Center;
            overlay.style.alignItems = Align.Center;

            var label = new Label("INICIANDO JUEGO...");
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.color = Color.white;
            label.style.fontSize = 48;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;

            overlay.Add(label);
            root.Add(overlay);
        }
        else
        {
            overlay.style.display = DisplayStyle.Flex;
        }
    }

    private void ExitGame()
    {
        Debug.Log("Saliendo del juego...");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
