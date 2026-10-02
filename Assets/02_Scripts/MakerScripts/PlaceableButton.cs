using UnityEngine;

public class PlaceableButton : MonoBehaviour
{
    [SerializeField] private LevelEditor editor;
    [SerializeField] private PlaceableItem item;

    public void Pick() => editor.Select(item);
}