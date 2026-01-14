using UnityEngine;

public static class ExerciseManager
{
    // Desteklediðimiz hareket türleri
    public enum ExerciseType
    {
        Squat,
        Plank,
        SidePlank
    }

    // Seçilen hareket (Varsayýlan Squat olsun)
    public static ExerciseType currentExercise = ExerciseType.Squat;

    // Hareketin hedef süresi veya tekrar sayýsý (Opsiyonel, þimdilik dursun)
    public static float targetDuration = 0f;


    // Egzersizden mi dönüyoruz yoksa sýfýrdan mý açýldýk?
    public static bool returningFromGame = false;



    // --- YENÝ EKLENENLER ---
    public static int userTargetSets = 3;  // Varsayýlan 3 Set
    public static int userTargetReps = 12; // Varsayýlan 12 Tekrar (veya 30 saniye)
}