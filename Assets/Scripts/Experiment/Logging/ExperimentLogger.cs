using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ImmersiveMapInterface.Interaction;
using UnityEngine;
using UnityEngine.Networking;

namespace ImmersiveMapInterface.Experiment.Logging
{
    /// <summary>
    /// Aggregates per-trial metrics (found lines, detection intervals, cancels, movement time, etc.)
    /// and persists them to CSV (and later GAS).
    /// </summary>
    public class ExperimentLogger : MonoBehaviour
    {
        [Header("Config & References")]
        public ExperimentConfig config;
        public BirdHeadLocomotion locomotion;
        [Tooltip("Miniature BoardGrabRotate (used to measure manipulation time in Internal+Miniature).")]
        public BoardGrabRotate miniatureManipulator;

        [Header("Fallbacks")]
        [Tooltip("Used when ExperimentConfig is missing or has invalid values.")]
        [Min(1f)] public float defaultTimeLimitSeconds = 180f;

        private DateTime startTimeUtc;
        private float elapsedSeconds;
        private float timeLimitSeconds;
        private bool running;

        private readonly List<float> detectionIntervals = new();
        private float lastDetectionElapsed;
        private int foundLines;
        private int wrongAttempts;
        private int cancelCount;

        private float moveActiveSeconds;
        private float miniatureManipSeconds;

        [System.Serializable]
        private class TrialRecord
        {
            public string timestamp;
            public string subjectId;
            public string condition;
            public string patternId;
            public float timeLimitSec;
            public float totalSeconds;
            public int foundLines;
            public int wrongSelections;
            public int cancelCount;
            public string detectionIntervals;
            public string moveTime;
            public string miniManipTime;
            public string device;
        }

        private void Reset()
        {
            if (locomotion == null)
            {
                locomotion = FindObjectOfType<BirdHeadLocomotion>(true);
            }
            if (miniatureManipulator == null)
            {
                foreach (var rotate in FindObjectsOfType<BoardGrabRotate>(true))
                {
                    if (rotate != null && rotate.gameObject.name.ToLower().Contains("mini"))
                    {
                        miniatureManipulator = rotate;
                        break;
                    }
                }
            }
        }

        private void Update()
        {
            if (!running) return;

            float dt = Time.deltaTime;
            elapsedSeconds += dt;

            if (ShouldTrackMiniatureMetrics())
            {
                if (locomotion != null && locomotion.IsMoving)
                {
                    moveActiveSeconds += dt;
                }

                if (miniatureManipulator != null && miniatureManipulator.IsGrabbing)
                {
                    miniatureManipSeconds += dt;
                }
            }

            if (elapsedSeconds >= timeLimitSeconds)
            {
                FinishSession();
            }
        }

        public void StartSession()
        {
            detectionIntervals.Clear();
            foundLines = 0;
            wrongAttempts = 0;
            cancelCount = 0;
            moveActiveSeconds = 0f;
            miniatureManipSeconds = 0f;
            elapsedSeconds = 0f;
            lastDetectionElapsed = 0f;
            timeLimitSeconds = ResolveTimeLimit();
            startTimeUtc = DateTime.UtcNow;
            running = true;
        }

        public void AbortSession()
        {
            running = false;
        }

        public void OnWrongAttempt()
        {
            if (!running) return;
            wrongAttempts++;
        }

        public void OnCorrectLineFound()
        {
            if (!running) return;
            foundLines++;
            float interval = Mathf.Max(0f, elapsedSeconds - lastDetectionElapsed);
            detectionIntervals.Add(interval);
            lastDetectionElapsed = elapsedSeconds;
        }

        public void OnSelectionCanceled()
        {
            if (!running) return;
            cancelCount++;
        }

        public void FinishSession()
        {
            if (!running) return;
            running = false;
            var record = BuildRecord();
            PersistLocalCsv(record);
            TrySendToGas(record);
        }

        private float ResolveTimeLimit()
        {
            if (config != null && config.timeLimitSeconds > 0f)
            {
                return config.timeLimitSeconds;
            }
            return defaultTimeLimitSeconds;
        }

        private bool ShouldTrackMiniatureMetrics()
        {
            return config != null && config.condition == ExperimentCondition.InternalWithMiniature;
        }

        private TrialRecord BuildRecord()
        {
            bool trackMini = ShouldTrackMiniatureMetrics();
            return new TrialRecord
            {
                timestamp = DateTime.UtcNow.ToString("o"),
                subjectId = config != null ? config.subjectId : "",
                condition = config != null ? config.condition.ToString() : "",
                patternId = config != null && config.pattern != null ? config.pattern.patternId : "",
                timeLimitSec = timeLimitSeconds,
                totalSeconds = elapsedSeconds,
                foundLines = foundLines,
                wrongSelections = wrongAttempts,
                cancelCount = cancelCount,
                detectionIntervals = detectionIntervals.Count > 0
                    ? string.Join("|", detectionIntervals.Select(v => v.ToString("F3")))
                    : "",
                moveTime = trackMini ? moveActiveSeconds.ToString("F3") : "",
                miniManipTime = trackMini ? miniatureManipSeconds.ToString("F3") : "",
                device = SystemInfo.deviceModel
            };
        }

        private void PersistLocalCsv(TrialRecord record)
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
                        sw.WriteLine("timestamp,subjectId,condition,patternId,timeLimitSec,totalSeconds,foundLines,wrongSelections,cancelCount,detectionIntervals,moveTime,miniManipTime,device");
                    }

                    sw.WriteLine($"{record.timestamp},{record.subjectId},{record.condition},{record.patternId},{record.timeLimitSec:F1},{record.totalSeconds:F3},{record.foundLines},{record.wrongSelections},{record.cancelCount},{record.detectionIntervals},{record.moveTime},{record.miniManipTime},{record.device}");
                }
                Debug.Log($"ExperimentLogger: wrote CSV to {path}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"ExperimentLogger: failed to write CSV: {ex.Message}");
            }
        }

        private void TrySendToGas(TrialRecord record)
        {
            if (config == null || string.IsNullOrWhiteSpace(config.gasUrl)) return;
            StartCoroutine(SendToGasRoutine(record, config.gasUrl));
        }

        private System.Collections.IEnumerator SendToGasRoutine(TrialRecord record, string url)
        {
            string json = JsonUtility.ToJson(record);
            using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"ExperimentLogger: GAS POST failed ({request.result}) {request.error}");
            }
            else
            {
                Debug.Log("ExperimentLogger: GAS POST succeeded.");
            }
        }
    }
}
