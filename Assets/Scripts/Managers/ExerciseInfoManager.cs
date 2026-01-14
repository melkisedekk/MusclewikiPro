using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;
using UnityEngine.SceneManagement;

public class ExerciseInfoManager : MonoBehaviour
{
    [System.Serializable]
    public class ExerciseData
    {
        public string exerciseID;
        public string title;
        public VideoClip demoClip1;
        public VideoClip demoClip2;
        [TextArea(5, 15)] public string instructions;
        public string sceneToLoadName; // BURASI ÇOK ÖNEMLÝ
    }

    [Header("Egzersiz Verileri")]
    public List<ExerciseData> allExercises = new List<ExerciseData>();

    [Header("UI Baðlantýlarý")]
    public GameObject infoPanel;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI instructionsText;
    public Button startButton;
    public Button backButton;
    public Button homeButton;

    public VideoPlayer videoPlayer1;
    public VideoPlayer videoPlayer2;

    [Header("Ayar Kontrolleri")]
    public TextMeshProUGUI setDisplay;
    public Button setMinusBtn;
    public Button setPlusBtn;
    public TextMeshProUGUI repDisplay;
    public Button repMinusBtn;
    public Button repPlusBtn;

    private int currentSets = 3;
    private int currentReps = 12;

    private string currentExerciseID;
    private string currentSceneToLoad;

    void Start()
    {
        if (infoPanel != null) infoPanel.SetActive(false);

        if (startButton) startButton.onClick.AddListener(LoadSelectedScene);
        if (backButton) backButton.onClick.AddListener(CloseInfoPanel);
        if (homeButton) homeButton.onClick.AddListener(GoToMainMenu);

        if (setMinusBtn) setMinusBtn.onClick.AddListener(() => ChangeSet(-1));
        if (setPlusBtn) setPlusBtn.onClick.AddListener(() => ChangeSet(1));
        if (repMinusBtn) repMinusBtn.onClick.AddListener(() => ChangeRep(-1));
        if (repPlusBtn) repPlusBtn.onClick.AddListener(() => ChangeRep(1));
    }

    public void OpenInfoPanel(string exerciseID)
    {
        ExerciseData data = allExercises.Find(x => x.exerciseID == exerciseID);

        if (data != null)
        {
            titleText.text = data.title;
            instructionsText.text = data.instructions;

            if (data.demoClip1 != null) { videoPlayer1.gameObject.SetActive(true); videoPlayer1.clip = data.demoClip1; videoPlayer1.Play(); }
            else videoPlayer1.gameObject.SetActive(false);

            if (data.demoClip2 != null) { videoPlayer2.gameObject.SetActive(true); videoPlayer2.clip = data.demoClip2; videoPlayer2.Play(); }
            else videoPlayer2.gameObject.SetActive(false);

            currentSceneToLoad = data.sceneToLoadName;
            currentExerciseID = data.exerciseID;

            // --- VARSAYILAN DEÐERLER ---
            if (currentExerciseID == "squat") { currentSets = 3; currentReps = 12; }
            else if (currentExerciseID == "plank") { currentSets = 3; currentReps = 30; }
            else if (currentExerciseID == "sideplank") { currentSets = 3; currentReps = 20; } // YENÝ

            UpdateSettingsUI();
            infoPanel.SetActive(true);
        }
    }

    void ChangeSet(int amount)
    {
        currentSets += amount;
        if (currentSets < 1) currentSets = 1;
        if (currentSets > 10) currentSets = 10;
        UpdateSettingsUI();
    }

    void ChangeRep(int amount)
    {
        int step = (currentExerciseID == "plank" || currentExerciseID == "sideplank") ? 5 : 1;
        int finalChange = (amount > 0) ? step : -step;
        currentReps += finalChange;
        if (currentReps < 1) currentReps = 1;
        UpdateSettingsUI();
    }

    void UpdateSettingsUI()
    {
        if (setDisplay) setDisplay.text = currentSets.ToString();

        if (repDisplay)
        {
            if (currentExerciseID == "plank" || currentExerciseID == "sideplank")
                repDisplay.text = currentReps.ToString() + " sn";
            else
                repDisplay.text = currentReps.ToString();
        }
    }

    public void CloseInfoPanel()
    {
        if (videoPlayer1.isPlaying) videoPlayer1.Stop();
        if (videoPlayer2.isPlaying) videoPlayer2.Stop();
        infoPanel.SetActive(false);
    }

    public void GoToMainMenu() { SceneManager.LoadScene("MenuScene"); }

    private void LoadSelectedScene()
    {
        ExerciseManager.userTargetSets = currentSets;
        ExerciseManager.userTargetReps = currentReps;

        if (!string.IsNullOrEmpty(currentSceneToLoad))
        {
            if (currentExerciseID == "squat") ExerciseManager.currentExercise = ExerciseManager.ExerciseType.Squat;
            else if (currentExerciseID == "plank") ExerciseManager.currentExercise = ExerciseManager.ExerciseType.Plank;
            else if (currentExerciseID == "sideplank") ExerciseManager.currentExercise = ExerciseManager.ExerciseType.SidePlank; // YENÝ

            SceneManager.LoadScene(currentSceneToLoad);
        }
    }
}