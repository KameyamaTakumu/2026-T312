using UnityEngine;

/// <summary>
/// コインコンポーネント
/// プレイヤーが回収できるコインの挙動を管理する
/// 
/// 主な役割：
/// ・自転演出
/// ・プレイヤーへの引き寄せ
/// ・回収判定
/// ・CoinManager への加算通知
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Coin : MonoBehaviour
{
    [Header("コイン設定")]

    // このコインを取得した時に増える枚数
    [CustomLabel("コイン価値"), SerializeField]
    private int value = 1;

    // プレイヤーへ吸い寄せられる速度
    [CustomLabel("ホバー引き寄せ速度"), SerializeField]
    private float attractSpeed = 12f;

    // プレイヤーとの距離がこの値以下になったら回収扱いになる
    [CustomLabel("回収判定距離（プレイヤーとの）"), SerializeField]
    private float collectDistance = 0.8f;

    // コインの自転速度
    [CustomLabel("自転速度（deg/s）"), SerializeField]
    private float spinSpeed = 180f;

    /// <summary>
    /// コインの現在状態
    /// Idle       : 通常状態
    /// Attracting : プレイヤーへ引き寄せ中
    /// Collected  : 回収済み（削除待ち）
    /// </summary>
    public enum CoinState
    {
        Idle,
        Attracting,
        Collected
    }

    // 現在状態（外部からは参照のみ可能）
    public CoinState State { get; private set; } = CoinState.Idle;

    // 引き寄せ先のプレイヤー
    private Transform playerTransform;

    // Rigidbody キャッシュ
    private Rigidbody rb;

    // 惑星重力制御用
    // 引き寄せ中は一時的に OFF にする
    private GravityBody gravityBody;

    private void Awake()
    {
        // 毎回 GetComponent を呼ばないようにキャッシュ
        rb = GetComponent<Rigidbody>();
        gravityBody = GetComponent<GravityBody>();
    }

    private void Update()
    {
        // 見た目用の自転
        // Space.Self にすることで自身のローカル軸で回転する
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.Self);
    }

    private void FixedUpdate()
    {
        // Rigidbody を扱う処理は FixedUpdate で行う
        if (State == CoinState.Attracting)
            FixedUpdate_Attracting();
    }

    /// <summary>
    /// プレイヤーへ吸い寄せられている時の処理
    /// </summary>
    private void FixedUpdate_Attracting()
    {
        // プレイヤー参照が消えていたら通常状態へ戻す
        if (playerTransform == null)
        {
            SetIdle();
            return;
        }

        // プレイヤー方向ベクトル
        Vector3 dir = playerTransform.position - rb.position;

        // プレイヤーまでの距離
        float dist = dir.magnitude;

        // 一定距離まで近づいたら回収
        if (dist < collectDistance)
        {
            Collect();
            return;
        }

        // プレイヤー方向へ速度を与える
        // normalized によって方向だけを取り出している
        rb.linearVelocity = dir.normalized * attractSpeed;
    }

    // ─────────────────────────────────────────
    // 公開メソッド
    // ─────────────────────────────────────────

    /// <summary>
    /// ポインターがコインをホバーした時に呼ばれる
    /// プレイヤーへの引き寄せ開始
    /// </summary>
    public void StartAttracting(Transform playerTransform)
    {
        // 既に回収済みなら無視
        if (State == CoinState.Collected)
            return;

        this.playerTransform = playerTransform;
        State = CoinState.Attracting;

        // 惑星重力を止める
        // 重力と引き寄せが競合しないようにするため
        if (gravityBody != null)
            gravityBody.enabled = false;

        // 現在速度をリセット
        // これをしないと慣性で変な方向へ動くことがある
        rb.linearVelocity = Vector3.zero;
    }

    /// <summary>
    /// ホバー解除時に通常状態へ戻す
    /// </summary>
    public void SetIdle()
    {
        // 回収済みなら変更不要
        if (State == CoinState.Collected)
            return;

        State = CoinState.Idle;

        // プレイヤー参照解除
        playerTransform = null;

        // 惑星重力を再開
        if (gravityBody != null)
            gravityBody.enabled = true;
    }

    /// <summary>
    /// コイン回収処理
    /// </summary>
    public void Collect()
    {
        // 二重回収防止
        if (State == CoinState.Collected)
            return;

        State = CoinState.Collected;

        // CoinManager に枚数加算
        // ?. を使うことで Instance が null の時でもエラーにならない
        CoinManager.Instance?.AddCoins(value);

        // コイン削除
        Destroy(gameObject);
    }

    /// <summary>
    /// コイン価値取得
    /// </summary>
    public int GetValue() => value;

    private void OnTriggerEnter(Collider other)
    {
        // 回収済みなら処理不要
        if (State == CoinState.Collected)
            return;

        // プレイヤー接触で回収
        if (other.CompareTag("Player"))
            Collect();
    }
}