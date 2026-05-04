using UnityEngine;
using UnityEngine.UI;

namespace Astraleum
{
    /// <summary>
    /// Composant UI qui dessine un triangle plein pointant vers le HAUT par défaut.
    /// La pointe est au centre-haut du RectTransform ; la base en bas.
    /// Utiliser localEulerAngles pour orienter la pointe dans la bonne direction.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class UITriangle : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect r = rectTransform.rect;
            float halfW = r.width  * 0.5f;
            float halfH = r.height * 0.5f;

            Color32 c = color;

            // Pointe au centre-haut, base en bas
            UIVertex tip = new UIVertex { position = new Vector3(0f,     halfH,  0f), color = c };
            UIVertex bl  = new UIVertex { position = new Vector3(-halfW, -halfH, 0f), color = c };
            UIVertex br  = new UIVertex { position = new Vector3( halfW, -halfH, 0f), color = c };

            vh.AddVert(tip);  // 0
            vh.AddVert(bl);   // 1
            vh.AddVert(br);   // 2
            vh.AddTriangle(0, 1, 2);
        }
    }
}
