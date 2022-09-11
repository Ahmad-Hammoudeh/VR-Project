using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TsunamiMovement : MonoBehaviour
{
    private float vel = 3.5f;
    private float tsunamiMagnitude = 800;
    private Vector3 direction;

    public ComputeShader shader;

    private GameObject tsunamiCube;
    private Vector3 initialPosition;
    private bool isTriggered = false;

    void Start()
    {
        tsunamiCube = GameObject.Find("tsunami cube");
        initialPosition = tsunamiCube.transform.position;
        direction = tsunamiCube.transform.forward * (-1);
        
        initShader();
    }
    public void initShader()
    {
        shader.SetBool("tsunamiIsTriggered", false);
        shader.SetVector("tsunamiDirection", direction);
        shader.SetFloat("tsunamiMagnitude", tsunamiMagnitude);
        shader.SetVector("tsunmaiMinBound", tsunamiCube.GetComponent<Renderer>().bounds.min);
        shader.SetVector("tsunmaiMaxBound", tsunamiCube.GetComponent<Renderer>().bounds.max);

        shader.SetVector("beachNormal", GameObject.Find("collider beach (2)").transform.forward * (-1));
        shader.SetVector("beachSurface", GameObject.Find("collider beach (2)").transform.position);

    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            isTriggered = true;
            shader.SetBool("tsunamiIsTriggered", isTriggered);
        }
        if(Input.GetKeyDown(KeyCode.R))
        {
            tsunamiCube.transform.position = initialPosition;
            isTriggered = false;
            shader.SetBool("tsunamiIsTriggered", isTriggered);
        }
        if(isTriggered)
        {
            tsunamiCube.transform.position += vel * direction;

            shader.SetVector("tsunmaiMinBound", tsunamiCube.GetComponent<Renderer>().bounds.min);
            shader.SetVector("tsunmaiMaxBound", tsunamiCube.GetComponent<Renderer>().bounds.max);
        }

        tsunamiMagnitude = SliderUI.slidersValues[SliderUI.getIndex("Tsunami Force")];
        shader.SetFloat("tsunamiMagnitude", tsunamiMagnitude);
    }
}
