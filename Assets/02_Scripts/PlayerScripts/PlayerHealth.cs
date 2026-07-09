using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// プレイヤーのHP管理クラス
/// 
/// 主な役割：
/// ・HPの保持
/// ・ダメージ処理
/// ・回復処理
/// ・無敵時間管理
/// ・死亡処理
/// ・HP UI更新
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    [Header("HP設定")]

    // プレイヤーの最大HP
    [CustomLabel("最大HP"), SerializeField]
    private int maxHp = 8;

    // ダメージを受けた後の無敵時間
    // この間は追加ダメージを受けない
    [CustomLabel("被ダメージ後の無敵時間（秒）"), SerializeField]
    private float invincibleDuration = 1.5f;

    [Header("UI")]

    // HP表示用 TextMeshPro
    [CustomLabel("HPテキスト（TMP）"), SerializeField]
    private TMP_Text hpText;

    // ─────────────────────────────────────────
    // 公開プロパティ
    // ─────────────────────────────────────────

    // 現在HP
    public int CurrentHp { get; private set; }

    // 無敵状態か
    public bool IsInvincible { get; private set; }

    // 死亡済みか
    public bool IsDead { get; private set; }

    // Rigidbody キャッシュ
    private Rigidbody rb;

    // ─────────────────────────────────────────
    // 初期化
    // ─────────────────────────────────────────

    private void Awake()
    {
        // Rigidbody取得
        rb = GetComponent<Rigidbody>();

        // 初期HP設定
        CurrentHp = maxHp;

        // UI更新
        UpdateUI();
    }

    // ─────────────────────────────────────────
    // 公開 API
    // ─────────────────────────────────────────

    /// <summary>
    /// ダメージを受ける
    /// 
    /// 無敵中または死亡済みなら無効
    /// </summary>
    public void TakeDamage(int amount)
    {
        // 死亡中 or 無敵中は受け付けない
        if (IsDead || IsInvincible)
            return;

        // HP減少
        CurrentHp -= amount;

        // 0未満にならないよう制限
        CurrentHp = Mathf.Max(CurrentHp, 0);

        SE.Damage_Player.Play();

        // UI更新
        UpdateUI();

        // HP0で死亡
        if (CurrentHp <= 0)
            Die();
        else
            StartCoroutine(InvincibleCoroutine());
    }

    /// <summary>
    /// 即死
    /// 無敵・HP に関わらず即座に死亡する
    /// </summary>
    public void InstantKill()
    {
        if (IsDead) return;
        CurrentHp = 0;
        UpdateUI();
        Die();
    }

    /// <summary>
    /// HP回復
    /// </summary>
    public void Heal(int amount)
    {
        // 死亡中は回復不可
        if (IsDead)
            return;

        // 最大HPを超えないよう制限
        CurrentHp = Mathf.Min(CurrentHp + amount, maxHp);

        // UI更新
        UpdateUI();
    }

    // ─────────────────────────────────────────
    // 内部処理
    // ─────────────────────────────────────────

    /// <summary>
    /// 死亡処理
    /// </summary>
    private void Die()
    {
        IsDead = true;

        // プレイヤー操作停止
        PlayerController ctrl =
            GetComponent<PlayerController>();

        if (ctrl != null)
            ctrl.enabled = false;

        if (ScreenFader.Instance != null)
        {
            // 丸く閉じる → 閉じきったらシーンリロード
            ScreenFader.Instance.FadeOut(ReloadScene);
        }
        else
        {
            ReloadScene();
        }

        Debug.Log("[PlayerHealth] プレイヤーが死亡しました");
    }

    /// <summary>
    /// 無敵時間処理
    /// 
    /// 一定時間ダメージを無効化し、
    /// 点滅演出を行う
    /// </summary>
    private IEnumerator InvincibleCoroutine()
    {
        // 無敵開始
        IsInvincible = true;

        // 子オブジェクト含む Renderer 取得
        Renderer[] renderers =
            GetComponentsInChildren<Renderer>();

        float elapsed = 0f;

        while (elapsed < invincibleDuration)
        {
            // 0.1秒ごとに表示ON/OFF
            bool visible =
                Mathf.FloorToInt(elapsed / 0.1f) % 2 == 0;

            // 全Renderer切り替え
            foreach (var r in renderers)
                r.enabled = visible;

            elapsed += Time.deltaTime;

            yield return null;
        }

        // 最後は必ず表示状態へ戻す
        foreach (var r in renderers)
            r.enabled = true;

        // 無敵終了
        IsInvincible = false;
    }

    /// <summary>
    /// HP表示更新
    /// </summary>
    private void UpdateUI()
    {
        // UI未設定対策
        if (hpText != null)
        {
            hpText.text =
                $"LIFE: {CurrentHp} / {maxHp}";
        }
    }

    private void ReloadScene()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }
}