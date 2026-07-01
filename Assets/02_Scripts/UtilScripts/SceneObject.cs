/*
 * インスペクタ上で SceneAsset を選択できるようにするクラス
 * 
 * このクラスを使うことで、
 * Inspector から SceneAsset を直接選択できるようになる。
 * 
 * ------------------------------------------------------------
 * ▼使い方
 * 
 * public SceneObject selectScene;
 * 
 * ------------------------------------------------------------
 * ▼ロード例
 * 
 * selectScene.Load();
 * selectScene.LoadAsync();
 * selectScene.Load(LoadSceneMode.Additive);
 * 
 * ------------------------------------------------------------
 * ▼特徴
 * 
 * ・Inspector 上で SceneAsset を選択可能
 * ・内部的にはシーン名 string を保持
 * ・Build Settings に登録済みシーンのみ使用可能
 * ・同期ロード / 非同期ロード両対応
 * ・string との暗黙変換対応
 */

using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// シーン参照クラス
/// Inspector 上では SceneAsset を選択できるが、
/// 実行時はシーン名 string として扱う
/// </summary>
[System.Serializable]
public class SceneObject
{
    // 実際に保存されるシーン名
    // SceneAsset そのものは Build 後に保持できないため、
    // 実行時は string で管理する
    [SerializeField]
    private string m_SceneName;

    /// <summary>
    /// シーンが有効か
    /// シーン名が設定されているか確認する
    /// </summary>
    public bool IsValid()
    {
        return !string.IsNullOrEmpty(m_SceneName);
    }

    /// <summary>
    /// シーンを同期ロードする
    /// LoadScene はロード完了まで処理停止するため、
    /// 大きなシーンでは一瞬止まる場合がある
    /// </summary>
    /// <param name="mode">
    /// Single:現在シーンを破棄して切替
    /// Additive:現在シーンに追加ロード
    /// </param>
    public void Load(LoadSceneMode mode = LoadSceneMode.Single)
    {
        // シーン未設定
        if (!IsValid())
        {
            Debug.LogWarning("シーン名が設定されていません。");
            return;
        }

        // シーンロード
        SceneManager.LoadScene(m_SceneName, mode);
    }

    /// <summary>
    /// シーンを非同期ロードする
    /// 非同期なのでロード中もゲームを止めずに処理可能
    /// ローディング画面向け
    /// </summary>
    /// <param name="mode">
    /// Single:現在シーンを破棄して切替
    /// Additive:現在シーンに追加ロード
    /// </param>
    /// <returns>
    /// AsyncOperation
    /// progress を監視することで
    /// ロード進捗表示も可能
    /// </returns>
    public AsyncOperation LoadAsync(LoadSceneMode mode = LoadSceneMode.Single)
    {
        // シーン未設定
        if (!IsValid())
        {
            Debug.LogWarning("[SceneObject] シーン名が設定されていません。");
            return null;
        }

        // 非同期ロード開始
        return SceneManager.LoadSceneAsync(m_SceneName, mode);
    }

    // ─────────────────────────────────────────
    // 暗黙変換
    // ─────────────────────────────────────────

    /// <summary>
    /// SceneObject → string
    /// SceneObject をそのまま string として扱えるようにする
    /// </summary>
    public static implicit operator string(SceneObject sceneObject)
    {
        return sceneObject.m_SceneName;
    }

    /// <summary>
    /// string → SceneObject
    /// string から SceneObject を生成する
    /// </summary>
    public static implicit operator SceneObject(string sceneName)
    {
        return new SceneObject()
        {
            m_SceneName = sceneName
        };
    }
}

#if UNITY_EDITOR

/// <summary>
/// SceneObject 専用 Inspector 描画クラス
/// 通常 string 表示される m_SceneName を、
/// SceneAsset 選択フィールドとして表示する
/// </summary>
[CustomPropertyDrawer(typeof(SceneObject))]
public class SceneObjectEditor : PropertyDrawer
{
    /// <summary>
    /// シーン名から SceneAsset を取得
    /// Build Settings に登録されているシーンのみ対象
    /// </summary>
    /// <param name="sceneObjectName">
    /// シーン名
    /// </param>
    protected SceneAsset GetSceneObject(string sceneObjectName)
    {
        // 未設定
        if (string.IsNullOrEmpty(sceneObjectName))
            return null;

        // Build Settings 登録シーンを走査
        for (int i = 0; i < EditorBuildSettings.scenes.Length; i++)
        {
            EditorBuildSettingsScene scene =
                EditorBuildSettings.scenes[i];

            // シーンファイル名取得（拡張子除外）
            string sceneFileName =
                System.IO.Path.GetFileNameWithoutExtension(
                    scene.path);

            // 完全一致
            if (sceneFileName == sceneObjectName)
            {
                return AssetDatabase.LoadAssetAtPath(
                    scene.path,
                    typeof(SceneAsset)
                ) as SceneAsset;
            }
        }

        // Build Settings 未登録
        Debug.Log(
            "Scene [" + sceneObjectName + "] cannot be used. " +
            "Add this scene to the 'Scenes in the Build' " +
            "in the build settings."
        );

        return null;
    }

    /// <summary>
    /// Inspector の高さ
    /// </summary>
    public override float GetPropertyHeight(
        SerializedProperty property,
        GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight;
    }

    /// <summary>
    /// Inspector 描画
    /// </summary>
    public override void OnGUI(
        Rect position,
        SerializedProperty property,
        GUIContent label)
    {
        // 現在の SceneAsset を取得
        var sceneObj = GetSceneObject(
            property.FindPropertyRelative("m_SceneName")
                    .stringValue
        );

        // SceneAsset 選択欄描画
        var newScene = EditorGUI.ObjectField(
            position,
            label,
            sceneObj,
            typeof(SceneAsset),
            false
        );

        // 未選択
        if (newScene == null)
        {
            // シーン名クリア
            property.FindPropertyRelative("m_SceneName")
                    .stringValue = "";
        }
        else
        {
            // シーン変更された場合
            if (newScene.name !=
                property.FindPropertyRelative("m_SceneName")
                        .stringValue)
            {
                // Build Settings に登録されているか確認
                var scnObj = GetSceneObject(newScene.name);

                if (scnObj == null)
                {
                    Debug.LogWarning(
                        "The scene " + newScene.name +
                        " cannot be used. " +
                        "To use this scene add it to " +
                        "the build settings for the project."
                    );
                }
                else
                {
                    // シーン名保存
                    property.FindPropertyRelative("m_SceneName")
                            .stringValue = newScene.name;
                }
            }
        }
    }
}
#endif