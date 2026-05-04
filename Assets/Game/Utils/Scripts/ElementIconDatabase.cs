using UnityEngine;

namespace Astraleum
{
    [CreateAssetMenu(fileName = "ElementIconDatabase",
                     menuName  = "Astraleum/ElementIconDatabase")]
    public class ElementIconDatabase : ScriptableObject
    {
        private static ElementIconDatabase _instance;
        public static ElementIconDatabase Instance
        {
            get
            {
                if (_instance == null)
                    _instance = Resources.Load<ElementIconDatabase>("ElementIconDatabase");
                return _instance;
            }
        }

        [Header("Icônes des éléments")]
        public Sprite iconFeu;
        public Sprite iconEau;
        public Sprite iconTerre;
        public Sprite iconAir;
        public Sprite iconLumiere;
        public Sprite iconTenebres;
        public Sprite iconAstral;

        public Sprite GetIcon(Element element)
        {
            return element switch
            {
                Element.Feu      => iconFeu,
                Element.Eau      => iconEau,
                Element.Terre    => iconTerre,
                Element.Air      => iconAir,
                Element.Lumiere  => iconLumiere,
                Element.Tenebres => iconTenebres,
                Element.Astral   => iconAstral,
                _                => null
            };
        }
    }
}