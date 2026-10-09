using UnityEngine;

public class GravityAttractor : MonoBehaviour
{
    public enum Shape { Sphere, Box }

    [Header("形状設定")]

    // Sphere = 惑星(中心方向)、Box = 建物など(最寄りの面の法線方向)
    [CustomLabel("重力の形状"), SerializeField]
    private Shape shape = Shape.Sphere;

    // Box のみ使用。この距離(表面から)まで近づくと、この重力に切り替わる
    [CustomLabel("影響距離（Boxのみ・表面からの距離）"), SerializeField]
    private float influenceDistance = 3f;

    // マイナス値にすることで中心方向へ引っ張る
    [CustomLabel("重力の強さ"), SerializeField]
    private float gravity = -9.81f;

    [Header("リスポーン設定")]
    [CustomLabel("この惑星のリスポーン地点"), SerializeField]
    private Transform respawnPoint;

    [Header("BGM設定")]
    [CustomLabel("到着時惑星BGM"), SerializeField]
    private BGM planetBGM = BGM.None;

    [Header("カメラ設定（この惑星にいる間だけカメラ角度を変える）")]
    [CustomLabel("カメラ見下ろし角度を上書きする"), SerializeField]
    private bool overrideCameraPitch = false;

    [CustomLabel("上書き時のカメラ角度（マイナス値で見上げる）"), SerializeField]
    private float cameraPitchOverride = -20f;

    private BoxCollider boxCollider;

    public Transform RespawnPoint => respawnPoint;
    public bool OverrideCameraPitch => overrideCameraPitch;
    public float CameraPitchOverride => cameraPitchOverride;

    // Box 形状として扱えるか（BoxCollider が無い場合は Sphere 扱い）
    public bool IsBox => shape == Shape.Box && boxCollider != null;
    public float InfluenceDistance => influenceDistance;

    public static readonly System.Collections.Generic.List<GravityAttractor> All = new();

    private void OnEnable() => All.Add(this);
    private void OnDisable() => All.Remove(this);

    private void Awake()
    {
        boxCollider = GetComponent<BoxCollider>();
    }

    /// <summary>
    /// 位置 pos から Box 表面までの距離（Box 内部なら 0）
    /// </summary>
    public float GetSurfaceDistance(Vector3 pos)
    {
        Vector3 closest = boxCollider.ClosestPoint(pos);
        return Vector3.Distance(closest, pos);
    }

    /// <summary>
    /// 位置 pos における「上方向」（重力と逆向き）を返す
    /// </summary>
    public Vector3 GetGravityUp(Vector3 pos)
    {
        if (!IsBox)
            return (pos - transform.position).normalized;

        // ローカル座標に変換し、Box のサイズで正規化する
        // → 値が最大の軸が「いちばん近い面」
        Vector3 local = transform.InverseTransformPoint(pos) - boxCollider.center;
        Vector3 half = boxCollider.size * 0.5f;

        float nx = local.x / half.x;
        float ny = local.y / half.y;
        float nz = local.z / half.z;

        Vector3 axis;
        if (Mathf.Abs(nx) >= Mathf.Abs(ny) && Mathf.Abs(nx) >= Mathf.Abs(nz))
            axis = new Vector3(Mathf.Sign(nx), 0f, 0f);
        else if (Mathf.Abs(ny) >= Mathf.Abs(nz))
            axis = new Vector3(0f, Mathf.Sign(ny), 0f);
        else
            axis = new Vector3(0f, 0f, Mathf.Sign(nz));

        return transform.TransformDirection(axis).normalized;
    }

    public void Attract(Rigidbody body)
    {
        Vector3 gravityUp = GetGravityUp(body.position);

        body.rotation =
            Quaternion.FromToRotation(body.transform.up, gravityUp)
            * body.rotation;

        body.AddForce(gravityUp * gravity);
    }

    public void PlayPlanetBGM()
    {
        if (SoundManager.Instance == null)
            return;
        if (planetBGM == BGM.None)
            return;
        SoundManager.Instance.PlayBGM(planetBGM);
    }

    private Collider anyCollider;

    /// <summary>
    /// 位置 pos から、この重力源の表面までの距離（内部なら 0）
    /// </summary>
    public float DistanceToSurface(Vector3 pos)
    {
        if (anyCollider == null)
            anyCollider = GetComponent<Collider>();

        if (anyCollider == null)
            return Vector3.Distance(pos, transform.position);

        return Vector3.Distance(anyCollider.ClosestPoint(pos), pos);
    }
}