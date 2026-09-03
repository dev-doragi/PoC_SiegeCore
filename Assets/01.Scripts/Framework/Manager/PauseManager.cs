using UnityEngine;
// Compatibility adapter for Inspector bindings; do not add to AppRoot.
[System.Obsolete("Pause is owned by GameManager.")]
public class PauseManager : MonoBehaviour
{
    public void RequestPause()
    {
        GameManager.Instance.RequestPause();
    }

    public void RequestResume()
    {
        GameManager.Instance.RequestResume();
    }
}
