using System.IO;
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

    private PlaceableItem selected;
    private GameObject ghost;
    private float yaw;
    private float scale = 1f;

    // UI のボタンから呼ぶ
    public void Select(PlaceableItem item)
    {
        selected = item;
        scale = item != null ? item.defaultScale : 1f;

        if (ghost != null) Destroy(ghost);
        if (item != null)
        {
            ghost = Instantiate(item.prefab);
            MakeInert(ghost);
        }
    }

    private void Update()
    {
        if (selected == null || cam == null) return;

        // UI の上では配置しない
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            if (ghost != null) ghost.SetActive(false);
            return;
        }

        // 回転・拡大縮小・奥行き
        if (Input.GetKey(KeyCode.Q)) yaw -= 120f * Time.deltaTime;
        if (Input.GetKey(KeyCode.E)) yaw += 120f * Time.deltaTime;

        float wheel = Input.GetAxis("Mouse ScrollWheel");
        if (selected.allowScale) scale = Mathf.Clamp(scale + wheel * 5f, 0.2f, 50f);
        else if (!selected.alignToSurface) freePlaceDistance = Mathf.Clamp(freePlaceDistance + wheel * 50f, 5f, 200f);

        // 配置位置を計算
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        Vector3 pos;
        Quaternion rot;

        if (selected.alignToSurface &&
            Physics.Raycast(ray, out RaycastHit hit, 300f, surfaceMask, QueryTriggerInteraction.Ignore))
        {
            pos = hit.point;
            // 面の法線を上方向に揃える（惑星の曲面でも建物の壁でも対応）
            rot = Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.Euler(0f, yaw, 0f);
        }
        else if (selected.alignToSurface)
        {
            if (ghost != null) ghost.SetActive(false);
            return;
        }
        else
        {
            pos = ray.GetPoint(freePlaceDistance);
            rot = Quaternion.Euler(0f, yaw, 0f);
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
        // 右クリックで削除（UI の上は除く）
        if (!Input.GetMouseButtonDown(1)) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 300f, ~0, QueryTriggerInteraction.Ignore))
        {
            PlacedObject po = hit.collider.GetComponentInParent<PlacedObject>();
            if (po != null) Destroy(po.gameObject);
        }
    }

    private void Place(Vector3 pos, Quaternion rot)
    {
        GameObject obj = Instantiate(selected.prefab, pos, rot, levelRoot);
        obj.transform.localScale = Vector3.one * scale;

        // 配置物は動かさない（敵の徘徊やコインの回収を止める）
        // コライダーは残すので、その上に別の物を置ける
        MakeInert(obj, keepColliders: true);

        var po = obj.AddComponent<PlacedObject>();
        po.id = selected.id;
        po.scale = scale;
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

    // ─────────── 保存・読込 ───────────

    private string PathOf(string levelName)
        => Path.Combine(Application.persistentDataPath, levelName + ".json");

    public void Save(string levelName)
    {
        var data = new LevelData();
        foreach (var po in levelRoot.GetComponentsInChildren<PlacedObject>())
        {
            data.items.Add(new PlacedData
            {
                id = po.id,
                pos = po.transform.position,
                rot = po.transform.rotation,
                scale = po.scale
            });
        }
        File.WriteAllText(PathOf(levelName), JsonUtility.ToJson(data, true));
    }

    public void Load(string levelName)
    {
        string path = PathOf(levelName);
        if (!File.Exists(path)) return;

        foreach (Transform child in levelRoot) Destroy(child.gameObject);

        var data = JsonUtility.FromJson<LevelData>(File.ReadAllText(path));
        foreach (var d in data.items)
        {
            var item = catalog.Find(d.id);
            if (item == null) continue;

            GameObject obj = Instantiate(item.prefab, d.pos, d.rot, levelRoot);
            obj.transform.localScale = Vector3.one * d.scale;
            MakeInert(obj, keepColliders: true);

            var po = obj.AddComponent<PlacedObject>();
            po.id = d.id;
            po.scale = d.scale;
        }
    }
}