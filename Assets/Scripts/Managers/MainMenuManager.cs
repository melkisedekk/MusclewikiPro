using UnityEngine;
using UnityEngine.SceneManagement; // Sahne deðiþimi için þart

public class MainMenuManager : MonoBehaviour
{
    // Bacaklara týklanýrsa çalýþacak
    public void OnLegsSelected()
    {
        Debug.Log("Bacak seçildi -> Squat baþlýyor");

        // Hafýzaya "Squat yapacaðýz" diye not alýyoruz
        ExerciseManager.currentExercise = ExerciseManager.ExerciseType.Squat;

        // Oyun sahnesini aç (Senin ana sahnenin adý 'GameScene' olmalý)
        SceneManager.LoadScene("GameScene");
    }

    // Karýn bölgesine týklanýrsa çalýþacak
    public void OnAbsSelected()
    {
        Debug.Log("Karýn seçildi -> Plank baþlýyor");

        // Hafýzaya "Plank yapacaðýz" diye not alýyoruz
        ExerciseManager.currentExercise = ExerciseManager.ExerciseType.Plank;
        ExerciseManager.targetDuration = 30f; // Örnek: 30 saniye hedef

        // Oyun sahnesini aç
        SceneManager.LoadScene("GameScene");
    }
}