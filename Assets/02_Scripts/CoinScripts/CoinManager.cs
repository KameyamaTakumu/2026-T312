using UnityEngine;
using TMPro;

/// <summary>
/// コイン枚数管理シングルトン
/// 
/// 主な役割：
/// ・現在所持コイン数の管理
/// ・UI 更新
/// ・コイン消費処理
/// </summary>
public class CoinManager : MonoBehaviour
{
    // シングルトンインスタンス
    public static CoinManager Instance { get; private set; }

    // 現在の所持コイン枚数
    [CustomLabel("現在のコイン枚数"), SerializeField]
    private int coinCount = 0;
    // プレイヤーが所持できる最大コイン数
    [CustomLabel("最大所持コイン数"), SerializeField]
    private int maxCoinCount = 99;

    // 所持枚数表示用 UI
    [CustomLabel("コイン枚数 UIテキスト（TMP）"), SerializeField]
    private TMP_Text coinText;

    // 外部から参照専用で取得可能
    public int CoinCount => coinCount;

    private void Awake()
    {
        // シングルトン重複防止
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // UI 初期更新
        UpdateUI();
    }

    /// <summary>
    /// コインを加算する
    /// 主にコイン回収時に呼ばれる
    /// </summary>
    public void AddCoins(int amount)
    {
        // 上限を超えないように加算
        coinCount = Mathf.Min(coinCount + amount, maxCoinCount);

        SE.CoinGet.Play();

        // UI 更新
        UpdateUI();
    }

    /// <summary>
    /// コイン消費
    /// 
    /// 残量不足なら false を返す
    /// 消費成功なら true
    /// </summary>
    public bool ConsumeCoins(int amount)
    {
        // 足りない場合は消費不可
        if (coinCount < amount)
            return false;

        // 消費
        coinCount -= amount;

        // UI 更新
        UpdateUI();

        return true;
    }

    /// <summary>
    /// コイン表示UI更新
    /// </summary>
    private void UpdateUI()
    {
        // UI 未設定対策
        if (coinText != null)
            coinText.text = $"× {coinCount:00}";
    }
}