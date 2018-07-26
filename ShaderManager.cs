using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShaderManager : MonoBehaviour {

    public GameObject LeftPlane;
    public GameObject RightPlane;
    public float ScanningFrequency = 0.3f;
    public float ScanningSpeed = 0.01f;

    private Material LeftPlane_Mat;
    private Material RightPlane_Mat;
    private float OffsetTex;
    

    void Start() {
        LeftPlane_Mat = LeftPlane.GetComponent<Renderer>().material;
        RightPlane_Mat = RightPlane.GetComponent<Renderer>().material;
    }
	// Update is called once per frame
	void Update () {
        OffsetTex += ScanningSpeed;

        LeftPlane_Mat.SetTextureScale("_EmissionTex", new Vector2(0, ScanningFrequency));
        RightPlane_Mat.SetTextureScale("_EmissionTex", new Vector2(0, ScanningFrequency));

        LeftPlane_Mat.SetTextureOffset("_EmissionTex", new Vector2(0, OffsetTex));
        RightPlane_Mat.SetTextureOffset("_EmissionTex", new Vector2(0, OffsetTex));
    }
}
