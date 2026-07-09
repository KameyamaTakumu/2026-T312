using System.Collections;
using UnityEngine;

/// <summary>
/// プレイヤーのスピン攻撃を管理するコンポーネント
/// 
/// 主な役割：
/// ・スピン入力受付
/// ・スピン回転演出
/// ・敵への攻撃判定
/// ・壊せるオブジェクトへの攻撃
/// ・スピン中の状態管理
/// ・クールタイム管理
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerSpin : MonoBehaviour
{
    // ─────────────────────────────────────────
    // インスペクタ設定
    // ─────────────────────────────────────────

    [Header("スピン設定")]

    // スピン攻撃の継続時間
    // この時間が経過すると自動で終了する
    [CustomLabel("スピン継続時間（秒）"), SerializeField]
    private float spinDuration = 0.4f;

    // スピン終了後のクールタイム
    // この間は再度スピンできない
    [CustomLabel("スピンのクールタイム（秒）"), SerializeField]
    private float spinCooldown = 0.6f;

    // スピン攻撃の当たり判定半径
    // transform.position を中心に OverlapSphere を行う
    [CustomLabel("スピン攻撃の半径"), SerializeField]
    private float spinRadius = 1.5f;

    // スピン攻撃の当たり判定高さオフセット
    [CustomLabel("スピン攻撃の高さオフセット"), SerializeField]
    private float spinHeightOffset = 1.0f;

    // 敵へ与えるダメージ量
    [CustomLabel("スピンダメージ量"), SerializeField]
    private int spinDamage = 1;

    // スピン中の移動速度倍率
    //
    // 0   = 完全停止
    // 1   = 通常速度
    // 0.5 = 半分速度
    [CustomLabel("スピン中の移動速度倍率"), SerializeField]
    [Range(0f, 1f)]
    private float spinMoveMultiplier = 0.4f;

    [Header("演出設定")]

    // スピン中の回転速度
    // 単位は「度/秒」
    //
    // 1080 = 1秒間に3回転
    [CustomLabel("スピン回転速度（度/秒）"), SerializeField]
    private float spinRotateSpeed = 1080f;

    [Header("判定設定")]

    // スピン攻撃対象レイヤー
    // 不要なオブジェクトを判定しないために使用
    [CustomLabel("スピン攻撃対象レイヤー"), SerializeField]
    private LayerMask spinTargetLayer = ~0;

    // ─────────────────────────────────────────
    // 内部状態
    // ─────────────────────────────────────────

    // 現在スピン中か
    private bool isSpinning = false;

    // クールタイム中か
    private bool onCooldown = false;

    // スピン経過時間
    private float spinTimer = 0f;

    // Rigidbody キャッシュ
    private Rigidbody rb;

    private Animator anim;

    // ─────────────────────────────────────────
    // 公開プロパティ（PlayerController から参照）
    // ─────────────────────────────────────────

    /// <summary>
    /// 現在スピン中か
    /// </summary>
    public bool IsSpinning => isSpinning;

    /// <summary>
    /// スピン中の移動速度倍率
    /// 
    /// PlayerController 側から参照し、
    /// スピン中のみ移動速度を落とす用途で使う
    /// </summary>
    public float SpinMoveMultiplier =>
        isSpinning ? spinMoveMultiplier : 1f;

    // ─────────────────────────────────────────
    // Unity ライフサイクル
    // ─────────────────────────────────────────

    void Awake()
    {
        // Rigidbody キャッシュ
        rb = GetComponent<Rigidbody>();

        // Animator キャッシュ
        anim = GetComponent<Animator>();
    }

    void Update()
    {
        // スピン入力検知
        //
        // GetButtonDown は1フレームのみ true になるため、
        // 物理処理ではなく Update で取得する
        if(Input.GetKeyDown(KeyCode.LeftShift)
            && !isSpinning
            && !onCooldown)
        {
            StartSpin();
        }
    }

    void FixedUpdate()
    {
        // スピン中のみ処理
        if (!isSpinning) return;

        // ────────────────────────────────
        // スピン回転演出
        // ────────────────────────────────

        // transform.up を軸に回転
        //
        // 惑星重力環境でも、
        // プレイヤーの「上方向」を基準に回転できる
        rb.MoveRotation(
            rb.rotation * Quaternion.AngleAxis(
                spinRotateSpeed * Time.fixedDeltaTime,
                transform.up
            )
        );

        // ────────────────────────────────
        // スピン時間管理
        // ────────────────────────────────

        spinTimer += Time.fixedDeltaTime;

        // 指定時間経過で終了
        if (spinTimer >= spinDuration)
        {
            EndSpin();
        }
    }

    // ─────────────────────────────────────────
    // スピン制御
    // ─────────────────────────────────────────

    /// <summary>
    /// スピン開始
    /// </summary>
    private void StartSpin()
    {
        isSpinning = true;

        // タイマー初期化
        spinTimer = 0f;

        anim.SetTrigger("SpinTrigger");

        // チュートリアルへ通知
        TutorialManager.Instance?.NotifySpin();

        // スピン開始と同時に攻撃判定を行う
        PerformSpinAttack();

        SE.Spin.Play();
    }

    /// <summary>
    /// スピン終了
    /// </summary>
    private void EndSpin()
    {
        isSpinning = false;

        // クールタイム開始
        StartCoroutine(CooldownCoroutine());
    }

    /// <summary>
    /// スピン攻撃判定
    /// 
    /// OverlapSphere を使用して、
    /// 半径内に存在する対象を検索する
    /// </summary>
    private void PerformSpinAttack()
    {
        // 範囲内の Collider 一覧取得
        Collider[] hits = Physics.OverlapSphere(
            transform.position + Vector3.up * spinHeightOffset,
            spinRadius,
            spinTargetLayer,

            // Trigger は無視
            QueryTriggerInteraction.Ignore
        );

        foreach (Collider hit in hits)
        {
            // ────────────────────────────
            // 自分自身を除外
            // ────────────────────────────

            // プレイヤー本体や子オブジェクトに
            // 当たらないよう除外する
            if (hit.transform == transform
                || hit.transform.IsChildOf(transform))
            {
                continue;
            }

            // ────────────────────────────
            // 敵への処理
            // ────────────────────────────

            EnemyBase enemy =
                hit.GetComponent<EnemyBase>()
                ?? hit.GetComponentInParent<EnemyBase>();

            if (enemy != null)
            {
                // ダメージを与える
                enemy.TakeDamage(spinDamage);

                // Enemy として処理済みなので次へ
                continue;
            }

            // ────────────────────────────
            // 壊せるオブジェクトへの処理
            // ────────────────────────────

            SpinBreakable breakable =
                hit.GetComponent<SpinBreakable>()
                ?? hit.GetComponentInParent<SpinBreakable>();

            if (breakable != null)
            {
                // スピンヒット通知
                breakable.OnSpin(transform);
            }

            // ────────────────────────────
            // スピンスイッチへの処理
            // ────────────────────────────

            SpinSwitch spinSwitch =
                hit.GetComponent<SpinSwitch>()
                ?? hit.GetComponentInParent<SpinSwitch>();

            if (spinSwitch != null)
            {
                spinSwitch.OnSpin(transform);
            }
        }
    }

    /// <summary>
    /// クールタイム管理コルーチン
    /// </summary>
    private IEnumerator CooldownCoroutine()
    {
        onCooldown = true;

        // 指定秒待機
        yield return new WaitForSeconds(spinCooldown);

        onCooldown = false;
    }

    // ─────────────────────────────────────────
    // Gizmo
    // ─────────────────────────────────────────

#if UNITY_EDITOR

    /// <summary>
    /// SceneView 上でスピン攻撃範囲を表示
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // 半透明の塗りつぶし
        Gizmos.color =
            new Color(0.2f, 0.8f, 1f, 0.15f);

        Gizmos.DrawSphere(
            transform.position + Vector3.up * spinHeightOffset,
            spinRadius
        );

        // ワイヤーフレーム
        Gizmos.color =
            new Color(0.2f, 0.8f, 1f, 0.8f);

        Gizmos.DrawWireSphere(
            transform.position + Vector3.up * spinHeightOffset,
            spinRadius
        );
    }

#endif
}