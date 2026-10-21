using System.Collections;
using System.Collections.Generic;
using InfinityPBR.Demo;
using UnityEngine;

public class SFB_MedusaDemoCameras : MonoBehaviour
{
    public DemoControl demoControl;
    public GameObject thisCamera;

    void OnEnable()
    {
        demoControl.cameraObject = thisCamera;
    }
}
