using TMPro;
using Unity.Cinemachine;
using UnityEngine;

public class ObjectFocusInteraction : MonoBehaviour
{
    [SerializeField] private CinemachineCamera focusCamera;
    [SerializeField] private int normalPriority = 10;
    [SerializeField] private int focusPriority = 30;
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Header("World Space UI")]
    [SerializeField] private GameObject promptUI; // World Space Canvas
    [SerializeField] private TextMeshProUGUI promptText;
    [SerializeField] private string enterPromptMessage = "Eキーで注目する";
    [SerializeField] private string exitPromptMessage = "Eキーで元に戻す";

    private bool playerInRange = false;
    private bool isFocusing = false;

    private void Start()
    {
        focusCamera.Priority = normalPriority;
        if (promptUI != null)
            promptUI.SetActive(false);

        if (promptText != null)
            promptText.text = enterPromptMessage;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        playerInRange = true;
        if (!isFocusing)
            ShowPrompt(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        playerInRange = false;
        ShowPrompt(false);
        ExitFocus();
    }

    private void Update()
    {
        if (!playerInRange) return;

        if (Input.GetKeyDown(interactKey))
        {
            if (!isFocusing)
                EnterFocus();
            else
                ExitFocus();
        }
    }

    private void EnterFocus()
    {
        isFocusing = true;
        focusCamera.Priority = focusPriority;
        //ShowPrompt(false); // 注目中はプロンプトを隠す

        if (promptText != null)
            promptText.text = exitPromptMessage;
    }

    private void ExitFocus()
    {
        isFocusing = false;
        focusCamera.Priority = normalPriority;

        if (playerInRange)
            ShowPrompt(true); // 範囲内ならプロンプト再表示

        if (promptText != null)
            promptText.text = enterPromptMessage;
    }

    private void ShowPrompt(bool show)
    {
        if (promptUI != null)
            promptUI.SetActive(show);
    }
}