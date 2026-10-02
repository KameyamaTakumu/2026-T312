using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PlacedData
{
    public string id;
    public Vector3 pos;
    public Quaternion rot;
    public float scale = 1f;
}

[Serializable]
public class LevelData
{
    public List<PlacedData> items = new List<PlacedData>();
}

/// <summary>配置したオブジェクトに付けて、ID とスケールを覚えさせる</summary>
public class PlacedObject : MonoBehaviour
{
    public string id;
    public float scale = 1f;
}