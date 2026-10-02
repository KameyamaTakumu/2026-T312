using System.IO;
using UnityEngine;

public class LevelLoader : MonoBehaviour
{
    [SerializeField] private PlaceableCatalog catalog;
    [SerializeField] private string levelName = "level01";

    private void Awake()
    {
        string path = Path.Combine(Application.persistentDataPath, levelName + ".json");
        if (!File.Exists(path)) return;

        var data = JsonUtility.FromJson<LevelData>(File.ReadAllText(path));
        foreach (var d in data.items)
        {
            var item = catalog.Find(d.id);
            if (item == null) continue;

            GameObject obj = Instantiate(item.prefab, d.pos, d.rot);
            obj.transform.localScale = Vector3.one * d.scale;
        }
    }
}