/*========================================================
 * ゲーム設定管理クラス
 * 
 * 概要
 * ・カメラ操作反転（InvertCamera）
 * などのゲーム全体設定を一括管理するシングルトンマネージャ
 * 
 * -------------------------------------------------------
 * 使い方
 * 
 * 1. 参照
 * GameSettingsManager.Instance.InvertCamera
 * 
 * 2. 変更
 * GameSettingsManager.Instance.SetInvertCamera(true);
 * 
 * -------------------------------------------------------
 * 特徴
 * 
 * ・PlayerPrefs による設定の保存 / 復元
 * ・シーンを跨いで保持（DontDestroyOnLoad）
 * ・設定変更を購読できるイベント（OnInvertCameraChanged）
 *========================================================
*/

using System;
using UnityEngine;

/// <summary>
/// ゲーム設定管理クラス
/// カメラ反転などの設定値を保持・保存する
/// シングルトンマネージャ
/// </summary>
public class GameSettingsManager : MonoBehaviour
{
    // ─────────────────────────────────────
    // Singleton
    // ─────────────────────────────────────

    /// <summary>
    /// グローバルアクセス用インスタンス
    /// どこからでも
    /// GameSettingsManager.Instance
    /// でアクセス可能
    /// </summary>
    public static GameSettingsManager Instance;

    // ─────────────────────────────────────
    // PlayerPrefs Key
    // ─────────────────────────────────────

    private const string KEY_INVERT_CAMERA = "Settings_InvertCamera";

    // ─────────────────────────────────────
    // 設定値
    // ─────────────────────────────────────

    /// <summary>
    /// カメラ操作反転フラグ
    /// true  = A/D の入力方向を反転させる
    /// false = 通常操作
    /// </summary>
    public bool InvertCamera { get; private set; }

    // ─────────────────────────────────────
    // イベント
    // ─────────────────────────────────────

    /// <summary>
    /// カメラ反転設定が変更された時に発火
    /// UI 側の表示更新などに利用する
    /// </summary>
    public event Action<bool> OnInvertCameraChanged;

    // ─────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────

    /// <summary>
    /// 初期化処理
    /// </summary>
    private void Awake()
    {
        // ─────────────────────────────
        // シングルトン重複防止
        // ─────────────────────────────

        if (Instance != null &&
            Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // シーン切り替え時に破棄しない
        DontDestroyOnLoad(gameObject);

        // 保存済み設定の読み込み
        LoadSettings();
    }

    // ─────────────────────────────────────
    // Save / Load
    // ─────────────────────────────────────

    /// <summary>
    /// PlayerPrefs から設定を読み込む
    /// 未保存の場合は false（通常操作）をデフォルトとする
    /// </summary>
    private void LoadSettings()
    {
        InvertCamera =
            PlayerPrefs.GetInt(KEY_INVERT_CAMERA, 0) == 1;
    }

    /// <summary>
    /// 現在の設定を PlayerPrefs に保存する
    /// </summary>
    private void SaveSettings()
    {
        PlayerPrefs.SetInt(
            KEY_INVERT_CAMERA,
            InvertCamera ? 1 : 0
        );

        PlayerPrefs.Save();
    }

    // ─────────────────────────────────────
    // Setter
    // ─────────────────────────────────────

    /// <summary>
    /// カメラ反転設定を変更する
    /// </summary>
    /// <param name="invert">
    /// true  = 反転する
    /// false = 反転しない
    /// </param>
    public void SetInvertCamera(bool invert)
    {
        if (InvertCamera == invert)
        {
            return;
        }

        InvertCamera = invert;

        SaveSettings();

        // 購読側（UIなど）へ通知
        OnInvertCameraChanged?.Invoke(InvertCamera);
    }

    /// <summary>
    /// カメラ反転設定をトグル（ON/OFF切り替え）する
    /// </summary>
    public void ToggleInvertCamera()
    {
        SetInvertCamera(!InvertCamera);
    }
}