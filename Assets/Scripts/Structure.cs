using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Structure : MonoBehaviour
{
    [System.Serializable]
    public struct StructurePoperty
    {
        public float requiredSpace;
        public float buildMaxTime;
        public Sprite graphic;
    }
    public enum StructureStatus { Building, Builded }
    public StructureStatus status;
    float buildTime;
    [HideInInspector] public SpriteRenderer rend;
    public StructurePoperty[] properties;
    public int structureToBuild;

    public void UpdateStructure()
    {
        if (buildTime == properties[structureToBuild].buildMaxTime || status == StructureStatus.Builded) return;

        status = buildTime >= properties[structureToBuild].buildMaxTime ? StructureStatus.Builded : StructureStatus.Building;

        buildTime = status == StructureStatus.Building ? buildTime + 1 * Time.deltaTime : properties[structureToBuild].buildMaxTime;

        rend.sprite = properties[structureToBuild].graphic;

        rend.color = status == StructureStatus.Building ? new Color(1, 1, 1, 0.8f) : new Color(1, 1, 1, 1);
    }
}