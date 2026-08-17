using System.Collections;
using UnityEngine;

/// <summary>
/// 重力制御コンポーネント
///
/// 主な役割：
/// ・通常時の重力制御
/// ・引力ジャンプの開始と飛行制御
/// ・GravityJumpZone のリレー移動
/// ・着地後の GroundedLock 管理
/// ・現在適用する GravityAttractor の管理
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class GravityBody : MonoBehaviour
{
    // ─────────────────────────────────────────
    // Inspector 設定
    // ─────────────────────────────────────────

    [Header("引力ジャンプ設定")]
    [CustomLabel("引力ジャンプのジャンプ力"), SerializeField]
    private float gravityJumpForce = 12f;

    // ─────────────────────────────────────────
    // 内部参照
    // ─────────────────────────────────────────

    private Rigidbody _rb;
    private GravityAttractor[] _attractors;

    // 現在重力を受けている惑星（GroundedLock 解除後の引き継ぎにも使用）
    private GravityAttractor _currentAttractor;

    // ─────────────────────────────────────────
    // 引力ジャンプ状態
    // ─────────────────────────────────────────

    // 現在向かっている（または侵入している）ゾーン
    private GravityJumpZone _currentZone;

    // 引力ジャンプ飛行中か
    private bool _isBeingAttracted = false;

    // GroundedLock 中か（終点着地直後の誤判定防止）
    private bool _isGroundedLocked = false;

    // GroundedLock 中に使用する強制惑星
    private GravityAttractor _forcedAttractor = null;

    [SerializeField] private bool controlsBGM = false; // Inspectorでプレイヤーのみtrueに
    private GravityAttractor _lastBGMAttractor; // 直前にBGMを鳴らした惑星
    [SerializeField] private bool controlsRespawn = false;

    // 到着判定（区間とゾーン球の交差判定）に使う、直前フレームの位置
    private Vector3 _prevAttractedPos;

    // ─────────────────────────────────────────
    // 公開プロパティ
    // ─────────────────────────────────────────

    public bool IsBeingAttracted => _isBeingAttracted;

    /// <summary>
    /// 現在プレイヤーが重力を受けている惑星。
    /// 惑星ごとにカメラ角度などを変えたい場合、PlayerController側から参照する。
    /// </summary>
    public GravityAttractor CurrentAttractor => _currentAttractor;

    /// <summary>
    /// PlayerController が毎 FixedUpdate で設定する接地フラグ。
    /// 接地中はゾーン侵入登録を無視して、歩行中に意図せず
    /// ジャンプが発動するのを防ぐ。
    /// </summary>
    public bool IsGrounded { get; set; }

    // ─────────────────────────────────────────
    // Unity ライフサイクル
    // ─────────────────────────────────────────

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity = false;
        _rb.constraints = RigidbodyConstraints.FreezeRotation;
    }

    private void Start()
    {
        _attractors = Object.FindObjectsByType<GravityAttractor>();
    }

    private void FixedUpdate()
    {
        if (_isBeingAttracted)
            UpdateAttractedMovement();
        else
            UpdateGravity();
    }

    // ─────────────────────────────────────────
    // 重力処理
    // ─────────────────────────────────────────

    private void UpdateGravity()
    {
        if (_attractors == null || _attractors.Length == 0) return;

        GravityAttractor target;

        if (_isGroundedLocked && _forcedAttractor != null)
        {
            // GroundedLock 中は終点ゾーンで指定された惑星を強制使用
            target = _forcedAttractor;
        }
        else if (_currentAttractor != null && _isGroundedLocked == false
                 && _forcedAttractor == null)
        {
            // GroundedLock 解除直後はcurrentAttractor に引き継いだ惑星を使う
            // GetNearestAttractor() が 1フレーム遅れで意図しない惑星を返すのを防ぐ
            // _currentAttractor は次の GetNearestAttractor() 呼び出しで上書きされる
            target = _currentAttractor;

            // 惑星間の重なり問題を避けるため、接地した惑星が引力圏にある間は
            // 近くても他の惑星を選ばないよう距離チェックで確認してから切り替え
            GravityAttractor nearest = GetNearestAttractor();
            if (nearest != null && nearest != _currentAttractor)
            {
                // 現在惑星と最寄り惑星の距離差が一定以上なら切り替える
                float distCurrent = Vector3.Distance(transform.position, _currentAttractor.transform.position);
                float distNearest = Vector3.Distance(transform.position, nearest.transform.position);
                if (distNearest < distCurrent * 0.7f)
                    target = nearest;
            }
        }
        else
        {
            target = GetNearestAttractor();
        }

        if (target != null)
        {
            _currentAttractor = target;
            target.Attract(_rb);

            // 惑星が変わった時だけBGMを鳴らす
            if (controlsBGM && _currentAttractor != _lastBGMAttractor)
            {
                _currentAttractor.PlayPlanetBGM();
                _lastBGMAttractor = _currentAttractor;
            }

            if (controlsRespawn && RespawnManager.Instance != null)
                RespawnManager.Instance.SetRespawnPoint(_currentAttractor.RespawnPoint);
        }
    }

    // ─────────────────────────────────────────
    // 引力ジャンプ飛行中処理
    // ─────────────────────────────────────────

    private void UpdateAttractedMovement()
    {
        if (_currentZone == null)
        {
            // 飛行キャンセル
            _isBeingAttracted = false;
            return;
        }

        //Vector3 toZone = _currentZone.transform.position - _rb.position;
        //float distance = toZone.magnitude;
        Vector3 zoneCenter = _currentZone.transform.position;
        Vector3 currentPos = _rb.position;

        // ── 到着判定 ──
        if (SegmentIntersectsSphere(_prevAttractedPos, currentPos, zoneCenter, _currentZone.ArrivalDistance))
        {
            OnArrivedAtZone();
            return;
        }

        // ── ゾーン中心へ加速 ──
        Vector3 toZone = zoneCenter - currentPos;
        Vector3 attractDir = toZone.normalized;
        _rb.AddForce(attractDir * _currentZone.AttractForce);

        // 速度上限
        if (_rb.linearVelocity.magnitude > _currentZone.MaxAttractSpeed)
            _rb.linearVelocity = _rb.linearVelocity.normalized * _currentZone.MaxAttractSpeed;

        // 進行方向へ向きを合わせる
        if (toZone.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(attractDir, transform.up);
            _rb.MoveRotation(Quaternion.Slerp(_rb.rotation, targetRot, 5f * Time.fixedDeltaTime));
        }

        _prevAttractedPos = currentPos;
    }

    // ─────────────────────────────────────────
    // ゾーン到着処理
    // ─────────────────────────────────────────

    private void OnArrivedAtZone()
    {
        GravityJumpZone arrived = _currentZone;
        GravityJumpZone next = arrived.NextZone;

        Debug.Log($"引力ゾーンに到着：{arrived.gameObject.name}。次ゾーン：{(next != null ? next.gameObject.name : "なし（終点）")}");

        if (next != null)
        {
            // ── 中継ゾーン：飛行を止めずに次ゾーンへ切り替える ──
            // 速度は引き継ぎ（急ブレーキなし）次ゾーンの引力で自然に加速する
            _currentZone = next;
            _prevAttractedPos = _rb.position;
            Debug.Log($"リレー継続 → {next.gameObject.name}");
        }
        else
        {
            // ── 終点ゾーン：惑星へ着地させる ──
            _isBeingAttracted = false;
            _currentZone = null;

            // 着地先惑星を決定
            // 終点ゾーンで指定された惑星を優先して使用する
            _forcedAttractor = arrived.TargetPlanet != null
                ? arrived.TargetPlanet
                : GetNearestAttractor();

            if (_forcedAttractor != null)
            {
                // 惑星方向へ初速を与えて飛ばす
                // GravityAttractor の引力と合わさって自然に引き寄せられる
                Vector3 toPlanet = (_forcedAttractor.transform.position - _rb.position).normalized;
                _rb.linearVelocity = toPlanet * gravityJumpForce;
                Debug.Log($"終点到着。惑星 {_forcedAttractor.gameObject.name} へ発射");
            }
            else
            {
                _rb.linearVelocity = Vector3.zero;
            }

            // GroundedLock 開始（着地後の誤発動防止）
            StartCoroutine(GroundedLockCoroutine(arrived.GroundedLockDuration));
        }
    }

    // ─────────────────────────────────────────
    // GroundedLock（終点着地後の保護期間）
    // ─────────────────────────────────────────

    private IEnumerator GroundedLockCoroutine(float duration)
    {
        _isGroundedLocked = true;
        yield return new WaitForSeconds(duration);
        _isGroundedLocked = false;

        // 解除前に強制惑星を _currentAttractor に引き継ぐ
        if (_forcedAttractor != null)
        {
            _currentAttractor = _forcedAttractor;
            _forcedAttractor = null;
        }

        // GroundedLock 解除後、プレイヤーがゾーン内に立っている場合
        // OnTriggerEnter は再発火しないため、Overlap で現在地のゾーンを取得する
        // これにより解除後すぐにジャンプ入力しても _currentZone が null にならない
        if (_currentZone == null)
        {
            Collider[] hits = Physics.OverlapSphere(_rb.position, 0.1f);
            foreach (var col in hits)
            {
                GravityJumpZone z = col.GetComponent<GravityJumpZone>();
                if (z != null)
                {
                    _currentZone = z;
                    Debug.Log($"惑星固定解除。ゾーン再取得：{z.gameObject.name}");
                    break;
                }
            }
        }

        if (_currentZone == null)
            Debug.Log("惑星固定解除。通常重力に復帰");
    }

    // ─────────────────────────────────────────
    // 引力ジャンプ（PlayerController から呼ぶ）
    // ─────────────────────────────────────────

    public bool TryGravityJump()
    {
        if (_currentZone == null) return false;
        if (_isGroundedLocked) return false;
        if (_isBeingAttracted) return false;

        _isBeingAttracted = true;
        Vector3 dir = (_currentZone.transform.position - _rb.position).normalized;
        _rb.linearVelocity = dir * gravityJumpForce;
        _prevAttractedPos = _rb.position;

        Debug.Log($"引力ジャンプ開始：{_currentZone.gameObject.name}");
        return true;
    }

    // ─────────────────────────────────────────
    // ゾーン検知コールバック（GravityJumpZone から呼ばれる）
    // ─────────────────────────────────────────

    public void OnEnterGravityJumpZone(GravityJumpZone zone)
    {
        // GroundedLock 中は完全無視
        if (_isGroundedLocked) return;

        if (_isBeingAttracted)
        {
            // 飛行中：現在向かっているゾーンの NextZone に一致する場合のみリレー切替
            if (_currentZone != null && zone == _currentZone.NextZone)
            {
                _currentZone = zone;
                Debug.Log($"TriggerEnter リレー切替 → {zone.gameObject.name}");
            }
            return;
        }

        // 通常状態：侵入ゾーンを登録
        _currentZone = zone;
        Debug.Log($"引力ゾーン侵入：{zone.gameObject.name}");
    }

    public void OnExitGravityJumpZone(GravityJumpZone zone)
    {
        // 飛行中は Exit を無視（飛行ルートで複数ゾーンをまたぐため）
        if (_isBeingAttracted) return;

        if (_currentZone == zone)
        {
            _currentZone = null;
            Debug.Log($"引力ゾーン退出：{zone.gameObject.name}");
        }
    }

    // ─────────────────────────────────────────
    // ヘルパー
    // ─────────────────────────────────────────

    private GravityAttractor GetNearestAttractor()
    {
        GravityAttractor nearest = null;
        float minDist = float.MaxValue;
        foreach (var a in _attractors)
        {
            float d = Vector3.Distance(transform.position, a.transform.position);
            if (d < minDist) { minDist = d; nearest = a; }
        }
        return nearest;
    }

    public void ForceSyncGravity()
    {
        if (_attractors == null || _attractors.Length == 0)
            _attractors = Object.FindObjectsByType<GravityAttractor>();

        GravityAttractor nearest = GetNearestAttractor();
        if (nearest != null)
        {
            _currentAttractor = nearest;
            nearest.Attract(_rb);
        }
    }

    /// <summary>
    /// 移動区間 p0→p1 が、中心 center・半径 radius の球と交差するかどうかを判定する。
    /// 区間上でcenterに最も近づく点を内積で求め、その点との距離で判定することで、
    /// 1フレームの移動量が大きい場合でも交差の見落としを防ぐ。
    /// </summary>
    private bool SegmentIntersectsSphere(Vector3 p0, Vector3 p1, Vector3 center, float radius)
    {
        Vector3 seg = p1 - p0;
        float segLenSqr = seg.sqrMagnitude;

        if (segLenSqr < 1e-6f)
            return (p0 - center).sqrMagnitude <= radius * radius;

        float t = Mathf.Clamp01(Vector3.Dot(center - p0, seg) / segLenSqr);
        Vector3 closest = p0 + seg * t;
        return (closest - center).sqrMagnitude <= radius * radius;
    }
}