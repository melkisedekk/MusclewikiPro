using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using MPImage = Mediapipe.Image;
using Mediapipe.Unity;
using Mediapipe.Tasks.Vision.PoseLandmarker;

namespace Mediapipe.Unity.Sample.PoseLandmarkDetection
{
    public class PoseLandmarkerRunner : VisionTaskApiRunner<PoseLandmarker>
    {
        [SerializeField] private PoseLandmarkerResultAnnotationController _poseLandmarkerResultAnnotationController;

        [Header("Görsel Ayarlar")]
        [SerializeField] private GameObject skeletonVisualizerObject;
        public UnityEngine.Color correctColor = UnityEngine.Color.green;
        public UnityEngine.Color wrongColor = UnityEngine.Color.red;

        [Header("Ses Ayarlarý")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip repSound;
        [SerializeField] private AudioClip finishSound;

        [Header("UI Ayarlarý")]
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI countdownText;

        [Header("Navigasyon")]
        [SerializeField] private Button backButton;
        [SerializeField] private Button homeButton;

        private string feedbackMessage = "Hazýrlan...";

        // Bu deðiþkeni artýk sadece analiz kontrolü için kullanacaðýz
        private bool isGameStarted = false;

        private long stateStableTimer = 0;
        private const long STABILITY_DELAY = 200;

        private bool isSquatting = false;
        private int squatCount = 0;
        private long lastSquatTime = 0;

        // --- SET VE HEDEF ---
        private int squatTargetCount = 12;
        private int currentSetNumber = 1;
        private int targetSetCount = 3;

        private float plankTimer = 0f;
        private bool isPlankCorrect = false;
        private float plankTargetTime = 30f;

        private Experimental.TextureFramePool _textureFramePool;
        public readonly PoseLandmarkDetectionConfig config = new PoseLandmarkDetectionConfig();

        [Header("Sonuç Ekraný")]
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private TMPro.TextMeshProUGUI finalScoreText;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button backToMenuButton;

        private bool isExerciseFinished = false;
        private bool currentPoseState = false;
        private bool triggerSquatSound = false;
        private bool triggerFinishExercise = false;

        private void Awake()
        {
            if (backButton != null) backButton.onClick.AddListener(ReturnToInfoPanel);
            if (homeButton != null) homeButton.onClick.AddListener(ReturnToHome);
            if (resultPanel != null) resultPanel.SetActive(false);
            if (restartButton != null) restartButton.onClick.AddListener(RestartGame);
            if (backToMenuButton != null) backToMenuButton.onClick.AddListener(ReturnToHome);

            if (audioSource == null) audioSource = GetComponent<AudioSource>();

            if (ExerciseManager.userTargetReps > 0)
            {
                squatTargetCount = ExerciseManager.userTargetReps;
                plankTargetTime = ExerciseManager.userTargetReps;
                targetSetCount = ExerciseManager.userTargetSets;
            }
        }

        // DÝKKAT: Start() fonksiyonunu tamamen sildik! 
        // Böylece Base Class'ýn (VisionTaskApiRunner) Start'ý çalýþacak ve kamerayý açacak.

        private IEnumerator CountdownRoutine()
        {
            isGameStarted = false;
            if (countdownText != null)
            {
                countdownText.gameObject.SetActive(true);
                countdownText.text = "3"; yield return new WaitForSeconds(1f);
                countdownText.text = "2"; yield return new WaitForSeconds(1f);
                countdownText.text = "1"; yield return new WaitForSeconds(1f);
                countdownText.text = "BAÞLA!"; yield return new WaitForSeconds(0.5f);
                countdownText.gameObject.SetActive(false);
            }
            isGameStarted = true;
        }

        private void Update()
        {
            // Oyun baþlamadýysa (Geri sayým bitmediyse) Update çalýþmasýn
            if (!isGameStarted) return;

            UpdateSkeletonColor(currentPoseState);

            if (triggerSquatSound) { PlaySound(repSound); triggerSquatSound = false; }
            if (triggerFinishExercise) { FinishExercise(); triggerFinishExercise = false; }

            // --- YAN PLANK VE NORMAL PLANK AYRIMI ---
            if (ExerciseManager.currentExercise == ExerciseManager.ExerciseType.Plank ||
                ExerciseManager.currentExercise == ExerciseManager.ExerciseType.SidePlank)
            {
                if (isPlankCorrect && plankTimer < plankTargetTime)
                {
                    plankTimer += Time.deltaTime;
                }

                if (plankTimer >= plankTargetTime)
                {
                    if (currentSetNumber < targetSetCount)
                    {
                        currentSetNumber++;
                        plankTimer = 0;
                        isPlankCorrect = false;
                        feedbackMessage = "DÝNLEN! SONRAKÝ SET...";
                        triggerSquatSound = true;
                    }
                    else
                    {
                        feedbackMessage = "BRAVO! BÝTTÝ!";
                        isPlankCorrect = false;
                        triggerFinishExercise = true;
                    }
                }
            }

            // --- UI ---
            if (scoreText != null)
            {
                if (ExerciseManager.currentExercise == ExerciseManager.ExerciseType.Squat)
                {
                    scoreText.text = $"SET: {currentSetNumber} / {targetSetCount}\nSQUAT: {squatCount} / {squatTargetCount}\n{feedbackMessage}";
                }
                else
                {
                    string exName = (ExerciseManager.currentExercise == ExerciseManager.ExerciseType.SidePlank) ? "YAN PLANK" : "PLANK";
                    string color = isPlankCorrect ? "green" : "red";
                    scoreText.text = $"SET: {currentSetNumber} / {targetSetCount}\n{exName}: {plankTimer:F1} / {plankTargetTime}\n<color={color}>{feedbackMessage}</color>";
                }
            }

            if (Input.GetKeyDown(KeyCode.F)) triggerFinishExercise = true;
            if (Input.GetKey(KeyCode.Space)) currentPoseState = true;
        }

        private void PlaySound(AudioClip clip) { if (audioSource != null && clip != null) audioSource.PlayOneShot(clip); }

        public void FinishExercise()
        {
            if (isExerciseFinished) return;
            isExerciseFinished = true;
            PlaySound(finishSound);
            if (resultPanel != null) { resultPanel.SetActive(true); if (finalScoreText != null) finalScoreText.text = "Tebrikler!\nEgzersiz Tamamlandý!"; }
        }

        public void RestartGame() { SceneManager.LoadScene(SceneManager.GetActiveScene().name); }
        public void ReturnToInfoPanel() { ExerciseManager.returningFromGame = true; SceneManager.LoadScene("MenuScene"); }
        public void ReturnToHome() { ExerciseManager.returningFromGame = false; SceneManager.LoadScene("MenuScene"); }
        public override void Stop() { base.Stop(); _textureFramePool?.Dispose(); _textureFramePool = null; }

        // --- ASIL SÝHÝR BURADA: RUN FONKSÝYONU ---
        protected override IEnumerator Run()
        {
            // 1. MediaPipe Hazýrlýklarý
            yield return AssetLoader.PrepareAssetAsync(config.ModelPath);
            var options = config.GetPoseLandmarkerOptions(config.RunningMode == Tasks.Vision.Core.RunningMode.LIVE_STREAM ? OnPoseLandmarkDetectionOutput : null);
            taskApi = PoseLandmarker.CreateFromOptions(options, GpuManager.GpuResources);

            // 2. Kamerayý Hazýrla ve AÇ
            var imageSource = ImageSourceProvider.ImageSource;
            if (imageSource is WebCamSource webCamSource)
            {
                WebCamDevice[] devices = WebCamTexture.devices;
                for (int i = 0; i < devices.Length; i++) { if (!devices[i].isFrontFacing) { webCamSource.SelectSource(i); break; } }
            }
            yield return imageSource.Play(); // <-- KAMERA BURADA AÇILIYOR

            if (!imageSource.isPrepared) yield break;

            // 3. Ekran ve Texture Ayarlarý
            _textureFramePool = new Experimental.TextureFramePool(imageSource.textureWidth, imageSource.textureHeight, TextureFormat.RGBA32, 10);
            screen.Initialize(imageSource);
            SetupAnnotationController(_poseLandmarkerResultAnnotationController, imageSource);
            _poseLandmarkerResultAnnotationController.InitScreen(imageSource.textureWidth, imageSource.textureHeight);

            // --- DÜZELTME: GERÝ SAYIMI BURAYA ALDIK ---
            // Kamera açýldý, kullanýcý kendini görüyor. Þimdi 3-2-1 sayabiliriz.
            if (countdownText != null) countdownText.text = "";
            yield return StartCoroutine(CountdownRoutine());
            // ------------------------------------------

            AsyncGPUReadbackRequest req = default;
            var waitUntilReqDone = new WaitUntil(() => req.done);
            var waitForEndOfFrame = new WaitForEndOfFrame();

            // 4. Analiz Döngüsü (Sonsuz Döngü)
            while (true)
            {
                if (isPaused) yield return new WaitWhile(() => isPaused);
                if (!_textureFramePool.TryGetTextureFrame(out var textureFrame)) { yield return new WaitForEndOfFrame(); continue; }

                // CPU Modunda Görüntüyü Al
                MPImage image = textureFrame.BuildCPUImage();
                textureFrame.Release();

                // Analiz Et
                taskApi.DetectAsync(image, GetCurrentTimestampMillisec(), new Tasks.Vision.Core.ImageProcessingOptions(rotationDegrees: 0));
                yield return waitForEndOfFrame;
            }
        }

        private void OnPoseLandmarkDetectionOutput(PoseLandmarkerResult result, MPImage image, long timestamp)
        {
            // Oyun baþlamadýysa (Geri sayým sürüyorsa) analiz sonuçlarýný iþleme
            if (!isGameStarted) return;

            if (result.poseLandmarks != null && result.poseLandmarks.Count > 0)
            {
                var landmarks = result.poseLandmarks[0].landmarks;
                if (landmarks.Count >= 33)
                {
                    if (ExerciseManager.currentExercise == ExerciseManager.ExerciseType.Squat) RunSquatLogic(landmarks, timestamp);
                    else if (ExerciseManager.currentExercise == ExerciseManager.ExerciseType.Plank) RunPlankLogic(landmarks, timestamp);
                    else if (ExerciseManager.currentExercise == ExerciseManager.ExerciseType.SidePlank) RunSidePlankLogic(landmarks, timestamp);
                }
            }
            _poseLandmarkerResultAnnotationController.DrawLater(result);
            DisposeAllMasks(result);
        }

        private void RunSidePlankLogic(List<Mediapipe.Tasks.Components.Containers.NormalizedLandmark> landmarks, long timestamp)
        {
            var lShoulder = landmarks[11]; var lElbow = landmarks[13]; var lHip = landmarks[23]; var lAnkle = landmarks[27];
            var rShoulder = landmarks[12]; var rElbow = landmarks[14]; var rHip = landmarks[24]; var rAnkle = landmarks[28];

            bool isLeftDown = lElbow.y > rElbow.y;

            var shoulder = isLeftDown ? lShoulder : rShoulder;
            var elbow = isLeftDown ? lElbow : rElbow;
            var hip = isLeftDown ? lHip : rHip;
            var ankle = isLeftDown ? lAnkle : rAnkle;

            float bodyAngle = CalculateAngle(shoulder, hip, ankle);
            bool isBodyStraight = bodyAngle > 160 && bodyAngle < 200;

            float xDiff = Math.Abs(shoulder.x - elbow.x);
            bool isArmVertical = xDiff < 0.15f;

            if (isBodyStraight && isArmVertical)
            {
                isPlankCorrect = true;
                feedbackMessage = "HARÝKA! KALÇANI TUT";
            }
            else
            {
                isPlankCorrect = false;
                if (!isArmVertical) feedbackMessage = "DÝRSEÐÝNÝ OMZUNUN ALTINA ÇEK";
                else if (bodyAngle < 160) feedbackMessage = "KALÇANI YUKARI ÝT!";
                else feedbackMessage = "VÜCUDUNU DÜZ TUT";
            }
            currentPoseState = isPlankCorrect;
        }

        // Logicler ve Yardýmcýlar
        private void RunSquatLogic(List<Mediapipe.Tasks.Components.Containers.NormalizedLandmark> landmarks, long timestamp)
        {
            var hip = landmarks[23]; var knee = landmarks[25]; var ankle = landmarks[27];
            float angle = CalculateAngle(hip, knee, ankle);
            currentPoseState = angle < 150;

            if (angle < 90) { if (!isSquatting) { if (stateStableTimer == 0) stateStableTimer = timestamp; if (timestamp - stateStableTimer > STABILITY_DELAY) { isSquatting = true; stateStableTimer = 0; feedbackMessage = "KALK!"; } } else stateStableTimer = 0; }
            else if (angle > 160)
            {
                if (isSquatting)
                {
                    if (stateStableTimer == 0) stateStableTimer = timestamp; if (timestamp - stateStableTimer > STABILITY_DELAY)
                    {
                        if (timestamp - lastSquatTime > 1000)
                        {
                            isSquatting = false; squatCount++; triggerSquatSound = true; lastSquatTime = timestamp; stateStableTimer = 0;
                            if (squatCount >= squatTargetCount) { if (currentSetNumber < targetSetCount) { currentSetNumber++; squatCount = 0; feedbackMessage = "DÝNLEN! SONRAKÝ SET..."; } else { feedbackMessage = "BRAVO! BÝTTÝ!"; triggerFinishExercise = true; } } else { feedbackMessage = "HARÝKA!"; }
                        }
                    }
                }
                else stateStableTimer = 0;
            }
            else stateStableTimer = 0;
        }

        private void RunPlankLogic(List<Mediapipe.Tasks.Components.Containers.NormalizedLandmark> landmarks, long timestamp)
        {
            var shoulder = landmarks[11]; var elbow = landmarks[13]; var hip = landmarks[23]; var ankle = landmarks[27];
            float bodyAngle = CalculateAngle(shoulder, hip, ankle);
            bool isBodyStraight = bodyAngle > 165 && bodyAngle < 195;
            double slopeRad = Math.Atan2(shoulder.y - ankle.y, shoulder.x - ankle.x);
            double slopeDeg = Math.Abs(slopeRad * 180.0 / Math.PI);
            bool isHorizontal = (slopeDeg < 35) || (slopeDeg > 145);
            double armRad = Math.Atan2(shoulder.y - elbow.y, shoulder.x - elbow.x);
            double armDeg = Math.Abs(armRad * 180.0 / Math.PI);
            bool isArmVertical = armDeg > 70 && armDeg < 110;

            if (isBodyStraight && isHorizontal && isArmVertical) { isPlankCorrect = true; feedbackMessage = "MÜKEMMEL! BOZMA"; }
            else { isPlankCorrect = false; if (!isHorizontal) feedbackMessage = "YERE YATAY OL!"; else if (!isArmVertical) feedbackMessage = "DÝRSEKLERÝNÝ DÜZELT!"; else if (bodyAngle < 165) feedbackMessage = "KALÇANI KALDIR!"; else feedbackMessage = "KALÇANI ÝNDÝR!"; }
            currentPoseState = isPlankCorrect;
        }

        private float CalculateAngle(Mediapipe.Tasks.Components.Containers.NormalizedLandmark p1, Mediapipe.Tasks.Components.Containers.NormalizedLandmark p2, Mediapipe.Tasks.Components.Containers.NormalizedLandmark p3) { Vector3 v1 = new Vector3(p1.x - p2.x, p1.y - p2.y, 0); Vector3 v2 = new Vector3(p3.x - p2.x, p3.y - p2.y, 0); return Vector3.Angle(v1, v2); }
        private void DisposeAllMasks(PoseLandmarkerResult result) { if (result.segmentationMasks != null) foreach (var mask in result.segmentationMasks) mask.Dispose(); }
        public void UpdateSkeletonColor(bool isCorrect) { if (skeletonVisualizerObject == null) return; UnityEngine.Color targetColor = isCorrect ? correctColor : wrongColor; var lines = skeletonVisualizerObject.GetComponentsInChildren<LineRenderer>(); foreach (var line in lines) { line.startColor = targetColor; line.endColor = targetColor; } }
    }
}