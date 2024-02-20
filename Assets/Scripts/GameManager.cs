using JetBrains.Annotations;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using static UnityEngine.Rendering.DebugUI.Table;
/// <summary>
/// PENDANT | POSSIBLE OPTIMIZATION | TIPS | TECHNIQUES TO USE | ANOTATIONS
/// -Divide the world grid into chunks of Tiles and only analyze the Tiles inside chunks that fulfill certain parametters (Mouse Proximity etc)
/// -Encapsulate some usefull and often used methods and operations inside Namespaces to avoid spaguetti code and make reusable code
/// -Use Scriptable objects to hold Structures & Tiles properties
/// -Avoid some elements update in every frame if possible, try not to create arrays and lists in realtime, its more time consuming but its possible and can help to maintain an stable framerate
/// -More intuitive names for some variables (i was lazy about making this test)
/// -This code and also any game that require an exact simulation behaviour should be deterministic (it can be done here), that way you ensure consistency across platforms, and it helps if you want to make the game multiplayer in the future, with a byte per byte deterministic behaviour you can implement Lockstep or Rollback Netcode which is great!
/// -The use of the SAT(separated axis theorem is a plus since you dont have to rely on unity's physics engine and its very adaptable for many situations, it can be optimized by separating the sumulation area in Chunks too)
/// -The grid object is fully customizable thanks to the SAT and the PointInsideTile methods, so you can have buildings of any shape || size, and it will works fine, though it can be improbed a lot
/// -I didnt used any optimization technique due to time lack, thats the reason i put some thoughts here
/// -Every Structure takes 1 second to finish build but you can modify that on the Structure Properties object from the inspector (can be added to a scriptable object as mentioned before)
/// -Navigation algorithm used was A*, which is pretty simple and effective, tho it can be optimized a lot
/// -Right now the algorithm is not dynamic which means that if you build an structure in the middle of the Agent's path, it wont avoid it, the pathfinding algorithm will find the way across obstacles and or non walkable areas (red tiles) placed before its calculation (FindPath()), it can be called anytime to have the desired behaviour tho
/// The Pathfinding algorithm needs some tweeks, specially in the distance calculation which is causing diagonal movement not being correctly executed (implement a custom distance formula ? Manhatam ?? euclidean ??)
/// </summary>
public class GameManager : MonoBehaviour
{
    [SerializeField] byte checkDistance = 1;//the lower the value the closer the world tile center needs to be from the mouse position to perform any check for collisions or other operations, less presition but more performance
    [SerializeField] AnimationCurve worldTileGridAlpha;
    bool showBuildMenu = false;
    int selectedStructure = -1;
    [SerializeField] Vector2Int tileAmmount;
    [SerializeField] Vector2 spacing;
    [SerializeField] Vector2 offset;
    Vector2 currentTilePosition;
    Vector2 mousePos;
    [SerializeField] GameObject gridTilePrefab;
    [SerializeField] GameObject structure;
    [SerializeField] Agent agent;
    [SerializeField] UnityEvent OnShowBuildMenu, OnHideBuildMenu;
    [SerializeField] GridTile[] worldGridTiles = new GridTile[0];
    [Header("Exposed for debug purposes")]
    [SerializeField] GridTile builderTile = new GridTile();
    List<Structure> structures = new List<Structure>();//A list since we want to Add new structures and maybe be able to remove them
    Camera cam;

    void Start()
    {
        cam = Camera.main;
    }

    void Update()
    {
        mousePos = cam.ScreenToWorldPoint(Input.mousePosition);
        ManageControls();
        CheckForPointedInsideMapTile();
        CheckForBuilderTileValidPosition();
        UpdateMapTile();
        UpdateBuilderTile();
        UpdateSructures();
        UpdateAgents();
    }

    [ContextMenu("Auto generate new world grid")]
    public void BuildWorldGridOnce()
    {
        #region Clean old world & Builder Tile
        int childCount = transform.childCount;
        for (int i = childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            DestroyImmediate(child.gameObject);
        }
        worldGridTiles = new GridTile[0];
        if(builderTile != null) DestroyImmediate(builderTile.gameObject);
        builderTile = null;
        #endregion

        #region Create new world & Builder Tile
        if (gridTilePrefab != null)
        {
            builderTile = Instantiate(gridTilePrefab, transform.position, Quaternion.identity).GetComponent<GridTile>();
            builderTile.grid.avaiableColor = Color.green;
            builderTile.grid.unavaiableColor = Color.red;
            builderTile.rends = new SpriteRenderer[2];
            builderTile.rends[0] = builderTile.gameObject.transform.GetComponent<SpriteRenderer>();
            builderTile.rends[1] = builderTile.gameObject.transform.GetChild(0).GetComponent<SpriteRenderer>();

            worldGridTiles = new GridTile[tileAmmount.x * tileAmmount.y];
            for (int i = 0; i < worldGridTiles.Length; i++)
            {
                worldGridTiles[i] = Instantiate(gridTilePrefab, transform.position, Quaternion.identity, this.transform).GetComponent<GridTile>();
                worldGridTiles[i].rends = new SpriteRenderer[2];
                worldGridTiles[i].rends[0] = worldGridTiles[i].gameObject.transform.GetComponent<SpriteRenderer>();
                worldGridTiles[i].rends[1] = worldGridTiles[i].gameObject.transform.GetChild(0).GetComponent<SpriteRenderer>();
                worldGridTiles[i].rends[1].transform.localScale = new Vector2(worldGridTiles[i].grid.size * 0.9f, worldGridTiles[i].grid.size * 0.9f);
            }
        }
        #endregion

        #region Update the World Grid position
        int index = 0;
        for (int y = 0; y < tileAmmount.y; y++)
        {
            for (int x = 0; x < tileAmmount.x; x++)
            {
                Vector3 basePosition = new Vector2(x, y);
                float xOffset = (y % 2 == 0) ? 0 : 0.5f;
                Vector3 positionWithOffset = new Vector3((basePosition.x * spacing.x) + xOffset + offset.x, (basePosition.y * spacing.y) + offset.y, 0);

                if (index < worldGridTiles.Length)
                {
                    worldGridTiles[index].transform.position = positionWithOffset;
                    worldGridTiles[index].grid.center = new Vector2(positionWithOffset.x, positionWithOffset.y);
                    index++;
                }
            }
        }
        #endregion

        FindNeighborsTiles(tileAmmount.x, tileAmmount.y, worldGridTiles);
    }

    public void SelectStructure(int id)
    {
        selectedStructure = id;
        builderTile.grid.size = structure.GetComponent<Structure>().properties[id].requiredSpace;
        builderTile.rends[0].sprite = structure.GetComponent<Structure>().properties[id].graphic;
        builderTile.rends[1].transform.localScale = new Vector2(builderTile.grid.size, builderTile.grid.size);
    }

    void ManageControls()
    {
        if (!showBuildMenu)
        {
            if (Input.GetMouseButtonDown(2))
            {
                if (!showBuildMenu)
                {
                    showBuildMenu = true;
                    OnShowBuildMenu.Invoke();
                }
            }
        }
        else
        {
            if (Input.GetMouseButtonDown(1))
            {
                showBuildMenu = false;
                selectedStructure = -1;
                builderTile.rends[0].sprite = null;
                builderTile.avaiable = false;
                OnHideBuildMenu.Invoke();
            }
        }
    }

    void PlaceStructure()
    {
        GameObject obj = Instantiate(structure, currentTilePosition, Quaternion.identity);
        var s = obj.GetComponent<Structure>();
        s.rend = obj.GetComponent<SpriteRenderer>();
        s.structureToBuild = selectedStructure;
        structures.Add(s);
    }

    void CheckForPointedInsideMapTile()
    {
        if (worldGridTiles == null || worldGridTiles.Length == 0) return;

        foreach (var item in worldGridTiles)
        {
            float dist = Vector2.Distance(mousePos, new Vector2(item.grid.center.x, item.grid.center.y));

            if (dist > checkDistance) continue;

            bool pointInsideTile = !(dist > checkDistance) && PointInsideTile(mousePos, item.grid.up, item.grid.right, item.grid.down, item.grid.left);

            if (!pointInsideTile) continue;

            if (pointInsideTile)
            {
                currentTilePosition = item.grid.center;

                if (Input.GetMouseButtonDown(0)) agent.endTile = item;
            }
        }

        foreach (var item in worldGridTiles)
        {
            float dist = Vector2.Distance(agent.gameObject.transform.position, new Vector2(item.grid.center.x, item.grid.center.y));

            if (dist > checkDistance) continue;

            bool agentInsideTile = !(dist > checkDistance) && PointInsideTile(agent.gameObject.transform.position, item.grid.up, item.grid.right, item.grid.down, item.grid.left);

            if (agentInsideTile)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    agent.startTile = item;
                    if (agent.startTile != null && agent.endTile != null && !showBuildMenu)
                    {
                        agent.path = FindPath(agent.startTile, agent.endTile);
                        agent.currentWayPoint = agent.path.Count - 1;
                    }
                } 
            }
        }
    }

    void CheckForBuilderTileValidPosition()
    {
        if (showBuildMenu == false || selectedStructure < 0) return;

        var worldTilesInsideBuilderTile = worldGridTiles.Where(item => AreQuadsColliding(builderTile.grid.up, builderTile.grid.right, builderTile.grid.down, builderTile.grid.left, item.grid.up, item.grid.right, item.grid.down, item.grid.left)).ToList();

        builderTile.avaiable = !worldTilesInsideBuilderTile.Any(item => !item.avaiable);

        if (!builderTile.avaiable) return;

        if (Input.GetMouseButtonDown(0) && builderTile.avaiable)
        {
            worldTilesInsideBuilderTile.ForEach(item => item.avaiable = false);
            PlaceStructure();
        }
    }

    void UpdateMapTile()
    {
        foreach (var item in worldGridTiles)
        {
            float dist = Vector2.Distance(mousePos, new Vector2(item.grid.center.x, item.grid.center.y));

            item.grid.up = new Vector2(item.transform.position.x + item.grid.offset.x, (item.transform.position.y + item.grid.offset.y) + item.grid.size);
            item.grid.down = new Vector2(item.transform.position.x + item.grid.offset.x, (item.transform.position.y + item.grid.offset.y) - item.grid.size);
            item.grid.left = new Vector2((item.transform.position.x + item.grid.offset.x) - item.grid.size * 2, item.transform.position.y + item.grid.offset.y);
            item.grid.right = new Vector2((item.transform.position.x + item.grid.offset.x) + item.grid.size * 2, item.transform.position.y + item.grid.offset.y);

            item.draw = showBuildMenu == true && selectedStructure >= 0;
            item.rends[1].color = item.avaiable ? item.grid.avaiableColor * new Color(1, 1, 1, worldTileGridAlpha.Evaluate(dist)) : item.grid.unavaiableColor * new Color(1, 1, 1, worldTileGridAlpha.Evaluate(dist));
        }
    }

    void UpdateBuilderTile()
    {
        builderTile.transform.position = new Vector3(currentTilePosition.x, currentTilePosition.y, 0);

        builderTile.grid.up = new Vector2(builderTile.transform.position.x + builderTile.grid.offset.x, (builderTile.transform.position.y + builderTile.grid.offset.y) + builderTile.grid.size);
        builderTile.grid.down = new Vector2(builderTile.transform.position.x + builderTile.grid.offset.x, (builderTile.transform.position.y + builderTile.grid.offset.y) - builderTile.grid.size);
        builderTile.grid.left = new Vector2((builderTile.transform.position.x + builderTile.grid.offset.x) - builderTile.grid.size * 2, builderTile.transform.position.y + builderTile.grid.offset.y);
        builderTile.grid.right = new Vector2((builderTile.transform.position.x + builderTile.grid.offset.x) + builderTile.grid.size * 2, builderTile.transform.position.y + builderTile.grid.offset.y);

        builderTile.draw = showBuildMenu == true && selectedStructure >= 0;
        builderTile.rends[0].color = builderTile.avaiable ? Color.white : Color.red;
        builderTile.rends[1].color = builderTile.avaiable ? Color.green : Color.red;
        builderTile.rends[1].gameObject.SetActive(showBuildMenu == true && selectedStructure >= 0);
    }

    void UpdateSructures()
    {
        foreach (var item in structures)
        {
            item.UpdateStructure();
        }
    }

    void UpdateAgents()
    {
        if (agent.path.Count <= 0 || agent.currentWayPoint < 0) return;

        agent.transform.position = Vector3.MoveTowards(agent.transform.position, agent.path[agent.currentWayPoint].transform.position, agent.moveSpeed * Time.deltaTime);

        if (Vector3.Distance(agent.transform.position, agent.path[agent.currentWayPoint].transform.position) <= agent.distanceThresholdToSelectNextWaypoint)
        {
            agent.currentWayPoint --;
        }
    }

    #region Utiles
    bool PointInsideTile(Vector2 mousePos, Vector2 v1, Vector2 v2, Vector2 v3, Vector2 v4)
    {
        bool CheckCrossProductSign(Vector2 a, Vector2 b, Vector2 c)
        {
            Vector2 vectorA = b - a;
            Vector2 vectorB = c - a;
            float crossProduct = vectorA.x * vectorB.y - vectorA.y * vectorB.x;
            return crossProduct > 0;
        }

        bool sign1 = CheckCrossProductSign(v1, v2, mousePos);
        bool sign2 = CheckCrossProductSign(v2, v3, mousePos);
        bool sign3 = CheckCrossProductSign(v3, v4, mousePos);
        bool sign4 = CheckCrossProductSign(v4, v1, mousePos);

        return (sign1 == sign2) && (sign2 == sign3) && (sign3 == sign4);
    }

    void FindNeighborsTiles(int cols, int rows, GridTile[] tiles)
    {
        for (int x = 0; x < rows; x++)
        {
            for (int y = 0; y < cols; y++)
            {
                int index = x * cols + y;
                List<GridTile> neighbors = new List<GridTile>();

                bool isOffsetRow = (x % 2 == 1);

                if (y > 0) neighbors.Add(tiles[index - 1]);
                if (y < cols - 1) neighbors.Add(tiles[index + 1]);

                if (x > 0)
                {
                    int northIndexOffset = isOffsetRow ? 0 : -1;
                    if (y + northIndexOffset >= 0) neighbors.Add(tiles[index - cols + northIndexOffset]); // Up Left
                    if (y + northIndexOffset + 1 < cols) neighbors.Add(tiles[index - cols + northIndexOffset + 1]); // Up Right
                }
                if (x < rows - 1)
                {
                    int southIndexOffset = isOffsetRow ? 0 : -1;
                    if (y + southIndexOffset >= 0) neighbors.Add(tiles[index + cols + southIndexOffset]); // Down Left
                    if (y + southIndexOffset + 1 < cols) neighbors.Add(tiles[index + cols + southIndexOffset + 1]); // Down Right
                }
                tiles[index].neighbors = neighbors.ToArray();
            }
        }
    }

    List<GridTile> FindPath(GridTile startTile, GridTile endTile)
    {
        List<GridTile> checking = new List<GridTile>() { startTile };
        List<GridTile> done = new List<GridTile>();

        while (checking.Any())
        {
            var current = checking[0];
            foreach (var item in checking)
            {
                if (item.f < current.f || (item.f == current.f && item.h < current.h))
                {
                    current = item;
                }
            }

            done.Add(current);
            checking.Remove(current);

            if (current == endTile)
            {
                var pathTile = endTile;
                var path = new List<GridTile>();
                while (pathTile != startTile)
                {
                    path.Add(pathTile);
                    pathTile = pathTile.previousTile;
                }
                return path;
            }

            foreach (var neighbor in current.neighbors.Where(i => i.avaiable && !done.Contains(i)))
            {
                bool inChecking = checking.Contains(neighbor);

                int costToNeighbor = (int)(current.g + (Vector3.Distance(current.transform.position, neighbor.transform.position)) * 10);

                if (!inChecking || (costToNeighbor < neighbor.g))
                {
                    neighbor.g = costToNeighbor;
                    neighbor.previousTile = current;
                    if (!inChecking)
                    {
                        neighbor.h = (int)(Vector3.Distance(neighbor.transform.position, endTile.transform.position) * 10);
                        checking.Add(neighbor);
                    }
                }
            }
        }
        return null;
    }
    #endregion

    #region SAT (Separated Axis Teorem) for collision between the builder tile and every world tile
    bool AreQuadsColliding(Vector2 quad1A, Vector2 quad1B, Vector2 quad1C, Vector2 quad1D, Vector2 quad2A, Vector2 quad2B, Vector2 quad2C, Vector2 quad2D)
    {
        Vector2[] axes1 = {
            (quad1B - quad1A).normalized,
            (quad1C - quad1B).normalized,
            (quad1D - quad1C).normalized,
            (quad1A - quad1D).normalized
        };

        for (int i = 0; i < axes1.Length; i++)
        {
            axes1[i] = new Vector2(-axes1[i].y, axes1[i].x);
        }

        Vector2[] axes2 = {
            (quad2B - quad2A).normalized,
            (quad2C - quad2B).normalized,
            (quad2D - quad2C).normalized,
            (quad2A - quad2D).normalized
        };

        for (int i = 0; i < axes2.Length; i++)
        {
            axes2[i] = new Vector2(-axes2[i].y, axes2[i].x);
        }

        foreach (Vector2 axis in axes1)
        {
            if (!IsOverlapOnAxis(axis, quad1A, quad1B, quad1C, quad1D, quad2A, quad2B, quad2C, quad2D)) return false;
        }

        foreach (Vector2 axis in axes2)
        {
            if (!IsOverlapOnAxis(axis, quad1A, quad1B, quad1C, quad1D, quad2A, quad2B, quad2C, quad2D)) return false;
        }

        return true;
    }

    bool IsOverlapOnAxis(Vector2 axis, Vector2 q1A, Vector2 q1B, Vector2 q1C, Vector2 q1D, Vector2 q2A, Vector2 q2B, Vector2 q2C, Vector2 q2D)
    {
        float minQuad1, maxQuad1;
        ProjectVerticesOnAxis(axis, q1A, q1B, q1C, q1D, out minQuad1, out maxQuad1);
        float minQuad2, maxQuad2;
        ProjectVerticesOnAxis(axis, q2A, q2B, q2C, q2D, out minQuad2, out maxQuad2);
        return maxQuad1 >= minQuad2 && maxQuad2 >= minQuad1;
    }

    void ProjectVerticesOnAxis(Vector2 axis, Vector2 v1, Vector2 v2, Vector2 v3, Vector2 v4, out float min, out float max)
    {
        float dot1 = Vector2.Dot(axis, v1);
        float dot2 = Vector2.Dot(axis, v2);
        float dot3 = Vector2.Dot(axis, v3);
        float dot4 = Vector2.Dot(axis, v4);

        min = Mathf.Min(dot1, dot2, dot3, dot4);
        max = Mathf.Max(dot1, dot2, dot3, dot4);
    }
    #endregion
}