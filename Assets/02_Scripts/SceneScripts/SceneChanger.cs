using UnityEngine;

public class SceneChanger : MonoBehaviour
{
    [SerializeField] private SceneObject sceneObject;

    [Header("チュートリアル遷移用（任意）")]
    [CustomLabel("チュートリアル入口として記録する")]
    [SerializeField] private bool recordAsEntryPoint = false;

    [SerializeField] private SceneObject selfSceneObject;

    [Header("チュートリアル遷移用（戻り先自動判定）")]
    [CustomLabel("記録された遷移元シーンへ戻る")]
    [SerializeField] private bool useRecordedEntryScene = false;

    public void ButtonChangeScene()
    {
        // このボタンがチュートリアルへの入口として使われる場合、
        // 「今いたシーン」を遷移元として記録しておく
        if (recordAsEntryPoint && selfSceneObject != null)
        {
            TutorialEntryContext.EntryScene = selfSceneObject;
        }

        if (ScreenFader.Instance != null)
        {
            // 丸く閉じる → 閉じきったらシーンリロード
            ScreenFader.Instance.FadeOut(LoadScene);
        }
        else
        {
            LoadScene();
        }
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
                    "SceneChanger: 遷移元シーンが記録されていません。" +
                    "フォールバック先（sceneObject）を使用します。"
                );
            }
        }

        if (destination != null)
        {
            destination.Load();
        }
        else
        {
            Debug.LogError("SceneChanger: 遷移先の SceneObject が設定されていません。");
        }
    }
}