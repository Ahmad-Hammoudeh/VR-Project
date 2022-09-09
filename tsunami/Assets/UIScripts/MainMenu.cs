using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public void playScene1()
    {
        
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1 , LoadSceneMode.Single);
    }
    public void playScene2()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 2);
    }
    public void playScene3()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 3);
    }
}
