using UnityEngine;
using UnityEngine.InputSystem;

namespace BotsBolts.Players
{
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [SerializeField] private InputActionAsset actions;
        private InputAction move;
        private InputAction jump;
        private InputAction interact;
        private bool gamepadBinding;

        public Vector2 Movement => move != null && move.enabled && Application.isFocused
            ? Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f) : Vector2.zero;
        public bool JumpPressed => jump != null && jump.enabled && Application.isFocused && jump.WasPressedThisFrame();
        public bool InteractPressed => interact != null && interact.enabled && Application.isFocused && interact.WasPressedThisFrame();
        public string InteractBinding => gamepadBinding ? "Y / Triangle" : "E";

        private void Update()
        {
            if (move == null || !move.enabled) return;
            InputAction last = interact.WasPressedThisFrame() ? interact : jump.WasPressedThisFrame() ? jump :
                move.WasPerformedThisFrame() ? move : null;
            if (last != null) gamepadBinding = last.activeControl?.device is Gamepad;
        }

        public void SetLocalControl(bool value)
        {
            if (value && move == null)
            {
                // Each owner needs only these independent actions, not a copy
                // of every gameplay and UI action in the source asset.
                move = actions.FindAction("Player/Move", true).Clone();
                jump = actions.FindAction("Player/Jump", true).Clone();
                interact = actions.FindAction("Player/Interact", true).Clone();
            }
            if (move == null) return;
            if (value) { move.Enable(); jump.Enable(); interact.Enable(); }
            else { move.Disable(); jump.Disable(); interact.Disable(); }
        }

        private void OnDisable() => SetLocalControl(false);

        private void OnDestroy()
        {
            move?.Dispose();
            jump?.Dispose();
            interact?.Dispose();
            move = jump = interact = null;
        }
    }
}
