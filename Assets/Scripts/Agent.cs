using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Agent : MonoBehaviour
{
    public List<GridTile> path;
    public GridTile startTile, endTile;

    public int currentWayPoint;
    public float moveSpeed;
    public float distanceThresholdToSelectNextWaypoint = 0.15f;

    void OnDrawGizmos()
    {
        if (path == null || path.Count < 2) return;

        for (int i = 0; i < path.Count; i++)
        {
            if (i + 1 < path.Count)
            {
                Debug.DrawLine(path[i].transform.position, path[i + 1].transform.position);
            }
        }
    }
}