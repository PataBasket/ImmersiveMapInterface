using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImmersiveMapInterface.Interaction;
using UnityEngine;

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
            PersistLocalCsv();
            // TODO: add Google Apps Script submission.
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

        private void PersistLocalCsv()
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

                    string ts = DateTime.UtcNow.ToString("o");
                    string subj = config != null ? config.subjectId : "";
                    string cond = config != null ? config.condition.ToString() : "";
                    string pat = config != null && config.pattern != null ? config.pattern.patternId : "";
                    string detection = detectionIntervals.Count > 0
                        ? string.Join("|", detectionIntervals.Select(v => v.ToString("F3")))
                        : "";
                    string moveTime = ShouldTrackMiniatureMetrics() ? moveActiveSeconds.ToString("F3") : "";
                    string miniTime = ShouldTrackMiniatureMetrics() ? miniatureManipSeconds.ToString("F3") : "";
                    string device = SystemInfo.deviceModel;

                    sw.WriteLine($"{ts},{subj},{cond},{pat},{timeLimitSeconds:F1},{elapsedSeconds:F3},{foundLines},{wrongAttempts},{cancelCount},{detection},{moveTime},{miniTime},{device}");
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
