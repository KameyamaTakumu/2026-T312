using UnityEngine;

public class SceneChanger : MonoBehaviour
{
    [SerializeField] private SceneObject sceneObject;

    public void ButtonChangeScene()
    {
        sceneObject.Load();
    }
}
