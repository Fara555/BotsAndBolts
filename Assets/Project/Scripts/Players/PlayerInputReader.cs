using UnityEngine;
using UnityEngine.InputSystem;

namespace BotsBolts.Players
{
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [SerializeField] private InputActionAsset actions;
        private InputActionAsset instance;
        private InputAction move;
        private InputAction jump;

        public Vector2 Movement => move != null && move.enabled && Application.isFocused
            ? Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f) : Vector2.zero;
        public bool JumpPressed => jump != null && jump.enabled && Application.isFocused && jump.WasPressedThisFrame();

        public void SetLocalControl(bool value)
        {
            if (value && instance == null)
            {
                instance = Instantiate(actions);
                move = instance.FindAction("Player/Move", true);
                jump = instance.FindAction("Player/Jump", true);
            }
            if (move == null) return;
            if (value) { move.Enable(); jump.Enable(); }
            else { move.Disable(); jump.Disable(); }
        }

        private void OnDisable() => SetLocalControl(false);

        private void OnDestroy()
        {
            if (instance == null) return;
            instance.Disable();
            Destroy(instance);
        }
    }
}
