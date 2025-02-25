using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;
using Rnd = UnityEngine.Random;
using UnityEngine.UI;
using HarmonyLib;

public class Dummy : MonoBehaviour {

    private bool ModuleSolved;
    void Start()
    {
        GetComponent<KMSelectable>().OnFocus += delegate () { { Solve(); } };
        
    }

    public void Solve()
    {
        if (ModuleSolved) return;
        GetComponent<KMBombModule>().HandlePass();
        ModuleSolved = true;
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        KMSelectable kms = GetComponent<KMSelectable>();
        kms.OnFocus();
        //kms.OnDefocus();

        while (!ModuleSolved)
            yield return null;
    }
}
