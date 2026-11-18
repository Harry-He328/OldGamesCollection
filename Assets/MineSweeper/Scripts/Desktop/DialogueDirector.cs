using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DesktopMinesweeper
{
    /// <summary>
    /// Owns the pet's speech bubble and plays dialogue one line at a time.
    /// Story code only needs <see cref="Say"/>; requests made while another sequence is
    /// playing are queued, and a request id is not queued twice. This is the seam future
    /// level/story flow should use instead of talking to the bubble directly.
    /// </summary>
    public sealed class DialogueDirector : MonoBehaviour
    {
        public static DialogueDirector Instance { get; private set; }

        /// <summary>Raised with the request id once its last line is dismissed.</summary>
        public event Action<string> Finished;

        private sealed class Request
        {
            public string Id;
            public string[] Lines;
        }

        private readonly List<Request> queue = new List<Request>();
        private DesktopPet pet;
        private DialogueBox box;
        private GameObject blocker;
        private Request current;
        private int lineIndex;
        private bool playing;
        private bool manualBlock;
        private Func<bool> allowDefaultClick;
        private Func<bool> previousAllowClick;

        public bool IsPlaying => playing;

        public static DialogueDirector Create(DesktopPet pet)
        {
            if (pet == null) return null;
            if (Instance != null) return Instance;

            var go = new GameObject("Dialogue Director");
            var director = go.AddComponent<DialogueDirector>();
            director.pet = pet;
            director.box = DialogueBox.Create(pet);
            if (director.box == null)
            {
                Destroy(go);
                return null;
            }
            director.CreateBlocker();

            // While a line is playing, a pet click should only advance the dialogue.
            director.previousAllowClick = pet.AllowDefaultClick;
            director.allowDefaultClick = director.AllowDefault;
            pet.AllowDefaultClick = director.allowDefaultClick;
            director.box.Clicked += director.Advance;
            pet.Clicked += director.Advance;
            Instance = director;
            return director;
        }

        /// <summary>Queues a short dialogue. Duplicate ids are ignored while pending.</summary>
        public void Say(string id, params string[] lines)
        {
            if (lines == null || lines.Length == 0) return;
            id = id ?? string.Empty;
            if ((current != null && current.Id == id) || IsQueued(id)) return;

            queue.Add(new Request { Id = id, Lines = lines });
            TryStartNext();
        }

        /// <summary>
        /// Reaction variant: drops the current line (without raising <see cref="Finished"/>)
        /// and plays this one next. The pet answers immediately instead of waiting behind
        /// an ongoing monologue.
        /// </summary>
        public void Interrupt(string id, params string[] lines)
        {
            if (lines == null || lines.Length == 0) return;
            id = id ?? string.Empty;
            if ((current != null && current.Id == id) || IsQueued(id)) return;
            if (current != null)
            {
                current = null;
                playing = false;
                box.Hide();
            }
            queue.Insert(0, new Request { Id = id, Lines = lines });
            TryStartNext();
            SyncBlocker();
        }

        /// <summary>
        /// Loads a cutscene-style input lock on top of the dialogue lock. Used while the pet is
        /// flying out of the window, before the first line starts.
        /// </summary>
        public void SetInputBlocked(bool blocked)
        {
            manualBlock = blocked;
            SyncBlocker();
        }

        private bool AllowDefault() => !playing;

        private bool IsQueued(string id)
        {
            foreach (Request request in queue)
                if (request.Id == id) return true;
            return false;
        }

        private void Update()
        {
            if (playing && (pet == null || !pet.OnDesktop))
            {
                // The boss transition pulled the pet into the window button; drop the line
                // rather than dragging the bubble along.
                current = null;
                playing = false;
                box.Hide();
            }
            if (!playing) TryStartNext();
            SyncBlocker();
        }

        private void TryStartNext()
        {
            if (playing || queue.Count == 0) return;
            // The pet can be hidden or inside the window button during a transition.
            if (pet == null || !pet.OnDesktop || !pet.gameObject.activeInHierarchy) return;

            current = queue[0];
            queue.RemoveAt(0);
            lineIndex = 0;
            playing = true;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            box.Show(current.Lines[0]);
            SyncBlocker();
        }

        private void Advance()
        {
            if (!playing) return;
            if (box.IsTyping)
            {
                box.CompleteTyping();
                return;
            }
            lineIndex++;
            if (lineIndex >= current.Lines.Length)
            {
                FinishCurrent();
                return;
            }
            box.Show(current.Lines[lineIndex]);
        }

        private void FinishCurrent()
        {
            string id = current.Id;
            current = null;
            playing = false;
            box.Hide();
            Finished?.Invoke(id);
            SyncBlocker();
            TryStartNext();
        }

        private void CreateBlocker()
        {
            RectTransform desktop = pet != null ? (RectTransform)pet.Rect.parent : null;
            if (desktop == null) return;

            var go = new GameObject("Dialogue Input Blocker", typeof(RectTransform), typeof(Image));
            go.layer = pet.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(desktop, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = true;
            go.SetActive(false);
            blocker = go;
        }

        private void SyncBlocker()
        {
            if (blocker == null) return;
            bool show = playing || manualBlock;
            if (blocker.activeSelf == show) return;
            blocker.SetActive(show);
            if (!show || pet == null) return;
            // Keep the blocker above windows/taskbar but below the pet (and its bubble).
            blocker.transform.SetAsLastSibling();
            pet.Rect.SetAsLastSibling();
        }

        private void OnDestroy()
        {
            if (box != null)
            {
                box.Clicked -= Advance;
                Destroy(box.gameObject);
            }
            if (blocker != null) Destroy(blocker);
            if (pet != null)
            {
                pet.Clicked -= Advance;
                if (pet.AllowDefaultClick == allowDefaultClick)
                    pet.AllowDefaultClick = previousAllowClick;
            }
            if (Instance == this) Instance = null;
        }
    }
}
