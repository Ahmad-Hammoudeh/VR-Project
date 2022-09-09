using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SwitchToggle : MonoBehaviour
{
    public static bool[] toggleValues = { true, false, false };

    [SerializeField] RectTransform uiHandleRectTransform;
    [SerializeField] Color backgroundActiveColor;
    [SerializeField] Color handleActiveColor;

    TextMeshProUGUI toggleLabel;

    Image backgroundImage, handleImage;

    Color backgroundDefaultColor, handleDefaultColor;

    Toggle toggle;

    Vector2 handlePosition;

    public static int getIndex(string name)
    {
        switch (name)
        {
            case "Render Particles":
                return 0;
            case "Render Water":
                return 1;
            case "Radix Sort":
                return 2;
            
        }
        return -1;
    }

    void Awake()
    {
        toggle = GetComponent<Toggle>();

        toggleLabel = toggle.GetComponentInChildren<TextMeshProUGUI>();

        handlePosition = uiHandleRectTransform.anchoredPosition;

        backgroundImage = uiHandleRectTransform.parent.GetComponent<Image>();
        handleImage = uiHandleRectTransform.GetComponent<Image>();

        backgroundDefaultColor = backgroundImage.color;
        handleDefaultColor = handleImage.color;

        int index = getIndex(toggleLabel.text);
        toggle.isOn = toggleValues[index];

        toggle.onValueChanged.AddListener(OnSwitch);

        if (toggle.isOn)
            OnSwitch(true);
    }

    void OnSwitch(bool on)
    {
        int index = getIndex(toggleLabel.text);
        toggleValues[index] = on;

        uiHandleRectTransform.anchoredPosition = on ? handlePosition * -1 : handlePosition;

        backgroundImage.color = on ? backgroundActiveColor : backgroundDefaultColor;

        handleImage.color = on ? handleActiveColor : handleDefaultColor;
    }

    void OnDestroy()
    {
        toggle.onValueChanged.RemoveListener(OnSwitch);
    }
}

