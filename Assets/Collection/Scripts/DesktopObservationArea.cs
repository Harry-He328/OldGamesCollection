using UnityEngine;

namespace OldGames.Collection
{
    // Identifies an application's content area without depending on any game or level.
    public sealed class DesktopObservationArea : MonoBehaviour
    {
        [SerializeField] private RectTransform area;
        [SerializeField] private bool excluded;
        public RectTransform Area => area != null ? area : (RectTransform)transform;
        /// <summary>True for a window whose content must never be used as an observation.</summary>
        public bool Excluded => excluded;
        public void Configure(RectTransform content) => area = content;
        public void Exclude() => excluded = true;
    }
}
