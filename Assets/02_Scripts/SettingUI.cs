/*========================================================
 * オプション画面 UI 制御クラス
 * 
 * 概要
 * ・BGM音量スライダー / SE音量スライダー
 * ・現在の音量をテキスト表示
 * ・カメラ操作反転トグル
 * ・現在の操作状態（通常 / 反転）をテキスト表示
 * を制御する
 * 
 * -------------------------------------------------------
 * 使い方
 * 
 * 1. オプション画面の Canvas 内に以下を配置
 *    ・BGM用 Slider
 *    ・SE用  Slider
 *    ・BGM音量表示用 Text
 *    ・SE音量表示用  Text
 *    ・カメラ反転用  Toggle
 *    ・カメラ操作状態表示用 Text
 * 
 * 2. 本スクリプトを空の GameObject にアタッチし、
 *    Inspector で上記 UI 要素を割り当てる
 * 
 * -------------------------------------------------------
 * 前提
 * 
 * ・SoundManager.Instance が存在すること
 *   （GetBGMVolume / GetSEVolume / SetBGMVolume / SetSEVolume）
 * ・GameSettingsManager.Instance が存在すること
 *   （InvertCamera / SetInvertCamera / OnInvertCameraChanged）
 *========================================================
*/

using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 設定画面UI
/// </summary>
public class SettingsUI : MonoBehaviour
{
    // ─────────────────────────────────────
    // BGM
    // ─────────────────────────────────────

    [Header("BGM音量設定")]

    /// <summary>
    /// BGM音量スライダー（0〜1）
    /// </summary>
    [CustomLabel("BGM音量スライダー"), SerializeField]
    private Slider bgmSlider;

    /// <summary>
    /// 現在のBGM音量を表示するテキスト
    /// 例）「BGM音量：80%」
    /// </summary>
    [CustomLabel("BGM音量テキスト"), SerializeField]
    private TMP_Text bgmVolumeText;

    // ─────────────────────────────────────
    // SE
    // ─────────────────────────────────────

    [Header("SE音量設定")]

    /// <summary>
    /// SE音量スライダー（0〜1）
    /// </summary>
    [CustomLabel("SE音量スライダー"), SerializeField]
    private Slider seSlider;

    /// <summary>
    /// 現在のSE音量を表示するテキスト
    /// 例）「SE音量：80%」
    /// </summary>
    [CustomLabel("SE音量テキスト"), SerializeField]
    private TMP_Text seVolumeText;

    // ─────────────────────────────────────
    // カメラ操作反転
    // ─────────────────────────────────────

    [Header("カメラ操作設定")]

    /// <summary>
    /// カメラ操作反転トグル
    /// ON  = 反転
    /// OFF = 通常
    /// </summary>
    [CustomLabel("カメラ反転トグル"), SerializeField]
    private Toggle invertCameraToggle;

    /// <summary>
    /// 現在のカメラ操作状態を表示するテキスト
    /// 例）「カメラ操作：反転中」
    /// </summary>
    [CustomLabel("カメラ操作状態テキスト"), SerializeField]
    private TMP_Text invertCameraStateText;

    // ─────────────────────────────────────
    // 表示フォーマット
    // ─────────────────────────────────────

    [Header("表示フォーマット")]

    [CustomLabel("BGM音量ラベル"), SerializeField]
    private string bgmLabelFormat = "BGM音量：{0}%";

    [CustomLabel("SE音量ラベル"), SerializeField]
    private string seLabelFormat = "SE音量：{0}%";

    [CustomLabel("カメラ通常時ラベル"), SerializeField]
    private string cameraNormalLabel = "カメラ操作：通常";

    [CustomLabel("カメラ反転時ラベル"), SerializeField]
    private string cameraInvertLabel = "カメラ操作：反転中";

    // ─────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────

    /// <summary>
    /// UIの初期化
    /// 現在の設定値をスライダー・トグル・テキストへ反映する
    /// </summary>
    private void Start()
    {
        InitBGMSlider();
        InitSESlider();
        InitCameraToggle();
    }

    /// <summary>
    /// 有効化時：設定変更イベントを購読
    /// </summary>
    private void OnEnable()
    {
        if (GameSettingsManager.Instance != null)
        {
            GameSettingsManager.Instance.OnInvertCameraChanged
                += HandleInvertCameraChanged;
        }
    }

    /// <summary>
    /// 無効化時：イベント購読解除
    /// </summary>
    private void OnDisable()
    {
        if (GameSettingsManager.Instance != null)
        {
            GameSettingsManager.Instance.OnInvertCameraChanged
                -= HandleInvertCameraChanged;
        }
    }

    // ─────────────────────────────────────
    // 初期化処理
    // ─────────────────────────────────────

    /// <summary>
    /// BGMスライダーの初期化
    /// 現在の音量を反映し、リスナーを登録する
    /// </summary>
    private void InitBGMSlider()
    {
        if (bgmSlider == null ||
            SoundManager.Instance == null)
        {
            return;
        }

        // 現在の音量を初期値として設定
        // （リスナー登録前に値を設定することで
        //   不要なコールバック発火を防ぐ）
        bgmSlider.SetValueWithoutNotify(
            SoundManager.Instance.GetBGMVolume()
        );

        UpdateBGMText(bgmSlider.value);

        // リスナー登録
        bgmSlider.onValueChanged.AddListener(OnBGMSliderChanged);
    }

    /// <summary>
    /// SEスライダーの初期化
    /// 現在の音量を反映し、リスナーを登録する
    /// </summary>
    private void InitSESlider()
    {
        if (seSlider == null ||
            SoundManager.Instance == null)
        {
            return;
        }

        seSlider.SetValueWithoutNotify(
            SoundManager.Instance.GetSEVolume()
        );

        UpdateSEText(seSlider.value);

        seSlider.onValueChanged.AddListener(OnSESliderChanged);
    }

    /// <summary>
    /// カメラ反転トグルの初期化
    /// 現在の設定を反映し、リスナーを登録する
    /// </summary>
    private void InitCameraToggle()
    {
        if (GameSettingsManager.Instance == null)
        {
            return;
        }

        bool invert = GameSettingsManager.Instance.InvertCamera;

        if (invertCameraToggle != null)
        {
            invertCameraToggle.SetIsOnWithoutNotify(invert);
            invertCameraToggle.onValueChanged.AddListener(OnInvertCameraToggleChanged);
        }

        UpdateCameraStateText(invert);
    }

    // ─────────────────────────────────────
    // BGM
    // ─────────────────────────────────────

    /// <summary>
    /// BGMスライダー変更時のコールバック
    /// </summary>
    /// <param name="value">
    /// スライダー値（0〜1）
    /// </param>
    public void OnBGMSliderChanged(float value)
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.SetBGMVolume(value);
        }

        UpdateBGMText(value);
    }

    /// <summary>
    /// BGM音量テキストを更新する
    /// </summary>
    private void UpdateBGMText(float value)
    {
        if (bgmVolumeText == null)
        {
            return;
        }

        int percent = Mathf.RoundToInt(value * 100f);
        bgmVolumeText.text = string.Format(bgmLabelFormat, percent);
    }

    // ─────────────────────────────────────
    // SE
    // ─────────────────────────────────────

    /// <summary>
    /// SEスライダー変更時のコールバック
    /// </summary>
    /// <param name="value">
    /// スライダー値（0〜1）
    /// </param>
    public void OnSESliderChanged(float value)
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.SetSEVolume(value);
        }

        UpdateSEText(value);
    }

    /// <summary>
    /// SE音量テキストを更新する
    /// </summary>
    private void UpdateSEText(float value)
    {
        if (seVolumeText == null)
        {
            return;
        }

        int percent = Mathf.RoundToInt(value * 100f);
        seVolumeText.text = string.Format(seLabelFormat, percent);
    }

    // ─────────────────────────────────────
    // カメラ操作反転
    // ─────────────────────────────────────

    /// <summary>
    /// カメラ反転トグル変更時のコールバック
    /// </summary>
    /// <param name="isOn">
    /// true  = 反転
    /// false = 通常
    /// </param>
    public void OnInvertCameraToggleChanged(bool isOn)
    {
        if (GameSettingsManager.Instance != null)
        {
            GameSettingsManager.Instance.SetInvertCamera(isOn);
        }

        // GameSettingsManager 側のイベントでも更新されるが、
        // Instance が存在しない場合に備えてここでも直接更新しておく
        UpdateCameraStateText(isOn);
    }

    /// <summary>
    /// GameSettingsManager からの変更通知を受けて
    /// トグル・テキストを同期する
    /// （他画面から設定が変更された場合にも追従できるようにする）
    /// </summary>
    private void HandleInvertCameraChanged(bool isOn)
    {
        if (invertCameraToggle != null)
        {
            invertCameraToggle.SetIsOnWithoutNotify(isOn);
        }

        UpdateCameraStateText(isOn);
    }

    /// <summary>
    /// カメラ操作状態テキストを更新する
    /// </summary>
    private void UpdateCameraStateText(bool isInverted)
    {
        if (invertCameraStateText == null)
        {
            return;
        }

        invertCameraStateText.text =
            isInverted ? cameraInvertLabel : cameraNormalLabel;
    }
}