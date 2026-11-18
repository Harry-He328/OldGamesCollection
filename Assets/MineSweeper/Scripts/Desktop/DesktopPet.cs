using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DesktopMinesweeper
{
    /// <summary>
    /// The Minesweeper smiley, moved out of the window button and living on the desktop.
    /// It is the same Image the controller already drives for ready/won/lost and the boss
    /// hit flash, so the pet always reflects the current game state.
    /// Later story/dialogue work can subscribe to <see cref="Clicked"/> and veto the default
    /// level action through <see cref="AllowDefaultClick"/>.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class DesktopPet : MonoBehaviour, IPointerClickHandler
    {
        public const float DesktopSize = 72f;
        public const float TransitionSeconds = .5f;

        /// <summary>Raised for every click on the pet, before the default level action runs.</summary>
        public event Action Clicked;
        /// <summary>Return false from this to keep a click from reaching the Minesweeper button logic.</summary>
        public Func<bool> AllowDefaultClick { get; set; }
        /// <summary>Story beats can freeze the pet without detaching it.</summary>
        public bool Interactable { get; set; } = true;
        public Image Face { get; private set; }
        public RectTransform Rect => rect;
        public bool OnDesktop => onDesktop;

        private readonly Vector2 desktopAnchor = new Vector2(-84, 84);
        private MinesweeperController owner;
        private RectTransform rect;
        private RectTransform desktop;
        private RectTransform button;
        private Vector2 buttonSize = new Vector2(28, 28);
        private bool onDesktop;
        private Coroutine move;

        /// <summary>
        /// Reparents the authored face onto the desktop. Must run after CampaignHud has been
        /// built, because that class finds the button through the face's hierarchy.
        /// </summary>
        public static DesktopPet Attach(MinesweeperController controller, Image face)
        {
            if (controller == null || face == null) return null;
            RectTransform desktop = controller.DesktopRoot;
            Button button = face.GetComponentInParent<Button>(true);
            if (desktop == null || button == null) return null;

            DesktopPet pet = face.GetComponent<DesktopPet>();
            if (pet == null) pet = face.gameObject.AddComponent<DesktopPet>();
            pet.owner = controller;
            pet.rect = (RectTransform)face.transform;
            pet.Face = face;
            pet.desktop = desktop;
            pet.button = (RectTransform)button.transform;
            pet.buttonSize = pet.rect.rect.size;
            face.raycastTarget = true;
            pet.ShowOnDesktop(0);
            return pet;
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || !onDesktop || !Interactable) return;
            Clicked?.Invoke();
            Func<bool> allow = AllowDefaultClick;
            if (allow != null && !allow()) return;
            if (owner != null && owner.WindowOpen) owner.OnFacePressed();
        }

        /// <summary>Moves the pet out onto the desktop (no-op when it is already there).</summary>
        public void ShowOnDesktop(float duration, Action done = null)
        {
            if (onDesktop && rect.parent == desktop)
            {
                done?.Invoke();
                return;
            }
            StopMove();
            onDesktop = true;
            if (duration <= 0)
            {
                PlaceOnDesktop();
                done?.Invoke();
                return;
            }
            move = StartCoroutine(MoveToDesktop(duration, done));
        }

        /// <summary>Hides the pet while a story beat prepares its entrance.</summary>
        public void HideForStory()
        {
            StopMove();
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Flies the pet out of an icon (the opening beat uses the Computer). It starts
        /// small at the icon's center, hops onto the desktop anchor, then calls <paramref name="done"/>.
        /// </summary>
        public void AppearFrom(RectTransform origin, float duration, Action done = null)
        {
            gameObject.SetActive(true);
            StopMove();
            onDesktop = true;
            rect.SetParent(desktop, false);
            rect.anchorMin = rect.anchorMax = new Vector2(1, 0);
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = Vector2.one * DesktopSize;
            rect.localRotation = Quaternion.identity;

            Vector2 startWorld = origin != null
                ? origin.TransformPoint(origin.rect.center)
                : DesktopEndWorld();
            rect.position = startWorld;
            rect.localScale = Vector3.one * .15f;
            if (duration <= 0)
            {
                PlaceOnDesktop();
                done?.Invoke();
                return;
            }
            move = StartCoroutine(AppearRoutine(startWorld, duration, done));
        }

        /// <summary>Instantly shows the pet in its New Game button slot (opening beat).</summary>
        public void RevealOnButton()
        {
            gameObject.SetActive(true);
            StopMove();
            onDesktop = false;
            PlaceOnButton();
        }

        /// <summary>Flies the pet back into the window button (used when the boss level starts).</summary>
        public void AttachToButton(float duration, Action done = null)
        {
            if (!onDesktop && rect.parent == button)
            {
                done?.Invoke();
                return;
            }
            StopMove();
            onDesktop = false;
            if (duration <= 0)
            {
                PlaceOnButton();
                done?.Invoke();
                return;
            }
            move = StartCoroutine(MoveToButton(duration, done));
        }

        private void LateUpdate()
        {
            // The pet is the top-most desktop object; windows re-raise themselves on focus.
            if (!onDesktop || rect == null || rect.parent != desktop) return;
            if (rect.GetSiblingIndex() != desktop.childCount - 1) rect.SetAsLastSibling();
        }

        private IEnumerator MoveToDesktop(float duration, Action done)
        {
            Vector2 startWorld = rect.position;
            Vector2 startSize = rect.rect.size;
            rect.SetParent(desktop, false);
            rect.anchorMin = rect.anchorMax = new Vector2(1, 0);
            rect.pivot = new Vector2(.5f, .5f);
            rect.localScale = Vector3.one;
            rect.position = startWorld;

            Vector2 endWorld = DesktopEndWorld();
            for (float t = 0; t < 1; t += Time.deltaTime / duration)
            {
                float e = Mathf.SmoothStep(0, 1, t);
                rect.position = Vector2.Lerp(startWorld, endWorld, e);
                rect.sizeDelta = Vector2.Lerp(startSize, Vector2.one * DesktopSize, e);
                yield return null;
            }
            PlaceOnDesktop();
            move = null;
            done?.Invoke();
        }

        private IEnumerator AppearRoutine(Vector2 startWorld, float duration, Action done)
        {
            Vector2 endWorld = DesktopEndWorld();
            for (float t = 0; t < 1; t += Time.deltaTime / duration)
            {
                float e = Mathf.SmoothStep(0, 1, t);
                rect.position = Vector2.Lerp(startWorld, endWorld, e) +
                    Vector2.up * (Mathf.Sin(e * Mathf.PI) * 26f);
                rect.localScale = Vector3.one * Mathf.Lerp(.15f, 1f, e);
                yield return null;
            }
            PlaceOnDesktop();
            move = null;
            done?.Invoke();
        }

        private Vector2 DesktopEndWorld()
        {
            return desktop.TransformPoint(new Vector3(
                desktop.rect.xMax + desktopAnchor.x, desktop.rect.yMin + desktopAnchor.y, 0));
        }

        private IEnumerator MoveToButton(float duration, Action done)
        {
            Vector2 startWorld = rect.position;
            Vector2 startSize = rect.rect.size;
            Vector2 endWorld = button.TransformPoint(button.rect.center);
            for (float t = 0; t < 1; t += Time.deltaTime / duration)
            {
                float e = Mathf.SmoothStep(0, 1, t);
                rect.position = Vector2.Lerp(startWorld, endWorld, e);
                rect.sizeDelta = Vector2.Lerp(startSize, buttonSize, e);
                yield return null;
            }
            PlaceOnButton();
            move = null;
            done?.Invoke();
        }

        private void PlaceOnDesktop()
        {
            rect.SetParent(desktop, false);
            rect.anchorMin = rect.anchorMax = new Vector2(1, 0);
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = Vector2.one * DesktopSize;
            rect.anchoredPosition = desktopAnchor;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.SetAsLastSibling();
        }

        private void PlaceOnButton()
        {
            rect.SetParent(button, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = buttonSize;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private void StopMove()
        {
            if (move == null) return;
            StopCoroutine(move);
            move = null;
        }
    }
}
