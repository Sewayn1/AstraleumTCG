using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Astraleum
{
    // Posé sur tout TMP_Text passé par TMPIconReplacer (descriptions de compétences) :
    // affiche une infobulle glossaire au survol d'une icône mot-clé (DGT/PV/HEAL/ARM/BURN).
    [AddComponentMenu("Astraleum/UI/TMP Keyword Tooltip")]
    [RequireComponent(typeof(TMP_Text))]
    public class TMPKeywordTooltip : MonoBehaviour, IPointerMoveHandler, IPointerExitHandler
    {
        private static readonly Dictionary<string, (string titleKey, string descKey)> _glossary = new()
        {
            { "dgt",  ("keyword_dgt_title",  "keyword_dgt_desc") },
            { "pv",   ("keyword_pv_title",   "keyword_pv_desc") },
            { "heal", ("keyword_heal_title", "keyword_heal_desc") },
            { "arm",  ("keyword_arm_title",  "keyword_arm_desc") },
            { "burn", ("status_title_burn",  "codex_states_burn") },
        };

        private TMP_Text _text;
        private int _lastCharIndex = -1;

        private void Awake() => _text = GetComponent<TMP_Text>();

        public void OnPointerMove(PointerEventData eventData)
        {
            int charIndex = TMP_TextUtilities.FindIntersectingCharacter(_text, eventData.position, eventData.enterEventCamera, true);
            string spriteName = GetSpriteNameAt(charIndex);

            if (spriteName == null || !_glossary.TryGetValue(spriteName, out var keys))
            {
                if (_lastCharIndex != -1)
                {
                    TooltipSystem.Instance?.Hide();
                    _lastCharIndex = -1;
                }
                return;
            }

            if (charIndex == _lastCharIndex) return;
            _lastCharIndex = charIndex;

            TooltipSystem.Instance?.Show(
                LocalizationManager.Get(keys.titleKey),
                LocalizationManager.Get(keys.descKey),
                TooltipAnchor.RightOfCursor);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _lastCharIndex = -1;
            TooltipSystem.Instance?.Hide();
        }

        private string GetSpriteNameAt(int charIndex)
        {
            if (charIndex < 0 || _text.textInfo == null) return null;

            var charInfo = _text.textInfo.characterInfo;
            if (charIndex >= charInfo.Length) return null;

            var info = charInfo[charIndex];
            if (!info.isVisible || info.elementType != TMP_TextElementType.Sprite) return null;

            return (info.textElement as TMP_SpriteCharacter)?.name;
        }
    }
}
