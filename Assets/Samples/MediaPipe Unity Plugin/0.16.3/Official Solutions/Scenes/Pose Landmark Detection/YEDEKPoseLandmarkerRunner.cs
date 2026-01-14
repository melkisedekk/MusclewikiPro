// Copyright (c) 2023 homuler
//
// Use of this source code is governed by an MIT-style
// license that can be found in the LICENSE file or at
// https://opensource.org/licenses/MIT.

using System.Collections;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using UnityEngine;
using UnityEngine.Rendering;

using TMPro;

namespace Mediapipe.Unity.Sample.PoseLandmarkDetection
{
    public class PoseLandmarkerRunner : VisionTaskApiRunner<PoseLandmarker>
    {
        [SerializeField] private PoseLandmarkerResultAnnotationController _poseLandmarkerResultAnnotationController;

        // --- UI VE SAYAÇ DEÐÝÞKENLERÝ ---
        [SerializeField] private TextMeshProUGUI scoreText;
        private bool isSquatting = false;
        private int squatCount = 0;
        private string feedbackMessage = "Hazir";

        // DEÐÝÞÝKLÝK 1: Zamaný float (saniye) deðil, long (milisaniye) tutuyoruz.
        private long lastSquatTime = 0;
        // --------------------------------
        // YENÝ EKLENEN: Durumun sabit kalma süresini takip etmek için
        private long stateStableTimer = 0;
        // Kaç milisaniye beklemesi gerektiði (300ms idealdir, çok yavaþ gelirse 200 yap)
        private const long STABILITY_DELAY = 300;



        private Experimental.TextureFramePool _textureFramePool;

        public readonly PoseLandmarkDetectionConfig config = new PoseLandmarkDetectionConfig();

        private void Update()
        {
            if (scoreText != null)
            {
                scoreText.text = $"Squat: {squatCount}\n{feedbackMessage}";
            }
        }

        public override void Stop()
        {
            base.Stop();
            _textureFramePool?.Dispose();
            _textureFramePool = null;
        }

        protected override IEnumerator Run()
        {
            Debug.Log($"Delegate = {config.Delegate}");
            Debug.Log($"Running Mode = {config.RunningMode}");

            yield return AssetLoader.PrepareAssetAsync(config.ModelPath);

            var options = config.GetPoseLandmarkerOptions(config.RunningMode == Tasks.Vision.Core.RunningMode.LIVE_STREAM ? OnPoseLandmarkDetectionOutput : null);
            taskApi = PoseLandmarker.CreateFromOptions(options, GpuManager.GpuResources);
            var imageSource = ImageSourceProvider.ImageSource;

            yield return imageSource.Play();

            if (!imageSource.isPrepared)
            {
                Logger.LogError(TAG, "Failed to start ImageSource, exiting...");
                yield break;
            }

            _textureFramePool = new Experimental.TextureFramePool(imageSource.textureWidth, imageSource.textureHeight, TextureFormat.RGBA32, 10);
            screen.Initialize(imageSource);

            SetupAnnotationController(_poseLandmarkerResultAnnotationController, imageSource);
            _poseLandmarkerResultAnnotationController.InitScreen(imageSource.textureWidth, imageSource.textureHeight);

            var transformationOptions = imageSource.GetTransformationOptions();
            var flipHorizontally = transformationOptions.flipHorizontally;
            var flipVertically = transformationOptions.flipVertically;
            var imageProcessingOptions = new Tasks.Vision.Core.ImageProcessingOptions(rotationDegrees: 0);

            AsyncGPUReadbackRequest req = default;
            var waitUntilReqDone = new WaitUntil(() => req.done);
            var waitForEndOfFrame = new WaitForEndOfFrame();
            var result = PoseLandmarkerResult.Alloc(options.numPoses, options.outputSegmentationMasks);

            var canUseGpuImage = SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3 && GpuManager.GpuResources != null;
            using var glContext = canUseGpuImage ? GpuManager.GetGlContext() : null;

            while (true)
            {
                if (isPaused)
                {
                    yield return new WaitWhile(() => isPaused);
                }

                if (!_textureFramePool.TryGetTextureFrame(out var textureFrame))
                {
                    yield return new WaitForEndOfFrame();
                    continue;
                }

                Image image;
                switch (config.ImageReadMode)
                {
                    case ImageReadMode.GPU:
                        if (!canUseGpuImage) throw new System.Exception("ImageReadMode.GPU is not supported");
                        textureFrame.ReadTextureOnGPU(imageSource.GetCurrentTexture(), flipHorizontally, flipVertically);
                        image = textureFrame.BuildGPUImage(glContext);
                        yield return waitForEndOfFrame;
                        break;
                    case ImageReadMode.CPU:
                        yield return waitForEndOfFrame;
                        textureFrame.ReadTextureOnCPU(imageSource.GetCurrentTexture(), flipHorizontally, flipVertically);
                        image = textureFrame.BuildCPUImage();
                        textureFrame.Release();
                        break;
                    case ImageReadMode.CPUAsync:
                    default:
                        req = textureFrame.ReadTextureAsync(imageSource.GetCurrentTexture(), flipHorizontally, flipVertically);
                        yield return waitUntilReqDone;
                        if (req.hasError) { continue; }
                        image = textureFrame.BuildCPUImage();
                        textureFrame.Release();
                        break;
                }

                switch (taskApi.runningMode)
                {
                    case Tasks.Vision.Core.RunningMode.IMAGE:
                        if (taskApi.TryDetect(image, imageProcessingOptions, ref result)) _poseLandmarkerResultAnnotationController.DrawNow(result);
                        else _poseLandmarkerResultAnnotationController.DrawNow(default);
                        DisposeAllMasks(result);
                        break;
                    case Tasks.Vision.Core.RunningMode.VIDEO:
                        if (taskApi.TryDetectForVideo(image, GetCurrentTimestampMillisec(), imageProcessingOptions, ref result)) _poseLandmarkerResultAnnotationController.DrawNow(result);
                        else _poseLandmarkerResultAnnotationController.DrawNow(default);
                        DisposeAllMasks(result);
                        break;
                    case Tasks.Vision.Core.RunningMode.LIVE_STREAM:
                        taskApi.DetectAsync(image, GetCurrentTimestampMillisec(), imageProcessingOptions);
                        break;
                }
            }
        }

        private void OnPoseLandmarkDetectionOutput(PoseLandmarkerResult result, Image image, long timestamp)
        {
            if (result.poseLandmarks != null && result.poseLandmarks.Count > 0)
            {
                var landmarks = result.poseLandmarks[0].landmarks;

                if (landmarks.Count >= 33)
                {
                    var hip = landmarks[23];
                    var knee = landmarks[25];
                    var ankle = landmarks[27];

                    Vector3 p1 = new Vector3(hip.x, hip.y, 0);
                    Vector3 p2 = new Vector3(knee.x, knee.y, 0);
                    Vector3 p3 = new Vector3(ankle.x, ankle.y, 0);

                    Vector3 v1 = p1 - p2;
                    Vector3 v2 = p3 - p2;

                    float angle = Vector3.Angle(v1, v2);

                    // --- GÜNCELLENMÝÞ SQUAT MANTIÐI (GECÝKMELÝ) ---

                    // 1. Durum: Aþaðý Ýniþ Kontrolü
                    if (angle < 90)
                    {
                        // Henüz "eðildi" modunda deðilsek
                        if (!isSquatting)
                        {
                            // Eðer zamanlayýcý 0 ise, þu anki zamaný baþlangýç olarak kaydet
                            if (stateStableTimer == 0) stateStableTimer = timestamp;

                            // Eðer belirlediðimiz süre (300ms) boyunca hala 90 derecenin altýndaysak
                            if (timestamp - stateStableTimer > STABILITY_DELAY)
                            {
                                isSquatting = true;
                                stateStableTimer = 0; // Zamanlayýcýyý sýfýrla
                                feedbackMessage = "KALK!";
                                Debug.Log("SQUAT: Aþaðý indi (Onaylandý)");
                            }
                        }
                        else
                        {
                            // Zaten eðiksek zamanlayýcýyý sýfýrlý tut (ki kalkarken kullanabilesin)
                            stateStableTimer = 0;
                        }
                    }
                    // 2. Durum: Yukarý Çýkýþ Kontrolü
                    else if (angle > 160)
                    {
                        // Eðer "eðildi" modundaysak (yani kalkmaya çalýþýyorsak)
                        if (isSquatting)
                        {
                            // Zamanlayýcýyý baþlat
                            if (stateStableTimer == 0) stateStableTimer = timestamp;

                            // Eðer belirlediðimiz süre (300ms) boyunca hala 160 derecenin üstündeysek
                            // (Bu sayede anlýk iskelet bozulmalarýný yoksaymýþ oluyoruz)
                            if (timestamp - stateStableTimer > STABILITY_DELAY)
                            {
                                // Son squattan beri yeterince süre geçti mi? (Hýzlý spam'i engelle)
                                if (timestamp - lastSquatTime > 1000)
                                {
                                    isSquatting = false;
                                    squatCount++;
                                    lastSquatTime = timestamp;
                                    stateStableTimer = 0; // Zamanlayýcýyý sýfýrla

                                    feedbackMessage = "AFERIN!";
                                    Debug.Log($"SQUAT: Tamamlandý! Toplam: {squatCount}");
                                }
                            }
                        }
                        else
                        {
                            // Zaten ayaktaysak zamanlayýcýyý sýfýrlý tut
                            stateStableTimer = 0;
                        }
                    }
                    else
                    {
                        // Eðer açý 90 ile 160 arasýndaysa (ara geçiþ),
                        // zamanlayýcýyý sýfýrla. Çünkü hareket kararsýz.
                        stateStableTimer = 0;
                    }
                }
            }

            _poseLandmarkerResultAnnotationController.DrawLater(result);
            DisposeAllMasks(result);
        }

        private void DisposeAllMasks(PoseLandmarkerResult result)
        {
            if (result.segmentationMasks != null)
            {
                foreach (var mask in result.segmentationMasks)
                {
                    mask.Dispose();
                }
            }
        }
    }
}