using System.Collections;
using UnityEngine;

/// <summary>
/// プレイヤーのスピン攻撃を管理するコンポーネント。
///
/// 主な役割：
/// ・スピン入力受付
/// ・スピン回転演出
/// ・敵／破壊可能オブジェクト／スイッチへの攻撃判定
/// ・スピン中の状態管理とクールタイム管理
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerSpin : MonoBehaviour
{
    [Header("スピン設定")]

    // この時間が経過すると自動で終了する
    [CustomLabel("スピン継続時間（秒）"), SerializeField]
    private float spinDuration = 0.4f;

    // 終了後、この間は再度スピンできない
    [CustomLabel("スピンのクールタイム（秒）"), SerializeField]
    private float spinCooldown = 0.6f;

    // transform.position を中心にOverlapSphereで判定する
    [CustomLabel("スピン攻撃の半径"), SerializeField]
    private float spinRadius = 1.5f;

    [CustomLabel("スピン攻撃の高さオフセット"), SerializeField]
    private float spinHeightOffset = 1.0f;

    [CustomLabel("スピンダメージ量"), SerializeField]
    private int spinDamage = 1;

    // 0 = 完全停止、1 = 通常速度
    [CustomLabel("スピン中の移動速度倍率"), SerializeField]
    [Range(0f, 1f)]
    private float spinMoveMultiplier = 0.4f;

    [Header("演出設定")]

    // 度/秒。例：1080 = 1秒間に3回転
    [CustomLabel("スピン回転速度（度/秒）"), SerializeField]
    private float spinRotateSpeed = 1080f;

    [Header("判定設定")]

    [CustomLabel("スピン攻撃対象レイヤー"), SerializeField]
    private LayerMask spinTargetLayer = ~0;

    // ─────────────────────────────────────────
    // 内部状態
    // ─────────────────────────────────────────

    private bool isSpinning;
    private bool onCooldown;
    private float spinTimer;

    private Rigidbody rb;
    private Animator anim;

    // ─────────────────────────────────────────
    // 公開プロパティ（PlayerController から参照）
    // ─────────────────────────────────────────

    public bool IsSpinning => isSpinning;

    /// <summary>
    /// スピン中の移動速度倍率。PlayerController側でスピン中の移動速度を
    /// 落とす用途に使う（非スピン中は1を返す）
    /// </summary>
    public float SpinMoveMultiplier => isSpinning ? spinMoveMultiplier : 1f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        anim = GetComponent<Animator>();
    }

    private void Update()
    {
        // GetKeyDownは1フレームのみtrueになるため、物理処理ではなくUpdateで取得する
        bool spinKeyPressed = Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift);

        if (spinKeyPressed && !isSpinning && !onCooldown)
            StartSpin();
    }

    private void FixedUpdate()
    {
        if (!isSpinning)
            return;

        // 惑星重力環境でも、プレイヤーの「上方向」を基準に回転させる
        rb.MoveRotation(rb.rotation * Quaternion.AngleAxis(spinRotateSpeed * Time.fixedDeltaTime, transform.up));

        spinTimer += Time.fixedDeltaTime;
        if (spinTimer >= spinDuration)
            EndSpin();
    }

    private void StartSpin()
    {
        isSpinning = true;
        spinTimer = 0f;

        anim.SetTrigger("SpinTrigger");
        TutorialManager.Instance?.NotifySpin();

        // スピン開始と同時に攻撃判定を行う
        PerformSpinAttack();

        SE.Spin.Play();
    }

    private void EndSpin()
    {
        isSpinning = false;
        StartCoroutine(CooldownCoroutine());
    }

    /// <summary>
    /// スピン攻撃判定。OverlapSphereで半径内の対象を検索し、
    /// 敵／破壊可能オブジェクト／スイッチそれぞれに応じた処理を行う。
    /// </summary>
    private void PerformSpinAttack()
    {
        Vector3 center = transform.position + Vector3.up * spinHeightOffset;

        Collider[] hits = Physics.OverlapSphere(
            center,
            spinRadius,
            spinTargetLayer,
            QueryTriggerInteraction.Ignore);

        foreach (Collider hit in hits)
        {
            // プレイヤー本体・子オブジェクトは対象外
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
                continue;

            EnemyBase enemy = GetComponentInSelfOrParent<EnemyBase>(hit);
            if (enemy != null)
            {
                enemy.TakeDamage(spinDamage);
                continue;
            }

            SpinBreakable breakable = GetComponentInSelfOrParent<SpinBreakable>(hit);
            if (breakable != null)
                breakable.OnSpin(transform);

            SpinSwitch spinSwitch = GetComponentInSelfOrParent<SpinSwitch>(hit);
            if (spinSwitch != null)
                spinSwitch.OnSpin(transform);
        }
    }

    /// <summary>
    /// 対象のColliderが子オブジェクトに付与されているケースにも対応するため、
    /// 自身のGetComponentで見つからなければ親を辿って検索する。
    /// </summary>
    private static T GetComponentInSelfOrParent<T>(Component hit) where T : Component
    {
        return hit.GetComponent<T>() ?? hit.GetComponentInParent<T>();
    }

    private IEnumerator CooldownCoroutine()
    {
        onCooldown = true;
        yield return new WaitForSeconds(spinCooldown);
        onCooldown = false;
    }

#if UNITY_EDITOR
    /// <summary>
    /// SceneView上でスピン攻撃範囲を表示する。
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Vector3 center = transform.position + Vector3.up * spinHeightOffset;

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.15f);
        Gizmos.DrawSphere(center, spinRadius);

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f);
        Gizmos.DrawWireSphere(center, spinRadius);
    }
#endif
}