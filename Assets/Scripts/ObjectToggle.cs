using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// オブジェクトの表示・非表示を制御するコンポーネント
///
/// 主な役割：
/// ・対象オブジェクトの表示
/// ・対象オブジェクトの非表示
/// ・表示時にCinema Cameraの注視対象へ設定
/// </summary>
public class ObjectVisibilityController  : MonoBehaviour
{
    [Header("表示・非表示を切り替えるオブジェクト")]
    [SerializeField] private GameObject targetObject;

    [Header("通常時のカメラ")]
    [SerializeField] private CinemachineCamera mainCamera;

    [Header("惑星表示用カメラ")]
    [SerializeField] private CinemachineCamera planetCamera;

    [Header("表示時にカメラを向けるか")]
    [SerializeField] private bool lookAtWhenShown = true;

    /// <summary>
    /// オブジェクトを表示する
    /// </summary>
    public void Show()
    {
        if (targetObject == null)
            return;

        targetObject.SetActive(true);

        if (mainCamera != null)
            mainCamera.Priority = 10;

        if (planetCamera != null)
            planetCamera.Priority = 100;
    }

    /// <summary>
    /// オブジェクトを非表示にする
    /// </summary>
    public void Hide()
    {
        if (targetObject == null)
            return;

        targetObject.SetActive(false);

        if (mainCamera != null)
            mainCamera.Priority = 100;

        if (planetCamera != null)
            planetCamera.Priority = 10;
    }

    /// <summary>
    /// 表示状態を切り替える
    /// </summary>
    public void Toggle()
    {
        if (targetObject == null)
            return;

        if (targetObject.activeSelf)
        {
            Hide();
        }
        else
        {
            Show();
        }
    }
}
