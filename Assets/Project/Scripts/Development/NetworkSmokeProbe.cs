#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Threading;
using BotsBolts.Players;
using BotsBolts.Session;
using BotsBolts.Presentation;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using VContainer;

namespace BotsBolts.Development
{
    // Opt-in checks use the real transport, admission, motor and disconnect paths.
    // No automated input runs unless --bb-smoke is explicitly supplied.
    public sealed class NetworkSmokeProbe : MonoBehaviour
    {
        private WorkshopSession session;
        private WorkshopSettings settings;

        [Inject]
        public void Construct(WorkshopSession value, WorkshopSettings configuration)
        {
            session = value;
            settings = configuration;
        }

        private void Start()
        {
            string mode = Argument("--bb-smoke");
            if (mode == null) return;
            Run(mode, this.GetCancellationTokenOnDestroy()).Forget(Debug.LogException);
        }

        private async UniTask Run(string mode, CancellationToken lifetime)
        {
            string output = Argument("--bb-result");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
            timeout.CancelAfter(TimeSpan.FromSeconds(50));
            CancellationToken token = timeout.Token;
            try
            {
                if (mode == "preview")
                {
                    session.Host();
                    await WaitForPlayers(1, token);
                    await UniTask.Delay(1500, cancellationToken: token);
                    string screenshot = Argument("--bb-screenshot");
                    if (screenshot == null) throw new ArgumentException("Preview requires --bb-screenshot.");
                    CapturePreview(screenshot);
                }
                else if (mode == "movement")
                {
                    session.Host();
                    await WaitForPlayers(1, token);
                    NetworkPlayer player = FindObjectsByType<NetworkPlayer>()[0];
                    PlayerMotor motor = player.GetComponent<PlayerMotor>();
                    WorkshopFollowCamera follow = player.NetworkManager.GetComponent<WorkshopCameraCoordinator>().FollowCamera;
                    await UniTask.WaitUntil(() => motor.IsGrounded, cancellationToken: token);
                    await UniTask.Delay(1000, cancellationToken: token);
                    if (follow.Target != player.transform || Camera.allCamerasCount != 1)
                        throw new InvalidOperationException("Local camera ownership is incorrect.");
                    motor.SetValidationInput(Vector2.right);
                    await UniTask.Delay(60, cancellationToken: token);
                    if (motor.PlanarVelocity.magnitude <= 0 || motor.PlanarVelocity.magnitude >= settings.MoveSpeed)
                        throw new InvalidOperationException("Movement did not accelerate gradually.");
                    await UniTask.Delay(250, cancellationToken: token);
                    motor.SetValidationInput(Vector2.zero);
                    await UniTask.Delay(200, cancellationToken: token);
                    if (motor.PlanarVelocity.sqrMagnitude > 0.01f)
                        throw new InvalidOperationException("Braking did not settle.");
                    Vector3 leapStart = player.transform.position;
                    motor.SetValidationInput(Vector2.right);
                    motor.QueueValidationJump();
                    await UniTask.Delay(150, cancellationToken: token);
                    if (!motor.IsForwardJump || player.transform.position.y < leapStart.y + 0.3f)
                        throw new InvalidOperationException("Forward leap failed to leave the ground.");
                    motor.QueueValidationJump(); // A second press during the arc must not add another upward impulse.
                    float peak = player.transform.position.y;
                    for (int i = 0; i < 45; i++)
                    {
                        peak = Mathf.Max(peak, player.transform.position.y);
                        await UniTask.Delay(10, cancellationToken: token);
                    }
                    if (peak > leapStart.y + settings.LeapHeight + 0.15f)
                        throw new InvalidOperationException("Midair input produced an extra jump impulse.");
                    await UniTask.WaitUntil(() => motor.IsGrounded, cancellationToken: token);
                    if (player.transform.position.x - leapStart.x < 1.5f)
                        throw new InvalidOperationException("Leap did not propel the player forward.");
                    motor.SetValidationInput(Vector2.down);
                    await UniTask.Delay(1800, cancellationToken: token);
                    motor.QueueValidationJump();
                    await UniTask.Delay(800, cancellationToken: token);
                    if (player.transform.position.z < -4.6f || player.transform.position.y < -0.1f)
                        throw new InvalidOperationException("Leap escaped the front floor boundary.");
                    motor.SetValidationInput(Vector2.zero);
                    await UniTask.Delay(1000, cancellationToken: token);
                    float expectedX = player.transform.position.x + settings.CameraOffset.x;
                    if (Mathf.Abs(follow.transform.position.x - expectedX) > 0.1f)
                        throw new InvalidOperationException("Follow camera did not settle behind its player.");
                    session.Leave();
                    await WaitForPlayers(0, token);
                    if (follow.Target != null) throw new InvalidOperationException("Camera retained a despawned target.");
                }
                else if (mode == "solo")
                {
                    for (int i = 0; i < 3; i++)
                    {
                        session.Host();
                        await WaitForPlayers(1, token);
                        session.Leave();
                        await WaitForPlayers(0, token);
                        await UniTask.WaitUntil(() => session.Status == SessionStatus.Offline, cancellationToken: token);
                        await UniTask.Delay(500, cancellationToken: token);
                    }
                }
                else
                {
                    bool host = mode == "host";
                    if (host) session.Host(); else session.Join("127.0.0.1");
                    await WaitForPlayers(2, token);
                    NetworkPlayer[] players = FindObjectsByType<NetworkPlayer>();
                    NetworkPlayer own = players[0].IsOwner ? players[0] : players[1];
                    NetworkPlayer remote = players[0].IsOwner ? players[1] : players[0];
                    if (!own.IsOwner || remote.IsOwner) throw new InvalidOperationException("Invalid input ownership.");
                    WorkshopFollowCamera localView = own.NetworkManager.GetComponent<WorkshopCameraCoordinator>().FollowCamera;
                    if (localView.Target != own.transform) throw new InvalidOperationException("Camera follows another peer's player.");
                    Vector3 ownStart = own.transform.position;
                    Vector3 remoteStart = remote.transform.position;
                    PlayerMotor motor = own.GetComponent<PlayerMotor>();
                    motor.SetValidationInput(new Vector2(host ? 1 : -1, 0));
                    await UniTask.Delay(1200, cancellationToken: token);
                    motor.SetValidationInput(Vector2.zero);
                    await UniTask.Delay(1000, cancellationToken: token);
                    if (Vector3.Distance(ownStart, own.transform.position) < 2f ||
                        Vector3.Distance(remoteStart, remote.transform.position) < 2f)
                        throw new InvalidOperationException("Local or replicated movement was not observed.");
                    Debug.Log("[BotsBolts] Smoke: both owned and observed players moved.");
                    motor.SetValidationInput(Vector2.up);
                    motor.QueueValidationJump();
                    float localPeak = own.transform.position.y;
                    float remotePeak = remote.transform.position.y;
                    for (int i = 0; i < 80; i++)
                    {
                        localPeak = Mathf.Max(localPeak, own.transform.position.y);
                        remotePeak = Mathf.Max(remotePeak, remote.transform.position.y);
                        await UniTask.Delay(10, cancellationToken: token);
                    }
                    motor.SetValidationInput(Vector2.zero);
                    if (localPeak < 0.35f || remotePeak < 0.25f || localView.Target != own.transform)
                        throw new InvalidOperationException("Forward leap replication or local camera isolation failed.");
                    Debug.Log("[BotsBolts] Smoke: both forward leaps replicated; camera stayed on the local owner.");
                    if (host)
                    {
                        await WaitForPlayers(1, token);
                        await WaitForPlayers(2, token);
                        await UniTask.Delay(2500, cancellationToken: token);
                        session.Leave();
                        await WaitForPlayers(0, token);
                    }
                    else
                    {
                        // Let the host finish its movement observations before leaving.
                        await UniTask.Delay(1500, cancellationToken: token);
                        session.Leave();
                        await WaitForPlayers(0, token);
                        await UniTask.WaitUntil(() => session.Status == SessionStatus.Offline, cancellationToken: token);
                        await UniTask.Delay(1500, cancellationToken: token);
                        session.Join("127.0.0.1");
                        await WaitForPlayers(2, token);
                        await UniTask.WaitUntil(() => session.Status == SessionStatus.Offline, cancellationToken: token);
                        await WaitForPlayers(0, token);
                        if (string.IsNullOrWhiteSpace(session.Message)) throw new InvalidOperationException("Host-loss message missing.");
                    }
                }
                WriteResult(output, true, mode, "Real transport checks completed.");
                Application.Quit(0);
            }
            catch (Exception exception)
            {
                WriteResult(output, false, mode, exception.ToString());
                Debug.LogException(exception);
                session.Leave();
                Application.Quit(1);
            }
        }

        private static async UniTask WaitForPlayers(int count, CancellationToken token)
        {
            await UniTask.WaitUntil(() => HasPlayers(count), cancellationToken: token);
            await UniTask.Yield(token);
        }

        private static bool HasPlayers(int count)
        {
            NetworkPlayer[] players = FindObjectsByType<NetworkPlayer>();
            if (players.Length != count) return false;
            if (count == 0) return true;
            int owners = 0;
            for (int i = 0; i < players.Length; i++)
            {
                if (!players[i].IsClientInitialized) return false;
                if (players[i].IsOwner) owners++;
            }
            return owners == 1;
        }

        private static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == name) return args[i + 1];
            return null;
        }

        private static void CapturePreview(string path)
        {
            // Render requests work even when a hidden player window has no presenting backbuffer.
            Camera camera = Camera.main;
            Canvas[] canvases = FindObjectsByType<Canvas>();
            var texture = new RenderTexture(1280, 720, 24);
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            float previousAspect = camera.aspect;
            try
            {
                camera.aspect = 1280f / 720;
                for (int i = 0; i < canvases.Length; i++)
                {
                    canvases[i].renderMode = RenderMode.ScreenSpaceCamera;
                    canvases[i].worldCamera = camera;
                    canvases[i].planeDistance = 1;
                }
                Canvas.ForceUpdateCanvases();
                texture.Create();
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = texture });
                RenderTexture.active = texture;
                pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                for (int i = 0; i < canvases.Length; i++)
                {
                    canvases[i].renderMode = RenderMode.ScreenSpaceOverlay;
                    canvases[i].worldCamera = null;
                }
                camera.aspect = previousAspect;
                RenderTexture.active = previous;
                texture.Release();
                Destroy(texture);
                Destroy(pixels);
            }
        }

        private static void WriteResult(string output, bool passed, string mode, string detail)
        {
            var result = new Result { passed = passed, mode = mode, detail = detail };
            if (output != null) File.WriteAllText(output, JsonUtility.ToJson(result, true));
            Debug.Log("[BotsBolts] Smoke result: " + JsonUtility.ToJson(result));
        }

        [Serializable]
        private sealed class Result
        {
            public bool passed;
            public string mode;
            public string detail;
        }
    }
}
#else
namespace BotsBolts.Development
{
    // Scene references remain valid; automation is absent in release builds.
    public sealed class NetworkSmokeProbe : UnityEngine.MonoBehaviour { }
}
#endif
