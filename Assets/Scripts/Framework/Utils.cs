using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Utils
{
    public static void OpenStore()
    {
        Luna.Unity.LifeCycle.GameEnded();
        Luna.Unity.Playable.InstallFullGame();
    }
}
