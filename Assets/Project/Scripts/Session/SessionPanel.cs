using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using VContainer;

namespace BotsBolts.Session
{
    public sealed class SessionPanel : MonoBehaviour
    {
        [SerializeField] private InputField hostAddress;
        [SerializeField] private Button hostButton;
        [SerializeField] private Button joinButton;
        [SerializeField] private Button leaveButton;
        [SerializeField] private Text status;
        [SerializeField] private EventSystem eventSystem;
        private WorkshopSession session;

        [Inject]
        public void Construct(WorkshopSession value)
        {
            session = value;
            session.Changed += Refresh;
            hostButton.onClick.AddListener(Host);
            joinButton.onClick.AddListener(Join);
            leaveButton.onClick.AddListener(Leave);
            Refresh();
        }

        private void Host() => session.Host();
        private void Join() => session.Join(hostAddress.text);
        private void Leave() => session.Leave();

        private void Refresh()
        {
            bool offline = session.Status == SessionStatus.Offline;
            hostButton.interactable = offline;
            joinButton.interactable = offline;
            hostAddress.interactable = offline;
            leaveButton.interactable = !offline && session.Status != SessionStatus.Disconnecting;
            status.text = session.Message;
            // The same gamepad stick/A button drive movement/leap during play, not menu selection/submit.
            if (eventSystem != null) eventSystem.sendNavigationEvents = offline;
        }

        private void OnDestroy()
        {
            if (session == null) return;
            session.Changed -= Refresh;
            hostButton.onClick.RemoveListener(Host);
            joinButton.onClick.RemoveListener(Join);
            leaveButton.onClick.RemoveListener(Leave);
            if (eventSystem != null) eventSystem.sendNavigationEvents = true;
        }
    }
}
