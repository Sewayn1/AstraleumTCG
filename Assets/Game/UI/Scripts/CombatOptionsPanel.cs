using UnityEngine;
using UnityEngine.UI;

namespace Astraleum
{
    /// <summary>
    /// Options d'affichage/son du combat — Panel_Settings (scène Combat).
    /// Persisté via PlayerPrefs, appliqué immédiatement.
    /// </summary>
    public class CombatOptionsPanel : MonoBehaviour
    {
        private const string KEY_DAMAGE_PREVIEW = "Combat_ShowDamagePreviewBar";
        private const string KEY_TURN_SOUND     = "Combat_TurnStartSound";

        [Header("Toggles")]
        public Toggle toggleDamagePreviewBar;
        public Toggle toggleTurnStartSound;

        public static bool DamagePreviewBarEnabled => PlayerPrefs.GetInt(KEY_DAMAGE_PREVIEW, 1) == 1;
        public static bool TurnStartSoundEnabled   => PlayerPrefs.GetInt(KEY_TURN_SOUND, 1) == 1;

        private void OnEnable()
        {
            LoadAndApply();
            BindListeners();
        }

        private void OnDisable()
        {
            UnbindListeners();
        }

        private void BindListeners()
        {
            if (toggleDamagePreviewBar != null) toggleDamagePreviewBar.onValueChanged.AddListener(OnDamagePreviewToggled);
            if (toggleTurnStartSound   != null) toggleTurnStartSound  .onValueChanged.AddListener(OnTurnStartSoundToggled);
        }

        private void UnbindListeners()
        {
            if (toggleDamagePreviewBar != null) toggleDamagePreviewBar.onValueChanged.RemoveListener(OnDamagePreviewToggled);
            if (toggleTurnStartSound   != null) toggleTurnStartSound  .onValueChanged.RemoveListener(OnTurnStartSoundToggled);
        }

        private void LoadAndApply()
        {
            if (toggleDamagePreviewBar != null) toggleDamagePreviewBar.SetIsOnWithoutNotify(DamagePreviewBarEnabled);
            if (toggleTurnStartSound   != null) toggleTurnStartSound  .SetIsOnWithoutNotify(TurnStartSoundEnabled);
        }

        private void OnDamagePreviewToggled(bool isOn)
        {
            PlayerPrefs.SetInt(KEY_DAMAGE_PREVIEW, isOn ? 1 : 0);
            PlayerPrefs.Save();
            if (!isOn) CombatUIManager.Instance?.HideDamagePreview();
        }

        private void OnTurnStartSoundToggled(bool isOn)
        {
            PlayerPrefs.SetInt(KEY_TURN_SOUND, isOn ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
