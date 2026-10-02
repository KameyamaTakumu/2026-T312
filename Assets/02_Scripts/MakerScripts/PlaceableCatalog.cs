using UnityEngine;

[CreateAssetMenu(menuName = "Editor/Placeable Catalog")]
public class PlaceableCatalog : ScriptableObject
{
    public PlaceableItem[] items;

    public PlaceableItem Find(string id)
    {
        foreach (var i in items)
            if (i != null && i.id == id) return i;
        return null;
    }
}