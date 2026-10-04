using UnityEngine;

[DisallowMultipleComponent]
public sealed class MuseumWindWardBossDefeatedVisual : MonoBehaviour
{
    [SerializeField]
    private MeshRenderer targetRenderer;

    private void Start()
    {
        if (targetRenderer == null)
        {
            Debug.LogError(
                "[MuseumWindWardBossDefeatedVisual] Target renderer is missing.",
                this
            );
            return;
        }

        targetRenderer.enabled =
            GameState.Instance != null &&
            GameState.Instance.WindWardBossDefeated;
    }
}
