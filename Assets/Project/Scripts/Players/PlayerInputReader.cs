using UnityEngine;
using UnityEngine.InputSystem;

namespace BotsBolts.Players
{
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [SerializeField] private InputActionAsset actions;
        private InputAction move;
        private InputAction jump;

        public Vector2 Movement => move != null && move.enabled && Application.isFocused
            ? Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f) : Vector2.zero;
        public bool JumpPressed => jump != null && jump.enabled && Application.isFocused && jump.WasPressedThisFrame();

        public void SetLocalControl(bool value)
        {
            if (value && move == null)
            {
                // Each owner needs only these two independent actions, not a copy
                // of every gameplay and UI action in the source asset.
                move = actions.FindAction("Player/Move", true).Clone();
                jump = actions.FindAction("Player/Jump", true).Clone();
            }
            if (move == null) return;
            if (value) { move.Enable(); jump.Enable(); }
            else { move.Disable(); jump.Disable(); }
        }

        private void OnDisable() => SetLocalControl(false);

        private void OnDestroy()
        {
            move?.Dispose();
            jump?.Dispose();
            move = jump = null;
        }
    }
}
