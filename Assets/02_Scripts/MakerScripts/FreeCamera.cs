using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// ステージエディット用の自由飛行カメラ
///
/// LevelEditor が使っている入力（左クリック・右クリック・Q/E・ホイール）と
/// 重ならないよう、視点回転はマウス中ボタンのドラッグにしている
/// </summary>
public class FreeCamera : MonoBehaviour
{
    [Header("移動設定")]

    [CustomLabel("移動速度"), SerializeField]
    private float moveSpeed = 15f;

    [CustomLabel("高速移動の倍率（Shift）"), SerializeField]
    private float fastMultiplier = 3f;

    [Header("視点設定")]

    [CustomLabel("視点の回転感度"), SerializeField]
    private float lookSensitivity = 2f;

    [CustomLabel("上下の視点の反転"), SerializeField]
    private bool invertY = false;

    [Header("視点プリセット")]

    [CustomLabel("真上視点にした時の最低高度"), SerializeField]
    private float topViewMinHeight = 80f;

    private float yaw;
    private float pitch;

    // 中ボタンのドラッグ中か
    private bool isLooking;

    private void Start()
    {
        // 現在のカメラの向きを初期値にする
        Vector3 angles = transform.eulerAngles;
        yaw = angles.y;
        pitch = angles.x > 180f ? angles.x - 360f : angles.x;
    }

    private void Update()
    {
        HandleViewPreset();
        HandleLook();
        HandleMove();
    }

    private void OnDisable()
    {
        // 無効化された時にカーソルを戻す
        if (isLooking)
            EndLook();
    }

    /// <summary>
    /// 1=真上 / 2=正面 / 3=横 に視点を切り替える
    /// </summary>
    private void HandleViewPreset()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
            SnapView(89f, yaw, true);      // 真上（向きは今のまま）
        else if (Input.GetKeyDown(KeyCode.Alpha2))
            SnapView(0f, 0f, false);       // 正面
        else if (Input.GetKeyDown(KeyCode.Alpha3))
            SnapView(0f, 90f, false);      // 横
    }

    private void SnapView(float newPitch, float newYaw, bool raise)
    {
        pitch = newPitch;
        yaw = newYaw;
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

        // 真上視点では、低すぎると何も見えないので高度を確保する
        if (raise && transform.position.y < topViewMinHeight)
        {
            Vector3 p = transform.position;
            p.y = topViewMinHeight;
            transform.position = p;
        }
    }

    /// <summary>
    /// 中ボタンを押している間だけ視点を回転する
    /// </summary>
    private void HandleLook()
    {
        // ドラッグ開始（UIの上で押した場合は開始しない）
        if (Input.GetMouseButtonDown(2))
        {
            bool overUI = EventSystem.current != null
                       && EventSystem.current.IsPointerOverGameObject();
            if (!overUI)
                BeginLook();
        }

        if (Input.GetMouseButtonUp(2) && isLooking)
            EndLook();

        if (!isLooking)
            return;

        float mx = Input.GetAxis("Mouse X");
        float my = Input.GetAxis("Mouse Y");

        yaw += mx * lookSensitivity;
        pitch += (invertY ? my : -my) * lookSensitivity;

        // 真上・真下で裏返らないよう制限
        pitch = Mathf.Clamp(pitch, -89f, 89f);

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void BeginLook()
    {
        isLooking = true;

        // ドラッグ中はカーソルを固定して、画面端で止まらないようにする
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void EndLook()
    {
        isLooking = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>
    /// WASD・Space・Ctrl による移動
    /// </summary>
    private void HandleMove()
    {
        Vector3 dir = Vector3.zero;

        Vector3 fwd = transform.forward;

        // 真上・真下を向いている時は、W/S が上下移動にならないよう
        // 画面の上方向（水平成分）を前進方向にする
        if (Mathf.Abs(fwd.y) > 0.9f)
        {
            Vector3 screenUp = fwd.y < 0 ? transform.up : -transform.up;
            fwd = Vector3.ProjectOnPlane(screenUp, Vector3.up).normalized;
        }

        //if (Input.GetKey(KeyCode.W)) dir += transform.forward;
        //if (Input.GetKey(KeyCode.S)) dir -= transform.forward;
        if (Input.GetKey(KeyCode.W)) dir += fwd;
        if (Input.GetKey(KeyCode.S)) dir -= fwd;
        if (Input.GetKey(KeyCode.D)) dir += transform.right;
        if (Input.GetKey(KeyCode.A)) dir -= transform.right;

        // 上下はカメラの向きに関係なくワールドの上下
        if (Input.GetKey(KeyCode.Space)) dir += Vector3.up;
        if (Input.GetKey(KeyCode.LeftControl)) dir -= Vector3.up;

        if (dir == Vector3.zero)
            return;

        float speed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift))
            speed *= fastMultiplier;

        // timeScale の影響を受けないよう unscaledDeltaTime を使う
        transform.position += dir.normalized * speed * Time.unscaledDeltaTime;
    }
}