using UnityEngine;

public class ObjectBoard : MonoBehaviour
{
    private Camera mainCam;

    private void Start()
    {
        mainCam = Camera.main;
    }

    private void LateUpdate()
    {
        if (mainCam == null)
        {
            mainCam = Camera.main;
            if (mainCam == null) return;
        }

        // カメラと同じ向き + カメラの up を使って、ロールも一致させる
        transform.rotation = Quaternion.LookRotation(
            mainCam.transform.forward,
            mainCam.transform.up);
    }
}
