using UnityEngine;

// Learning survives a new run; health and upgrades never do.
public static class TutorialProgress
{
    // Saved tutorial completion
    private const string CompletionKey = "Catacombs.TutorialCompleted.v1";
    public static bool Completed => PlayerPrefs.GetInt(CompletionKey, 0) == 1;

    // Replay state and tutorial entry decision
    public static bool Replaying { get; set; }
    public static bool ShouldPlay => Replaying || !Completed;

    public static void Complete()
    {
        PlayerPrefs.SetInt(CompletionKey, 1);
        PlayerPrefs.Save();

        Replaying = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        Replaying = false;
    }
}
