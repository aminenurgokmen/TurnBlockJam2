using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
public class CanvasManager : MonoBehaviour
{
    // Start is called before the first frame update
    public static CanvasManager Instance;
    public GameObject gamePanel;

    public CollectedUIScript collectedUIPrefab;
    public List<TargetScript> targetScripts;
    public GameObject successPanel;
    public GameObject failPanel;
    public GameObject shuffleText;
    public TextMeshProUGUI levelText;
    void Awake()
    {
        Instance = this;
    }
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
levelText.text = "LEVEL " + (SceneManager.GetActiveScene().buildIndex + 1);
    }
    public void ReloadScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
    public void LoadNextScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }
}
