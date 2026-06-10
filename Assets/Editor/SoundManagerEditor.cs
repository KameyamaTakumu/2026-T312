/*========================================================
 * SoundManagerEditor.cs
 * 
 * 概要
 * SoundManager 用カスタムインスペクタ
 * 
 * BGM / SE の enum に応じて
 * AudioClip リストを自動生成する
 * 
 * -------------------------------------------------------
 * 主な機能
 * 
 * ・enum の数に合わせて
 *   AudioClip 配列サイズを自動調整
 * 
 * ・enum 名をそのままラベル表示
 * 
 * ・Inspector を見やすく整理
 * 
 * -------------------------------------------------------
 * 使い方
 * 
 * 1. BGM / SE enum に追加
 * 
 * public enum BGM
 * {
 *     Title,
 *     Battle,
 * }
 * 
 * public enum SE
 * {
 *     Jump,
 *     Coin,
 * }
 * 
 * 2. SoundManager の Inspector に
 * AudioClip を設定
 * 
 * enum の順番と
 * Inspector の順番が一致する
 *========================================================
*/

using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SoundManager 専用カスタムエディタ
/// enum に対応した AudioClip リストを
/// 自動整列して描画する
/// </summary>
[CustomEditor(typeof(SoundManager))]
public class SoundManagerEditor : Editor
{
    // ─────────────────────────────────────
    // SerializedProperty
    // ─────────────────────────────────────

    /// <summary>
    /// BGMリスト参照
    /// </summary>
    private SerializedProperty bgmListProp;

    /// <summary>
    /// SEリスト参照
    /// </summary>
    private SerializedProperty seListProp;

    // ─────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────

    /// <summary>
    /// Inspector 初期化時
    /// SerializedProperty を取得してキャッシュ
    /// </summary>
    private void OnEnable()
    {
        bgmListProp = serializedObject.FindProperty("bgmList");
        seListProp = serializedObject.FindProperty("seList");
    }

    // ─────────────────────────────────────
    // Inspector GUI
    // ─────────────────────────────────────

    /// <summary>
    /// カスタムInspector描画
    /// </summary>
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // 通常プロパティを描画
        DrawPropertiesExcluding(
            serializedObject,
            "bgmList",
            "seList"
        );

        // ─────────────────────────────
        // BGMセクション
        // ─────────────────────────────

        EditorGUILayout.Space(10);

        EditorGUILayout.LabelField(
            "BGM 設定",
            EditorStyles.boldLabel
        );

        DrawEnumList<BGM>(bgmListProp);

        // ─────────────────────────────
        // SEセクション
        // ─────────────────────────────

        EditorGUILayout.Space(10);

        EditorGUILayout.LabelField(
            "SE 設定",
            EditorStyles.boldLabel
        );

        DrawEnumList<SE>(seListProp);

        serializedObject.ApplyModifiedProperties();
    }

    // ─────────────────────────────────────
    // Draw Enum List
    // ─────────────────────────────────────

    /// <summary>
    /// enum に対応した
    /// AudioClip リストを描画する
    /// </summary>
    /// <typeparam name="T">
    /// 対象 enum 型
    /// </typeparam>
    /// <param name="listProp">
    /// 描画対象の SerializedProperty
    /// </param>
    private void DrawEnumList<T>(
        SerializedProperty listProp
    ) where T : Enum
    {
        // enum 全要素取得
        var enumValues =
            (T[])Enum.GetValues(typeof(T));

        // ─────────────────────────────
        // リストサイズ調整
        // ─────────────────────────────

        // enum 数より少ない場合追加
        while (listProp.arraySize < enumValues.Length)
        {
            listProp.InsertArrayElementAtIndex(
                listProp.arraySize
            );
        }

        // enum 数より多い場合削除
        while (listProp.arraySize > enumValues.Length)
        {
            listProp.DeleteArrayElementAtIndex(
                listProp.arraySize - 1
            );
        }

        // ─────────────────────────────
        // AudioClip 描画
        // ─────────────────────────────

        for (int i = 0; i < enumValues.Length; i++)
        {
            // リスト要素取得
            var element =
                listProp.GetArrayElementAtIndex(i);

            // enum 名取得
            string enumName =
                enumValues[i].ToString();

            // enum 名をラベルとして描画
            EditorGUILayout.PropertyField(
                element,
                new GUIContent(enumName)
            );
        }
    }
}