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

        foreach (var d in data.items)
        {
            var item = catalog.Find(d.id);
            if (item == null) continue;

            GameObject obj = Instantiate(item.prefab, d.pos, d.rot);
            obj.transform.localScale = Vector3.one * d.scale;

            var sp = obj.GetComponent<PlayerSpawnPoint>();
            if (sp != null)
                spawn = sp;

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

        if (spawn != null)
            ApplySpawn(spawn);
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