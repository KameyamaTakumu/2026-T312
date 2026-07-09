using UnityEngine;

public class TitleManager : MonoBehaviour
{
    [CustomLabel("クレジットキャンバス"), SerializeField]
    private GameObject creditCanvas;

    void Start()
    {
        BGM.Title.Play();
    }

    public void OnClickCreditButton()
    {
        creditCanvas.SetActive(true);
    }

    public void OnClickBackCreditButton()
    {
        creditCanvas.SetActive(false);
    }
}
