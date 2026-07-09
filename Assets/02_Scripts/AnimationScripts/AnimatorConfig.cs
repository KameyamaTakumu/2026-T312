using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor.Animations;
#endif

/// <summary>
/// アニメーション設定アセット
///
/// 主な役割：
/// ・アニメーションステート管理
/// ・AnimatorController生成
/// ・ParameterDriver管理
/// ・遷移情報管理
///
/// この ScriptableObject を編集することで
/// コードを書き換えずにアニメーション構成を変更できる。
///
/// 作成方法：
/// Create → Player → Animator Config
/// </summary>
[CreateAssetMenu(
    menuName = "Animator/Animator Config",
    fileName = "AnimatorConfig")]
public class AnimatorConfig : ScriptableObject
{
    // ─────────────────────────────────────────
    // ステート定義
    // ─────────────────────────────────────────

    /// <summary>
    /// Animator内に作成するステート一覧
    ///
    /// Idle
    /// Run
    /// Jump
    /// Attack
    ///
    /// などをここで定義する。
    /// </summary>
    [Header("ステート定義（追加・削除可）")]
    public List<AnimatorStateDefinition> States = new();

    // ─────────────────────────────────────────
    // パラメータドライバー
    // ─────────────────────────────────────────

    /// <summary>
    /// Animatorパラメータ更新処理一覧
    ///
    /// MoveSpeed更新
    /// IsAir更新
    /// AttackTrigger送信
    ///
    /// などを担当する。
    /// </summary>
    [Header("パラメータドライバー（追加・削除可）")]
    [SerializeReference]
    public List<AnimatorParameterDriver> ParameterDrivers = new();

#if UNITY_EDITOR

    /// <summary>
    /// Config内容からAnimatorControllerを生成する
    ///
    /// エディター専用機能。
    ///
    /// 実行時に
    /// States
    /// ParameterDrivers
    ///
    /// の内容を読み取り
    /// AnimatorControllerを構築する。
    /// </summary>
    public AnimatorController BuildControllerEditor(GameObject owner)
    {
        // 新規AnimatorController生成
        var ctrl =
    AnimatorController.CreateAnimatorControllerAtPath(
        "Assets/CharacterAnimator_Generated.controller");

        ctrl.name = "CharacterAnimator_Generated";

        // BaseLayer作成
        ctrl.AddLayer("Base Layer");

        // ───────────────────────
        // パラメータ生成
        // ───────────────────────

        var paramSet = new HashSet<string>();

        foreach (var driver in ParameterDrivers)
        {
            // パラメータ名が空なら無視
            if (string.IsNullOrEmpty(driver.ParameterName))
                continue;

            // 同名パラメータ重複防止
            if (!paramSet.Add(driver.ParameterName))
                continue;

            ctrl.AddParameter(
                driver.ParameterName,
                (AnimatorControllerParameterType)
                driver.ParameterType);
        }

        // ステートマシン取得
        var rootSM = ctrl.layers[0].stateMachine;

        // ───────────────────────
        // ステート生成
        // ───────────────────────

        var stateMap =
            new Dictionary<string, AnimatorState>();

        foreach (var def in States)
        {
            AnimatorState state;

            // BlendTree使用ステート
            if (def.UseBlendTree)
            {
                state =
                    ctrl.CreateBlendTreeInController(
                        def.StateName,
                        out BlendTree tree);

                tree.blendType =
                    BlendTreeType.Simple1D;

                tree.blendParameter =
                    def.BlendParameter;

                // 自動閾値無効
                tree.useAutomaticThresholds = false;

                foreach (var child in def.BlendChildren)
                {
                    if (child.Clip == null)
                        continue;

                    tree.AddChild(
                        child.Clip,
                        child.Threshold);
                }
            }
            else
            {
                // 通常ステート
                state = rootSM.AddState(
                    def.StateName);

                if (def.Clip != null)
                {
                    state.motion = def.Clip;
                }
            }

            // 名前から検索できるよう登録
            stateMap[def.StateName] = state;

            // デフォルトステート設定
            if (def.IsDefault)
            {
                rootSM.defaultState = state;
            }
        }

        // ───────────────────────
        // 遷移生成
        // ───────────────────────

        foreach (var def in States)
        {
            // 元ステート取得失敗
            if (!stateMap.TryGetValue(
                def.StateName,
                out var fromState))
            {
                continue;
            }

            foreach (var trans in def.Transitions)
            {
                AnimatorStateTransition t;

                // AnyState遷移
                if (trans.FromAnyState)
                {
                    t = rootSM.AddAnyStateTransition(
                        stateMap[trans.ToState]);

                    t.canTransitionToSelf =
                        trans.CanTransitionToSelf;
                }
                else
                {
                    // 遷移先取得失敗
                    if (!stateMap.TryGetValue(
                        trans.ToState,
                        out var toState))
                    {
                        continue;
                    }

                    t = fromState.AddTransition(
                        toState);
                }

                // ExitTime設定
                t.hasExitTime =
                    trans.HasExitTime;

                t.exitTime =
                    trans.ExitTime;

                // ブレンド時間
                t.duration =
                    trans.Duration;

                // 条件追加
                foreach (var cond in trans.Conditions)
                {
                    t.AddCondition(
                        (AnimatorConditionMode)
                        cond.Mode,

                        cond.Threshold,
                        cond.Parameter);
                }
            }
        }

        return ctrl;
    }

#endif
}

/// <summary>
/// Driverへ渡す実行コンテキスト
///
/// Driverが必要とする情報を
/// まとめて保持する構造体。
///
/// これを使うことで
/// Drive() の引数を増やさずに
/// 情報を追加できる。
/// </summary>
public struct DriveContext
{
    /// <summary>
    /// Animator参照
    /// </summary>
    public Animator Anim;

    /// <summary>
    /// Rigidbody参照
    /// </summary>
    public Rigidbody Rb;

    /// <summary>
    /// Transform参照
    /// </summary>
    public Transform Tf;

    /// <summary>
    /// Floatパラメータキャッシュ
    ///
    /// 前回値保持に使用。
    /// </summary>
    public Dictionary<string, float> FloatCache;

    /// <summary>
    /// Boolパラメータキャッシュ
    ///
    /// 状態変化検出に使用。
    /// </summary>
    public Dictionary<string, bool> BoolCache;

    /// <summary>
    /// 今フレーム送信済みTrigger一覧
    ///
    /// Trigger重複送信防止。
    /// </summary>
    public HashSet<string> TriggersSent;

    /// <summary>
    /// Float補間速度
    /// </summary>
    public float MoveBlendSpeed;
}

/// <summary>
/// Animatorパラメータ制御用の基底クラス
///
/// 全てのDriverはこのクラスを継承して作成する。
///
/// 主な役割：
/// ・Animatorパラメータ更新
/// ・速度取得
/// ・補間処理
/// ・Trigger送信補助
///
/// などは全てこのクラスを継承している。
///
/// Drive() が毎フレーム呼ばれ、
/// その中で Animator を更新する。
/// </summary>
[Serializable]
public abstract class AnimatorParameterDriver
{
    // ─────────────────────────────────────────
    // 共通設定
    // ─────────────────────────────────────────

    /// <summary>
    /// Animatorパラメータ名
    ///
    /// AnimatorController側の変数名と
    /// 完全一致させる必要がある。
    /// </summary>
    [Tooltip("Animator パラメータ名（Animator Controller の変数名と一致させる）")]
    public string ParameterName = "";

    /// <summary>
    /// パラメータ型
    ///
    /// Float
    /// Bool
    /// Trigger
    /// Int
    ///
    /// のいずれか。
    /// </summary>
    [Tooltip("パラメータの型")]
    public AnimatorParameterTypeEnum ParameterType =
        AnimatorParameterTypeEnum.Float;

    // ─────────────────────────────────────────
    // メイン処理
    // ─────────────────────────────────────────

    /// <summary>
    /// 毎フレーム呼ばれる更新処理
    ///
    /// 派生クラス側で実装する。
    ///
    /// ここでAnimatorパラメータを更新する。
    /// </summary>
    public abstract void Drive(DriveContext ctx);

    // ─────────────────────────────────────────
    // ヘルパーメソッド
    // ─────────────────────────────────────────

    /// <summary>
    /// 水平方向速度を取得する
    ///
    /// 重力方向成分を除去した速度。
    ///
    /// 通常の
    /// Rigidbody.velocity.magnitude
    /// を使うと
    ///
    /// ・ジャンプ
    /// ・落下
    /// ・惑星重力
    ///
    /// の影響まで含まれてしまう。
    ///
    /// このメソッドは
    /// キャラクターが地面に沿って移動している
    /// 速度のみ取得する。
    ///
    /// Idle → Run のブレンド用。
    /// </summary>
    protected Vector3 HorizontalVelocity(
        DriveContext ctx)
    {
        Vector3 up = ctx.Tf.up;

        return ctx.Rb.linearVelocity
             - Vector3.Project(
                 ctx.Rb.linearVelocity,
                 up);
    }

    /// <summary>
    /// 上方向速度取得
    ///
    /// 正：
    /// 上昇
    ///
    /// 負：
    /// 落下
    ///
    /// ジャンプ判定や
    /// 空中判定で使用する。
    /// </summary>
    protected float VerticalSpeed(
        DriveContext ctx)
    {
        return Vector3.Dot(
            ctx.Rb.linearVelocity,
            ctx.Tf.up);
    }

    /// <summary>
    /// Floatパラメータを
    /// スムーズ補間してセットする
    ///
    /// 例えば
    ///
    /// MoveSpeed
    /// 0 → 1
    ///
    /// を瞬時に切り替えると
    /// アニメーションがカクつく。
    ///
    /// そこで前フレーム値を保持し、
    /// Lerpで徐々に変化させる。
    ///
    /// キャッシュ：
    /// ctx.FloatCache
    /// を利用する。
    /// </summary>
    protected void SetFloatSmooth(
        DriveContext ctx,
        float target)
    {
        // 前回値取得
        ctx.FloatCache.TryGetValue(
            ParameterName,
            out float current);

        // 補間
        float next =
            Mathf.Lerp(
                current,
                target,
                Time.deltaTime *
                ctx.MoveBlendSpeed);

        // 次回用保存
        ctx.FloatCache[ParameterName] =
            next;

        // Animator更新
        ctx.Anim.SetFloat(
            ParameterName,
            next);
    }

    /// <summary>
    /// Boolパラメータ設定
    ///
    /// 単純なラッパー。
    ///
    /// 派生クラス側で
    /// Animatorを直接触らずに済む。
    /// </summary>
    protected void SetBool(
        DriveContext ctx,
        bool value)
    {
        ctx.Anim.SetBool(
            ParameterName,
            value);
    }

    /// <summary>
    /// false → true になった瞬間だけ
    /// Triggerを送信する
    ///
    /// Triggerは毎フレーム送るものではなく、
    /// 「発生した瞬間」だけ送る必要がある。
    ///
    /// 例：
    ///
    /// 攻撃開始
    /// スピン開始
    /// ダメージ発生
    ///
    /// など。
    ///
    /// BoolCacheで前回状態を記録し、
    /// 立ち上がりのみ検出する。
    ///
    /// また、
    /// 同フレーム内で複数回送信されないよう
    /// TriggersSentで重複防止している。
    /// </summary>
    protected void SetTriggerOnRise(
        DriveContext ctx,
        bool nowActive)
    {
        // 前フレーム状態取得
        ctx.BoolCache.TryGetValue(
            ParameterName,
            out bool wasActive);

        // false → true
        if (nowActive &&
            !wasActive &&
            !ctx.TriggersSent.Contains(
                ParameterName))
        {
            // Trigger送信
            ctx.Anim.SetTrigger(
                ParameterName);

            // 今フレーム送信済みに登録
            ctx.TriggersSent.Add(
                ParameterName);
        }

        // 現在状態保存
        ctx.BoolCache[ParameterName] =
            nowActive;
    }
}

/// <summary>
/// Animatorパラメータ型
///
/// Unity標準の
/// AnimatorControllerParameterType
/// と同じ値を持つ。
///
/// Editor拡張側で
/// UnityEditor 名前空間に依存しないよう
/// 独自enumとして定義している。
/// </summary>
public enum AnimatorParameterTypeEnum
{
    /// <summary>
    /// 小数値パラメータ
    ///
    /// 主な用途：
    /// MoveSpeed
    /// Blend値
    /// </summary>
    Float = 1,

    /// <summary>
    /// 整数値パラメータ
    /// </summary>
    Int = 3,

    /// <summary>
    /// 真偽値パラメータ
    ///
    /// 主な用途：
    /// IsAir
    /// IsRunning
    /// </summary>
    Bool = 4,

    /// <summary>
    /// Triggerパラメータ
    ///
    /// 主な用途：
    /// Attack
    /// Spin
    /// Damage
    /// </summary>
    Trigger = 9,
}

// ══════════════════════════════════════════════════════════════════════
//  ステート・遷移の定義クラス群
// ══════════════════════════════════════════════════════════════════════

/// <summary>
/// Animatorステート定義
///
/// Idle
/// Run
/// Jump
/// Attack
///
/// など1つのアニメーション状態を表す。
///
/// BuildControllerEditor() 実行時に
/// この情報から AnimatorState が生成される。
/// </summary>
[Serializable]
public class AnimatorStateDefinition
{
    /// <summary>
    /// ステート名
    ///
    /// 遷移先指定にも使用するため
    /// 重複しない名前にする。
    /// </summary>
    [Tooltip("ステート名（遷移先の指定にも使われる）")]
    public string StateName = "NewState";

    /// <summary>
    /// 初期ステートか
    ///
    /// ゲーム開始時に最初に入る状態。
    /// 通常は Idle を設定する。
    /// </summary>
    [Tooltip("ゲーム開始時にこのステートから始める")]
    public bool IsDefault = false;

    // ─────────────────────────────────────────
    // 通常ステート設定
    // ─────────────────────────────────────────

    /// <summary>
    /// 再生するAnimationClip
    ///
    /// UseBlendTree が false の場合のみ使用。
    /// </summary>
    [Header("単一クリップ（UseBlendTree = false のとき）")]
    public AnimationClip Clip;

    // ─────────────────────────────────────────
    // BlendTree設定
    // ─────────────────────────────────────────

    /// <summary>
    /// BlendTreeを使用するか
    ///
    /// true：
    /// BlendTree
    ///
    /// false：
    /// 単一AnimationClip
    /// </summary>
    [Header("BlendTree 設定（UseBlendTree = true のとき）")]
    [Tooltip("チェックするとクリップの代わりに BlendTree を使う")]
    public bool UseBlendTree = false;

    /// <summary>
    /// BlendTreeを制御する
    /// Floatパラメータ名
    ///
    /// 例：
    /// MoveSpeed
    /// </summary>
    [Tooltip("BlendTree のブレンド値として使う Float パラメータ名")]
    public string BlendParameter = "MoveSpeed";

    /// <summary>
    /// BlendTreeに登録する子クリップ一覧
    /// </summary>
    public List<BlendChild> BlendChildren = new();

    // ─────────────────────────────────────────
    // 遷移定義
    // ─────────────────────────────────────────

    /// <summary>
    /// このステートから出る遷移一覧
    ///
    /// Run → Jump
    /// Jump → Idle
    ///
    /// などを定義する。
    /// </summary>
    [Header("このステートからの遷移")]
    public List<TransitionDefinition> Transitions = new();
}

/// <summary>
/// BlendTree内の子モーション定義
///
/// BlendParameter の値に応じて
/// このクリップが再生される。
///
/// 例：
///
/// Threshold = 0
/// Idle
///
/// Threshold = 1
/// Walk
///
/// Threshold = 2
/// Run
/// </summary>
[Serializable]
public class BlendChild
{
    /// <summary>
    /// 再生するアニメーションクリップ
    /// </summary>
    public AnimationClip Clip;

    /// <summary>
    /// このクリップが100%になる値
    ///
    /// BlendParameter が
    /// この値に到達した時
    /// 完全にこのモーションになる。
    /// </summary>
    [Tooltip("このクリップが 100% になる BlendParameter の値")]
    public float Threshold;
}

/// <summary>
/// Animatorステート遷移定義
///
/// ステートA → ステートB
///
/// のルールを定義する。
/// </summary>
[Serializable]
public class TransitionDefinition
{
    /// <summary>
    /// AnyState遷移か
    ///
    /// trueなら
    /// 現在どのステートにいても
    /// 条件を満たせば遷移する。
    /// </summary>
    [Tooltip("ON: AnyState（どのステートからでも遷移）/ OFF: このステートからのみ遷移")]
    public bool FromAnyState = false;

    /// <summary>
    /// 自分自身への遷移を許可するか
    ///
    /// AnyState専用。
    /// </summary>
    [Tooltip("AnyState 遷移のとき、すでに ToState にいても遷移するか")]
    public bool CanTransitionToSelf = false;

    /// <summary>
    /// 遷移先ステート名
    ///
    /// StateName と一致させる。
    /// </summary>
    [Tooltip("遷移先のステート名")]
    public string ToState = "";

    /// <summary>
    /// ExitTimeを使用するか
    ///
    /// true：
    /// アニメ再生途中では遷移しない
    ///
    /// false：
    /// 条件成立で即遷移
    /// </summary>
    [Tooltip("ON: クリップを ExitTime まで再生してから遷移 / OFF: 条件が揃い次第すぐ遷移")]
    public bool HasExitTime = false;

    /// <summary>
    /// 何％再生後に遷移可能になるか
    ///
    /// 0 = 開始直後
    /// 1 = 再生終了直前
    /// </summary>
    [Range(0f, 1f)]
    [Tooltip("HasExitTime ON のとき、クリップの何割再生したら遷移開始するか")]
    public float ExitTime = 0.9f;

    /// <summary>
    /// 遷移ブレンド時間
    ///
    /// 0なら即切り替え。
    /// </summary>
    [Tooltip("遷移にかけるブレンド時間（秒）")]
    public float Duration = 0.1f;

    /// <summary>
    /// 遷移条件一覧
    ///
    /// 全条件成立で遷移する。
    /// </summary>
    public List<ConditionDefinition> Conditions = new();
}

/// <summary>
/// 遷移条件定義
///
/// Animatorの
/// Conditions
/// に対応する。
/// </summary>
[Serializable]
public class ConditionDefinition
{
    /// <summary>
    /// 判定対象パラメータ名
    ///
    /// 例：
    /// MoveSpeed
    /// IsAir
    /// AttackTrigger
    /// </summary>
    [Tooltip("判定するパラメータ名")]
    public string Parameter = "";

    /// <summary>
    /// 判定方法
    ///
    /// Greater
    /// Less
    /// If
    /// IfNot
    /// など。
    /// </summary>
    [Tooltip("判定の種類")]
    public AnimatorConditionModeEnum Mode =
        AnimatorConditionModeEnum.If;

    /// <summary>
    /// 比較値
    ///
    /// Greater
    /// Less
    /// Equals
    ///
    /// で使用される。
    /// </summary>
    [Tooltip("比較に使用する値")]
    public float Threshold = 0f;
}

/// <summary>
/// 遷移条件モード
///
/// Unityの AnimatorConditionMode と
/// 同じ値を持つ。
///
/// Editor拡張が
/// UnityEditor に依存しないためのラッパー。
/// </summary>
public enum AnimatorConditionModeEnum
{
    /// <summary>
    /// Boolがtrue
    /// </summary>
    If = 1,

    /// <summary>
    /// Boolがfalse
    /// </summary>
    IfNot = 2,

    /// <summary>
    /// 値がThresholdより大きい
    /// </summary>
    Greater = 4,

    /// <summary>
    /// 値がThresholdより小さい
    /// </summary>
    Less = 6,

    /// <summary>
    /// 値がThresholdと等しい
    /// </summary>
    Equals = 8,

    /// <summary>
    /// 値がThresholdと異なる
    /// </summary>
    NotEqual = 9,
}