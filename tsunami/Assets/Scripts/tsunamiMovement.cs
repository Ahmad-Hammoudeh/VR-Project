using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class tsunamiMovement : MonoBehaviour
{
    public float vel = 3.5f;
    public float tsunamiMagnitude = 800;
    public Vector3 direction;

    public ComputeShader shader;

    public GameObject cube;
    public GameObject surface;
    public Vector3 initialPosition;
    public bool isTriggered = false;

    void Start()
    {
        cube = GameObject.Find("Cube");
        initialPosition = cube.transform.position;
        direction = cube.transform.forward * (-1);

        surface = GameObject.Find("tsunamiSurface");

        InitShader();
    }
    void InitShader()
    {
        shader.SetBool("tsunamiIsTriggered", false);
        shader.SetVector("tsunamiDirection", direction);
        shader.SetFloat("tsunamiMagnitude", tsunamiMagnitude);
        shader.SetVector("tsunmaiMinBound", cube.GetComponent<Renderer>().bounds.min);
        shader.SetVector("tsunmaiMaxBound", cube.GetComponent<Renderer>().bounds.max);
    }
    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            isTriggered = true;
        }
        if(Input.GetKeyDown(KeyCode.R))
        {
            cube.transform.position = initialPosition;
            isTriggered = false;
        }
        if(isTriggered)
        {
            cube.transform.position += vel * direction;
        }
        tsunamiMagnitude = (float)SliderUI.slidersValues[SliderUI.getIndex("Tsunami Force")];

        shader.SetBool("tsunamiIsTriggered", isTriggered);
        shader.SetVector("tsunamiDirection", direction);
        shader.SetFloat("tsunamiMagnitude", tsunamiMagnitude);


        shader.SetVector("normal", GameObject.Find("collider beach (2)").transform.forward * (-1));
        shader.SetVector("surface", GameObject.Find("collider beach (2)").transform.position);

        //print("min: " + cube.GetComponent<Renderer>().bounds.min);
        //print("max: " + cube.GetComponent<Renderer>().bounds.max);
        shader.SetVector("tsunmaiMinBound", cube.GetComponent<Renderer>().bounds.min); 
        shader.SetVector("tsunmaiMaxBound", cube.GetComponent<Renderer>().bounds.max);
    }
}
