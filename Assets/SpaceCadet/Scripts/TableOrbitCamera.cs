using UnityEngine;
using UnityEngine.InputSystem;

namespace OldGames.SpaceCadet
{
    /// <summary>Orbit works independently of the table's physics and playfield coordinates.</summary>
    public sealed class TableOrbitCamera : MonoBehaviour
    {
        public Vector3 target = new Vector3(0, 0, 0);
        public float yaw, pitch = 58, distance = 24;
        public bool autoRotate;
        public bool inputEnabled = true;
        void LateUpdate()
        {
            var mouse = inputEnabled ? Mouse.current : null;
            var keyboard = inputEnabled ? Keyboard.current : null;
            if (mouse != null)
            {
                if (mouse.rightButton.isPressed || mouse.middleButton.isPressed)
                {
                    Vector2 d = mouse.delta.ReadValue();
                    yaw += d.x * .18f;
                    pitch = Mathf.Clamp(pitch - d.y * .18f, 15, 88);
                }
                distance = Mathf.Clamp(distance - mouse.scroll.ReadValue().y * .012f, 9, 38);
            }
            if (keyboard != null)
            {
                if (keyboard.rKey.wasPressedThisFrame) { yaw = 0; pitch = 58; distance = 24; }
                if (keyboard.vKey.wasPressedThisFrame) autoRotate = !autoRotate;
                if (keyboard.digit1Key.wasPressedThisFrame) { yaw = 0; pitch = 58; distance = 24; }
                if (keyboard.digit2Key.wasPressedThisFrame) { yaw = 0; pitch = 88; distance = 25; }
                if (keyboard.digit3Key.wasPressedThisFrame) { yaw = 48; pitch = 38; distance = 24; }
            }
            if (autoRotate) yaw += Time.deltaTime * 8;
            Apply();
        }
        public void Apply()
        {
            transform.position = target + Quaternion.Euler(pitch, yaw, 0) * new Vector3(0, 0, -distance);
            transform.LookAt(target);
        }
    }
}
