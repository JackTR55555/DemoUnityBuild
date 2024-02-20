using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridTile : MonoBehaviour
{
    public bool avaiable = true;
    public bool draw = true;
    public GridRect grid = new GridRect();
    [HideInInspector] public SpriteRenderer[] rends;

    #region Pathfinding
    public GridTile[] neighbors = new GridTile[0];
    [HideInInspector] public GridTile previousTile;
    [HideInInspector] public int g, h;
    [HideInInspector] public int f { get { return g + h; }}
    #endregion

    public void OnDrawGizmos()
    {
        if (!draw) return;

        if (!Application.isPlaying)
        {
            grid.up = new Vector2(transform.position.x + grid.offset.x, (transform.position.y + grid.offset.y) + grid.size);
            grid.down = new Vector2(transform.position.x + grid.offset.x, (transform.position.y + grid.offset.y) - grid.size);
            grid.left = new Vector2((transform.position.x + grid.offset.x) - grid.size * 2, transform.position.y + grid.offset.y);
            grid.right = new Vector2((transform.position.x + grid.offset.x) + grid.size * 2, transform.position.y + grid.offset.y);
        }

        Gizmos.color = avaiable ? grid.avaiableColor : grid.unavaiableColor;

        Gizmos.DrawLine(grid.up, grid.right);
        Gizmos.DrawLine(grid.right, grid.down);
        Gizmos.DrawLine(grid.down, grid.left);
        Gizmos.DrawLine(grid.left, grid.up);
    }
}
[System.Serializable]
public struct GridRect
{
    public Color avaiableColor, unavaiableColor;
    public float size;
    public Vector2 offset;
    public Vector2 center;
    [HideInInspector] public Vector2 up, down, left, right;
}