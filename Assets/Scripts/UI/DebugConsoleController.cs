using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Toggleable in-build debug console. Press F2 to show/hide it.
/// Wraps the three existing raw debug panels (GameLoopOutput, DieOutput,
/// SelectionOutput) as tabs instead of leaving them always on screen.
/// Those three panels stay disabled (m_IsActive: 0) in the scene by default;
/// this controller is the only thing that ever turns them on, and only
/// while the console is open and that tab is selected.
/// </summary>
public class DebugConsoleController : MonoBehaviour
{
    [Header("Existing raw debug panels (kept inactive until F2 is pressed)")]
    [SerializeField] private GameObject gameLoopPanel;   // GameLoopOutput
    [SerializeField] private GameObject dicePanel;        // DieOutput
    [SerializeField] private GameObject selectionPanel;   // SelectionOutput

    [Header("Canvas the tab bar gets built into (auto-found if left empty)")]
    [SerializeField] private Canvas targetCanvas;

    private GameObject[] panels;
    private readonly string[] tabNames = { "Game", "Dice", "Selection" };
    private int activeTab = 0;
    private bool consoleOpen = false;
    private GameObject tabBarRoot;
    private Button[] tabButtons;

    private void Awake()
    {
        panels = new[] { gameLoopPanel, dicePanel, selectionPanel };
        foreach (var p in panels)
            if (p != null) p.SetActive(false);

        if (targetCanvas == null) targetCanvas = GetComponentInParent<Canvas>();
        if (targetCanvas == null) targetCanvas = FindFirstObjectByType<Canvas>();

        BuildTabBar();
        SetConsoleOpen(false);
    }

    private void Update()
    {
        bool f2Pressed = false;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
            f2Pressed = Keyboard.current.f2Key.wasPressedThisFrame;
#else
        f2Pressed = Input.GetKeyDown(KeyCode.F2);
#endif
        if (f2Pressed)
            SetConsoleOpen(!consoleOpen);
    }

    private void SetConsoleOpen(bool open)
    {
        consoleOpen = open;
        if (tabBarRoot != null) tabBarRoot.SetActive(open);
        ApplyPanelVisibility();
    }

    private void SelectTab(int index)
    {
        activeTab = index;
        ApplyPanelVisibility();
        RefreshTabVisuals();
    }

    private void ApplyPanelVisibility()
    {
        for (int i = 0; i < panels.Length; i++)
            if (panels[i] != null) panels[i].SetActive(consoleOpen && i == activeTab);
    }

    private void BuildTabBar()
    {
        if (targetCanvas == null)
        {
            Debug.LogWarning("[DebugConsoleController] No Canvas found - F2 console tab bar was not built.");
            return;
        }

        tabBarRoot = new GameObject("DebugConsoleTabs", typeof(RectTransform));
        tabBarRoot.transform.SetParent(targetCanvas.transform, false);
        tabBarRoot.transform.SetAsLastSibling();

        var rootRt = tabBarRoot.GetComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(0, 1);
        rootRt.anchorMax = new Vector2(0, 1);
        rootRt.pivot = new Vector2(0, 1);
        rootRt.anchoredPosition = new Vector2(10, -10);
        rootRt.sizeDelta = new Vector2(300, 40);

        var hlg = tabBarRoot.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 6;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;

        tabButtons = new Button[tabNames.Length];
        for (int i = 0; i < tabNames.Length; i++)
        {
            int captured = i;
            var btnGo = new GameObject(tabNames[i] + "Tab", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(tabBarRoot.transform, false);
            var btnRt = btnGo.GetComponent<RectTransform>();
            btnRt.sizeDelta = new Vector2(90, 32);

            var img = btnGo.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.6f);

            var textGo = new GameObject("Label", typeof(RectTransform));
            textGo.transform.SetParent(btnGo.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = tabNames[i];
            tmp.fontSize = 16;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;

            var btn = btnGo.GetComponent<Button>();
            btn.onClick.AddListener(() => SelectTab(captured));
            tabButtons[i] = btn;
        }

        RefreshTabVisuals();
    }

    private void RefreshTabVisuals()
    {
        if (tabButtons == null) return;
        for (int i = 0; i < tabButtons.Length; i++)
        {
            var img = tabButtons[i].GetComponent<Image>();
            img.color = (i == activeTab) ? new Color(0.2f, 0.6f, 1f, 0.85f) : new Color(0f, 0f, 0f, 0.6f);
        }
    }
}
