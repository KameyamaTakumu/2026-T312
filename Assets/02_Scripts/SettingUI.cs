using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ê›íËâÊñ UI
/// </summary>
public class SettingsUI : MonoBehaviour
{
    [SerializeField] Slider bgmSlider;
    [SerializeField] Slider seSlider;
    [SerializeField]
    TMP_Text bgmText;
    [SerializeField]
    TMP_Text seText;
    [SerializeField] Toggle invertToggle;
    [SerializeField]
    TMP_Text invertToggleText;

    void Start()
    {
        bgmSlider.value = GameSettingsManager.Instance.BGMVolume;
        bgmText.text = $" {bgmSlider.value:F2}";
        seSlider.value = GameSettingsManager.Instance.SEVolume;
        seText.text = $" {seSlider.value:F2}";
        invertToggle.isOn = GameSettingsManager.Instance.InvertCamera;

        if (invertToggle.isOn)
        {
            invertToggleText.text = "ON";
        }
        else
        {
            invertToggleText.text = "OFF";
        }
    }
}