using UnityEngine;

/// <summary>
/// ボタン等から呼び出してシーン遷移を行うコンポーネント。
///
/// 通常は指定した sceneObject へ遷移するだけだが、チュートリアルの
/// 入口／出口ボタンとして使う場合は以下のオプションと組み合わせる。
/// ・recordAsEntryPoint : このボタンが押された時点のシーンを
///   TutorialEntryContext に記録する（チュートリアルへの入口用）
/// ・useRecordedEntryScene : 記録されたシーンへ遷移する
///   （チュートリアルからの戻りボタン用）。記録が無い場合は
///   sceneObject をフォールバック先として使用する
/// </summary>
public class SceneChanger : MonoBehaviour
{
    [Header("遷移先")]

    // 通常時の遷移先。useRecordedEntryScene有効時は記録が無い場合のフォールバックにもなる
    [SerializeField] private SceneObject sceneObject;

    [Header("チュートリアル遷移用（任意）")]

    [CustomLabel("チュートリアル入口として記録する")]
    [SerializeField] private bool recordAsEntryPoint = false;

    // recordAsEntryPoint有効時、TutorialEntryContextへ記録する「今いるシーン」の情報
    [SerializeField] private SceneObject selfSceneObject;

    [Header("チュートリアル遷移用（戻り先自動判定）")]

    [CustomLabel("記録された遷移元シーンへ戻る")]
    [SerializeField] private bool useRecordedEntryScene = false;

    /// <summary>ボタンのOnClickから呼び出すシーン遷移処理</summary>
    public void ButtonChangeScene()
    {
        // このボタンがチュートリアルへの入口として使われる場合、今いたシーンを記録しておく
        if (recordAsEntryPoint && selfSceneObject != null)
            TutorialEntryContext.EntryScene = selfSceneObject;

        if (ScreenFader.Instance != null)
            ScreenFader.Instance.FadeOut(LoadScene); // 丸く閉じる → 閉じきったらシーンロード
        else
            LoadScene();
    }

    private void LoadScene()
    {
        SceneObject destination = sceneObject;

        if (useRecordedEntryScene)
        {
            if (TutorialEntryContext.EntryScene != null)
            {
                destination = TutorialEntryContext.EntryScene;
            }
            else
            {
                Debug.LogWarning(
                    "SceneChanger: 遷移元シーンが記録されていません。フォールバック先（sceneObject）を使用します。");
            }
        }

        if (destination != null)
            destination.Load();
        else
            Debug.LogError("SceneChanger: 遷移先のSceneObjectが設定されていません。");
    }
}