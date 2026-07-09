using UnityEngine;

public class SceneChanger : MonoBehaviour
{
    [SerializeField] private SceneObject sceneObject;

    public void ButtonChangeScene()
    {
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

        sceneObject.Load();
    }
}
