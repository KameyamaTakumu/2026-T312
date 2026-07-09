using UnityEngine;

/// <summary>
/// ゲーム設定管理
///
/// ・BGM音量
/// ・SE音量
/// ・カメラ左右反転
///
/// を管理するシングルトン
///
/// PlayerPrefs に保存されるため
/// 次回起動時にも設定が保持される
/// </summary>
public class GameSettingsManager : MonoBehaviour
{
    public static GameSettingsManager Instance;

    /// <summary>
    /// カメラ左右反転
    /// </summary>
    public bool InvertCamera
    {
        get;
        private set;
    }

    /// <summary>
    /// BGM音量
    /// </summary>
    public float BGMVolume
    {
        get;
        private set;
    }

    /// <summary>
    /// SE音量
    /// </summary>
    public float SEVolume
    {
        get;
        private set;
    }

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

    /// <summary>
    /// 保存データ読み込み
    /// </summary>
    public void LoadSettings()
    {
        BGMVolume = PlayerPrefs.GetFloat("BGMVolume", 1f);
        SEVolume = PlayerPrefs.GetFloat("SEVolume", 1f);
        InvertCamera = PlayerPrefs.GetInt("InvertCamera", 0) == 1;

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.SetBGMVolume(BGMVolume);
            SoundManager.Instance.SetSEVolume(SEVolume);
        }
    }

    /// <summary>
    /// BGM音量変更
    /// </summary>
    public void SetBGMVolume(float volume)
    {
        BGMVolume = volume;

        PlayerPrefs.SetFloat("BGMVolume", volume);

        if (SoundManager.Instance != null)
            SoundManager.Instance.SetBGMVolume(volume);
    }

    /// <summary>
    /// SE音量変更
    /// </summary>
    public void SetSEVolume(float volume)
    {
        SEVolume = volume;

        PlayerPrefs.SetFloat("SEVolume", volume);

        if (SoundManager.Instance != null)
            SoundManager.Instance.SetSEVolume(volume);
    }

    /// <summary>
    /// カメラ左右反転変更
    /// </summary>
    public void SetInvertCamera(bool invert)
    {
        InvertCamera = invert;

        PlayerPrefs.SetInt("InvertCamera", invert ? 1 : 0);
    }
}