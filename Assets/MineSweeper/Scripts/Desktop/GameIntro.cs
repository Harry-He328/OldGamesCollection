using System.Collections;
using UnityEngine;

namespace DesktopMinesweeper
{
    /// <summary>
    /// Opening beat. The Minesweeper window starts closed and its desktop icon nudges the
    /// player to open it. On the first open the pet appears in its New Game button, hops out
    /// to the desktop anchor, then speaks a short intro. Input stays locked from the moment
    /// it appears until the intro is dismissed.
    /// Also reacts when the player tries to bin the Minesweeper shortcut, and nudges the
    /// player towards Notepad shortly after level 2 starts.
    /// No level is started here - future level flow hooks into <see cref="DialogueDirector"/>.
    /// </summary>
    internal sealed class GameIntro : MonoBehaviour
    {
        private static readonly string[] IntroLines =
        {
            "Hey. You're awake.",
            "This desktop is yours.",
            "Ready to start the game?"
        };

        private static readonly string[] RecycleLines =
        {
            "No, no, no! You can't do that.",
            "That's my favorite game!"
        };

        // Level 2's observation board only becomes interesting once the player opens Notepad.
        private const float NotepadHintDelay = 10f;
        private static readonly string[] NotepadHintLines =
        {
            "Open Notepad.",
            "You might find something unexpected."
        };

        private DesktopPet pet;
        private DialogueDirector director;
        private DesktopWindow window;
        private MinesweeperController controller;
        private Coroutine notepadHint;
        private RectTransform bin;
        private RectTransform shortcut;
        private Quaternion shortcutRest;
        private Coroutine wiggle;
        private bool opened;
        private bool readyForReaction;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var go = new GameObject("Game Intro");
            go.AddComponent<GameIntro>();
        }

        private void Start()
        {
            // Start runs after the controller's Awake has created the pet.
            pet = FindFirstObjectByType<DesktopPet>(FindObjectsInactive.Include);
            controller = FindFirstObjectByType<MinesweeperController>(FindObjectsInactive.Include);
            if (pet == null || controller == null)
            {
                Destroy(gameObject);
                return;
            }

            window = controller.GetComponent<DesktopWindow>();
            director = DialogueDirector.Create(pet);
            if (window == null || director == null)
            {
                Destroy(gameObject);
                return;
            }
            window.Shown += OnWindowShown;
            controller.HiddenBoardStarted += OnHiddenBoardStarted;
            controller.HiddenBoardEnded += OnHiddenBoardEnded;

            DesktopIcon binIcon = FindIcon("Recycle Bin");
            bin = binIcon != null ? (RectTransform)binIcon.transform : null;

            DesktopIcon shortcutIcon = FindIcon("Minesweeper Shortcut");
            if (shortcutIcon != null)
            {
                shortcut = (RectTransform)shortcutIcon.transform;
                shortcutRest = shortcut.localRotation;
                wiggle = StartCoroutine(Wiggle());
            }

            pet.Interactable = false;
            pet.HideForStory();
        }

        private void OnDestroy()
        {
            if (window != null) window.Shown -= OnWindowShown;
            if (controller != null)
            {
                controller.HiddenBoardStarted -= OnHiddenBoardStarted;
                controller.HiddenBoardEnded -= OnHiddenBoardEnded;
            }
            if (wiggle != null) StopCoroutine(wiggle);
            if (shortcut != null) shortcut.localRotation = shortcutRest;
        }

        private void OnEnable() => DesktopIconDrag.Dropped += OnIconDropped;
        private void OnDisable() => DesktopIconDrag.Dropped -= OnIconDropped;

        private void OnWindowShown()
        {
            if (opened) return;
            opened = true;
            if (wiggle != null)
            {
                StopCoroutine(wiggle);
                wiggle = null;
            }
            if (shortcut != null) shortcut.localRotation = shortcutRest;
            StartCoroutine(EnterFromButton());
        }

        private IEnumerator EnterFromButton()
        {
            // Keep the player from closing/moving the window while the pet is emerging.
            director.SetInputBlocked(true);
            yield return null; // Let the reopened window finish its layout.

            pet.RevealOnButton();
            yield return new WaitForSecondsRealtime(.45f);

            pet.ShowOnDesktop(DesktopPet.TransitionSeconds, () =>
            {
                readyForReaction = true;
                pet.Interactable = true;
                director.Say("intro", IntroLines);
                director.SetInputBlocked(false);
            });
        }

        private void OnHiddenBoardStarted()
        {
            // A restart tears down and starts level 2 again; the hint is timed from the newest start.
            if (notepadHint != null) StopCoroutine(notepadHint);
            notepadHint = StartCoroutine(NotepadHint());
        }

        private void OnHiddenBoardEnded()
        {
            if (notepadHint == null) return;
            StopCoroutine(notepadHint);
            notepadHint = null;
        }

        private IEnumerator NotepadHint()
        {
            yield return new WaitForSecondsRealtime(NotepadHintDelay);
            notepadHint = null;
            if (controller == null || !controller.InHiddenBoard) yield break;
            if (director == null || pet == null || !pet.OnDesktop) yield break;
            director.Say("level2_notepad", NotepadHintLines);
        }

        private IEnumerator Wiggle()
        {
            while (true)
            {
                for (float t = 0f; t < .45f; t += Time.deltaTime)
                {
                    float envelope = Mathf.Sin(t / .45f * Mathf.PI);
                    float angle = Mathf.Sin(t * 42f) * 7f * envelope;
                    shortcut.localRotation = shortcutRest * Quaternion.Euler(0f, 0f, angle);
                    yield return null;
                }
                shortcut.localRotation = shortcutRest;
                yield return new WaitForSecondsRealtime(1.2f);
            }
        }

        private void OnIconDropped(DesktopIcon icon, Vector2 screenPosition)
        {
            if (icon == null || bin == null || director == null || pet == null) return;
            // No reaction before the opening flight has finished.
            if (!readyForReaction || !pet.OnDesktop) return;
            if (icon.name != "Minesweeper Shortcut") return;
            if (!RectTransformUtility.RectangleContainsScreenPoint(bin, screenPosition, null)) return;
            director.Interrupt("recycle_minesweeper", RecycleLines);
        }

        private static DesktopIcon FindIcon(string name)
        {
            foreach (DesktopIcon icon in FindObjectsByType<DesktopIcon>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (icon.name == name) return icon;
            return null;
        }
    }
}
