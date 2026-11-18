using UnityEngine;

namespace OldGames.Collection
{
    // Identifies an application's content area without depending on any game or level.
    public sealed class DesktopObservationArea : MonoBehaviour
    {
        [SerializeField] private RectTransform area;
        public RectTransform Area => area != null ? area : (RectTransform)transform;
        public void Configure(RectTransform content) => area = content;
    }
}
