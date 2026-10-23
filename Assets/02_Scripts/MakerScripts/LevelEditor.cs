using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class LevelEditor : MonoBehaviour
{
    [SerializeField] private PlaceableCatalog catalog;
    [SerializeField] private Camera cam;
    [SerializeField] private Transform levelRoot;

    [Header("配置設定")]
    [SerializeField] private LayerMask surfaceMask = ~0;   // 吸着先（惑星・建物）
    [SerializeField] private float freePlaceDistance = 30f;

    [Header("作業平面（空間に置く物の高さ）")]
    [SerializeField] private float planeY = 0f;
    [SerializeField] private float planeMoveSpeed = 15f;
    [SerializeField] private float maxPlaceDistance = 300f;

    // 平面を可視化する半透明マテリアル（未設定なら平面は表示しない）
    [SerializeField] private Material planeMaterial;

    // 高さを表示するテキスト
    [SerializeField] private TMP_Text heightLabel;

    private Transform planeVisual;
    private bool planeVisible;

    private PlaceableItem selected;
    private GameObject ghost;
    private float yaw;
    private float scale = 1f;

    // 1つ目だけ置いて、まだ相方がいない土管
    private PlacedObject pendingPair;

    // 連続配置中のゾーン
    private readonly List<PlacedObject> currentChain = new List<PlacedObject>();
    private string chainId;
    private int chainCounter;

    // 置いたばかりで、行き先がまだ決まっていないランチャー
    private PlacedObject pendingTarget;

    // UI のボタンから呼ぶ
    public void Select(PlaceableItem item)
    {
        // 1つ目だけ置いた状態で別のアイテムを選んだら、その1つ目は消す
        CancelPending();
        EndChain();
        CancelTarget();

        selected = item;
        scale = item != null ? item.defaultScale : 1f;

        if (ghost != null) Destroy(ghost);
        if (item != null)
        {
            ghost = Instantiate(item.prefab);
            MakeInert(ghost);
        }
    }

    private void Start()
    {
        if (planeMaterial == null) return;

        // 平面の見た目（当たり判定は消す）
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Plane);
        go.name = "WorkPlane";
        Destroy(go.GetComponent<Collider>());

        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = planeMaterial;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        go.transform.localScale = new Vector3(30f, 1f, 30f);   // 300 x 300
        go.SetActive(false);
        planeVisual = go.transform;
    }

    private void Update()
    {
        planeVisible = false;

        // 入力欄の編集中は、キー操作をすべて無効にする
        if (TextInputGuard.IsTyping) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelPending();
            CancelTarget();
        }

        // Enter / Esc で連続配置を終える
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Return))
            EndChain();

        if (selected == null || cam == null) return;

        // UI の上では配置しない
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            if (ghost != null) ghost.SetActive(false);
            return;
        }

        // 行き先を選んでいる間は、配置せずに惑星のクリックだけを受け付ける
        if (pendingTarget != null)
        {
            if (ghost != null) ghost.SetActive(false);
            HandleTargetPick();
            return;
        }

        // 回転
        if (Input.GetKey(KeyCode.Q)) yaw -= 120f * Time.deltaTime;
        if (Input.GetKey(KeyCode.E)) yaw += 120f * Time.deltaTime;

        // 作業平面の高さ（R=上げる / F=下げる、Shift で高速）
        float heightInput = 0f;
        if (Input.GetKey(KeyCode.R)) heightInput += 1f;
        if (Input.GetKey(KeyCode.F)) heightInput -= 1f;
        float heightSpeed = planeMoveSpeed * (Input.GetKey(KeyCode.LeftShift) ? 4f : 1f);
        planeY += heightInput * heightSpeed * Time.deltaTime;

        // ホイール：拡大縮小できる物は拡大縮小、空間に置ける物は高さの調整
        float wheel = Input.GetAxis("Mouse ScrollWheel");
        if (selected.allowScale)
            scale = Mathf.Clamp(scale + wheel * 5f, 0.2f, 50f);
        else if (!selected.alignToSurface || selected.surfaceOrFree)
            planeY += wheel * 30f;

        // 空間に置ける物は、Alt を押している間は面に吸着せず作業平面に置く
        bool canUsePlane = !selected.alignToSurface || selected.surfaceOrFree;
        bool forcePlane = canUsePlane && Input.GetKey(KeyCode.LeftAlt);

        // 配置位置を計算
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        Vector3 pos;
        Quaternion rot;

        if (selected.alignToSurface && !forcePlane &&
            Physics.Raycast(ray, out RaycastHit hit, 300f, surfaceMask, QueryTriggerInteraction.Ignore))
        {
            // 面の法線方向に、設定した高さだけ浮かせる（スケールに比例させる）
            pos = hit.point + hit.normal * (selected.surfaceOffset * scale);
            // 面の法線を上方向に揃える（惑星の曲面でも建物の壁でも対応）
            rot = Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.Euler(0f, yaw, 0f);
        }
        else if (selected.alignToSurface && !selected.surfaceOrFree)
        {
            if (ghost != null) ghost.SetActive(false);
            return;
        }
        else
        {
            // 空間配置：マウスの位置を、作業平面（高さ planeY）に投影した点
            pos = PointOnWorkPlane(ray);
            rot = Quaternion.Euler(0f, yaw, 0f);
            planeVisible = true;
        }

        if (ghost != null)
        {
            ghost.SetActive(true);
            ghost.transform.SetPositionAndRotation(pos, rot);
            ghost.transform.localScale = Vector3.one * scale;
        }

        if (Input.GetMouseButtonDown(0))
            Place(pos, rot);
    }

    private void LateUpdate()
    {
        UpdatePlaneVisual();

        // 右クリックで削除（UI の上は除く）
        if (!Input.GetMouseButtonDown(1)) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        //Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        //if (Physics.Raycast(ray, out RaycastHit hit, 300f, ~0, QueryTriggerInteraction.Ignore))
        //{
        //    PlacedObject po = hit.collider.GetComponentInParent<PlacedObject>();
        //    if (po != null) DeleteWithPartner(po);
        //}
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 300f, ~0, QueryTriggerInteraction.Collide))
        {
            PlacedObject po = hit.collider.GetComponentInParent<PlacedObject>();
            if (po != null) DeleteWithPartner(po);
        }
    }

    private void Place(Vector3 pos, Quaternion rot)
    {
        // 1つだけのアイテムは、すでに置いてあるものを消して置き直す
        if (selected.unique)
        {
            foreach (var old in levelRoot.GetComponentsInChildren<PlacedObject>())
            {
                if (old.id == selected.id)
                    Destroy(old.gameObject);
            }
        }

        GameObject obj = Instantiate(selected.prefab, pos, rot, levelRoot);
        obj.transform.localScale = Vector3.one * scale;

        // 配置物は動かさない（敵の徘徊やコインの回収を止める）
        // コライダーは残すので、その上に別の物を置ける
        MakeInert(obj, keepColliders: true);
        EnsureClickable(obj);

        var po = obj.AddComponent<PlacedObject>();
        po.id = selected.id;
        po.scale = scale;
        po.uid = System.Guid.NewGuid().ToString();

        if (selected.pairedPlacement)
        {
            if (pendingPair == null)
            {
                // 1つ目：新しいペアIDを発行して、相方待ちにする
                po.pairId = System.Guid.NewGuid().ToString();
                pendingPair = po;
            }
            else
            {
                // 2つ目：1つ目と同じIDにしてペア成立
                po.pairId = pendingPair.pairId;
                pendingPair = null;
            }
        }

        if (selected.chainedPlacement)
        {
            // 削除済みのものを取り除き、空なら新しい連続配置を始める
            currentChain.RemoveAll(c => c == null);
            if (currentChain.Count == 0)
            {
                chainId = System.Guid.NewGuid().ToString();
                chainCounter = 0;
            }

            po.pairId = chainId;
            po.order = chainCounter++;
            currentChain.Add(po);
        }

        if (selected.needsTarget)
            pendingTarget = po;
    }

    /// <summary>エディット中は挙動を止める（スクリプト無効・物理無効）</summary>
    private static void MakeInert(GameObject obj, bool keepColliders = false)
    {
        foreach (var b in obj.GetComponentsInChildren<MonoBehaviour>())
            b.enabled = false;

        foreach (var rb in obj.GetComponentsInChildren<Rigidbody>())
            rb.isKinematic = true;

        if (!keepColliders)
            foreach (var c in obj.GetComponentsInChildren<Collider>())
                c.enabled = false;
    }


    private void DeleteWithPartner(PlacedObject target)
    {
        var item = catalog.Find(target.id);
        bool deleteGroup = item != null && item.pairedPlacement;

        if (deleteGroup && !string.IsNullOrEmpty(target.pairId))
        {
            foreach (var other in levelRoot.GetComponentsInChildren<PlacedObject>())
            {
                if (other != target && other.pairId == target.pairId)
                    Destroy(other.gameObject);
            }
        }

        if (pendingPair == target)
            pendingPair = null;

        // 消す惑星を行き先にしていたランチャーは、行き先を空にする
        if (!string.IsNullOrEmpty(target.uid))
        {
            foreach (var other in levelRoot.GetComponentsInChildren<PlacedObject>())
            {
                if (other.targetUid == target.uid)
                    other.targetUid = "";
            }
        }

        Destroy(target.gameObject);
    }

    private void CancelPending()
    {
        if (pendingPair == null) return;

        Destroy(pendingPair.gameObject);
        pendingPair = null;
    }

    /// <summary>
    /// 連続配置を終える。1つしか置いていない場合は意味がないので消す
    /// </summary>
    private void EndChain()
    {
        currentChain.RemoveAll(c => c == null);

        if (currentChain.Count == 1)
        {
            // 同じフレームの Save に拾われないよう、先に非アクティブにする
            currentChain[0].gameObject.SetActive(false);
            Destroy(currentChain[0].gameObject);
        }

        currentChain.Clear();
    }

    /// <summary>
    /// 惑星をクリックして、ランチャーの行き先に設定する
    /// </summary>
    private void HandleTargetPick()
    {
        if (!Input.GetMouseButtonDown(0)) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 300f, surfaceMask, QueryTriggerInteraction.Ignore))
            return;

        PlacedObject target = hit.collider.GetComponentInParent<PlacedObject>();
        if (target == null || target == pendingTarget) return;

        // 重力源（惑星）だけを行き先にできる
        if (target.GetComponentInChildren<GravityAttractor>(true) == null) return;

        pendingTarget.targetUid = target.uid;
        pendingTarget = null;
    }

    /// <summary>
    /// 行き先が決まっていないランチャーを取り消す
    /// </summary>
    private void CancelTarget()
    {
        if (pendingTarget == null) return;

        Destroy(pendingTarget.gameObject);
        pendingTarget = null;
    }

    /// <summary>
    /// マウスのレイと、高さ planeY の水平面との交点を返す
    /// </summary>
    private Vector3 PointOnWorkPlane(Ray ray)
    {
        Plane plane = new Plane(Vector3.up, new Vector3(0f, planeY, 0f));

        if (plane.Raycast(ray, out float enter) && enter <= maxPlaceDistance)
            return ray.GetPoint(enter);

        // 平面と交わらない（水平線の向こうを見ている等）場合は、カメラ前方の一定距離に置く
        return ray.GetPoint(freePlaceDistance);
    }

    /// <summary>
    /// 作業平面の表示と、高さのテキストを更新する
    /// </summary>
    private void UpdatePlaneVisual()
    {
        if (planeVisual != null)
        {
            planeVisual.gameObject.SetActive(planeVisible);

            // カメラの真下に追従させて、常に足元に広がっているように見せる
            if (planeVisible && cam != null)
            {
                Vector3 c = cam.transform.position;
                planeVisual.position = new Vector3(c.x, planeY, c.z);
            }
        }

        if (heightLabel != null)
            heightLabel.text = planeVisible ? $"高さ Y = {planeY:0.0}" : "";
    }

    /// <summary>
    /// エディット中に右クリックで選べるよう、非トリガーのコライダーが無い物には
    /// 見た目の大きさに合わせた Box Collider を付ける。
    /// プレイ用シーンは Prefab から生成するので、これは影響しない
    /// </summary>
    private static void EnsureClickable(GameObject obj)
    {
        // すでに有効な非トリガーのコライダーがあれば何もしない
        foreach (var c in obj.GetComponentsInChildren<Collider>())
        {
            if (c.enabled && !c.isTrigger)
                return;
        }

        // 見た目（Renderer）の範囲を集める（パーティクルや軌跡は除く）
        Bounds bounds = default;
        bool hasBounds = false;

        foreach (var r in obj.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer)
                continue;

            if (!hasBounds)
            {
                bounds = r.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }

        // 見た目が無い物（ゾーンなど）は、小さな球で代用する
        if (!hasBounds)
        {
            var sphere = obj.AddComponent<SphereCollider>();
            sphere.radius = 0.5f;
            return;
        }

        // ワールドの範囲を、このオブジェクトのローカル座標に直して設定する
        var box = obj.AddComponent<BoxCollider>();
        Vector3 s = obj.transform.lossyScale;
        box.center = obj.transform.InverseTransformPoint(bounds.center);
        box.size = new Vector3(
            bounds.size.x / Mathf.Max(Mathf.Abs(s.x), 0.0001f),
            bounds.size.y / Mathf.Max(Mathf.Abs(s.y), 0.0001f),
            bounds.size.z / Mathf.Max(Mathf.Abs(s.z), 0.0001f));
    }

    //// ─────────── 保存・読込 ───────────

    //private string PathOf(string levelName)
    //    => Path.Combine(Application.persistentDataPath, levelName + ".json");

    //public void Save(string levelName)
    //{
    //    EndChain();

    //    var data = new LevelData();
    //    foreach (var po in levelRoot.GetComponentsInChildren<PlacedObject>())
    //    {
    //        // 相方がまだいない土管は保存しない
    //        if (po == pendingPair) continue;
    //        if (po == pendingTarget) continue;

    //        data.items.Add(new PlacedData
    //        {
    //            id = po.id,
    //            pos = po.transform.position,
    //            rot = po.transform.rotation,
    //            scale = po.scale,
    //            pairId = po.pairId,
    //            order = po.order,
    //            uid = po.uid,
    //            targetUid = po.targetUid
    //        });
    //    }
    //    File.WriteAllText(PathOf(levelName), JsonUtility.ToJson(data, true));
    //}

    //public void Load(string levelName)
    //{
    //    pendingPair = null;
    //    pendingTarget = null;

    //    currentChain.Clear();

    //    string path = PathOf(levelName);
    //    if (!File.Exists(path)) return;

    //    foreach (Transform child in levelRoot) Destroy(child.gameObject);

    //    var data = JsonUtility.FromJson<LevelData>(File.ReadAllText(path));
    //    foreach (var d in data.items)
    //    {
    //        var item = catalog.Find(d.id);
    //        if (item == null) continue;

    //        GameObject obj = Instantiate(item.prefab, d.pos, d.rot, levelRoot);
    //        obj.transform.localScale = Vector3.one * d.scale;
    //        MakeInert(obj, keepColliders: true);
    //        EnsureClickable(obj);

    //        var po = obj.AddComponent<PlacedObject>();
    //        po.id = d.id;
    //        po.scale = d.scale;
    //        po.pairId = d.pairId;
    //        po.order = d.order;
    //        po.uid = string.IsNullOrEmpty(d.uid) ? System.Guid.NewGuid().ToString() : d.uid;
    //        po.targetUid = d.targetUid;
    //    }
    //}

    // ─────────── 保存・読込 ───────────

    /// <summary>現在の配置を LevelData にまとめる</summary>
    public LevelData BuildData()
    {
        EndChain();

        var data = new LevelData();
        foreach (var po in levelRoot.GetComponentsInChildren<PlacedObject>())
        {
            // 相方がまだいない土管、行き先が未指定のランチャーは保存しない
            if (po == pendingPair) continue;
            if (po == pendingTarget) continue;

            data.items.Add(new PlacedData
            {
                id = po.id,
                pos = po.transform.position,
                rot = po.transform.rotation,
                scale = po.scale,
                pairId = po.pairId,
                order = po.order,
                uid = po.uid,
                targetUid = po.targetUid
            });
        }
        return data;
    }

    /// <summary>LevelData の内容で、ステージを作り直す</summary>
    public void ApplyData(LevelData data)
    {
        if (data == null) return;

        pendingPair = null;
        pendingTarget = null;
        currentChain.Clear();

        // 同じフレームの保存に古いものが混ざらないよう、先に非アクティブにする
        foreach (Transform child in levelRoot)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }

        foreach (var d in data.items)
        {
            var item = catalog.Find(d.id);
            if (item == null) continue;

            GameObject obj = Instantiate(item.prefab, d.pos, d.rot, levelRoot);
            obj.transform.localScale = Vector3.one * d.scale;
            MakeInert(obj, keepColliders: true);
            EnsureClickable(obj);

            var po = obj.AddComponent<PlacedObject>();
            po.id = d.id;
            po.scale = d.scale;
            po.pairId = d.pairId;
            po.order = d.order;
            po.uid = string.IsNullOrEmpty(d.uid) ? System.Guid.NewGuid().ToString() : d.uid;
            po.targetUid = d.targetUid;
        }
    }

    // 既存のボタンの OnClick からも、これまで通り呼べる
    public void Save(string levelName)
    {
        if (LevelStorage.Save(levelName, BuildData()))
            LevelStorage.CurrentLevelName = LevelStorage.Sanitize(levelName);
    }

    public void Load(string levelName)
    {
        LevelData data = LevelStorage.Load(levelName);
        if (data == null) return;

        ApplyData(data);
        LevelStorage.CurrentLevelName = LevelStorage.Sanitize(levelName);
    }
}