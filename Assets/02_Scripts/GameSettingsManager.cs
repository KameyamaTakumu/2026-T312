using System;
using UnityEngine;

/// <summary>
/// ゲーム設定管理クラス。カメラ反転などの設定値を保持し、PlayerPrefsへ保存するシングルトンマネージャ。
/// </summary>
public class GameSettingsManager : MonoBehaviour
{
    public static GameSettingsManager Instance { get; private set; }

    private const string InvertCameraKey = "Settings_InvertCamera";

    /// <summary>
    /// カメラ操作反転フラグ。true = A/Dの入力方向を反転、false = 通常操作。
    /// </summary>
    public bool InvertCamera { get; private set; }

    /// <summary>
    /// カメラ反転設定が変更された時に発火する。UI側の表示更新などに利用する。
    /// </summary>
    public event Action<bool> OnInvertCameraChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadSettings();
    }

    // ─────────────────────────────────────
    // Save / Load
    // ─────────────────────────────────────

    /// <summary>
    /// PlayerPrefsから設定を読み込む。未保存の場合はfalse（通常操作）をデフォルトとする。
    /// </summary>
    private void LoadSettings()
    {
        InvertCamera = PlayerPrefs.GetInt(InvertCameraKey, 0) == 1;
    }

    private void SaveSettings()
    {
        PlayerPrefs.SetInt(InvertCameraKey, InvertCamera ? 1 : 0);
        PlayerPrefs.Save();
    }

    // ─────────────────────────────────────
    // Setter
    // ─────────────────────────────────────

    /// <summary>
    /// カメラ反転設定を変更し、PlayerPrefsへ保存したうえで購読側（UIなど）へ通知する。
    /// </summary>
    public void SetInvertCamera(bool invert)
    {
        if (InvertCamera == invert)
            return;

        InvertCamera = invert;
        SaveSettings();

        OnInvertCameraChanged?.Invoke(InvertCamera);
    }

    /// <summary>
    /// カメラ反転設定をON/OFF切り替える。
    /// </summary>
    public void ToggleInvertCamera()
    {
        SetInvertCamera(!InvertCamera);
    }
}