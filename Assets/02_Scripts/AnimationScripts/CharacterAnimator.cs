using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor.Animations;
#endif

/// <summary>
/// キャラクターアニメーション管理コンポーネント
///
/// 主な役割：
/// ・Animator の初期化
/// ・AnimatorController の生成
/// ・ParameterDriver の実行
/// ・アニメーションパラメータ更新
///
/// このクラス自身は
/// 「どのアニメーションを再生するか」
/// を直接判断しない。
///
/// 実際の判定処理は
/// AnimatorConfig に登録された
/// ParameterDriver が担当する。
///
/// そのため新しいアニメーションを追加する際は
/// 基本的にコードを書き換える必要はなく、
/// Config の設定だけで対応できる。
/// </summary>
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Rigidbody))]
public class CharacterAnimator : MonoBehaviour
{
    // ─────────────────────────────────────────
    // インスペクター設定
    // ─────────────────────────────────────────

    [Header("設定アセット")]

    /// <summary>
    /// アニメーション設定アセット
    ///
    /// ステート定義
    /// パラメータ定義
    /// ドライバー定義
    ///
    /// などが格納されている。
    /// </summary>
    [Tooltip("PlayerAnimatorConfig アセットをここにセットする")]
    [SerializeField]
    private AnimatorConfig config;

    [Header("移動ブレンド速度")]

    /// <summary>
    /// Floatパラメータ補間速度
    ///
    /// Idle → Run
    /// Walk → Run
    ///
    /// のようなブレンド時に使用される。
    ///
    /// 値が大きいほど
    /// パラメータが素早く変化する。
    /// </summary>
    [Tooltip("Idle ↔ Run などの切り替えが何秒で完了するかの目安（大きいほど素早い）")]
    [SerializeField]
    private float moveBlendSpeed = 8f;

    // ─────────────────────────────────────────
    // 内部参照
    // ─────────────────────────────────────────

    /// <summary>
    /// Animator キャッシュ
    ///
    /// 毎フレーム GetComponent を呼ばないために保持。
    /// </summary>
    private Animator _anim;

    /// <summary>
    /// Rigidbody キャッシュ
    ///
    /// ドライバーが速度などを取得するため使用。
    /// </summary>
    private Rigidbody _rb;

    // ─────────────────────────────────────────
    // パラメータキャッシュ
    // ─────────────────────────────────────────

    /// <summary>
    /// Floatパラメータの前回値
    ///
    /// ブレンド補間に使用する。
    ///
    /// 例：
    /// MoveSpeed
    /// 0 → 1 を瞬間変更せず、
    /// 徐々に変化させる。
    /// </summary>
    private readonly Dictionary<string, float> _floatCache = new();

    /// <summary>
    /// Boolパラメータの前回値
    ///
    /// false → true の変化検出などに使用する。
    /// </summary>
    private readonly Dictionary<string, bool> _boolCache = new();

    /// <summary>
    /// 今フレーム送信済み Trigger 一覧
    ///
    /// 同じTriggerが
    /// 複数Driverから送信されるのを防ぐ。
    /// </summary>
    private readonly HashSet<string> _triggersSent = new();

    // ─────────────────────────────────────────
    // Unity ライフサイクル
    // ─────────────────────────────────────────

    /// <summary>
    /// 初期化
    ///
    /// Animator と Rigidbody を取得し、
    /// Config の内容から AnimatorController を構築する。
    /// </summary>
    private void Awake()
    {
        _anim = GetComponent<Animator>();
        _rb = GetComponent<Rigidbody>();

        // Config未設定なら動作できない
        if (config == null)
        {
            Debug.LogWarning(
                "[PlayerAnimator] Config が未設定です。インスペクターで PlayerAnimatorConfig をセットしてください。");
            return;
        }

#if UNITY_EDITOR

        // エディター上では
        // ConfigからAnimatorControllerを自動生成する
        //
        // これによりAnimatorControllerを
        // 手作業で作る必要がなくなる。
        _anim.runtimeAnimatorController =
            config.BuildControllerEditor(gameObject);

#else

        // ビルド後は自動生成が使えないため、
        // 事前にControllerを作成しておく必要がある。
        if (_anim.runtimeAnimatorController == null)
        {
            Debug.LogWarning(
                "[PlayerAnimator] ランタイムビルドでは自動生成非対応。エディターで Animator Controller をアセット化してください。");
        }

#endif
    }

    /// <summary>
    /// 毎フレーム更新
    ///
    /// Config に登録されている
    /// 全 ParameterDriver を実行する。
    /// </summary>
    private void Update()
    {
        // Config未設定
        if (config == null)
            return;

        // Controller未設定
        if (_anim.runtimeAnimatorController == null)
            return;

        // ───────────────────────
        // Driver実行用コンテキスト生成
        // ───────────────────────
        //
        // DriveContext に必要な情報をまとめる。
        //
        // Driver側は
        // ctx.Anim
        // ctx.Rb
        // ctx.Tf
        //
        // を参照するだけで済む。
        //
        // 将来情報を追加したくなった場合も
        // DriveContextへ追加するだけで済む。
        //
        var ctx = new DriveContext
        {
            Anim = _anim,
            Rb = _rb,
            Tf = transform,

            FloatCache = _floatCache,
            BoolCache = _boolCache,
            TriggersSent = _triggersSent,

            MoveBlendSpeed = moveBlendSpeed,
        };

        // ───────────────────────
        // 全Driver実行
        // ───────────────────────
        //
        // Driverごとに
        // Animatorパラメータを更新する。
        //
        // 例：
        // HorizontalSpeedDriver
        // AirStateDriver
        // ComponentBoolTriggerDriver
        //
        foreach (var driver in config.ParameterDrivers)
        {
            driver.Drive(ctx);
        }

        // ───────────────────────
        // Trigger送信履歴クリア
        // ───────────────────────
        //
        // Triggerは
        // 「今フレーム中のみ」
        // 重複防止できればよい。
        //
        // 次フレームは再び送信可能にする。
        //
        _triggersSent.Clear();
    }
}