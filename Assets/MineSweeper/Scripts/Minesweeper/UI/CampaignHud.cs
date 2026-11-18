using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DesktopMinesweeper
{
    internal sealed class CampaignHud
    {
        private readonly TMP_Text title;
        private readonly TMP_Text menuLabel;
        private readonly TMP_Text guidance;
        private readonly Button stageButton;
        private readonly TMP_Text stageLabel;
        private readonly Button faceButton;
        private readonly RectTransform bossHud;
        private readonly TMP_Text bossLabel;
        private readonly Image bossHealth;
        private static readonly Color DefaultInk = new Color32(32, 40, 52, 255);

        public CampaignHud(RectTransform window, Image face, UnityAction restart, UnityAction pressFace,
            UnityAction advance)
        {
            title = window.Find("Title Bar/Title").GetComponent<TMP_Text>();
            var menu = (RectTransform)window.Find("Menu Bar");
            var menuButton = menu.GetComponentInChildren<Button>();
            menuLabel = menuButton.GetComponentInChildren<TMP_Text>();
            Bind(menuButton, restart);
            SetRect((RectTransform)menuButton.transform, Vector2.zero, Vector2.up, 2, 1, 96, -1);
            SetRect(menuLabel.rectTransform, Vector2.zero, Vector2.one, 4, 0, -4, 0);
            menuLabel.textWrappingMode = TextWrappingModes.NoWrap;

            stageButton = Object.Instantiate(menuButton, menu);
            stageButton.name = "Advance Stage";
            stageLabel = stageButton.GetComponentInChildren<TMP_Text>();
            Bind(stageButton, advance);
            var next = (RectTransform)stageButton.transform;
            SetRect(next, Vector2.right, Vector2.one, -124, 1, -2, -1);
            stageButton.image.color = new Color32(221, 228, 240, 255);
            guidance = MakeLabel(menu, "Instructions", menuLabel.font, 12f);
            SetRect(guidance.rectTransform, Vector2.zero, Vector2.one, 102, 0, -130, 0);

            faceButton = face.GetComponentInParent<Button>();
            Bind(faceButton, pressFace);
            var score = (RectTransform)faceButton.transform.parent;
            bossHud = MakeRect(score, "Boss HUD");
            SetRect(bossHud, Vector2.zero, Vector2.one, 0, 0, 0, 0);
            var left = MakeLabel(bossHud, "Player Health", menuLabel.font, 11f);
            left.text = "HP";
            SetRect(left.rectTransform, Vector2.zero, Vector2.up, 77, 0, 106, 0);
            var right = MakeLabel(bossHud, "Seconds", menuLabel.font, 11f);
            right.text = "SEC";
            SetRect(right.rectTransform, Vector2.right, Vector2.one, -108, 0, -77, 0);

            var center = new Vector2(.5f, .5f);
            bossLabel = MakeLabel(bossHud, "Boss Health", menuLabel.font, 12f);
            bossLabel.fontStyle = FontStyles.Bold;
            SetRect(bossLabel.rectTransform, center, center, 25, -1, 91, 16);
            var track = MakeRect(bossHud, "Health Track").gameObject.AddComponent<Image>();
            track.raycastTarget = false;
            track.color = new Color32(60, 46, 46, 255);
            SetRect(track.rectTransform, center, center, 25, -11, 91, -3);
            bossHealth = MakeRect(track.rectTransform, "Health Fill").gameObject.AddComponent<Image>();
            bossHealth.raycastTarget = false;
            bossHealth.color = new Color32(200, 55, 45, 255);
        }

        public void Show(int stage, int totalStages, string menu, string message, string next, bool boss, Color messageColor)
        {
            title.text = boss ? "Minesweeper  [Final Level]" : $"Minesweeper  [{stage}/{totalStages}]";
            menuLabel.text = menu;
            guidance.text = message;
            guidance.color = messageColor;
            guidance.gameObject.SetActive(message.Length != 0);
            stageLabel.text = next;
            bool hasNext = next.Length != 0;
            guidance.rectTransform.offsetMax = new Vector2(hasNext ? -130 : -8, 0);
            stageButton.gameObject.SetActive(hasNext);
            faceButton.enabled = !boss;
            bossHud.gameObject.SetActive(boss);
        }

        public void SetBossHealth(int health, int maximum)
        {
            bossLabel.text = $"BOSS  {health} / {maximum}";
            SetRect(bossHealth.rectTransform, Vector2.zero, new Vector2((float)health / maximum, 1), 0, 0, 0, 0);
        }

        private static void Bind(Button button, UnityAction action)
        {
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(action);
        }

        private static TMP_Text MakeLabel(RectTransform parent, string name, TMP_FontAsset font, float size)
        {
            var label = MakeRect(parent, name).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = size;
            label.fontSizeMax = size;
            label.fontSizeMin = 9;
            label.enableAutoSizing = true;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.color = DefaultInk;
            label.raycastTarget = false;
            return label;
        }

        private static RectTransform MakeRect(RectTransform parent, string name)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.layer = parent.gameObject.layer;
            child.transform.SetParent(parent, false);
            return (RectTransform)child.transform;
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max,
            float left, float bottom, float right, float top)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(right, top);
        }
    }
}
