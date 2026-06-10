using System.Collections;
using UnityEngine;

/// <summary>
/// 重力制御コンポーネント
///
/// 主な役割：
/// ・最寄り惑星の重力適用
/// ・引力ジャンプ制御
/// ・引力ゾーン管理
/// ・惑星固定処理
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class GravityBody : MonoBehaviour
{
    // ─────────────────────────────────────────
    // 引力ジャンプ設定
    // ─────────────────────────────────────────

    [Header("引力ジャンプ設定")]

    /// <summary>
    /// 引力ジャンプ開始時の初速度
    /// ゾーン中心へ向かって発射される
    /// </summary>
    [CustomLabel("引力ジャンプのジャンプ力"), SerializeField]
    private float gravityJumpForce = 12f;

    // ─────────────────────────────────────────
    // 内部参照
    // ─────────────────────────────────────────

    /// <summary>
    /// 現在重力を受けている惑星
    /// </summary>
    private GravityAttractor _currentAttractor;

    /// <summary>
    /// Rigidbody キャッシュ
    /// </summary>
    private Rigidbody _rb;

    /// <summary>
    /// シーン内の全惑星
    /// </summary>
    private GravityAttractor[] _attractors;

    // ─────────────────────────────────────────
    // 引力ジャンプ状態
    // ─────────────────────────────────────────

    /// <summary>
    /// 現在入っている引力ゾーン
    /// </summary>
    private GravityJumpZone _currentZone;

    /// <summary>
    /// 引力ジャンプ中か
    /// </summary>
    private bool _isBeingAttracted = false;

    /// <summary>
    /// 惑星固定中か
    /// 到着直後の重力誤判定防止用
    /// </summary>
    private bool _isGroundedLocked = false;

    /// <summary>
    /// 強制的に使用する惑星
    /// 到着後の惑星固定に使用
    /// </summary>
    private GravityAttractor _forcedAttractor = null;

    // ─────────────────────────────────────────
    // 公開プロパティ
    // ─────────────────────────────────────────

    /// <summary>
    /// 引力ジャンプ中か
    /// </summary>
    public bool IsBeingAttracted => _isBeingAttracted;

    // ─────────────────────────────────────────

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();

        // Unity標準重力は使用しない
        _rb.useGravity = false;

        // 回転は GravityAttractor 側で制御するため固定
        _rb.constraints =
            RigidbodyConstraints.FreezeRotation;
    }

    private void Start()
    {
        // シーン内の全惑星取得
        _attractors =
            FindObjectsByType<GravityAttractor>(
                FindObjectsSortMode.None);
    }

    private void FixedUpdate()
    {
        // 引力ジャンプ中なら専用処理
        if (_isBeingAttracted)
            UpdateAttractedMovement();
        else
            UpdateNormalGravity();
    }

    // ─────────────────────────────────────────
    // 通常重力
    // ─────────────────────────────────────────

    /// <summary>
    /// 通常時の重力処理
    /// </summary>
    private void UpdateNormalGravity()
    {
        if (_attractors == null ||
            _attractors.Length == 0)
            return;

        // 惑星固定中は強制惑星を使用
        GravityAttractor target =
            (_isGroundedLocked &&
             _forcedAttractor != null)
            ? _forcedAttractor
            : GetNearestAttractor();

        if (target != null)
        {
            _currentAttractor = target;

            // 重力適用
            target.Attract(_rb);
        }
    }

    // ─────────────────────────────────────────
    // 引力ジャンプ中処理
    // ─────────────────────────────────────────

    /// <summary>
    /// ゾーンへ引き寄せられている間の処理
    /// </summary>
    private void UpdateAttractedMovement()
    {
        if (_currentZone == null)
        {
            CancelAttraction();
            return;
        }

        // 現在位置からゾーン中心へ向かうベクトル
        // どちらの方向に飛べばよいかを表す
        Vector3 toZone =
            _currentZone.transform.position -
            _rb.position;

        // ゾーン中心までの直線距離
        // 到着判定に使用する
        float distance = toZone.magnitude;

        // 到着判定
        if (distance <= _currentZone.ArrivalDistance)
        {
            OnArrivedAtZone();
            return;
        }

        // ゾーン中心方向
        Vector3 attractDir =
            toZone.normalized;

        // ゾーン中心方向へ加速
        _rb.AddForce(
            attractDir *
            _currentZone.AttractForce);

        // 最大速度制限
        Vector3 vel = _rb.linearVelocity;

        // 速度が上限を超えた場合は強制的に制限する
        // ゾーンが遠い場合に異常な速度になるのを防ぐ
        if (vel.magnitude >
            _currentZone.MaxAttractSpeed)
        {
            _rb.linearVelocity =
                vel.normalized *
                _currentZone.MaxAttractSpeed;
        }

        // 飛行方向へ向きを合わせる
        if (toZone.sqrMagnitude > 0.01f)
        {
            // 進行方向を前方として回転情報を作成
            // attractDir = 前方向
            // transform.up = 上方向
            // 惑星重力で維持されている上方向を保持したまま飛行方向へ頭部を向ける
            Quaternion targetRot =
                Quaternion.LookRotation(
                    attractDir,
                    transform.up);

            // 現在の向きから目標方向へ徐々に回転
            // Slerpを使うことで一瞬で向きが変わるのではなく滑らかに旋回する
            _rb.MoveRotation(
                Quaternion.Slerp(
                    _rb.rotation,
                    targetRot,
                    5f * Time.fixedDeltaTime));
        }
    }

    // ─────────────────────────────────────────
    // 到着処理
    // ─────────────────────────────────────────

    private void OnArrivedAtZone()
    {
        _isBeingAttracted = false;
        _rb.linearVelocity = Vector3.zero;

        // ゾーンに設定された惑星を強制引力先として記憶する
        // null の場合は現時点の最寄り惑星にフォールバック
        _forcedAttractor = _currentZone.TargetPlanet != null
            ? _currentZone.TargetPlanet
            : GetNearestAttractor();

        StartCoroutine(GroundedLockCoroutine(_currentZone.GroundedLockDuration));

        Debug.Log($"引力ゾーンに到着。強制惑星：{_forcedAttractor?.gameObject.name}");
    }

    private IEnumerator GroundedLockCoroutine(float duration)
    {
        _isGroundedLocked = true;
        yield return new WaitForSeconds(duration);
        _isGroundedLocked = false;
        _forcedAttractor = null; // 強制惑星を解放し通常の最寄り判定に戻す

        // ロック解除時点でプレイヤーが物理的に重なっているゾーンを再設定する
        // これをしないと「到着したゾーン」が残り続けて逆方向に飛んでしまう
        _currentZone = FindZoneAtCurrentPosition();

        if (_currentZone != null)
            Debug.Log($"惑星固定解除。現在のゾーン：{_currentZone.gameObject.name}");
        else
            Debug.Log("惑星固定解除。通常重力に復帰");
    }

    private void CancelAttraction()
    {
        _isBeingAttracted = false;
        _currentZone = null;
    }

    // ─────────────────────────────────────────
    // 引力ジャンプ（PlayerController から呼ぶ）
    // ─────────────────────────────────────────

    public bool TryGravityJump()
    {
        if (_currentZone == null) return false;
        if (_isGroundedLocked) return false;

        // すでに引き寄せ中 → 次のゾーンへ切り替え
        if (_isBeingAttracted)
        {
            GravityJumpZone next = FindNextZone();
            if (next != null && next != _currentZone)
            {
                _currentZone = next;
                Debug.Log($"次の引力ゾーンへ移動：{next.gameObject.name}");
            }
            else
            {
                CancelAttraction();
                Debug.Log("次の引力ゾーンなし。通常重力に復帰");
            }
            return true;
        }

        // 通常状態 → 引き寄せ開始
        _isBeingAttracted = true;
        Vector3 dir = (_currentZone.transform.position - _rb.position).normalized;
        _rb.linearVelocity = dir * gravityJumpForce;

        Debug.Log($"引力ジャンプ開始：{_currentZone.gameObject.name}");
        return true;
    }

    // ─────────────────────────────────────────
    // ゾーン検知コールバック（GravityJumpZone から呼ばれる）
    // ─────────────────────────────────────────

    public void OnEnterGravityJumpZone(GravityJumpZone zone)
    {
        if (_isGroundedLocked) return;
        _currentZone = zone;
        Debug.Log($"引力ゾーン侵入：{zone.gameObject.name}");
    }

    public void OnExitGravityJumpZone(GravityJumpZone zone)
    {
        if (_currentZone == zone && !_isBeingAttracted)
        {
            _currentZone = null;
            Debug.Log($"引力ゾーン退出：{zone.gameObject.name}");
        }
    }

    // ─────────────────────────────────────────
    // ヘルパーメソッド
    // ─────────────────────────────────────────

    private GravityAttractor GetNearestAttractor()
    {
        GravityAttractor nearest = null;

        // 最小距離比較用変数
        float minDist = float.MaxValue;

        foreach (var attractor in _attractors)
        {
            float dist = Vector3.Distance(transform.position, attractor.transform.position);
            if (dist < minDist) { minDist = dist; nearest = attractor; }
        }
        return nearest;
    }

    /// <summary>
    /// 現在ゾーン以外で最寄りの GravityJumpZone を返す
    /// </summary>
    private GravityJumpZone FindNextZone()
    {
        GravityJumpZone[] zones = FindObjectsByType<GravityJumpZone>(FindObjectsSortMode.None);
        GravityJumpZone nearest = null;
        float minDist = float.MaxValue;

        foreach (var z in zones)
        {
            if (z == _currentZone) continue;
            float d = Vector3.Distance(transform.position, z.transform.position);
            if (d < minDist) { minDist = d; nearest = z; }
        }
        return nearest;
    }

    /// <summary>
    /// 現在プレイヤーが物理的に重なっているゾーンを返す。
    /// GroundedLock 解除後の _currentZone 再設定に使用する。
    /// </summary>
    private GravityJumpZone FindZoneAtCurrentPosition()
    {
        GravityJumpZone[] zones = FindObjectsByType<GravityJumpZone>(FindObjectsSortMode.None);

        foreach (var z in zones)
        {
            SphereCollider col = z.GetComponent<SphereCollider>();
            if (col == null) continue;

            // ワールドスケールを考慮した半径
            float radius = col.radius * Mathf.Max(
                z.transform.lossyScale.x,
                z.transform.lossyScale.y,
                z.transform.lossyScale.z);

            float dist = Vector3.Distance(_rb.position, z.transform.position);
            if (dist <= radius) return z;
        }
        return null;
    }
}