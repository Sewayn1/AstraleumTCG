using System.Collections;
using UnityEngine;
using TMPro;

namespace Astraleum
{
    public class CombatPopupHandler : MonoBehaviour
    {
        [Header("Références")]
        public TMP_Text healPopupText;
        public TMP_Text damagePopupText;

        private Coroutine healCoroutine;
        private Coroutine damageCoroutine;

        private void Awake()
        {
            if (healPopupText == null)
                healPopupText = transform.Find("HealPopup")
                                           ?.GetComponent<TMP_Text>();
            if (damagePopupText == null)
                damagePopupText = transform.Find("DamagePopup")
                                           ?.GetComponent<TMP_Text>();
        }

        // ── Soin ─────────────────────────────────────────────────────

        public void ShowHealPopup(int amount, Vector2 offset = default)
        {
            if (healPopupText == null) return;

            healPopupText.text = $"+{amount}";
            healPopupText.color = new Color(0.39f, 0.86f, 0.59f);
            healPopupText.fontSize = 18f; // ← plus grand
            healPopupText.rectTransform.anchoredPosition =
                offset == default ? Vector2.zero : offset;
            healPopupText.gameObject.SetActive(true);

            if (gameObject.activeInHierarchy)
                StartCoroutine(HideAfterDelay(healPopupText, 2f));
        }

        // ── Dégâts ────────────────────────────────────────────────────

        public void ShowDamagePopup(int amount, Vector2 offset = default)
        {
            if (damagePopupText == null) return;
            if (damageCoroutine != null) StopCoroutine(damageCoroutine);

            damagePopupText.text = $"-{amount}";
            damagePopupText.color = new Color(0.86f, 0.31f, 0.31f);
            damagePopupText.fontSize = 18f; // ← plus grand
            damagePopupText.rectTransform.anchoredPosition =
                offset == default ? Vector2.zero : offset;
            damagePopupText.gameObject.SetActive(true);

            if (gameObject.activeInHierarchy)
            {
                damageCoroutine = StartCoroutine(HideAfterDelay(damagePopupText, 2f));
                GetComponent<CardVisualUpdater>()?.TriggerHitShake();
            }
        }

        private IEnumerator ShowPopupWhenActive(TMP_Text text)
        {
            // Attend que le GameObject soit actif
            while (!gameObject.activeInHierarchy)
                yield return null;

            if (text != null)
            {
                text.gameObject.SetActive(true);
                damageCoroutine = StartCoroutine(HideAfterDelay(text, 2f));
            }
        }

        // ── Preview dégâts (affiché avant l'attaque au survol) ────────

        public void ShowDamagePreviewPopup(int amount, bool isHeal = false)
        {
            if (damagePopupText == null) return;
            if (!gameObject.activeInHierarchy) return; // ← NOUVEAU

            if (damageCoroutine != null)
            {
                StopCoroutine(damageCoroutine);
                damageCoroutine = null;
            }

            if (isHeal)
            {
                damagePopupText.text = $"+{amount}";
                damagePopupText.color = new Color(0.39f, 0.86f, 0.59f, 0.7f);
            }
            else
            {
                damagePopupText.text = $"-{amount}";
                damagePopupText.color = new Color(1f, 0.75f, 0.3f);
            }
            damagePopupText.gameObject.SetActive(true);
        }

        public void HideDamagePreviewPopup()
        {
            if (damagePopupText != null)
                damagePopupText.gameObject.SetActive(false);
        }

        // ─────────────────────────────────────────────────────────────

        private IEnumerator HideAfterDelay(TMP_Text target, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (target != null) target.gameObject.SetActive(false);
        }

        public void HideDamagePopupImmediate()
        {
            if (damageCoroutine != null)
            {
                StopCoroutine(damageCoroutine);
                damageCoroutine = null;
            }
            if (damagePopupText != null)
                damagePopupText.gameObject.SetActive(false);
        }

        public int GetCurrentPreviewValue()
        {
            if (damagePopupText == null || !damagePopupText.gameObject.activeSelf)
                return 0;

            // Parse la valeur affichée (format "-12" ou "+35")
            var text = damagePopupText.text.Replace("-", "").Replace("+", "");
            return int.TryParse(text, out int val) ? val : 0;
        }
    }
}