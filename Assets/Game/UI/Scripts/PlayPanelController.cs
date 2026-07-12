namespace Astraleum.UI
{
    /// <summary>
    /// À attacher sur Panel_Play.
    /// Réinitialise la sélection de deck à chaque ouverture du panel.
    /// </summary>
    public class PlayPanelController : UnityEngine.MonoBehaviour
    {
        private void OnEnable()
        {
            DeckSelectPanel.Instance?.ResetSelection();
        }

        private void OnDisable()
        {
            var dsp = DeckSelectPanel.Instance;
            if (dsp == null) return;
            // Ne pas annuler si une partie est en cours de lancement (LoadScene Combat)
            if (!dsp.IsLaunching)
                dsp.CancelAll();
            dsp.Hide();
        }
    }
}
