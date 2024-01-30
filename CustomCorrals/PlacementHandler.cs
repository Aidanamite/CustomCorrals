using System.Collections.Generic;
using UnityEngine;
using SRML.SR.SaveSystem.Data;
using InControl;
using System.Linq;

namespace CustomCorrals
{
    class PlacementHandler : SRSingleton<PlacementHandler>
    {
        public WeaponVacuum vacuum;
        public PlacementState state;
        public PlacementMode mode;
        public bool snapping = false;
        public int Cost => mode == PlacementMode.Wall ? placingCount * Main.wallCost : placingPlatform.Cost;
        public static List<Walls> wallSets = new List<Walls>();
        List<List<WallComponent>> placingWalls = new List<List<WallComponent>>();
        PlatformComponent placingPlatform = null;
        public static List<Transform> landPlotPosts = new List<Transform>();
        int placingCount { get { var c = 0; foreach (var l in placingWalls) c += l.Count; return c - 1; } }
        public override void Awake()
        {
            base.Awake();
            vacuum = GetComponent<WeaponVacuum>();
        }
        public static void OnInputChange(PlayerAction action)
        {
            if (Instance)
            {
                if (action == null)
                {
                    foreach (var i in Instance.hints)
                        i.Value.Refresh();
                }
                else if (Instance.hints.TryGetValue(action, out var h))
                    h.Refresh();
            }
        }
        public Hint GetHint(PlayerAction action)
        {
            if (hints.TryGetValue(action, out var hint))
                return hint;
            return hints[action] = new Hint(action);
        }
        Dictionary<PlayerAction, Hint> hints = new Dictionary<PlayerAction, Hint>();
        public class Hint
        {
            public Hint(PlayerAction Action) => action = Action;
            GameObject obj;
            string text;
            PlayerAction action;
            public string Text
            {
                get => text;
                set
                {
                    if (text == value)
                        return;
                    text = value;
                    if (value == null)
                    {
                        if (obj)
                            Destroy(obj);
                        return;
                    }
                    if (!obj)
                        obj = Clues.CreateClue(() => $"[{ContextClues.ContextClue.GetKeyString(action) ?? "UNSET"}]: {text}");
                    else
                        Refresh();
                }
            }
            public void Refresh() { if (obj) obj.SendMessage("Refresh"); }
        }
        void Update()
        {
            if (!vacuum.InGadgetMode())
            {
                foreach (var p in hints)
                    p.Value.Text = null;
                if (state != PlacementState.None)
                    CancelPlacement();
                return;
            }
            if (state == PlacementState.None)
            {
                GetHint(Main.confirm).Text = "Start placing " + mode.ToString().ToLowerInvariant();
                GetHint(Main.step).Text = "Change placing mode";
                GetHint(Main.snap).Text = null;
                var rayed = Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out RaycastHit hit3, 100, -1);
                var wall = rayed ? hit3.collider.GetComponentInParent<WallComponent>() : null;
                var platform = rayed ? hit3.collider.GetComponentInParent<PlatformComponent>() : null;
                GetHint(Main.back).Text = wall ? "Upgrade/destroy wall" : platform ? "Destroy platform" : null;
                if (Main.confirm.WasPressed)
                    PreparePlacement();
                else if (Main.back.WasPressed)
                {
                    if (wall)
                        Main.CreatePurchaseUI(wall);
                    else if (platform)
                        Main.CreatePurchaseUI(platform);
                }
                else if (Main.step.WasPressed)
                    Main.CreateModeUI(this);
            } else if (state == PlacementState.Start)
            {
                GetHint(Main.confirm).Text = "Confirm start point";
                GetHint(Main.back).Text = "Cancel placement";
                GetHint(Main.step).Text = null;
                GetHint(Main.snap).Text = "Toggle snapping";
                if (Main.confirm.WasPressed)
                    StartPlacement();
                else if (Main.back.WasPressed)
                    CancelPlacement();
            } else if (state == PlacementState.Started)
            {
                GetHint(Main.confirm).Text = (mode == PlacementMode.Wall ? placingCount > 0 : mode == PlacementMode.Platform ? placingPlatform.Points >= 3 : false) ? "Finish placement" : null;
                GetHint(Main.back).Text = "Undo placement step";
                GetHint(Main.step).Text = "Step placement";
                GetHint(Main.snap).Text = "Toggle snapping";
                if (Main.confirm.WasPressed && (mode == PlacementMode.Wall ? placingCount > 0 : mode == PlacementMode.Platform ? placingPlatform.Points >= 3 : false))
                    Utils.Purchase(null, false, EndPlacement, Cost);
                else if (Main.back.WasPressed)
                    CancelStepPlacement();
                else if (Main.step.WasPressed)
                    StepPlacement();
            }
            if (Main.snap.WasPressed)
                snapping = !snapping;
            if (Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out RaycastHit hit, 100, 1))
            {
                if (snapping)
                {
                    var n = Vector3.zero;
                    if (mode == PlacementMode.Platform && Config.platformsSnapToCorners)
                        n = PlatformComponent.FindNearestToPoint(hit.point);
                    if (mode == PlacementMode.Platform && Config.platformsSnapToEdges && Vector3.Distance(n, hit.point) > Config.snapDistance)
                        n = PlatformComponent.FindNearestPointOnEdgeAll(hit.point);
                    if (mode == PlacementMode.Wall || (mode == PlacementMode.Platform && Vector3.Distance(n,hit.point) > Config.snapDistance && Config.platformsSnapToWalls))
                        n = WallComponent.FindNearestToPoint(hit.point);
                    if (Vector3.Distance(n, hit.point) <= Config.snapDistance)
                        hit.point = n;
                }
                if (state == PlacementState.Start)
                {
                    if (mode == PlacementMode.Wall)
                    {
                        placingWalls[0][0].transform.position = hit.point;
                        placingWalls[0][0].Zone = SRSingleton<SceneContext>.Instance.PlayerZoneTracker.GetCurrentZone();
                    }
                    else if (mode == PlacementMode.Platform)
                        placingPlatform.ChangePoint(hit.point);
                } else if (state == PlacementState.Started)
                {
                    if (mode == PlacementMode.Wall)
                    {
                        var l = placingWalls.Last();
                        var s = l[0].transform.position;
                        var e = hit.point;
                        e.y = s.y;
                        var d = e - s;
                        if (e != s)
                        {
                            if (0 == Main.wallLength)
                                throw new System.Exception("wall length is ZERO");
                            var c = Mathf.CeilToInt(Mathf.Max(d.magnitude / Main.wallLength - 0.05f, 0)) + 1;
                            while (c > l.Count)
                            {
                                l.Insert(0, CreateWall(null, true));
                                l[0].Zone = l[1].Zone;
                            }
                            while (c < l.Count)
                            {
                                Destroy(l[0].gameObject);
                                l.RemoveAt(0);
                            }
                            var a = Quaternion.LookRotation(d);
                            for (int i = 0; i < l.Count; i++)
                            {
                                l[i].transform.rotation = a;
                                l[i].transform.position = s + d.normalized * i * Main.wallLength;
                            }
                        }
                    }
                    if (mode == PlacementMode.Platform)
                        placingPlatform.ChangePoint(hit.point);
                }
            }
        }

        void CancelPlacement()
        {
            //Main.Log("cancel");
            state = PlacementState.None;
            if (mode == PlacementMode.Wall)
            {
                foreach (var l in placingWalls)
                    foreach (var o in l)
                        Destroy(o.gameObject);
                placingWalls.Clear();
            } else if (mode == PlacementMode.Platform)
            {
                Destroy(placingPlatform.gameObject);
                placingPlatform = null;
            }
        }

        void PreparePlacement()
        {
            //Main.Log("prep");
            state = PlacementState.Start;
            if (mode == PlacementMode.Wall)
                placingWalls.Add(new List<WallComponent>(){ CreatePost(null, true) });
            if (mode == PlacementMode.Platform)
            {
                placingPlatform = CreatePlatform(null, true);
                placingPlatform.AddPoint();
                placingPlatform.ShowPointers = true;
            }
            landPlotPosts.Clear();
            foreach (var l in Resources.FindObjectsOfTypeAll<LandPlot>())
                landPlotPosts.AddRange(l.transform.FindChildrenRecursively("corralPost"));
        }

        void StartPlacement()
        {
            //Main.Log("start");
            state = PlacementState.Started;
            if (mode == PlacementMode.Platform)
                StepPlacement();
        }

        void CancelStepPlacement()
        {
            //Main.Log("cancel step");
            if (mode == PlacementMode.Wall)
            {
                if (placingWalls.Count <= 1)
                    CancelPlacement();
                else
                {
                    var l = placingWalls.Last();
                    var i = l.Last();
                    l.Remove(i);
                    foreach (var g in l)
                        Destroy(g.gameObject);
                    placingWalls.Remove(l);
                    placingWalls.Last().Add(i);
                }
            }else if (mode == PlacementMode.Platform)
            {
                if (placingPlatform.Points <= 2)
                    CancelPlacement();
                else
                    placingPlatform.RemovePoint();
            }
        }

        void StepPlacement()
        {
            //Main.Log("step");
            if (mode == PlacementMode.Wall)
            {
                var l = placingWalls.Last();
                if (l.Count == 1)
                    return;
                var p = l.Last();
                l.Remove(p);
                placingWalls.Add(new List<WallComponent>() { p });
            } else if (mode == PlacementMode.Platform)
                placingPlatform.AddPoint();
        }

        void EndPlacement()
        {
            //Main.Log("end");
            state = PlacementState.None;
            if (mode == PlacementMode.Wall)
            {
                foreach (var l in placingWalls)
                    foreach (var i in l)
                        i.transform.Find("corralPost").GetComponent<CapsuleCollider>().enabled = true;
                wallSets.Add(new Walls(placingWalls));
                placingWalls.Clear();
            }
            else if (mode == PlacementMode.Platform)
            {
                if (placingPlatform.Points < 3)
                    Destroy(placingPlatform.gameObject);
                else
                {
                    placingPlatform.transform.Find("collider").GetComponent<MeshCollider>().enabled = true;
                    placingPlatform.ShowPointers = false;
                }
                placingPlatform = null;
            }
        }

        public static WallComponent CreatePost(Transform parent, bool disableCollider = false, bool tallVarient = false)
        {
            var g = Instantiate(tallVarient ? Main.post2Prefab : Main.postPrefab, parent, false).GetComponent<WallComponent>();
            if (disableCollider)
                g.Collider.enabled = false;
            return g;
        }
        public static WallComponent CreateWall(Transform parent,bool disableCollider = false, bool tallVarient = false)
        {
            var g = Instantiate(tallVarient ? Main.wall2Prefab : Main.wallPrefab, parent, false).GetComponent<WallComponent>();
            if (disableCollider)
                g.Collider.enabled = false;
            return g;
        }
        public static PlatformComponent CreatePlatform(Transform parent, bool disableCollider = false)
        {
            var g = Instantiate(Main.platformPrefab, parent, false).GetComponent<PlatformComponent>();
            if (disableCollider)
                g.Collider.enabled = false;
            return g;
        }

        public static void ReadData(CompoundDataPiece data)
        {
            //Main.Log("Read Data");
            wallSets.Clear();

            if (data.HasPiece("wallSets")) {
                var wallData = data.GetCompoundPiece("wallSets");
                var il = wallData.GetValue<int>("Count");
                for (int i = 0; i < il; i++)
                {
                    var jl = wallData.GetValue<int>(i + "_Count");
                    var walls = new List<Wall>();
                    for (int j = 0; j < jl; j++)
                        try
                        {
                            if (jl == 1 && wallData.GetValue<int>(i + "_" + j + "_length") == 0)
                                break;
                            walls.Add(new Wall(
                                wallData.GetValue<Vector3>(i + "_" + j + "_start"),
                                wallData.GetValue<float>(i + "_" + j + "_angle"),
                                wallData.GetValue<int>(i + "_" + j + "_length"),
                                wallData.GetValue<bool>(i + "_" + j + "_hasPost"),
                                wallData.GetValue<bool>(i + "_" + j + "_upgraded")));
                            //Main.LogSuccess($"Loaded wall {i}, {j}");
                        } catch (System.Exception e)
                        {
                            Main.LogError($"Failed to load wall {i}, {j} | Error: {e.GetType().Name} - {e.Message}");
                        }
                    wallSets.Add(new Walls(walls, wallData.HasPiece(i + "_Zone") ? wallData.GetValue<ZoneDirector.Zone>(i + "_Zone") : default(ZoneDirector.Zone?)));
                }
            }
            if (data.HasPiece("platforms"))
            {
                var plaformData = data.GetCompoundPiece("platforms");
                var il = plaformData.GetValue<int>("Count");
                for (int i = 0; i < il; i++)
                {
                    try
                    {
                        var jl = plaformData.GetValue<int>(i + "_Count");
                        var points = new List<Vector3>();
                        for (int j = 0; j < jl; j++)
                                points.Add(plaformData.GetValue<Vector3>(i + "_" + j));
                        CreatePlatform(null).SetPoints(points);
                    }
                    catch (System.Exception e)
                    {
                        Main.LogError($"Failed to load platform {i} | Error: {e.GetType().Name} - {e.Message}");
                    }
                }
            }
        }

        public static void WriteData(CompoundDataPiece data)
        {
            //Main.Log("Write Data");
            var sets = new CompoundDataPiece("wallSets");
            if (data.HasPiece("wallSets"))
            {
                sets = data.GetCompoundPiece("wallSets");
                sets.DataList.Clear();
            }
            else
                data.AddPiece(sets);
            sets.SetValue("Count", wallSets.Count);
            for (int i = 0; i < wallSets.Count; i++)
            {
                sets.SetValue(i + "_Count",wallSets[i].walls.Count);
                if (wallSets[i].zone != null)
                    sets.SetValue(i + "_Zone", wallSets[i].zone.Value);
                for (int j = 0; j < wallSets[i].walls.Count; j++)
                {
                    sets.SetValue(i + "_" + j + "_start", wallSets[i].walls[j].start);
                    sets.SetValue(i + "_" + j + "_angle", wallSets[i].walls[j].angle);
                    sets.SetValue(i + "_" + j + "_hasPost", wallSets[i].walls[j].hasPost);
                    sets.SetValue(i + "_" + j + "_length", wallSets[i].walls[j].length);
                    sets.SetValue(i + "_" + j + "_upgraded", wallSets[i].walls[j].upgraded);
                    //Main.Log($"Saved wall {i}, {j}");
                }
            }
            
            var platforms = new CompoundDataPiece("platforms");
            if (data.HasPiece("platforms"))
            {
                platforms = data.GetCompoundPiece("platforms");
                platforms.DataList.Clear();
            }
            else
                data.AddPiece(platforms);
            platforms.SetValue("Count", PlatformComponent.platforms.Count);
            for (int i = 0; i < PlatformComponent.platforms.Count; i++)
            {
                if (PlatformComponent.platforms[i] == Instance.placingPlatform)
                    continue;
                platforms.SetValue(i + "_Count", PlatformComponent.platforms[i].Points);
                for (int j = 0; j < PlatformComponent.platforms[i].Points; j++)
                    platforms.SetValue(i + "_" + j, PlatformComponent.platforms[i].GetPoint(j));
            }
        }
    }
}