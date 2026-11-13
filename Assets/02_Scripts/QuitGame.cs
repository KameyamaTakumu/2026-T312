using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Escキー、またはボタンからゲームを終了する汎用スクリプト。
/// 空のGameObjectにアタッチして使用します。
/// </summary>
public class QuitGame : MonoBehaviour
{
    [Header("設定")]
    [SerializeField] private bool enableEscKey = true;

    private void Update()
    {
        if (enableEscKey && IsEscPressed())
        {
            Quit();
        }
    }

    private bool IsEscPressed()
    {
#if ENABLE_INPUT_SYSTEM
        // 新しいInput System
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        // 従来のInput Manager
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    /// <summary>
    /// ボタンのOnClickからも呼び出せる終了処理
    /// </summary>
    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}