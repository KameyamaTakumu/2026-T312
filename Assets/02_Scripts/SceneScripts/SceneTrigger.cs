using UnityEngine;

/// <summary>
/// プレイヤーが触れたら、同じオブジェクトの SceneChanger でシーン遷移する。
/// 遷移先・フェード・各種オプションは SceneChanger 側で設定する
/// </summary>
[RequireComponent(typeof(SceneChanger))]
public class SceneTrigger : MonoBehaviour
{
    [CustomLabel("触れてから遷移までの待ち時間（秒）"), SerializeField]
    private float delay = 0f;

    [CustomLabel("遷移中はプレイヤーを操作不能にする"), SerializeField]
    private bool freezePlayer = true;

    private SceneChanger changer;

    // フェード中に複数のコライダーが触れても、遷移を1回だけにする
    private bool triggered;

    private void Awake()
    {
        changer = GetComponent<SceneChanger>();
    }

    // すり抜けるオブジェクト（Is Trigger ON）の場合
    private void OnTriggerEnter(Collider other)
    {
        TryStart(other);
    }

    // 壁のように当たるオブジェクト（Is Trigger OFF）の場合
    private void OnCollisionEnter(Collision collision)
    {
        TryStart(collision.collider);
    }

    private void TryStart(Collider other)
    {
        if (triggered) return;
        if (!other.CompareTag("Player")) return;

        triggered = true;

        if (freezePlayer)
            FreezePlayer(other);

        if (delay > 0f)
            Invoke(nameof(Go), delay);
        else
            Go();
    }

    private void Go()
    {
        changer.ButtonChangeScene();
    }

    private static void FreezePlayer(Collider other)
    {
        // 操作を止め、その場で止まらせる（重力は GravityBody が続けるので、浮いたままにならない）
        PlayerController ctrl = other.GetComponent<PlayerController>()
                             ?? other.GetComponentInParent<PlayerController>();
        if (ctrl != null)
            ctrl.enabled = false;

        Rigidbody rb = other.GetComponent<Rigidbody>()
                    ?? other.GetComponentInParent<Rigidbody>();
        if (rb != null)
            rb.linearVelocity = Vector3.zero;
    }
}