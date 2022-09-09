using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SliderUI : MonoBehaviour
{
    public static int[] slidersValues = { 200000, 1000};

    [SerializeField] private Slider slider;
    [SerializeField] private TextMeshProUGUI sliderValue;
    [SerializeField] private TextMeshProUGUI sliderLabel;

    public static int getIndex(string name)
    {
        switch (name)
        {
            case "Particles":
                return 0;
            case "Tsunami Force":
                return 1;
        }
        return -1;
    }

    void Start()
    {
        int index = getIndex(sliderLabel.text);
        Debug.Log(index);
            slider.value = (int)slidersValues[index];
            sliderValue.text = ((int)slidersValues[index]).ToString();
            slider.onValueChanged.AddListener((v) =>
            {
                slidersValues[index] = (int)slider.value;
                sliderValue.text = slider.value.ToString();
            });
    }
}
