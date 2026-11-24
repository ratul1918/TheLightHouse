using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TheLightHouse
{
    public sealed class DeathScreenUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Text quoteText;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button loadButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private SaveLoadUI saveLoad;

        private static readonly string[] DeathQuotes =
        {
            "\"The sea took them all, and the sea never forgets.\"",
            "\"Never answer the singing from the cellar.\"",
            "\"Keep the light. Darkness lets them advance.\"",
            "\"Six sailors drowned in the gale. You were the only one who reached the shore.\"",
            "\"The lighthouse was never a guide. It was a leash.\""
        };

        public static DeathScreenUI EnsureInstance()
        {
            var existing = FindAnyObjectByType<DeathScreenUI>();
            if (existing) return existing;
            var canvas = FindAnyObjectByType<Canvas>();
            if (!canvas) return null;
            var go = new GameObject("Death Screen UI", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            return go.AddComponent<DeathScreenUI>();
        }

        private void Awake()
        {
            if (!panel) BuildDefaultUI();
            if (panel) panel.SetActive(false);
            if (canvasGroup) canvasGroup.alpha = 0f;

            if (retryButton) retryButton.onClick.AddListener(OnRetry);
            if (loadButton) loadButton.onClick.AddListener(OnLoad);
            if (mainMenuButton) mainMenuButton.onClick.AddListener(OnMainMenu);
        }

        private void BuildDefaultUI()
        {
            var rect = GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            panel = gameObject;
            canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

            var bgImg = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            bgImg.color = new Color(0.02f, 0.03f, 0.04f, 0.95f);

            var quoteObj = new GameObject("QuoteText", typeof(RectTransform), typeof(Text));
            quoteObj.transform.SetParent(transform, false);
            var quoteRect = quoteObj.GetComponent<RectTransform>();
            quoteRect.anchorMin = new Vector2(0.2f, 0.5f);
            quoteRect.anchorMax = new Vector2(0.8f, 0.75f);
            quoteRect.offsetMin = quoteRect.offsetMax = Vector2.zero;
            quoteText = quoteObj.GetComponent<Text>();
            quoteText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            quoteText.fontSize = 24;
            quoteText.fontStyle = FontStyle.Italic;
            quoteText.alignment = TextAnchor.MiddleCenter;
            quoteText.color = new Color(0.85f, 0.88f, 0.92f);

            retryButton = CreateButton("RetryButton", "RETRY CHECKPOINT", new Vector2(0.35f, 0.35f), new Vector2(0.65f, 0.43f));
            mainMenuButton = CreateButton("MainMenuButton", "MAIN MENU", new Vector2(0.35f, 0.24f), new Vector2(0.65f, 0.32f));
        }

        private Button CreateButton(string name, string label, Vector2 min, Vector2 max)
        {
            var btnObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(transform, false);
            var rect = btnObj.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var img = btnObj.GetComponent<Image>();
            img.color = new Color(0.18f, 0.22f, 0.25f, 0.9f);

            var txtObj = new GameObject("Label", typeof(RectTransform), typeof(Text));
            txtObj.transform.SetParent(btnObj.transform, false);
            var txtRect = txtObj.GetComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = txtRect.offsetMax = Vector2.zero;
            var txt = txtObj.GetComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 20;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.text = label;

            return btnObj.GetComponent<Button>();
        }

        public void ShowDeath()
        {
            if (!panel) return;
            panel.SetActive(true);
            if (quoteText)
            {
                int idx = UnityEngine.Random.Range(0, DeathQuotes.Length);
                quoteText.text = DeathQuotes[idx];
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            StopAllCoroutines();
            StartCoroutine(FadeInRoutine());
        }

        private IEnumerator FadeInRoutine()
        {
            float elapsed = 0f;
            const float duration = 1.5f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                if (canvasGroup) canvasGroup.alpha = Mathf.Clamp01(elapsed / duration);
                yield return null;
            }
            if (canvasGroup) canvasGroup.alpha = 1f;
        }

        private void OnRetry()
        {
            Time.timeScale = 1f;
            if (panel) panel.SetActive(false);
            var health = FindAnyObjectByType<PlayerHealthState>();
            if (health)
            {
                health.Respawn();
            }
            else
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
        }

        private void OnLoad()
        {
            if (saveLoad)
            {
                saveLoad.Open(false);
            }
        }

        private void OnMainMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("MainMenu");
        }
    }
}
