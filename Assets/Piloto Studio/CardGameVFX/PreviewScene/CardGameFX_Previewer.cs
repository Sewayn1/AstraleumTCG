using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CardGameFX_Previewer : MonoBehaviour
{
    public GameObject disableOnStart;
    public GameObject enableOnStart;
    public GameObject[] fx;
    [SerializeField]
    private int current;
    private void Start()
    {
        disableOnStart.SetActive(false);
        enableOnStart.SetActive(true);
    }

    public void Next()
    {
        fx[current].SetActive(false);
        current++;
        if (current > fx.Length - 1)
        {
            current = 0;
        }


        fx[current].SetActive(true);


    }


    public void Back()
    {
        fx[current].SetActive(false);
        if (current >= 1)
        {
            current--;
        }
        else
        {
            current = fx.Length - 1;
        }

        fx[current].SetActive(true);



    }


}
