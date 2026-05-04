using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace Astraleum
{
    public class StackDisplayManager : MonoBehaviour
    {
        public static StackDisplayManager Instance;

        [Header("Stack Display — Joueur 1 (bas)")]
        public StackIconGroup p1Icons;

        [Header("Stack Display — Joueur 2 (haut)")]
        public StackIconGroup p2Icons;

        [Header("Tooltip")]
        public GameObject tooltipPanel;
        public TMP_Text tooltipHeader;
        public IconLineManager tooltipLines; // ← remplace tooltipContent

        // ── Perspective réseau ────────────────────────────────────────────
        // Par défaut : bas (p1Icons) = joueur 0, haut (p2Icons) = joueur 1.
        // Pour P2 (LocalPlayerID=1) on inverse afin que chaque joueur voie ses propres stacks en bas.
        private int _perspBottom = 0;
        private int _perspTop    = 1;

        public void ApplyNetworkPerspective(int localPlayerID)
        {
            if (localPlayerID == 1)
            {
                _perspBottom = 1; // bas → joueur 1 (soi)
                _perspTop    = 0; // haut → joueur 0 (adversaire)
            }
            else
            {
                _perspBottom = 0;
                _perspTop    = 1;
            }
        }

        private void Awake() => Instance = this;

        private void LateUpdate()
        {
            if (StackManager.Instance == null) return;
            UpdateDisplay(p1Icons, _perspBottom);
            UpdateDisplay(p2Icons, _perspTop);
        }

        private void UpdateDisplay(StackIconGroup group, int playerID)
        {
            if (group == null) return;
            UpdateIcon(group.feu, playerID, Element.Feu);
            UpdateIcon(group.eau, playerID, Element.Eau);
            UpdateIcon(group.terre, playerID, Element.Terre);
            UpdateIcon(group.air, playerID, Element.Air);
            UpdateIcon(group.lumiere, playerID, Element.Lumiere);
            UpdateIcon(group.tenebres, playerID, Element.Tenebres);
            UpdateIcon(group.astral, playerID, Element.Astral);
        }

        private void UpdateIcon(StackIcon icon, int playerID, Element element)
        {
            if (icon == null) return;
            int stacks = StackManager.Instance.GetStacks(playerID, element);

            // Compteur
            if (icon.countText != null)
                icon.countText.text = stacks.ToString();

            // Opacité selon stacks
            if (icon.iconImage != null)
            {
                Color c = icon.iconImage.color;
                c.a = stacks == 0 ? 0.3f
                    : stacks >= 8 ? 1.0f
                    : stacks >= 5 ? 0.85f
                    : stacks >= 3 ? 0.70f
                    : 0.55f;
                icon.iconImage.color = c;
            }

            // Fond du compteur coloré selon seuil
            if (icon.countBG != null)
            {
                icon.countBG.color = stacks >= 5 ? new Color(1f, 0.85f, 0.2f, 0.9f)  // or
                                   : stacks >= 3 ? new Color(0.39f, 0.86f, 0.59f, 0.9f)  // vert
                                   : stacks > 0 ? new Color(0.3f, 0.3f, 0.5f, 0.9f)  // gris
                                   : new Color(0f, 0f, 0f, 0f);    // invisible si 0
            }
        }

        private System.Collections.IEnumerator PositionTooltip(Vector3 screenPos)
        {
            yield return null;
            yield return null;

            var linesRT   = tooltipLines?.GetComponent<RectTransform>();
            var tooltipRT = tooltipPanel?.GetComponent<RectTransform>();
            if (tooltipRT == null) yield break;

            // Préserve la largeur — réinitialise uniquement la hauteur pour que le CSF recalcule
            tooltipRT.sizeDelta = new Vector2(tooltipRT.sizeDelta.x, 0f);

            Canvas.ForceUpdateCanvases();
            if (linesRT != null) LayoutRebuilder.ForceRebuildLayoutImmediate(linesRT);
            LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRT);

            yield return null;
            Canvas.ForceUpdateCanvases();

            // Lit la taille réelle après rebuild — évite le calcul manuel de finalH
            // qui utilisait childLE.preferredHeight = -1 (non défini) pour les lignes
            float tipW = tooltipRT.rect.width;
            float tipH = tooltipRT.rect.height;

            var canvas = tooltipPanel.GetComponentInParent<Canvas>();
            if (canvas == null) yield break;

            var canvasRT = canvas.GetComponent<RectTransform>();
            float cW = canvasRT.rect.width;
            float cH = canvasRT.rect.height;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRT, screenPos, canvas.worldCamera, out Vector2 local);

            bool top   = screenPos.y > Screen.height / 2f;
            bool right = screenPos.x > Screen.width  / 2f;

            local += right ? new Vector2(-(tipW + 10f), top ? -(tipH + 5f) : 5f)
                           : new Vector2(10f,            top ? -(tipH + 5f) : 5f);

            local.x = Mathf.Clamp(local.x, -cW / 2f + tipW / 2f, cW / 2f - tipW / 2f);
            local.y = Mathf.Clamp(local.y, -cH / 2f + tipH / 2f, cH / 2f - tipH / 2f);

            tooltipRT.anchorMin      = new Vector2(0.5f, 0.5f);
            tooltipRT.anchorMax      = new Vector2(0.5f, 0.5f);
            tooltipRT.anchoredPosition = local;
        }

        // ── Tooltip ───────────────────────────────────────────────────

        public void ShowTooltip(int inspectorPlayerID, Element element, Vector3 screenPos)
        {
            // Sur la machine de P2, les icônes du bas (inspector playerID=0) représentent en réalité P2.
            int playerID = (NetworkBridge.IsActive && NetworkBridge.LocalPlayerID == 1)
                ? 1 - inspectorPlayerID
                : inspectorPlayerID;

            if (tooltipPanel == null || StackManager.Instance == null) return;

            int stacks = StackManager.Instance.GetStacks(playerID, element);
            bool isLocalPlayer = !NetworkBridge.IsActive || playerID == NetworkBridge.LocalPlayerID;
            string playerLabel = isLocalPlayer
                ? LocalizationManager.Get("stack_you")
                : LocalizationManager.Get("stack_opponent");
            string elemName = LocalizationManager.Get($"ui_element_{element.ToString().ToLower()}");

            if (tooltipHeader != null)
                tooltipHeader.text = LocalizationManager.Get("stack_header", elemName, playerLabel, stacks);

            if (tooltipLines != null)
            {
                tooltipLines.Clear();
                BuildBonusLines(playerID, element, stacks);
            }

            tooltipPanel.SetActive(true);
            StartCoroutine(PositionTooltip(screenPos));


        }

        public void HideTooltip()
        {
            if (tooltipPanel != null)
                tooltipPanel.SetActive(false);
        }

        private void BuildBonusLines(int playerID, Element element, int stacks)
        {

            var icons = IconLibrary.Instance;
            var lib = icons;

            Color activeColor = new Color(0.39f, 0.86f, 0.59f);
            Color inactiveColor = new Color(0.5f, 0.5f, 0.5f);
            Color goldColor = new Color(1f, 0.85f, 0.2f);

            if (stacks == 0)
            {
                tooltipLines.AddLine(null, LocalizationManager.Get("stack_no_bonus"), inactiveColor);
                tooltipLines.AddLine(null, LocalizationManager.Get("stack_place_cards"), inactiveColor);
                return;
            }

            switch (element)
            {
                case Element.Feu:
                    {
                        float bonus = stacks * 3f;
                        tooltipLines.AddLine(lib?.iconAttack, LocalizationManager.Get("stack_fire_minor", $"{bonus:0}"), activeColor);

                        tooltipLines.AddSeparator();

                        if (stacks >= 3)
                            tooltipLines.AddLine(lib?.iconAttack, LocalizationManager.Get("stack_fire_3_on"), activeColor);
                        else
                            tooltipLines.AddLine(lib?.iconBulletOff, LocalizationManager.Get("stack_fire_3_off", 3 - stacks), inactiveColor);

                        tooltipLines.AddSeparator();

                        if (stacks >= 5)
                            tooltipLines.AddLine(lib?.iconAttack, LocalizationManager.Get("stack_fire_5_on"), goldColor);
                        else
                            tooltipLines.AddLine(lib?.iconBulletOff, LocalizationManager.Get("stack_fire_5_off", 5 - stacks), inactiveColor);
                        break;
                    }

                case Element.Eau:
                    {
                        float red = stacks * 2f;
                        tooltipLines.AddLine(lib?.iconShield, LocalizationManager.Get("stack_water_minor", $"{red:0}"), activeColor);

                        tooltipLines.AddSeparator();

                        if (stacks >= 3)
                            tooltipLines.AddLine(lib?.iconShield, LocalizationManager.Get("stack_water_3_on"), activeColor);
                        else
                            tooltipLines.AddLine(lib?.iconBulletOff, LocalizationManager.Get("stack_water_3_off", 3 - stacks), inactiveColor);

                        tooltipLines.AddSeparator();

                        if (stacks >= 5)
                            tooltipLines.AddLine(lib?.iconShield, LocalizationManager.Get("stack_water_5_on"), goldColor);
                        else
                            tooltipLines.AddLine(lib?.iconBulletOff, LocalizationManager.Get("stack_water_5_off", 5 - stacks), inactiveColor);
                        break;
                    }

                case Element.Terre:
                    {
                        int armorStart = stacks * 5;
                        tooltipLines.AddLine(lib?.iconShield, LocalizationManager.Get("stack_earth_minor", armorStart), activeColor);

                        tooltipLines.AddSeparator();

                        int regen = StackManager.Instance.GetEarthArmorRegen(playerID);
                        if (stacks >= 3)
                            tooltipLines.AddLine(lib?.iconShield, LocalizationManager.Get("stack_earth_3_on", regen), activeColor);
                        else
                            tooltipLines.AddLine(lib?.iconBulletOff, LocalizationManager.Get("stack_earth_3_off", 3 - stacks), inactiveColor);

                        tooltipLines.AddSeparator();

                        if (stacks >= 5)
                            tooltipLines.AddLine(lib?.iconShield, LocalizationManager.Get("stack_earth_5_on"), goldColor);
                        else
                            tooltipLines.AddLine(lib?.iconBulletOff, LocalizationManager.Get("stack_earth_5_off", 5 - stacks), inactiveColor);
                        break;
                    }

                case Element.Air:
                    {
                        float chance = stacks * 1f;
                        tooltipLines.AddLine(lib?.iconBulletOn, LocalizationManager.Get("stack_air_minor", $"{chance:0}"), activeColor);

                        tooltipLines.AddSeparator();

                        if (stacks >= 3)
                            tooltipLines.AddLine(lib?.iconBulletOn, LocalizationManager.Get("stack_air_3_on"), activeColor);
                        else
                            tooltipLines.AddLine(lib?.iconBulletOff, LocalizationManager.Get("stack_air_3_off", 3 - stacks), inactiveColor);

                        tooltipLines.AddSeparator();

                        if (stacks >= 5)
                            tooltipLines.AddLine(lib?.iconBulletOn, LocalizationManager.Get("stack_air_5_on"), goldColor);
                        else
                            tooltipLines.AddLine(lib?.iconBulletOff, LocalizationManager.Get("stack_air_5_off", 5 - stacks), inactiveColor);
                        break;
                    }

                case Element.Lumiere:
                    {
                        float healBonus = stacks * 2f;
                        tooltipLines.AddLine(lib?.iconHeal, LocalizationManager.Get("stack_light_minor", $"{healBonus:0}"), activeColor);

                        tooltipLines.AddSeparator();

                        if (stacks >= 3)
                            tooltipLines.AddLine(lib?.iconHeal, LocalizationManager.Get("stack_light_3_on"), activeColor);
                        else
                            tooltipLines.AddLine(lib?.iconBulletOff, LocalizationManager.Get("stack_light_3_off", 3 - stacks), inactiveColor);

                        tooltipLines.AddSeparator();

                        if (stacks >= 5)
                            tooltipLines.AddLine(lib?.iconHeal, LocalizationManager.Get("stack_light_5_on"), goldColor);
                        else
                            tooltipLines.AddLine(lib?.iconBulletOff, LocalizationManager.Get("stack_light_5_off", 5 - stacks), inactiveColor);
                        break;
                    }

                case Element.Tenebres:
                    {
                        float dark = stacks * 3f;
                        tooltipLines.AddLine(lib?.iconAttack, LocalizationManager.Get("stack_dark_minor", $"{dark:0}"), activeColor);

                        tooltipLines.AddSeparator();

                        float poison = StackManager.Instance.GetPoisonPercent(playerID) * 100f;
                        if (stacks >= 3)
                            tooltipLines.AddLine(lib?.iconDeath, LocalizationManager.Get("stack_dark_3_on", $"{poison:0}"), activeColor);
                        else
                            tooltipLines.AddLine(lib?.iconBulletOff, LocalizationManager.Get("stack_dark_3_off", 3 - stacks), inactiveColor);

                        tooltipLines.AddSeparator();

                        if (stacks >= 5)
                            tooltipLines.AddLine(lib?.iconDeath, LocalizationManager.Get("stack_dark_5_on"), goldColor);
                        else
                            tooltipLines.AddLine(lib?.iconBulletOff, LocalizationManager.Get("stack_dark_5_off", 5 - stacks), inactiveColor);
                        break;
                    }

                case Element.Astral:
                    {
                        var astralCard = GetAstralCard(playerID);
                        var elem = astralCard != null ? StackManager.Instance.GetAstralElement(astralCard) : null;

                        // Séparateur
                        tooltipLines.AddSeparator();

                        if (elem.HasValue)
                        {
                            string copyElemName = LocalizationManager.Get($"ui_element_{elem.Value.ToString().ToLower()}");
                            tooltipLines.AddLine(lib?.iconBulletOn, LocalizationManager.Get("stack_astral_copy", copyElemName), activeColor);
                            tooltipLines.AddLine(lib?.iconBulletOn, LocalizationManager.Get("stack_astral_bonus"), activeColor);
                            tooltipLines.AddLine(lib?.iconBulletOff, LocalizationManager.Get("stack_astral_activate"), inactiveColor);
                        }
                        else
                        {
                            tooltipLines.AddLine(lib?.iconBulletOff, LocalizationManager.Get("stack_astral_no_left"), inactiveColor);
                            tooltipLines.AddLine(lib?.iconBulletOff, LocalizationManager.Get("stack_astral_place"), inactiveColor);
                        }
                        break;
                    }
            }
        }

        private CardInstance GetAstralCard(int playerID)
        {
            if (BoardManager.Instance == null) return null;
            var cards = BoardManager.Instance.GetAliveCards(playerID);
            return cards.Find(c => c.data.element == Element.Astral);
        }
    }



    // ── Structures de données ─────────────────────────────────────────

    [System.Serializable]
    public class StackIconGroup
    {
        public StackIcon feu;
        public StackIcon eau;
        public StackIcon terre;
        public StackIcon air;
        public StackIcon lumiere;
        public StackIcon tenebres;
        public StackIcon astral;
    }

    [System.Serializable]
    public class StackIcon
    {
        public Image iconImage;   // L'icône de l'élément
        public Image countBG;     // Le fond coloré du compteur
        public TMP_Text countText;   // Le chiffre du compteur
        public int playerID;    // 0 ou 1
        public Element element;     // L'élément associé
    }
}