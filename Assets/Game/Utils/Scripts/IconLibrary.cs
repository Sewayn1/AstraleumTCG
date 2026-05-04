using UnityEngine;

namespace Astraleum
{
    [CreateAssetMenu(fileName = "IconLibrary", menuName = "Astraleum/IconLibrary")]
    public class IconLibrary : ScriptableObject
    {
        private static IconLibrary _instance;
        public static IconLibrary Instance
        {
            get
            {
                if (_instance == null)
                    _instance = Resources.Load<IconLibrary>("IconLibrary");
                return _instance;
            }
        }

        [Header("Combat")]
        public Sprite iconAttack;       // dégâts / attaque
        public Sprite iconHP;           // points de vie
        public Sprite iconCooldown;     // cooldown
        public Sprite iconDeath;        // mort de carte
        public Sprite iconShield;       // armure / bouclier
        public Sprite iconHeal;         // soin

        [Header("Effets de statut")]
        public Sprite iconBurn;         // brûlure active sur une carte
        public Sprite iconPoison;       // poison actif sur une carte
        public Sprite iconSaignement;   // saignement actif sur une carte

        [Header("Interface")]
        public Sprite iconBulletOn;     // bullet actif ✦
        public Sprite iconBulletOff;    // bullet inactif ◦
        public Sprite iconBuff;
        public Sprite iconDebuff;            // bonus max ★
        public Sprite iconTurn;         // tour ↺
    }
}