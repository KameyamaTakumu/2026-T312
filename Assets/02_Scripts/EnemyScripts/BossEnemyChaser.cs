using System.Collections;
using UnityEngine;

/// <summary>
/// マリオギャラクシー風 追跡＋突進ボス
///
/// 主な役割：
/// ・未検知時は原点周辺を徘徊する（EnemyBase共通ロジック）
/// ・プレイヤーを検知すると追跡する
/// ・一定間隔で予備動作 → 突進を行う
/// ・突進中に岩（RockTag）へ衝突するとダメージを受ける
/// ・突進失敗後は硬直する
/// ・突進予備動作中は色を変えて警告する
/// ・被弾時は色をフラッシュさせる
/// </summary>
public class BossEnemyChaser : EnemyBase
{
    // ─────────────────────────────────────────
    // ステート定義
    // ─────────────────────────────────────────

    private enum BossState
    {
        Patrolling,  // 徘徊中（プレイヤー未検知）
        Chasing,     // 追跡中
        Telegraph,   // 突進予備動作
        Charging,    // 突進中
        Recovering   // 突進後の硬直
    }

    // ─────────────────────────────────────────
    // Inspector 設定
    // ─────────────────────────────────────────

    [Header("追跡設定")]

    [CustomLabel("追跡速度"), SerializeField]
    private float chaseSpeed = 4f;

    [CustomLabel("追跡時の旋回速度"), SerializeField]
    private float turnSpeed = 4f;

    [CustomLabel("プレイヤーとのこれ以上近づかない距離"), SerializeField]
    private float minChaseDistance = 2.5f;

    [Header("徘徊設定（Boss固有）")]

    // 徘徊時の移動速度
    // EnemyBase側の徘徊設定（半径・更新間隔など）と組み合わせて使う
    [CustomLabel("徘徊時の移動速度"), SerializeField]
    private float patrolSpeed = 2f;

    // 徘徊時の旋回速度
    [CustomLabel("徘徊時の旋回速度"), SerializeField]
    private float patrolTurnSpeed = 3f;

    [Header("突進設定")]

    [CustomLabel("突進間隔（秒）"), SerializeField]
    private float chargeInterval = 4f;

    [CustomLabel("突進予備動作の時間（秒）"), SerializeField]
    private float telegraphDuration = 0.8f;

    [CustomLabel("突進速度"), SerializeField]
    private float chargeSpeed = 14f;

    [CustomLabel("突進の最大持続時間（秒）"), SerializeField]
    private float chargeMaxDuration = 2f;

    [CustomLabel("突進が外れた時の硬直時間（秒）"), SerializeField]
    private float recoverDuration = 1.2f;

    [CustomLabel("岩に激突した時の硬直時間（秒）"), SerializeField]
    private float rockHitStunDuration = 1.5f;

    [Header("岩判定")]

    [CustomLabel("突進中にダメージを受ける岩のタグ"), SerializeField]
    private string rockTag = "Rock";

    [Header("色設定（予備動作の警告色）")]

    [CustomLabel("突進予備動作中の色"), SerializeField]
    private Color telegraphColor = new Color(1f, 0.3f, 0.3f, 1f);

    [CustomLabel("色変更の対象Renderer（未設定なら子孫から自動取得）"), SerializeField]
    private Renderer[] targetRenderers;

    [Header("被弾フラッシュ設定")]

    [CustomLabel("被弾時のフラッシュ色"), SerializeField]
    private Color damageFlashColor = Color.white;

    [CustomLabel("フラッシュの回数"), SerializeField]
    private int damageFlashCount = 3;

    [CustomLabel("フラッシュ1回あたりの点灯/消灯時間（秒）"), SerializeField]
    private float damageFlashInterval = 0.08f;

    [Header("参照")]

    [CustomLabel("プレイヤーのTransform（未設定なら自動検索）"), SerializeField]
    private Transform playerTransform;

    [SerializeField]
    private SceneObject gameClearScene;

    // ─────────────────────────────────────────
    // 内部状態
    // ─────────────────────────────────────────

    private Rigidbody _rb;

    // 初期状態は徘徊から開始する
    // （他の敵AIと同様、プレイヤーを検知するまでは追いかけない）
    private BossState _state = BossState.Patrolling;

    private float _chargeTimer = 0f;      // 次の突進までのタイマー
    private float _stateTimer = 0f;       // 各ステート内での経過時間
    private Vector3 _chargeDirection;     // 突進開始時に確定する方向

    // 色制御用
    private MaterialPropertyBlock _mpb;

    // Renderer × マテリアルスロットごとの「元の色」
    // （SkinnedMeshRendererのように複数マテリアルを持つ場合、
    //   スロットごとに色が違うのでインデックスごとに個別管理する）
    private Color[][] _originalColorsPerRenderer;

    // 現在「予備動作の警告色」を表示中かどうか
    // （被弾フラッシュの合間に戻す色を、これで切り替える）
    private bool _isTelegraphColorActive = false;

    private Coroutine _flashCoroutine;

    // Standard/Legacyシェーダー用と URP Lit系シェーダー用、両方のカラープロパティに対応する
    // （存在しない側のプロパティを書き込んでも、シェーダー側が無視するだけで害はない）
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");

    // ─────────────────────────────────────────
    // Unity ライフサイクル
    // ─────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();
        _rb = GetComponent<Rigidbody>();

        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                playerTransform = playerObj.transform;
        }

        InitializeColor();
    }

    private void FixedUpdate()
    {
        if (isDead) return;
        if (playerTransform == null) return;

        // ─────────────────────────────────────
        // Patrolling / Chasing は距離に応じて自動的に切り替える。
        // 突進系のステート（Telegraph/Charging/Recovering）は
        // 開始したら距離に関わらず最後まで継続させる。
        // ─────────────────────────────────────
        if (_state == BossState.Patrolling || _state == BossState.Chasing)
        {
            float distance = Vector3.ProjectOnPlane(
                playerTransform.position - transform.position,
                transform.up
            ).magnitude;

            _state = distance <= chaseRange
                ? BossState.Chasing
                : BossState.Patrolling;
        }

        switch (_state)
        {
            case BossState.Patrolling:
                UpdatePatrolling();
                break;
            case BossState.Chasing:
                UpdateChasing();
                break;
            case BossState.Telegraph:
                UpdateTelegraph();
                break;
            case BossState.Charging:
                UpdateCharging();
                break;
            case BossState.Recovering:
                UpdateRecovering();
                break;
        }
    }

    // ─────────────────────────────────────────
    // 徘徊ステート（未検知時）
    // ─────────────────────────────────────────

    /// <summary>
    /// プレイヤー未検知時、原点周辺をランダムに徘徊する。
    /// EnemyBase.GetWanderDirection() が方向を、
    /// ここでは向き・移動速度への反映だけを行う。
    /// </summary>
    private void UpdatePatrolling()
    {
        Vector3 up = transform.up;

        if (!enableWander)
        {
            // 徘徊しない設定の場合は静止（重力方向の速度のみ保持）
            Vector3 idleVerticalVel = Vector3.Project(_rb.linearVelocity, up);
            _rb.linearVelocity = idleVerticalVel;
            return;
        }

        Vector3 dir = GetWanderDirection();

        if (dir.sqrMagnitude < 0.001f)
        {
            // 徘徊目標とほぼ同じ位置：静止
            Vector3 idleVerticalVel = Vector3.Project(_rb.linearVelocity, up);
            _rb.linearVelocity = idleVerticalVel;
            return;
        }

        // 向きを合わせる
        Quaternion targetRot = Quaternion.LookRotation(dir, up);
        _rb.MoveRotation(Quaternion.Slerp(_rb.rotation, targetRot, patrolTurnSpeed * Time.fixedDeltaTime));

        // 移動（重力方向の速度は保持）
        Vector3 verticalVel = Vector3.Project(_rb.linearVelocity, up);
        _rb.linearVelocity = dir * patrolSpeed + verticalVel;
    }

    // ─────────────────────────────────────────
    // 追跡ステート
    // ─────────────────────────────────────────

    private void UpdateChasing()
    {
        Vector3 up = transform.up;

        // プレイヤー方向を惑星表面の接平面に投影する
        Vector3 toPlayer = playerTransform.position - transform.position;
        Vector3 flatToPlayer = Vector3.ProjectOnPlane(toPlayer, up);
        float distance = flatToPlayer.magnitude;

        if (distance > minChaseDistance)
        {
            Vector3 moveDir = flatToPlayer.normalized;

            // 進行方向へ向きを合わせる
            Quaternion targetRot = Quaternion.LookRotation(moveDir, up);
            _rb.MoveRotation(Quaternion.Slerp(_rb.rotation, targetRot, turnSpeed * Time.fixedDeltaTime));

            // 前方へ移動（重力方向の速度は保持する）
            Vector3 verticalVel = Vector3.Project(_rb.linearVelocity, up);
            _rb.linearVelocity = moveDir * chaseSpeed + verticalVel;
        }
        else
        {
            // 近すぎる時は水平方向の速度だけ止める
            Vector3 verticalVel = Vector3.Project(_rb.linearVelocity, up);
            _rb.linearVelocity = verticalVel;
        }

        // 突進間隔タイマー
        _chargeTimer += Time.fixedDeltaTime;

        if (_chargeTimer >= chargeInterval && distance <= chaseRange)
        {
            _chargeTimer = 0f;
            EnterTelegraph();
        }
    }

    // ─────────────────────────────────────────
    // 突進予備動作ステート
    // ─────────────────────────────────────────

    private void EnterTelegraph()
    {
        _state = BossState.Telegraph;
        _stateTimer = 0f;

        // 水平方向の動きを止める（重力方向はそのまま）
        Vector3 verticalVel = Vector3.Project(_rb.linearVelocity, transform.up);
        _rb.linearVelocity = verticalVel;

        // 予備動作の警告色に変更
        _isTelegraphColorActive = true;
        ApplyOverrideColor(telegraphColor);
    }

    private void UpdateTelegraph()
    {
        _stateTimer += Time.fixedDeltaTime;

        if (_stateTimer >= telegraphDuration)
        {
            // 突進方向をここで確定する（開始後は追尾しない）
            Vector3 toPlayer = playerTransform.position - transform.position;
            Vector3 flatToPlayer = Vector3.ProjectOnPlane(toPlayer, transform.up);

            _chargeDirection = flatToPlayer.sqrMagnitude > 0.01f
                ? flatToPlayer.normalized
                : transform.forward;

            EnterCharging();
        }
    }

    // ─────────────────────────────────────────
    // 突進ステート
    // ─────────────────────────────────────────

    private void EnterCharging()
    {
        _state = BossState.Charging;
        _stateTimer = 0f;

        Vector3 up = transform.up;
        Vector3 verticalVel = Vector3.Project(_rb.linearVelocity, up);
        _rb.linearVelocity = _chargeDirection * chargeSpeed + verticalVel;

        Quaternion targetRot = Quaternion.LookRotation(_chargeDirection, up);
        _rb.MoveRotation(targetRot);

        // 予備動作の警告色を元に戻す（マテリアルスロットごとの元の色を復元）
        _isTelegraphColorActive = false;
        ApplyOriginalColors();
    }

    private void UpdateCharging()
    {
        _stateTimer += Time.fixedDeltaTime;

        Vector3 up = transform.up;

        // 突進方向を現在の重力面（接平面）に再投影して曲面に沿わせる
        Vector3 flatDir = Vector3.ProjectOnPlane(_chargeDirection, up);
        if (flatDir.sqrMagnitude > 0.0001f)
        {
            _chargeDirection = flatDir.normalized;
        }

        Vector3 verticalVel = Vector3.Project(_rb.linearVelocity, up);
        _rb.linearVelocity = _chargeDirection * chargeSpeed + verticalVel;

        // 姿勢も毎フレーム up に合わせて更新する
        Quaternion targetRot = Quaternion.LookRotation(_chargeDirection, up);
        _rb.MoveRotation(targetRot);

        if (_stateTimer >= chargeMaxDuration)
        {
            EnterRecovering(recoverDuration);
        }
    }

    // ─────────────────────────────────────────
    // 硬直ステート
    // ─────────────────────────────────────────

    private void EnterRecovering(float duration)
    {
        _state = BossState.Recovering;
        _stateTimer = 0f;

        Vector3 verticalVel = Vector3.Project(_rb.linearVelocity, transform.up);
        _rb.linearVelocity = verticalVel;

        StartCoroutine(RecoverCoroutine(duration));
    }

    private IEnumerator RecoverCoroutine(float duration)
    {
        yield return new WaitForSeconds(duration);

        if (!isDead)
        {
            // 硬直明けは追跡から再開
            // （距離がすでに離れていれば、次のFixedUpdateでPatrollingへ自動的に戻る）
            _state = BossState.Chasing;
            _stateTimer = 0f;
        }
    }

    private void UpdateRecovering()
    {
        // 硬直中は静止（時間経過は RecoverCoroutine が管理）
        Vector3 verticalVel = Vector3.Project(_rb.linearVelocity, transform.up);
        _rb.linearVelocity = verticalVel;
    }

    // ─────────────────────────────────────────
    // 衝突処理（岩への激突判定）
    // ─────────────────────────────────────────

    protected override void OnCollisionEnter(Collision collision)
    {
        // プレイヤー接触処理は EnemyBase 側に任せる
        base.OnCollisionEnter(collision);

        if (isDead) return;
        if (_state != BossState.Charging) return;

        if (collision.gameObject.CompareTag(rockTag))
        {
            // 岩に激突 → ダメージ
            TakeDamage(1);

            SE.Damage_Enemy.Play();

            EnterRecovering(rockHitStunDuration);
        }
    }

    protected override void OnDeath()
    {
        // 死亡時にフラッシュが動いていたら止めて色を戻す
        if (_flashCoroutine != null)
        {
            StopCoroutine(_flashCoroutine);
            _flashCoroutine = null;
        }
        _isTelegraphColorActive = false;
        ApplyOriginalColors();

        SE.EnemyDie.Play();

        if (ScreenFader.Instance != null)
        {
            // 丸く閉じる → 閉じきったらシーンリロード
            ScreenFader.Instance.FadeOut(SceneChange);
        }
        else
        {
            SceneChange();
        }
    }

    private void SceneChange()
    {
        gameClearScene.Load();
    }

    // ─────────────────────────────────────────
    // 色制御（予備動作の警告色 / 被弾フラッシュ）
    // ─────────────────────────────────────────

    /// <summary>
    /// 色変更に使うRendererとMaterialPropertyBlockを準備し、
    /// 各Renderer・各マテリアルスロットの現在の色を「元の色」として記憶する。
    ///
    /// SkinnedMeshRendererのように1つのRendererに複数マテリアル
    /// （体・目・歯など）がある場合、スロットごとに元の色が違うため、
    /// Rendererまるごとではなくスロット単位で保持する。
    /// </summary>
    private void InitializeColor()
    {
        _mpb = new MaterialPropertyBlock();

        if (targetRenderers == null || targetRenderers.Length == 0)
        {
            targetRenderers = GetComponentsInChildren<Renderer>();
        }

        int rendererCount = targetRenderers != null ? targetRenderers.Length : 0;
        _originalColorsPerRenderer = new Color[rendererCount][];

        for (int i = 0; i < rendererCount; i++)
        {
            Renderer r = targetRenderers[i];
            if (r == null)
            {
                _originalColorsPerRenderer[i] = new Color[0];
                continue;
            }

            Material[] mats = r.sharedMaterials;
            Color[] colors = new Color[mats.Length];

            for (int j = 0; j < mats.Length; j++)
            {
                colors[j] = GetMaterialColor(mats[j]);
            }

            _originalColorsPerRenderer[i] = colors;
        }
    }

    /// <summary>
    /// マテリアルの現在の色を取得する。
    /// URP Lit系（_BaseColor）とStandard/Legacy系（_Color）の両方に対応。
    /// </summary>
    private Color GetMaterialColor(Material mat)
    {
        if (mat == null) return Color.white;

        if (mat.HasProperty(BaseColorPropertyId))
        {
            return mat.GetColor(BaseColorPropertyId);
        }

        if (mat.HasProperty(ColorPropertyId))
        {
            return mat.GetColor(ColorPropertyId);
        }

        return Color.white;
    }

    /// <summary>
    /// 全Renderer・全マテリアルスロットを同じ色で上書きする。
    /// 予備動作の警告色や被弾フラッシュなど、「一時的に単色にしたい」時に使う。
    /// </summary>
    private void ApplyOverrideColor(Color color)
    {
        if (targetRenderers == null) return;

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            Renderer r = targetRenderers[i];
            if (r == null) continue;

            int slotCount = r.sharedMaterials.Length;

            for (int slot = 0; slot < slotCount; slot++)
            {
                r.GetPropertyBlock(_mpb, slot);

                // どちらのプロパティを使うシェーダーか分からないため両方に書き込む
                // （シェーダーが持っていない側は単に無視されるだけで害はない）
                _mpb.SetColor(ColorPropertyId, color);
                _mpb.SetColor(BaseColorPropertyId, color);

                r.SetPropertyBlock(_mpb, slot);
            }
        }
    }

    /// <summary>
    /// 全Renderer・全マテリアルスロットを、それぞれの元の色に個別に戻す。
    /// （体・目・歯など元々色が違うスロットも、正しくそれぞれの色に戻る）
    /// </summary>
    private void ApplyOriginalColors()
    {
        if (targetRenderers == null || _originalColorsPerRenderer == null) return;

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            Renderer r = targetRenderers[i];
            if (r == null) continue;

            Color[] colors = (i < _originalColorsPerRenderer.Length) ? _originalColorsPerRenderer[i] : null;
            if (colors == null) continue;

            for (int slot = 0; slot < colors.Length; slot++)
            {
                r.GetPropertyBlock(_mpb, slot);

                _mpb.SetColor(ColorPropertyId, colors[slot]);
                _mpb.SetColor(BaseColorPropertyId, colors[slot]);

                r.SetPropertyBlock(_mpb, slot);
            }
        }
    }

    /// <summary>
    /// フラッシュの合間に戻すべき色を反映する。
    /// 予備動作中ならその警告色（単色）に、そうでなければ各スロットの元の色に戻す。
    /// </summary>
    private void ApplyCurrentBaseColor()
    {
        if (_isTelegraphColorActive)
        {
            ApplyOverrideColor(telegraphColor);
        }
        else
        {
            ApplyOriginalColors();
        }
    }

    /// <summary>
    /// 被弾直後に呼ばれる（EnemyBase.TakeDamage内から呼び出されるフック）。
    /// TakeDamage自体をoverrideするより、こちらの方が意図した使い方に沿っている。
    ///
    /// 注意：EnemyBase.TakeDamage内ではこのフックの後にHP判定→Die()が行われるため、
    /// 致命傷の場合でもここが呼ばれた時点ではまだ isDead == false。
    /// 即死時にもフラッシュさせたくない場合は、呼び出し側で currentHp を見るなど
    /// 別途調整が必要（現状は致命傷でも一瞬フラッシュしてから死亡演出に入る）。
    /// </summary>
    protected override void OnDamaged(int amount)
    {
        base.OnDamaged(amount);

        StartDamageFlash();
    }

    private void StartDamageFlash()
    {
        if (_flashCoroutine != null)
        {
            StopCoroutine(_flashCoroutine);
        }

        _flashCoroutine = StartCoroutine(DamageFlashCoroutine());
    }

    private IEnumerator DamageFlashCoroutine()
    {
        for (int i = 0; i < damageFlashCount; i++)
        {
            ApplyOverrideColor(damageFlashColor);
            yield return new WaitForSeconds(damageFlashInterval);

            // フラッシュの合間は「今の基準状態」
            // （通常時＝各スロットの元の色 / 予備動作中＝警告色）に戻す
            ApplyCurrentBaseColor();
            yield return new WaitForSeconds(damageFlashInterval);
        }

        _flashCoroutine = null;
    }
}