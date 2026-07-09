using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DG.Tweening;

/// <summary>
/// 円形フェード(アイリスワイプ)管理
/// 
/// 死亡時に閉じる／シーンリロード後に開く演出を制御する。
/// SoundManager 等と同じ DontDestroyOnLoad シングルトン。
/// </summary>
[RequireComponent(typeof(Image))]
public class ScreenFader : MonoBehaviour
{
    public static ScreenFader Instance { get; private set; }

    [CustomLabel("フェード時間（秒）"), SerializeField]
    private float fadeDuration = 1.0f;

    // 画面全体を覆いきるための半径（対角線分をカバーできる余裕値）
    [CustomLabel("全開放時の半径"), SerializeField]
    private float openRadius = 1.5f;

    private Image _image;
    private Material _material;
    private static readonly int RadiusID = Shader.PropertyToID("_Radius");

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _image = GetComponent<Image>();

        // 共有マテリアルを書き換えないようインスタンス化
        _material = new Material(_image.material);
        _image.material = _material;

        UpdateAspectRatio();

        // 初期状態：画面が見えている状態
        SetRadius(openRadius);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UpdateAspectRatio();

        // シーンロード直後は画面が閉じた(黒)状態から始め、
        // 広がるフェードでリスポーン感を出す
        FadeIn();
    }

    private void UpdateAspectRatio()
    {
        _material.SetFloat("_AspectRatio", (float)Screen.width / Screen.height);
    }

    private void SetRadius(float radius)
    {
        _material.SetFloat(RadiusID, radius);
    }

    /// <summary>
    /// 周囲から丸く閉じていく（死亡演出）
    /// </summary>
    public Tween FadeOut(System.Action onComplete = null)
    {
        return DOTween.To(() => _material.GetFloat(RadiusID), SetRadius, 0f, fadeDuration)
            .SetEase(Ease.InOutQuad)
            .OnComplete(() => onComplete?.Invoke());
    }

    /// <summary>
    /// 中心から丸く広がっていく（リスポーン演出）
    /// </summary>
    public Tween FadeIn(System.Action onComplete = null)
    {
        SetRadius(0f);
        return DOTween.To(() => _material.GetFloat(RadiusID), SetRadius, openRadius, fadeDuration)
            .SetEase(Ease.InOutQuad)
            .OnComplete(() => onComplete?.Invoke());
    }
}