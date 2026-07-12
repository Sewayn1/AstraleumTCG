using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
namespace IndieImpulseAssets
{
    public class CardVFX : MonoBehaviour
    {
        public Sprite sprite;
        public Material material;
        private Renderer _renderer;
        public GameObject Effect;
        private void Start()
        {
            _renderer = Effect.GetComponent<Renderer>();
            sprite = GetComponent<SpriteRenderer>().sprite;
            material = _renderer.material;

        }

    }
}