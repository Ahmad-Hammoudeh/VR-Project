using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class start : MonoBehaviour
{
    // Start is called before the first frame update
    public GameObject manager;
    public GameObject tsunamiManager;

    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Z))
        {
            //manager = (GameObject)Instantiate(manager, new Vector3(0 , 0 ,0), Quaternion.identity);
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
        if (Input.GetKeyDown(KeyCode.X))
        {
            manager.SetActive(true);
            tsunamiManager.SetActive(true);
        }
    }
}
