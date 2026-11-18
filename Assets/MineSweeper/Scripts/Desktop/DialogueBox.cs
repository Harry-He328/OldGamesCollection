using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DesktopMinesweeper
{
    /// <summary>
    /// Speech bubble anchored above the desktop pet. Shows one line at a time with a
    /// typewriter reveal: a click while typing completes the line, a click afterwards
    /// raises <see cref="Clicked"/> so a driver can advance the sequence.
    /// </summary>
    public sealed class DialogueBox : MonoBehaviour, IPointerClickHandler
    {
        public const float MaxWidth = 300f;
        public const float CharactersPerSecond = 26f;
        private const float PadX = 14f;
        private const float PadY = 12f;

        private static readonly Color32 BubbleColor = new Color32(250, 248, 240, 255);
        private static readonly Color32 InkColor = new Color32(32, 40, 52, 255);
        private static readonly Color32 BorderColor = new Color32(40, 48, 62, 255);

        /// <summary>Raised when a click should move to the next line.</summary>
        public event Action Clicked;

        private RectTransform root;
        private TMP_Text label;
        private Image more;
        private float revealed;
        private float pop;
        private bool typing;
        private bool visible;

        public bool IsTyping => typing;
        public bool Visible => visible;

        public static DialogueBox Create(DesktopPet pet)
        {
            if (pet == null) return null;
            var go = new GameObject("Dialogue Bubble", typeof(RectTransform), typeof(Image));
            go.layer = pet.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(pet.Rect, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
            rect.pivot = new Vector2(.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 12f);
            rect.sizeDelta = new Vector2(MaxWidth, 64f);

            var panel = go.GetComponent<Image>();
            panel.color = BubbleColor;
            panel.raycastTarget = true;
            var border = go.AddComponent<Outline>();
            border.effectColor = BorderColor;
            border.effectDistance = new Vector2(1.5f, -1.5f);

            var box = go.AddComponent<DialogueBox>();
            box.root = rect;
            box.CreateTail(rect);
            box.CreateText(rect);
            box.CreateMore(rect);
            go.SetActive(false);
            return box;
        }

        public void Show(string line)
        {
            visible = true;
            gameObject.SetActive(true);
            typing = true;
            revealed = 0f;
            pop = 0f;

            label.maxVisibleCharacters = 0;
            Layout(line);
            if (more != null) more.enabled = false;
        }

        public void CompleteTyping()
        {
            if (!typing) return;
            EndTyping();
        }

        public void Hide()
        {
            visible = false;
            typing = false;
            gameObject.SetActive(false);
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || !visible) return;
            if (typing)
            {
                EndTyping();
                return;
            }
            Clicked?.Invoke();
        }

        private void Update()
        {
            if (!visible) return;
            float dt = Time.unscaledDeltaTime;

            pop = Mathf.Min(1f, pop + dt * 9f);
            root.localScale = Vector3.one * Mathf.Lerp(.94f, 1f, pop);

            if (typing)
            {
                revealed += CharactersPerSecond * dt;
                int total = label.textInfo.characterCount;
                int shown = Mathf.Min(total, Mathf.CeilToInt(revealed));
                label.maxVisibleCharacters = shown;
                if (shown >= total) EndTyping();
            }

            if (more != null && more.enabled)
            {
                Color c = more.color;
                c.a = .25f + .75f * (.5f + .5f * Mathf.Sin(Time.unscaledTime * 5f));
                more.color = c;
            }

            ClampToScreen();
        }

        private void EndTyping()
        {
            typing = false;
            label.maxVisibleCharacters = label.textInfo.characterCount;
            if (more != null)
            {
                more.enabled = true;
                more.color = InkColor;
            }
        }

        private void Layout(string line)
        {
            // Pin the text width first, then grow the bubble upwards to fit the line.
            label.rectTransform.offsetMin = new Vector2(PadX, -PadY);
            label.rectTransform.offsetMax = new Vector2(-PadX, -PadY);
            label.text = line;
            label.ForceMeshUpdate();
            float height = Mathf.Ceil(label.preferredHeight);
            label.rectTransform.offsetMin = new Vector2(PadX, -PadY - height);
            label.rectTransform.offsetMax = new Vector2(-PadX, -PadY);
            root.sizeDelta = new Vector2(MaxWidth, height + PadY * 2f);
        }

        private void CreateText(RectTransform parent)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(.5f, 1f);
            rect.offsetMin = new Vector2(PadX, -PadY);
            rect.offsetMax = new Vector2(-PadX, -PadY);

            label = go.AddComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset;
            label.fontSize = 18f;
            label.color = InkColor;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            label.maxVisibleCharacters = 0;
        }

        private void CreateTail(RectTransform parent)
        {
            var go = new GameObject("Tail", typeof(RectTransform), typeof(Image));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(14f, 14f);
            rect.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var image = go.GetComponent<Image>();
            image.color = BubbleColor;
            image.raycastTarget = false;
        }

        private void CreateMore(RectTransform parent)
        {
            var go = new GameObject("More", typeof(RectTransform), typeof(Image));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-10f, 8f);
            rect.sizeDelta = new Vector2(6f, 6f);
            more = go.GetComponent<Image>();
            more.color = InkColor;
            more.raycastTarget = false;
            more.enabled = false;
        }

        private void ClampToScreen()
        {
            const float margin = 8f;
            Vector3 left = root.TransformPoint(new Vector3(root.rect.xMin, root.rect.center.y, 0f));
            Vector3 right = root.TransformPoint(new Vector3(root.rect.xMax, root.rect.center.y, 0f));
            float shift = 0f;
            if (left.x < margin) shift = margin - left.x;
            else if (right.x > Screen.width - margin) shift = Screen.width - margin - right.x;
            if (Mathf.Abs(shift) < .01f) return;

            float scale = root.parent != null ? Mathf.Abs(root.parent.lossyScale.x) : 1f;
            if (scale < .0001f) scale = 1f;
            Vector2 position = root.anchoredPosition;
            position.x += shift / scale;
            root.anchoredPosition = position;
        }
    }
}
