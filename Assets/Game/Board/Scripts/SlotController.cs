using UnityEngine;

namespace Astraleum
{
    public class SlotController : MonoBehaviour
    {
        [Header("Identité")]
        public int slotIndex;
        public int ownerPlayerID;

        [Header("État")]
        public CardInstance occupiedCard;

        public bool IsEmpty => occupiedCard == null;

        public void PlaceCard(CardInstance card)
        {
            occupiedCard   = card;
            card.slotIndex = slotIndex;
            // ownerPlayerID est défini par CardInstance.Initialize() — ne pas écraser ici.
            card.transform.SetParent(transform);
            card.transform.localPosition = Vector3.zero;
        }

        public void ClearSlot()
        {
            occupiedCard = null;
        }

        public void HighlightSlot(bool active)
        {
            // À connecter au visuel plus tard
            // Ex : activer/désactiver un sprite de surbrillance
        }
    }
}