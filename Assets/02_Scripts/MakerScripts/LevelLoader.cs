using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class LevelLoader : MonoBehaviour
{
    [SerializeField] private PlaceableCatalog catalog;
    [SerializeField] private string levelName = "level01";

    // スタート地点をリスポーン地点に登録したか
    private bool appliedSpawn;

    private void Awake()
    {
        string path = Path.Combine(Application.persistentDataPath, levelName + ".json");
        if (!File.Exists(path)) return;

        var data = JsonUtility.FromJson<LevelData>(File.ReadAllText(path));

        PlayerSpawnPoint spawn = null;
        var pairs = new Dictionary<string, List<PipeWarp>>();
        var chains = new Dictionary<string, List<(int order, GravityJumpZone zone)>>();
        var byUid = new Dictionary<string, GameObject>();
        var launchers = new List<(PlanetLauncher launcher, string targetUid)>();

        foreach (var d in data.items)
        {
            var item = catalog.Find(d.id);
            if (item == null) continue;

            GameObject obj = Instantiate(item.prefab, d.pos, d.rot);
            obj.transform.localScale = Vector3.one * d.scale;

            var sp = obj.GetComponent<PlayerSpawnPoint>();
            if (sp != null)
                spawn = sp;

            if (!string.IsNullOrEmpty(d.uid))
                byUid[d.uid] = obj;

            var launcher = obj.GetComponentInChildren<PlanetLauncher>();
            if (launcher != null && !string.IsNullOrEmpty(d.targetUid))
                launchers.Add((launcher, d.targetUid));

            // エディット専用の目印を非表示にする
            foreach (var m in obj.GetComponentsInChildren<EditorOnlyMarker>(true))
                m.gameObject.SetActive(false);

            // ゾーンの連続配置を集める
            if (!string.IsNullOrEmpty(d.pairId))
            {
                GravityJumpZone zone = obj.GetComponentInChildren<GravityJumpZone>();
                if (zone != null)
                {
                    if (!chains.ContainsKey(d.pairId))
                        chains[d.pairId] = new List<(int, GravityJumpZone)>();
                    chains[d.pairId].Add((d.order, zone));
                }
            }

            // ペアIDごとに土管を集める
            if (!string.IsNullOrEmpty(d.pairId))
            {
                PipeWarp pipe = obj.GetComponentInChildren<PipeWarp>();
                if (pipe != null)
                {
                    if (!pairs.ContainsKey(d.pairId))
                        pairs[d.pairId] = new List<PipeWarp>();
                    pairs[d.pairId].Add(pipe);
                }
            }
        }

        // ちょうど2つ揃ったペアだけ、お互いを接続する
        foreach (var kv in pairs)
        {
            if (kv.Value.Count != 2) continue;
            kv.Value[0].Connect(kv.Value[1]);
            kv.Value[1].Connect(kv.Value[0]);
        }

        // 惑星のスケールがコライダーに反映されてから距離を測る
        Physics.SyncTransforms();

        foreach (var kv in chains)
        {
            var list = kv.Value;
            if (list.Count < 2) continue;

            // 置いた順に並べ、前のゾーンの「次」を設定する
            list.Sort((a, b) => a.order.CompareTo(b.order));
            for (int i = 0; i < list.Count - 1; i++)
                list[i].zone.SetNext(list[i + 1].zone);

            // 終点は、いちばん近い惑星に着地させる
            var last = list[list.Count - 1].zone;
            last.SetTargetPlanet(FindNearestAttractor(last.transform.position));
        }

        // ランチャーの行き先を接続する
        foreach (var (launcher, targetUid) in launchers)
        {
            if (byUid.TryGetValue(targetUid, out GameObject targetObj))
                launcher.SetTarget(targetObj.transform);
        }

        if (spawn != null)
            ApplySpawn(spawn);
    }

    private static GravityAttractor FindNearestAttractor(Vector3 pos)
    {
        GravityAttractor best = null;
        float min = float.MaxValue;

        foreach (var a in GravityAttractor.All)
        {
            float d = a.DistanceToSurface(pos);
            if (d < min) { min = d; best = a; }
        }
        return best;
    }

    private void ApplySpawn(PlayerSpawnPoint spawn)
    {
        spawn.HideForPlay();

        // 通常はリスポーン地点として登録する。
        // PlayerController.Start がそこへ移動し、死亡時の復活先にもなる
        if (RespawnManager.Instance != null)
        {
            RespawnManager.Instance.SetRespawnPoint(spawn.transform);
            appliedSpawn = true;
            return;
        }

        // RespawnManager が無いシーン（プレイ用シーンを直接再生した時）は直接移動する
        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player == null) return;

        Rigidbody rb = player.GetComponent<Rigidbody>();
        rb.position = spawn.transform.position;
        rb.rotation = spawn.transform.rotation;
        rb.linearVelocity = Vector3.zero;
    }

    private void OnDestroy()
    {
        // プレイ用シーンを出る時に、ステージのスタート地点を解除する
        if (appliedSpawn && RespawnManager.Instance != null)
            RespawnManager.Instance.ClearRespawnPoint();
    }
}