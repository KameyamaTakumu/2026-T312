/*
 * インスペクタに表示される変数名を、
 * 任意の日本語などへ変更するための
 * カスタム属性 + カスタムエディタ
 * 
 * ------------------------------------------------------------
 * ▼使用例
 * 
 * [CustomLabel("移動速度")]
 * public float moveSpeed;
 * 
 * ↓ Inspector 表示
 * moveSpeed → 移動速度
 * 
 * ------------------------------------------------------------
 * ▼特徴
 * 
 * ・public/private 両対応
 * ・[SerializeField] private にも対応
 * ・継承先クラスにも対応
 * ・親クラスまで再帰検索
 * ・Unity 標準 Inspector を置き換え
 */

using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using System.Reflection;
using System;
#endif

/// <summary>
/// Inspector 表示名変更 Attribute
/// </summary>
public class CustomLabel : PropertyAttribute
{
    // Inspector に表示するラベル
    public readonly GUIContent Label;

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="label">
    /// 表示したい文字列
    /// </param>
    public CustomLabel(string label)
    {
        // GUIContent 化
        Label = new GUIContent(label);
    }
}

#if UNITY_EDITOR

/// <summary>
/// CustomLabel 専用 Inspector 描画クラス
/// Unity 標準 Inspector を拡張し、
/// CustomLabel が付いている変数だけ
/// 表示名を差し替える
/// </summary>
[CanEditMultipleObjects]

// UnityEngine.Object を継承する
// 全コンポーネント対象
[CustomEditor(typeof(UnityEngine.Object), true)]
public class CustomLabelEditor : Editor
{
    /// <summary>
    /// Inspector 描画
    /// </summary>
    public override void OnInspectorGUI()
    {
        // SerializedObject を最新状態へ同期
        serializedObject.Update();

        // 全プロパティ取得 Iterator
        SerializedProperty prop =
            serializedObject.GetIterator();

        // 最初だけ子階層へ入る
        bool enterChildren = true;

        // Inspector 表示対象を順番に処理
        while (prop.NextVisible(enterChildren))
        {
            enterChildren = false;

            // ────────────────────────────────
            // スクリプト欄
            // ────────────────────────────────

            // m_Script は Unity 内部変数
            // スクリプト参照欄
            if (prop.name == "m_Script")
            {
                // 編集禁止
                GUI.enabled = false;

                EditorGUILayout.PropertyField(prop, true);

                GUI.enabled = true;

                continue;
            }

            // ────────────────────────────────
            // フィールド検索
            // ────────────────────────────────

            // SerializedProperty 名から
            // 実際の FieldInfo を取得
            var field = GetFieldRecursive(
                target.GetType(),
                prop.name,

                // public/private 両対応
                BindingFlags.NonPublic |
                BindingFlags.Public |
                BindingFlags.Instance
            );

            // フィールド発見
            if (field != null)
            {
                // CustomLabel 属性取得
                var labelAttr =
                    (CustomLabel[])field.GetCustomAttributes(
                        typeof(CustomLabel),
                        false
                    );

                // CustomLabel が付いている
                if (labelAttr.Length > 0)
                {
                    // 指定ラベルで描画
                    EditorGUILayout.PropertyField(
                        prop,
                        labelAttr[0].Label,
                        true
                    );

                    continue;
                }
            }

            // ────────────────────────────────
            // 通常描画
            // ────────────────────────────────

            // CustomLabel 無し → Unity デフォルト表示
            EditorGUILayout.PropertyField(prop, true);
        }

        // 変更反映
        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>
    /// 親クラスまで再帰検索して FieldInfo を探す
    /// 通常の GetField() だけでは、
    /// 継承元 private フィールド取得に
    /// 対応できない場合がある
    /// </summary>
    private static FieldInfo GetFieldRecursive(Type type,string name,BindingFlags flags)
    {
        // 親クラスを順番に辿る
        while (
            type != null &&
            type != typeof(UnityEngine.Object)
        )
        {
            // 現在クラスから検索
            var field = type.GetField(name, flags);

            // 発見
            if (field != null)
                return field;

            // 親クラスへ
            type = type.BaseType;
        }

        // 見つからなかった
        return null;
    }
}
#endif