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

        transform.forward = mainCam.transform.forward;
    }
}
