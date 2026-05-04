using UnityEngine;
using UnityEditor;

namespace Astraleum
{
    [CustomPropertyDrawer(typeof(ConditionalBranch))]
    public class ConditionalBranchDrawer : PropertyDrawer
    {
        private const float LineH = 18f;
        private const float Pad   = 2f;

        // Retourne vrai si la condition nécessite compareOp + threshold/effectType dans l'inspecteur.
        private static bool NeedsExtraFields(ConditionType ct) =>
            ct == ConditionType.TargetHPPercent  ||
            ct == ConditionType.AttackerHPPercent ||
            ct == ConditionType.TargetHasEffect  ||
            ct == ConditionType.AttackerHasEffect;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded) return LineH;

            int lines = 1; // foldout header

            var cond     = property.FindPropertyRelative("condition");
            var condType = (ConditionType)cond.FindPropertyRelative("conditionType").enumValueIndex;

            lines++; // conditionType
            if (NeedsExtraFields(condType))
            {
                lines++; // compareOp
                lines++; // threshold ou effectType
            }

            lines++; // effectType
            lines++; // target
            lines++; // valueMode
            lines++; // value (percent or flat)

            var effectType = (BranchEffectType)property.FindPropertyRelative("effectType").enumValueIndex;
            if (effectType != BranchEffectType.InstantHeal && effectType != BranchEffectType.InstantDamage)
                lines++; // durationTurns (absent pour InstantHeal)

            return lines * (LineH + Pad) + Pad;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var rect = new Rect(position.x, position.y, position.width, LineH);

            property.isExpanded = EditorGUI.Foldout(rect, property.isExpanded, label, true);
            if (!property.isExpanded)
            {
                EditorGUI.EndProperty();
                return;
            }
            rect.y += LineH + Pad;

            EditorGUI.indentLevel++;

            // ── Condition ────────────────────────────────────────────────
            var cond         = property.FindPropertyRelative("condition");
            var condTypeProp = cond.FindPropertyRelative("conditionType");
            EditorGUI.PropertyField(rect, condTypeProp, new GUIContent("Condition"));
            rect.y += LineH + Pad;

            var condType = (ConditionType)condTypeProp.enumValueIndex;

            if (NeedsExtraFields(condType))
            {
                EditorGUI.PropertyField(rect, cond.FindPropertyRelative("compareOp"), new GUIContent("Opérateur"));
                rect.y += LineH + Pad;

                if (condType == ConditionType.TargetHPPercent || condType == ConditionType.AttackerHPPercent)
                {
                    EditorGUI.PropertyField(rect, cond.FindPropertyRelative("threshold"), new GUIContent("Seuil HP (%)"));
                    rect.y += LineH + Pad;
                }
                else if (condType == ConditionType.TargetHasEffect || condType == ConditionType.AttackerHasEffect)
                {
                    EditorGUI.PropertyField(rect, cond.FindPropertyRelative("effectType"), new GUIContent("Type d'effet"));
                    rect.y += LineH + Pad;
                }
            }

            // ── Effet de branche ─────────────────────────────────────────
            var effectTypeProp = property.FindPropertyRelative("effectType");
            EditorGUI.PropertyField(rect, effectTypeProp, new GUIContent("Effet"));
            rect.y += LineH + Pad;

            EditorGUI.PropertyField(rect, property.FindPropertyRelative("target"), new GUIContent("Cible"));
            rect.y += LineH + Pad;

            // ── Valeur ───────────────────────────────────────────────────
            var valueModeProp = property.FindPropertyRelative("valueMode");
            EditorGUI.PropertyField(rect, valueModeProp, new GUIContent("Mode valeur"));
            rect.y += LineH + Pad;

            var valueMode = (BranchValueMode)valueModeProp.enumValueIndex;
            if (valueMode == BranchValueMode.Percent)
            {
                EditorGUI.PropertyField(rect, property.FindPropertyRelative("valuePercent"), new GUIContent("Valeur (%)"));
            }
            else
            {
                EditorGUI.PropertyField(rect, property.FindPropertyRelative("valueFlat"), new GUIContent("Valeur (Flat)"));
            }
            rect.y += LineH + Pad;

            // ── Durée ────────────────────────────────────────────────────
            var effectType = (BranchEffectType)effectTypeProp.enumValueIndex;
            if (effectType != BranchEffectType.InstantHeal && effectType != BranchEffectType.InstantDamage)
            {
                string durationLabel = effectType == BranchEffectType.Stun ? "Tours de Stun" : "Durée (tours)";
                EditorGUI.PropertyField(rect, property.FindPropertyRelative("durationTurns"), new GUIContent(durationLabel));
            }

            EditorGUI.indentLevel--;
            EditorGUI.EndProperty();
        }
    }
}
