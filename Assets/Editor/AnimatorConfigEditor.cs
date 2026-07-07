#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// AnimatorConfig 用カスタムインスペクター
///
/// 主な役割
/// ・ステート一覧の編集UI
/// ・遷移設定UI
/// ・Animatorパラメータドライバー編集UI
/// ・SerializeReferenceドライバー追加UI
///
/// デザイナーが AnimatorController を直接編集しなくても
/// ScriptableObject 上からアニメーション構成を管理できるようにする。
/// </summary>
[CustomEditor(typeof(AnimatorConfig))]
public class AnimatorConfigEditor : Editor
{
    // ── ドライバー登録テーブル ────────────────────────────────────────
    // 「メニュー名」「説明」「型」の 3 列。追加したいドライバーはここに 1 行書く。
    //
    // Label       : メニュー表示名
    // Description : インスペクター内の説明文
    // Type        : 生成するドライバー型
    private static readonly (string Label, string Description, Type Type)[] DriverTypes =
    {
        (
            "水平速度 Float（Idle→Run など）",
            "地面を移動する速さに応じて 0〜1 の Float を流す。\nBlendTree で Idle と Run をブレンドするときに使う。",
            typeof(HorizontalSpeedDriver)
        ),
        (
            "空中判定 Bool（IsAir）",
            "ジャンプ・落下中かどうかを Bool で流す。\n上下方向の速度が閾値を超えたときに true になる。",
            typeof(AirStateDriver)
        ),
        (
            "コンポーネント Bool → Trigger（スピン・攻撃など）",
            "別コンポーネントの bool プロパティが\n「false→true」になった瞬間に Trigger を送る。\nスピンや攻撃のアニメーション開始に使う。",
            typeof(ComponentBoolTriggerDriver)
        ),
        (
            "速度閾値 Bool（ダッシュ判定など）",
            "水平速度が設定値を超えたら true を流す Bool ドライバー。\nダッシュアニメーションの切り替えに使う。",
            typeof(SpeedThresholdBoolDriver)
        ),
        (
            "敵の向きに応じた Float（敵の向きに応じて BlendTree）",
            "敵の向きに応じて -1〜1 の Float を流す。\nBlendTree で左向き・右向きをブレンドするときに使う。",
            typeof(EnemyMoveDriver)
        ),
    };

    // ── GUI スタイル・色 ──────────────────────────────────────────────
    private static GUIStyle _sectionHeader;
    private static GUIStyle _cardBox;
    private static GUIStyle _descLabel;
    private static GUIStyle _tagLabel;

    private static readonly Color ColSection = new(0.18f, 0.18f, 0.22f, 1f);
    private static readonly Color ColCard = new(0.22f, 0.22f, 0.28f, 1f);
    private static readonly Color ColAccent = new(0.3f, 0.7f, 1.0f, 1f);
    private static readonly Color ColWarning = new(1.0f, 0.75f, 0.2f, 1f);

    // foldout 状態（ステートカードごと）
    private readonly Dictionary<int, bool> _stateFoldouts = new();
    private readonly Dictionary<int, bool> _driverFoldouts = new();

    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// カスタムインスペクターのメイン描画処理。
    ///
    /// 表示内容
    /// ① ステート定義
    /// ② パラメータドライバー
    ///
    /// SerializedObject を利用して Undo / Redo に対応する。
    /// </summary>
    public override void OnInspectorGUI()
    {
        InitStyles();
        serializedObject.Update();

        var cfg = (AnimatorConfig)target;

        // ─── ① ステート定義セクション ───────────────────────────────
        DrawSectionHeader(" ステート定義", "アニメーションの「状態」を追加する");
        EditorGUILayout.Space(4);

        var statesProp = serializedObject.FindProperty("States");
        DrawStatesSection(statesProp, cfg);

        EditorGUILayout.Space(14);

        // ─── ② パラメータドライバーセクション ───────────────────────
        DrawSectionHeader("パラメータドライバー", "Animator の変数を自動で動かす処理を追加する");
        EditorGUILayout.Space(4);

        var driversProp = serializedObject.FindProperty("ParameterDrivers");
        DrawDriversSection(driversProp, cfg);

        EditorGUILayout.Space(8);

        serializedObject.ApplyModifiedProperties();
    }

    // ══════════════════════════════════════════════════════════════════
    //  ステートセクション描画
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// ステート一覧編集UIを描画する。
    ///
    /// 各ステートをカード形式で表示し、
    /// ・名前変更
    /// ・デフォルト設定
    /// ・BlendTree設定
    /// ・遷移設定
    /// ・削除
    /// を行える。
    /// </summary>
    void DrawStatesSection(SerializedProperty statesProp, AnimatorConfig cfg)
    {
        var stateNames = cfg.States.Select(s => s.StateName).ToArray();

        for (int i = 0; i < statesProp.arraySize; i++)
        {
            var elemProp = statesProp.GetArrayElementAtIndex(i);
            var nameProp = elemProp.FindPropertyRelative("StateName");
            var isDefProp = elemProp.FindPropertyRelative("IsDefault");
            var useBlendProp = elemProp.FindPropertyRelative("UseBlendTree");

            if (!_stateFoldouts.ContainsKey(i)) _stateFoldouts[i] = true;

            // ── カードヘッダー ──────────────────────────────────────
            using (new BackgroundColorScope(ColCard))
            using (var horizontal = new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                // foldout 矢印
                _stateFoldouts[i] = EditorGUILayout.Foldout(_stateFoldouts[i], GUIContent.none, true, GUIStyle.none);

                // デフォルト ★ バッジ
                if (isDefProp.boolValue)
                {
                    using (new GUIColorScope(ColAccent))
                        GUILayout.Label("★", GUILayout.Width(16));
                }
                else
                    GUILayout.Space(18);

                // ステート名（インライン編集）
                nameProp.stringValue = EditorGUILayout.TextField(nameProp.stringValue, EditorStyles.boldLabel, GUILayout.ExpandWidth(true));

                // タグ
                using (new GUIColorScope(useBlendProp.boolValue ? ColAccent : Color.gray))
                    GUILayout.Label(useBlendProp.boolValue ? "BlendTree" : "Clip", _tagLabel, GUILayout.Width(66));

                // デフォルトボタン
                using (new GUIEnabledScope(!isDefProp.boolValue))
                {
                    if (GUILayout.Button("デフォルト", GUILayout.Width(70)))
                    {
                        // 他を全部 false にしてからこれを true
                        for (int j = 0; j < statesProp.arraySize; j++)
                            statesProp.GetArrayElementAtIndex(j).FindPropertyRelative("IsDefault").boolValue = (j == i);
                    }
                }

                // 削除ボタン
                using (new GUIColorScope(ColWarning))
                {
                    if (GUILayout.Button("✕", GUILayout.Width(24)))
                    {
                        statesProp.DeleteArrayElementAtIndex(i);
                        serializedObject.ApplyModifiedProperties();
                        GUIUtility.ExitGUI();
                    }
                }
            }

            // ── カード内容（foldout が開いているとき） ─────────────
            if (_stateFoldouts[i])
            {
                EditorGUI.indentLevel++;
                using (new BackgroundColorScope(new Color(0.2f, 0.2f, 0.25f)))
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    // IsDefault はヘッダーで操作するので非表示
                    DrawPropertyExcluding(elemProp, "IsDefault");

                    // 遷移定義の補助 UI（ToState をドロップダウンで）
                    DrawTransitionsWithDropdown(
                        elemProp.FindPropertyRelative("Transitions"),
                        stateNames
                    );
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(2);
        }

        // ── ＋ ステートを追加ボタン ──────────────────────────────────
        EditorGUILayout.Space(2);
        using (new BackgroundColorScope(new Color(0.2f, 0.5f, 0.2f)))
        {
            if (GUILayout.Button("＋  ステートを追加", GUILayout.Height(28)))
            {
                statesProp.InsertArrayElementAtIndex(statesProp.arraySize);
                var newElem = statesProp.GetArrayElementAtIndex(statesProp.arraySize - 1);
                // Unityは配列コピーで前の値を引き継ぐので全フィールドをリセットする
                newElem.FindPropertyRelative("StateName").stringValue = $"NewState{statesProp.arraySize}";
                newElem.FindPropertyRelative("IsDefault").boolValue = (statesProp.arraySize == 1);
                newElem.FindPropertyRelative("UseBlendTree").boolValue = false;
                newElem.FindPropertyRelative("BlendParameter").stringValue = "MoveSpeed";
                newElem.FindPropertyRelative("BlendChildren").ClearArray();
                newElem.FindPropertyRelative("Transitions").ClearArray();
                newElem.FindPropertyRelative("Clip").objectReferenceValue = null;
            }
        }
    }

    // ── 遷移リストを ToState ドロップダウン付きで描画 ─────────────────

    /// <summary>
    /// ステート遷移一覧を描画する。
    ///
    /// ToState は自由入力ではなく
    /// 既存ステート一覧から選択できるようにする。
    /// </summary>
    void DrawTransitionsWithDropdown(SerializedProperty transitionsProp, string[] stateNames)
    {
        EditorGUILayout.LabelField("遷移", EditorStyles.miniBoldLabel);
        EditorGUI.indentLevel++;

        for (int t = 0; t < transitionsProp.arraySize; t++)
        {
            var tProp = transitionsProp.GetArrayElementAtIndex(t);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                // ヘッダー行
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"遷移 [{t}]", EditorStyles.boldLabel);
                    if (GUILayout.Button("✕", GUILayout.Width(24)))
                    {
                        transitionsProp.DeleteArrayElementAtIndex(t);
                        serializedObject.ApplyModifiedProperties();
                        GUIUtility.ExitGUI();
                    }
                }

                // FromAnyState
                var fromAnyProp = tProp.FindPropertyRelative("FromAnyState");
                EditorGUILayout.PropertyField(fromAnyProp);

                if (fromAnyProp.boolValue)
                    EditorGUILayout.PropertyField(tProp.FindPropertyRelative("CanTransitionToSelf"));

                // ToState → ドロップダウン
                var toStateProp = tProp.FindPropertyRelative("ToState");
                if (stateNames.Length > 0)
                {
                    int currentIdx = Mathf.Max(0, Array.IndexOf(stateNames, toStateProp.stringValue));
                    int newIdx = EditorGUILayout.Popup(
                        new GUIContent("遷移先", "どのステートに移動するか"),
                        currentIdx, stateNames);
                    toStateProp.stringValue = stateNames[newIdx];
                }
                else
                {
                    EditorGUILayout.HelpBox("先にステートを追加してください", MessageType.Info);
                }

                // HasExitTime / ExitTime
                var hasExitProp = tProp.FindPropertyRelative("HasExitTime");
                EditorGUILayout.PropertyField(hasExitProp);
                if (hasExitProp.boolValue)
                    EditorGUILayout.PropertyField(tProp.FindPropertyRelative("ExitTime"));

                EditorGUILayout.PropertyField(tProp.FindPropertyRelative("Duration"),
                    new GUIContent("ブレンド時間（秒）"));

                // 条件リスト（パラメータ型に応じた Mode のみ表示）
                DrawConditions(tProp.FindPropertyRelative("Conditions"));
            }
            EditorGUILayout.Space(2);
        }

        if (GUILayout.Button("＋  遷移を追加", GUILayout.Height(22)))
        {
            transitionsProp.InsertArrayElementAtIndex(transitionsProp.arraySize);
            var newT = transitionsProp.GetArrayElementAtIndex(transitionsProp.arraySize - 1);
            newT.FindPropertyRelative("FromAnyState").boolValue = false;
            newT.FindPropertyRelative("CanTransitionToSelf").boolValue = false;
            newT.FindPropertyRelative("ToState").stringValue = stateNames.Length > 0 ? stateNames[0] : "";
            newT.FindPropertyRelative("HasExitTime").boolValue = false;
            newT.FindPropertyRelative("ExitTime").floatValue = 0.9f;
            newT.FindPropertyRelative("Duration").floatValue = 0.1f;
            newT.FindPropertyRelative("Conditions").ClearArray();
        }

        EditorGUI.indentLevel--;
    }

    // ══════════════════════════════════════════════════════════════════
    //  ドライバーセクション描画
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Animatorパラメータドライバー一覧を描画する。
    ///
    /// SerializeReferenceを利用して
    /// 複数種類のドライバーを同じリストで管理する。
    /// </summary>
    void DrawDriversSection(SerializedProperty listProp, AnimatorConfig cfg)
    {
        for (int i = 0; i < listProp.arraySize; i++)
        {
            var elemProp = listProp.GetArrayElementAtIndex(i);
            if (elemProp.managedReferenceValue == null)
            {
                EditorGUILayout.HelpBox($"[{i}] 型が見つかりません（アセンブリ変更後に発生することがあります）", MessageType.Warning);
                if (GUILayout.Button($"削除 [{i}]"))
                {
                    listProp.DeleteArrayElementAtIndex(i);
                    serializedObject.ApplyModifiedProperties();
                    GUIUtility.ExitGUI();
                }
                continue;
            }

            if (!_driverFoldouts.ContainsKey(i)) _driverFoldouts[i] = true;

            string typeName = elemProp.managedReferenceValue.GetType().Name;

            // ── ドライバーカードヘッダー ────────────────────────────
            using (new BackgroundColorScope(ColCard))
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                _driverFoldouts[i] = EditorGUILayout.Foldout(_driverFoldouts[i], GUIContent.none, true, GUIStyle.none);

                // 種別バッジ
                using (new GUIColorScope(ColAccent))
                    GUILayout.Label(typeName, _tagLabel, GUILayout.Width(220));

                GUILayout.FlexibleSpace();

                // 削除ボタン
                using (new GUIColorScope(ColWarning))
                {
                    if (GUILayout.Button("✕", GUILayout.Width(24)))
                    {
                        listProp.DeleteArrayElementAtIndex(i);
                        serializedObject.ApplyModifiedProperties();
                        GUIUtility.ExitGUI();
                    }
                }
            }

            // ── ドライバーカード内容 ────────────────────────────────
            if (_driverFoldouts[i])
            {
                // 対応するメタ情報（説明文）を探す
                var meta = Array.Find(DriverTypes, d => d.Type == elemProp.managedReferenceValue.GetType());
                if (meta.Description != null)
                {
                    using (new GUIColorScope(new Color(0.7f, 0.85f, 1f)))
                        EditorGUILayout.LabelField(meta.Description, _descLabel);
                    EditorGUILayout.Space(2);
                }

                EditorGUI.indentLevel++;
                DrawManagedReferenceChildren(elemProp);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(3);
        }

        // ── ＋ ドライバーを追加ボタン ────────────────────────────────
        EditorGUILayout.Space(2);
        using (new BackgroundColorScope(new Color(0.2f, 0.4f, 0.6f)))
        {
            if (GUILayout.Button("＋  ドライバーを追加", GUILayout.Height(28)))
                ShowDriverPickerWindow(listProp, cfg);
        }
    }

    // ── ドライバー選択ウィンドウ（説明付きリスト） ───────────────────

    void ShowDriverPickerWindow(SerializedProperty listProp, AnimatorConfig cfg)
    {
        var menu = new GenericMenu();
        foreach (var (label, description, type) in DriverTypes)
        {
            var capturedType = type;
            menu.AddItem(new GUIContent(label), false, () =>
            {
                Undo.RecordObject(cfg, "Add Driver");
                cfg.ParameterDrivers.Add(
                    (AnimatorParameterDriver)Activator.CreateInstance(capturedType));
                EditorUtility.SetDirty(cfg);
                serializedObject.Update();
            });
        }
        menu.ShowAsContext();
    }

    // ══════════════════════════════════════════════════════════════════
    //  ユーティリティ
    // ══════════════════════════════════════════════════════════════════


    // ── 条件リストをパラメータ型に応じた Mode 選択肢で描画 ───────────────

    static readonly string[] ModesBool = { "If (= true)", "IfNot (= false)" };
    static readonly int[] ModeValsBool = { 1, 2 };

    static readonly string[] ModesFloat = { "Greater (>)", "Less (<)" };
    static readonly int[] ModeValsFloat = { 4, 6 };

    static readonly string[] ModesInt = { "Equals (=)", "NotEqual (≠)", "Greater (>)", "Less (<)" };
    static readonly int[] ModeValsInt = { 8, 9, 4, 6 };

    /// <summary>
    /// 遷移条件一覧を描画する。
    ///
    /// パラメータ型に応じて
    /// 利用可能な比較モードを切り替える。
    /// </summary>
    void DrawConditions(SerializedProperty condsProp)
    {
        EditorGUILayout.LabelField("Conditions", EditorStyles.miniBoldLabel);
        EditorGUI.indentLevel++;

        for (int c = 0; c < condsProp.arraySize; c++)
        {
            var cond = condsProp.GetArrayElementAtIndex(c);
            var paramProp = cond.FindPropertyRelative("Parameter");
            var modeProp = cond.FindPropertyRelative("Mode");
            var thrProp = cond.FindPropertyRelative("Threshold");

            using (new EditorGUILayout.HorizontalScope())
            {
                // パラメータ名
                paramProp.stringValue = EditorGUILayout.TextField(paramProp.stringValue, GUILayout.Width(120));

                // パラメータ型を ParameterDrivers から推定
                var paramType = InferParamType(paramProp.stringValue);

                if (paramType == AnimatorParameterTypeEnum.Bool)
                {
                    // If / IfNot のみ
                    int cur = Array.IndexOf(ModeValsBool, modeProp.intValue);
                    int sel = EditorGUILayout.Popup(Mathf.Max(0, cur), ModesBool, GUILayout.Width(130));
                    modeProp.intValue = ModeValsBool[sel];
                }
                else if (paramType == AnimatorParameterTypeEnum.Trigger)
                {
                    // Trigger は Mode 不要
                    EditorGUILayout.LabelField("(Trigger)", GUILayout.Width(130));
                    modeProp.intValue = 1; // If
                }
                else if (paramType == AnimatorParameterTypeEnum.Int)
                {
                    int cur = Array.IndexOf(ModeValsInt, modeProp.intValue);
                    int sel = EditorGUILayout.Popup(Mathf.Max(0, cur), ModesInt, GUILayout.Width(130));
                    modeProp.intValue = ModeValsInt[sel];
                    thrProp.floatValue = EditorGUILayout.FloatField(thrProp.floatValue, GUILayout.Width(50));
                }
                else
                {
                    // Float / 不明
                    int cur = Array.IndexOf(ModeValsFloat, modeProp.intValue);
                    int sel = EditorGUILayout.Popup(Mathf.Max(0, cur), ModesFloat, GUILayout.Width(130));
                    modeProp.intValue = ModeValsFloat[sel];
                    thrProp.floatValue = EditorGUILayout.FloatField(thrProp.floatValue, GUILayout.Width(50));
                }

                GUILayout.FlexibleSpace();
                using (new GUIColorScope(ColWarning))
                {
                    if (GUILayout.Button("✕", GUILayout.Width(22)))
                    {
                        condsProp.DeleteArrayElementAtIndex(c);
                        serializedObject.ApplyModifiedProperties();
                        GUIUtility.ExitGUI();
                    }
                }
            }
        }

        if (GUILayout.Button("＋  条件を追加", GUILayout.Height(20)))
        {
            condsProp.InsertArrayElementAtIndex(condsProp.arraySize);
            var newC = condsProp.GetArrayElementAtIndex(condsProp.arraySize - 1);
            newC.FindPropertyRelative("Parameter").stringValue = "";
            newC.FindPropertyRelative("Mode").intValue = 1; // If
            newC.FindPropertyRelative("Threshold").floatValue = 0f;
        }

        EditorGUI.indentLevel--;
    }

    /// <summary>
    /// パラメータ名から型を推定する。
    ///
    /// 登録済みドライバーを参照し
    /// Bool / Float / Int / Trigger を判定する。
    /// </summary>
    AnimatorParameterTypeEnum InferParamType(string paramName)
    {
        var cfg = (AnimatorConfig)target;
        foreach (var d in cfg.ParameterDrivers)
            if (d != null && d.ParameterName == paramName)
                return d.ParameterType;
        return AnimatorParameterTypeEnum.Float;
    }

    /// <summary>[SerializeReference] 要素の子フィールドを全て描画する</summary>

    /// <summary>
    /// SerializeReferenceオブジェクトの
    /// 子プロパティを再帰的に描画する。
    /// </summary>
    static void DrawManagedReferenceChildren(SerializedProperty parent)
    {
        var iter = parent.Copy();
        var end = parent.GetEndProperty();
        if (!iter.NextVisible(enterChildren: true)) return;
        while (!SerializedProperty.EqualContents(iter, end))
        {
            EditorGUILayout.PropertyField(iter, true);
            if (!iter.NextVisible(enterChildren: false)) break;
        }
    }

    /// <summary>指定プロパティ名を除いた子フィールドを描画する</summary>

    /// <summary>
    /// 指定したプロパティ名を除外して
    /// 子プロパティを描画する。
    /// </summary>
    static void DrawPropertyExcluding(SerializedProperty parent, params string[] excludeNames)
    {
        var iter = parent.Copy();
        var end = parent.GetEndProperty();
        if (!iter.NextVisible(enterChildren: true)) return;
        while (!SerializedProperty.EqualContents(iter, end))
        {
            // "Transitions" は DrawTransitionsWithDropdown で別途描画するので除外
            if (!excludeNames.Contains(iter.name) && iter.name != "Transitions")
                EditorGUILayout.PropertyField(iter, true);
            if (!iter.NextVisible(enterChildren: false)) break;
        }
    }

    // ── セクションヘッダー ────────────────────────────────────────────

    /// <summary>
    /// セクションタイトルを描画する。
    ///
    /// 見出し＋補足説明を表示する共通UI。
    /// </summary>
    void DrawSectionHeader(string title, string subtitle)
    {
        using (new BackgroundColorScope(ColSection))
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(title, _sectionHeader);
            using (new GUIColorScope(new Color(0.7f, 0.7f, 0.7f)))
                EditorGUILayout.LabelField(subtitle, EditorStyles.miniLabel);
        }
    }

    // ── スタイル初期化 ────────────────────────────────────────────────

    /// <summary>
    /// GUIStyle初期化。
    ///
    /// 初回のみ生成し、以降は再利用する。
    /// </summary>
    static void InitStyles()
    {
        if (_sectionHeader != null) return;

        _sectionHeader = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 13,
        };

        _cardBox = new GUIStyle(EditorStyles.helpBox)
        {
            padding = new RectOffset(8, 8, 6, 6),
        };

        _descLabel = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
        {
            wordWrap = true,
            richText = true,
        };

        _tagLabel = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
        };
    }

    // ── 短命スコープヘルパー ──────────────────────────────────────────

    /// <summary>
    /// GUI.backgroundColor を一時変更するスコープ。
    ///
    /// using終了時に元の色へ戻す。
    /// </summary>
    private class BackgroundColorScope : IDisposable
    {
        private readonly Color _prev;
        public BackgroundColorScope(Color c) { _prev = GUI.backgroundColor; GUI.backgroundColor = c; }
        public void Dispose() => GUI.backgroundColor = _prev;
    }

    /// <summary>
    /// GUI.color を一時変更するスコープ。
    ///
    /// using終了時に元の色へ戻す。
    /// </summary>
    private class GUIColorScope : IDisposable
    {
        private readonly Color _prev;
        public GUIColorScope(Color c) { _prev = GUI.color; GUI.color = c; }
        public void Dispose() => GUI.color = _prev;
    }

    /// <summary>
    /// GUI.enabled を一時変更するスコープ。
    ///
    /// ボタン無効化などに使用する。
    /// </summary>
    private class GUIEnabledScope : IDisposable
    {
        private readonly bool _prev;
        public GUIEnabledScope(bool enabled) { _prev = GUI.enabled; GUI.enabled = enabled; }
        public void Dispose() => GUI.enabled = _prev;
    }
}

#endif