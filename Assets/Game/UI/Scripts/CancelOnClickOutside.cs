using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Astraleum
{
    public class CancelOnClickOutside : MonoBehaviour
    {
        private void Update()
        {
            // Clic droit → annule toujours, partout
            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                CombatUIManager.Instance?.CancelSelection();
                return;
            }

            if (!Mouse.current.leftButton.wasPressedThisFrame) return;


            var ui = CombatUIManager.Instance;
            if (ui == null) return;
            if (!ui.HasSkillSelected && !ui.IsSkillPanelOpen) return;

            var pointerData = new PointerEventData(EventSystem.current)
            {
                position = Mouse.current.position.ReadValue()
            };

            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);

            if (results.Count == 0)
            {
                ui.CancelSelection();
                return;
            }

            foreach (var result in results)
            {
                if (ui.cardSkillPanel != null &&
                    (result.gameObject == ui.cardSkillPanel ||
                     result.gameObject.transform.IsChildOf(ui.cardSkillPanel.transform)))
                    return;

                var card = result.gameObject.GetComponentInParent<CardInstance>();
                if (card != null) return;
            }

            ui.CancelSelection();
        }
    }
}