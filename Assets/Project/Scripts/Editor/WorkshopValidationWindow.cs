using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace BotsBolts.Editor
{
    public sealed class WorkshopValidationWindow : OdinEditorWindow
    {
        private readonly List<Process> processes = new List<Process>(2);
        private string[] reports;
        [ShowInInspector, ReadOnly, MultiLineProperty(8)]
        private string status = "Build first via Bots & Bolts/Build/Windows Test Build. Checks run hidden development players on localhost. Close this window to stop your test players.";

        [MenuItem("Bots & Bolts/Validation/Network Checks")]
        private static void Open() => GetWindow<WorkshopValidationWindow>("Workshop Network Checks").Show();

        [Button("Check solo host and 3 restarts"), DisableIf("IsRunning")]
        private void Solo() => Launch(new[] { "solo" });

        [Button("Check host + client, movement and reconnect"), DisableIf("IsRunning")]
        private void Pair() => Launch(new[] { "host", "client" });

        [Button("Check acceleration, forward leap and local camera"), DisableIf("IsRunning")]
        private void Movement() => Launch(new[] { "movement" });

        private bool IsRunning => processes.Count > 0;

        private void Launch(string[] modes)
        {
            string executable = Path.GetFullPath("Builds/Workshop/BotsBolts.exe");
            if (!File.Exists(executable))
            {
                status = "Build not found. Use Bots & Bolts/Build/Windows Test Build first.";
                return;
            }
            Directory.CreateDirectory("Logs");
            reports = new string[modes.Length];
            try
            {
                for (int i = 0; i < modes.Length; i++)
                {
                    string result = Path.GetFullPath("Logs/NetworkCheck-" + modes[i] + ".json");
                    if (File.Exists(result)) File.Delete(result);
                    reports[i] = result;
                    string log = Path.GetFullPath("Logs/NetworkCheck-" + modes[i] + ".log");
                    processes.Add(Process.Start(new ProcessStartInfo(executable,
                        $"-batchmode -nographics --bb-smoke {modes[i]} --bb-result \"{result}\" -logFile \"{log}\"")
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    }));
                }
                status = "Running real transport checks… Results and full player logs are saved under Logs/.";
            }
            catch (Exception exception)
            {
                status = exception.ToString();
                StopProcesses();
            }
        }

        private void Update()
        {
            if (!IsRunning) return;
            for (int i = 0; i < processes.Count; i++) if (!processes[i].HasExited) return;
            status = "";
            for (int i = 0; i < reports.Length; i++)
                status += (File.Exists(reports[i]) ? File.ReadAllText(reports[i]) :
                    "No result: inspect the player log. Exit code " + processes[i].ExitCode) + "\n";
            StopProcesses();
            Repaint();
        }

        private void StopProcesses()
        {
            for (int i = 0; i < processes.Count; i++)
            {
                if (!processes[i].HasExited) processes[i].Kill();
                processes[i].Dispose();
            }
            processes.Clear();
        }

        protected override void OnDestroy()
        {
            StopProcesses();
            base.OnDestroy();
        }
    }
}
