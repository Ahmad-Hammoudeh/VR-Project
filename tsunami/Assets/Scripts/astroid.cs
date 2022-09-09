using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class astroid : MonoBehaviour
{
    // Start is called before the first frame update

    public float velz = 3.5f;
    public Vector3 direction;

    public ComputeShader shader;

    public Vector3 initialPosition;
    public bool isTriggered = false;

    void Start()
    {
        
        InitShader();
    }
    void InitShader()
    {
        //shader.SetBool("tsunamiIsTriggered", false);
        //shader.SetVector("tsunamiDirection", direction);
    }
    // Update is called once per frame
    void Update()
    {
        /*if (Input.GetKeyDown(KeyCode.Space))
        {
            isTriggered = true;
        }
        if (Input.GetKeyDown(KeyCode.R))
        {
            
        }
        if (isTriggered)
        {
            //transform.position.z += velz;
        }*/
        //tsunamiMagnitude = (float)SliderUI.slidersValues[SliderUI.getIndex("Force")];

        //shader.SetBool("tsunamiIsTriggered", isTriggered);
        //shader.SetVector("tsunamiDirection", direction);

    }
}
