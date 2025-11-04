using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ImmersiveMapInterface.Experiment.Logging
{
    public class ExperimentLogger : MonoBehaviour
    {
        [Header("Config")]
        public ExperimentConfig config;

        private DateTime startTime;
        private readonly List<TimeSpan> perLineTimes = new();
        private int wrongAttempts = 0;
        private bool running = false;

        public void StartSession()
        {
            perLineTimes.Clear();
            wrongAttempts = 0;
            startTime = DateTime.UtcNow;
            running = true;
        }

        public void AbortSession()
        {
            running = false;
        }

        public void OnWrongAttempt()
        {
            if (running) wrongAttempts++;
        }

        public void OnCorrectLineFound()
        {
            if (!running) return;
            var now = DateTime.UtcNow;
            perLineTimes.Add(now - startTime);
        }

        public void FinishSession()
        {
            if (!running) return;
            var total = DateTime.UtcNow - startTime;
            running = false;
            PersistLocalCsv(total);
            // TODO: add GAS POST when endpoint is available
        }

        private void PersistLocalCsv(TimeSpan total)
        {
            if (config != null && !config.saveCsvFallback) return;
            try
            {
                string dir = Application.persistentDataPath;
                string path = Path.Combine(dir, "experiment_log.csv");
                bool writeHeader = !File.Exists(path);
                using (var sw = new StreamWriter(path, append: true))
                {
                    if (writeHeader)
                    {
                        sw.WriteLine("timestamp,subjectId,condition,patternId,totalSeconds,line1Seconds,line2Seconds,line3Seconds,wrongAttempts,device");
                    }
                    string device = SystemInfo.deviceModel;
                    string ts = DateTime.UtcNow.ToString("o");
                    string subj = config != null ? config.subjectId : "";
                    string cond = config != null ? config.condition.ToString() : "";
                    string pat = config != null && config.pattern != null ? config.pattern.patternId : "";
                    string l1 = perLineTimes.Count > 0 ? perLineTimes[0].TotalSeconds.ToString("F3") : "";
                    string l2 = perLineTimes.Count > 1 ? perLineTimes[1].TotalSeconds.ToString("F3") : "";
                    string l3 = perLineTimes.Count > 2 ? perLineTimes[2].TotalSeconds.ToString("F3") : "";
                    sw.WriteLine($"{ts},{subj},{cond},{pat},{total.TotalSeconds:F3},{l1},{l2},{l3},{wrongAttempts},{device}");
                }
                Debug.Log($"ExperimentLogger: wrote CSV to {path}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"ExperimentLogger: failed to write CSV: {ex.Message}");
            }
        }
    }
}

