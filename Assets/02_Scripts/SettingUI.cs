using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 設定画面UI。BGM/SE音量とカメラ操作反転の設定をスライダー・トグルで操作し、
/// GameSettingsManager／SoundManager と同期する。
/// </summary>
public class SettingsUI : MonoBehaviour
{
    [Header("BGM音量設定")]

    [CustomLabel("BGM音量スライダー"), SerializeField]
    private Slider bgmSlider;

    // 例：「BGM音量：80%」
    [CustomLabel("BGM音量テキスト"), SerializeField]
    private TMP_Text bgmVolumeText;

    [Header("SE音量設定")]

    [CustomLabel("SE音量スライダー"), SerializeField]
    private Slider seSlider;

    [CustomLabel("SE音量テキスト"), SerializeField]
    private TMP_Text seVolumeText;

    [Header("カメラ操作設定")]

    // ON = 反転、OFF = 通常
    [CustomLabel("カメラ反転トグル"), SerializeField]
    private Toggle invertCameraToggle;

    [CustomLabel("カメラ操作状態テキスト"), SerializeField]
    private TMP_Text invertCameraStateText;

    [Header("表示フォーマット")]

    [CustomLabel("BGM音量ラベル"), SerializeField]
    private string bgmLabelFormat = "BGM音量：{0}%";

    [CustomLabel("SE音量ラベル"), SerializeField]
    private string seLabelFormat = "SE音量：{0}%";

    [CustomLabel("カメラ通常時ラベル"), SerializeField]
    private string cameraNormalLabel = "カメラ操作：通常";

    [CustomLabel("カメラ反転時ラベル"), SerializeField]
    private string cameraInvertLabel = "カメラ操作：反転中";

    private void Start()
    {
        InitBGMSlider();
        InitSESlider();
        InitCameraToggle();
    }

    private void OnEnable()
    {
        if (GameSettingsManager.Instance != null)
            GameSettingsManager.Instance.OnInvertCameraChanged += HandleInvertCameraChanged;
    }

    private void OnDisable()
    {
        if (GameSettingsManager.Instance != null)
            GameSettingsManager.Instance.OnInvertCameraChanged -= HandleInvertCameraChanged;
    }

    // ─────────────────────────────────────
    // 初期化処理
    // ─────────────────────────────────────

    private void InitBGMSlider()
    {
        if (bgmSlider == null || SoundManager.Instance == null)
            return;

        // リスナー登録前に現在値を反映することで、初期化時の不要なコールバック発火を防ぐ
        bgmSlider.SetValueWithoutNotify(SoundManager.Instance.GetBGMVolume());
        UpdateBGMText(bgmSlider.value);

        bgmSlider.onValueChanged.AddListener(OnBGMSliderChanged);
    }

    private void InitSESlider()
    {
        if (seSlider == null || SoundManager.Instance == null)
            return;

        seSlider.SetValueWithoutNotify(SoundManager.Instance.GetSEVolume());
        UpdateSEText(seSlider.value);

        seSlider.onValueChanged.AddListener(OnSESliderChanged);
    }

    private void InitCameraToggle()
    {
        if (GameSettingsManager.Instance == null)
            return;

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

    private void OnBGMSliderChanged(float value)
    {
        SoundManager.Instance?.SetBGMVolume(value);
        UpdateBGMText(value);
    }

    private void UpdateBGMText(float value)
    {
        if (bgmVolumeText == null)
            return;

        int percent = Mathf.RoundToInt(value * 100f);
        bgmVolumeText.text = string.Format(bgmLabelFormat, percent);
    }

    // ─────────────────────────────────────
    // SE
    // ─────────────────────────────────────

    private void OnSESliderChanged(float value)
    {
        SoundManager.Instance?.SetSEVolume(value);
        UpdateSEText(value);
    }

    private void UpdateSEText(float value)
    {
        if (seVolumeText == null)
            return;

        int percent = Mathf.RoundToInt(value * 100f);
        seVolumeText.text = string.Format(seLabelFormat, percent);
    }

    // ─────────────────────────────────────
    // カメラ操作反転
    // ─────────────────────────────────────

    private void OnInvertCameraToggleChanged(bool isOn)
    {
        GameSettingsManager.Instance?.SetInvertCamera(isOn);

        // GameSettingsManager側のイベントでも更新されるが、
        // Instanceが存在しない場合に備えてここでも直接更新しておく
        UpdateCameraStateText(isOn);
    }

    /// <summary>
    /// GameSettingsManager からの変更通知を受けてトグル・テキストを同期する。
    /// 他画面から設定が変更された場合にも追従できるようにするための購読処理。
    /// </summary>
    private void HandleInvertCameraChanged(bool isOn)
    {
        if (invertCameraToggle != null)
            invertCameraToggle.SetIsOnWithoutNotify(isOn);

        UpdateCameraStateText(isOn);
    }

    private void UpdateCameraStateText(bool isInverted)
    {
        if (invertCameraStateText == null)
            return;

        invertCameraStateText.text = isInverted ? cameraInvertLabel : cameraNormalLabel;
    }
}