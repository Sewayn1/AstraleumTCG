using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace IndieImpulseAssets
{
    public class StartVFX : MonoBehaviour
    {
        public float delay = 0;
        public void StartEffect()
        {

            GetComponent<SpriteRenderer>().color = new Color(255, 255, 255, 0);
            CardVFX crdVfx = GetComponent<CardVFX>();
            transform.GetChild(0).gameObject.SetActive(true);
            Sprite sprite = crdVfx.sprite;
            crdVfx.material.SetTexture("_MainTex", sprite.texture);
            if (GetComponent<SpriteFader>() != null)
                GetComponent<SpriteFader>().enabled = true;
        }

        private void Start()
        {
            Invoke("StartEffect", delay);
        }

        //void Update()
        //{
        //    if (Input.GetKeyDown(KeyCode.Space))
        //    {
        //        StartEffect();
        //    }
        //}
    }
}