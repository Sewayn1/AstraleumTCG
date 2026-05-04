namespace Astraleum.UI
{
    /// <summary>
    /// À attacher sur Panel_Play.
    /// Ferme Panel_DeckSelect automatiquement quand Panel_Play se désactive.
    /// </summary>
    public class PlayPanelController : UnityEngine.MonoBehaviour
    {
        private void OnDisable()
        {
            DeckSelectPanel.Instance?.Hide();
        }
    }
}
