using UnityEngine;
using System.Collections.Generic;

namespace CustomCorrals
{
    public class PlaceableComponent : MonoBehaviour
    {
        protected Collider collider;
        public Collider Collider => collider;
    }

    public class WallComponent : PlaceableComponent
    {
        static List<WallComponent> walls = new List<WallComponent>();
        public Wall Wall;
        bool Upgraded => Wall == null ? false : Wall.upgraded;
        SkinnedMeshRenderer renderer;
        List<WallComponent> disables = new List<WallComponent>();
        ZoneDirector.Zone zone = ZoneDirector.Zone.NONE;
        Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
        Material[] GetMaterials(Renderer renderer)
        {
            if (originalMaterials.TryGetValue(renderer, out var original))
                return original;
            return originalMaterials[renderer] = renderer.sharedMaterials;
        }
        public ZoneDirector.Zone Zone
        {
            get => zone;
            set
            {
                if (zone == value)
                    return;
                zone = value;
                foreach (var r in GetComponentsInChildren<Renderer>(true))
                {
                    var o = GetMaterials(r);
                    var m = new Material[o.Length];
                    for (int i = 0; i < o.Length; i++)
                        m[i] = o[i] ? (SRSingleton<SceneContext>.Instance.RanchDirector?.GetRecolorMaterial(o[i], zone) ?? o[i]) : o[i];
                    r.sharedMaterials = m;
                }
            }
        }
        void Awake()
        {
            walls.Add(this);
            renderer = transform.Find("corralPost").GetComponent<SkinnedMeshRenderer>();
            collider = transform.Find("corralPost").GetComponent<CapsuleCollider>();
        }
        void OnDestroy()
        {
            walls.Remove(this);
        }

        void Update()
        {
            disables.Clear();
            if (walls.Exists((x) => x.disables.Contains(this) && x.renderer.enabled))
            {
                renderer.enabled = false;
                return;
            }
            foreach (var a in walls)
            {
                if (a == this)
                    break;
                if ((a.transform.position - transform.position).magnitude < 0.1f)
                {
                    if (!a.Upgraded && Upgraded)
                        disables.Add(a);
                    else if (a.renderer.enabled)
                    {
                        renderer.enabled = false;
                        return;
                    }
                }

            }
            renderer.enabled = true;
        }
        public static Vector3 FindNearestToPoint(Vector3 point)
        {
            var d = float.PositiveInfinity;
            var p = Vector3.zero;
            foreach (var v in walls)
            {
                if (!v.collider.enabled)
                    continue;
                var i = v.transform.position;
                if (Vector3.Distance(i, point) < d)
                {
                    d = Vector3.Distance(i, point);
                    p = i;
                }
                i += Vector3.up * ((CapsuleCollider)v.collider).height + Vector3.down * 0.45f;
                if (Vector3.Distance(i, point) < d)
                {
                    d = Vector3.Distance(i, point);
                    p = i;
                }
            }
            foreach (var v in PlacementHandler.landPlotPosts)
                if (v.gameObject.activeInHierarchy)
                {
                    var collider = v.GetComponent<CapsuleCollider>();
                    if (!collider || !collider.enabled)
                        continue;
                    var i = v.position;
                    if (Vector3.Distance(i, point) < d)
                    {
                        d = Vector3.Distance(i, point);
                        p = i;
                    }
                    i += Vector3.up * collider.height + Vector3.down * 0.45f;
                    if (Vector3.Distance(i, point) < d)
                    {
                        d = Vector3.Distance(i, point);
                        p = i;
                    }
                }
            return p;
        }
    }

    public class PlatformComponent : PlaceableComponent
    {
        public static List<PlatformComponent> platforms = new List<PlatformComponent>();
        Mesh mesh;
        List<Vector3> points;
        public int Points => points.Count;
        public float SurfaceArea { get; private set; }
        public float OuterEdge { get; private set; }
        public int Cost => Mathf.CeilToInt(Mathf.Max(SurfaceArea, OuterEdge / 2) * Main.platformCost);
        public int DestroyCost => Mathf.CeilToInt(Mathf.Max(SurfaceArea, OuterEdge / 2) * Main.platformDestroyCost);
        List<GameObject> pointers = new List<GameObject>();
        bool showPointers = false;
        public bool ShowPointers
        {
            set
            {
                if (showPointers == value)
                    return;
                showPointers = value;
                if (showPointers)
                    UpdatePointers();
                else
                {
                    foreach (var o in pointers)
                        Destroy(o);
                    pointers.Clear();
                }
            }
            get => showPointers;
        }
        void Awake()
        {
            points = new List<Vector3>();
            platforms.Add(this);
            mesh = new Mesh();
            collider = transform.Find("collider").GetComponent<MeshCollider>();
            ((MeshCollider)collider).sharedMesh = mesh;
            collider.GetComponent<MeshFilter>().mesh = mesh;
        }
        void OnDestroy()
        {
            ShowPointers = false;
            platforms.Remove(this);
        }
        void UpdateMesh()
        {
            if (showPointers)
                UpdatePointers();
            var a = Vector3.zero;
            foreach (var p in points)
                a += p;
            a /= Points;
            transform.position = a;
            var v = new List<Vector3>();
            foreach (var p in points)
                v.Add(p - a);
            //if (!clockwise)
            //v.Reverse(1,Points-1);
            //RemoveInvalid(v);
            var s = new MeshSection(v);
            var m = s.GenerateMesh();
            var t = new List<int>();
            var l = v.Count;
            for (int i = 0; i < l; i++)
                v.Add(v[i] + Vector3.down * 0.2f);
            for (int i = 0; i < l; i++)
                AddSquare(t, i, i - 1);
            SurfaceArea = 0;
            for (int i = 0; i < m.Count; i += 3)
            {
                t.AddRange(new int[] { m[i], m[i + 1], m[i + 2], m[i] + l, m[i + 2] + l, m[i + 1] + l });
                SurfaceArea += Utils.SurfaceArea(v[m[i]], v[m[i + 1]], v[m[i + 2]]);
            }
            OuterEdge = 0;
            for (int i = 0; i < l; i++)
                OuterEdge += Vector3.Distance(v[i], v[(i + 1).Mod(l)]);
            if (!s.Clockwise)
                t.Reverse();
            mesh.vertices = v.ToArray();
            mesh.triangles = t.ToArray();
            mesh.uv = new Vector2[v.Count];
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
        }
        public void AddPoint() => points.Add(Vector3.zero);
        public void RemovePoint() => points.RemoveAt(points.Count - 1);
        public void ChangePoint(Vector3 vector)
        {
            points[points.Count - 1] = vector;
            //MakePointValid();
            UpdateMesh();
        }
        public Vector3 GetPoint(int i) => points.Get(i);
        public void SetPoints(List<Vector3> vectors)
        {
            points = vectors;
            UpdateMesh();
        }
        public void UpdatePointers()
        {
            while (pointers.Count < Points)
                pointers.Add(Instantiate(Main.pointerPrefab, null, false));
            while (pointers.Count > Points)
            {
                Destroy(pointers[0]);
                pointers.RemoveAt(0);
            }
            for (int i = 0; i < Points; i++)
                pointers[i].transform.position = points[i];
        }
        /*public List<int> Overlap(int lineInd)
        {
            var l = new List<int>();
            var a = points[lineInd];
            var b = points[(lineInd + 1) % Points];
            for (int i = 0; i < Points; i++)
                if (i != lineInd && MathUtils.Overlaps(a, b, points[i], points[(i + 1) % Points]))
                    l.Add(i);
            return l;
        }
        public void RemoveInvalid(List<Vector3> vectors)
        {
            var overlaps = new List<List<int>>();
            for (int i = 0; i < vectors.Count; i++)
                overlaps.Add(Overlap(i));
            var l = new List<int>();
            for (int i = vectors.Count - 1; i >= 0; i--)
                if (overlaps[i].Count > 0)
                {
                    if (l.Count > 0 && overlaps[i].Contains(l.Last()))
                    {
                        var last = l.Last();
                        foreach (var k in overlaps)
                            k.RemoveAll((x) => x <= last && x >= i);
                        vectors.RemoveRange(i, last - i);
                        overlaps.RemoveRange(i, last - i);
                        l.RemoveAll((x) => x <= last && x >= i);
                    } else
                        l.Add(i);
                }
        }*/
        public Vector3 FindNearest(Vector3 point)
        {
            var d = float.PositiveInfinity;
            var p = Vector3.zero;
            foreach (var v in points)
                if (Vector3.Distance(v, point) < d)
                {
                    d = Vector3.Distance(v, point);
                    p = v;
                }
            return p;
        }
        public static Vector3 FindNearestToPoint(Vector3 point)
        {
            var d = float.PositiveInfinity;
            var p = Vector3.zero;
            foreach (var v in platforms)
            {
                if (!v.collider.enabled)
                    continue;
                var i = v.FindNearest(point);
                if (Vector3.Distance(i, point) < d)
                {
                    d = Vector3.Distance(i, point);
                    p = i;
                }
            }
            return p;
        }
        public static Vector3 FindNearestPointOnEdgeAll(Vector3 point)
        {
            var d = float.PositiveInfinity;
            var v = Vector3.zero;
            foreach (var obj in platforms)
            {
                if (!obj.collider.enabled)
                    continue;
                var c = obj.FindNearestPointOnEdge(point);
                if (d > Vector3.Distance(c, point))
                {
                    d = Vector3.Distance(c, point);
                    v = c;
                }
            }
            return v;
        }
        public Vector3 FindNearestPointOnEdge(Vector3 point)
        {
            var d = float.PositiveInfinity;
            var v = Vector3.zero;
            for (int i = 0; i < points.Count; i++)
            {
                var c = Utils.FindNearestPointOnEdge(point, points[i], points.Get(1 + i));
                if (d > Vector3.Distance(c, point))
                {
                    d = Vector3.Distance(c, point);
                    v = c;
                }
            }
            return v;
        }
        public void AddSquare(List<int> t, int a, int b) { if (b < 0) b += points.Count; if (a < 0) a += points.Count; t.AddSquare(a, b, b + points.Count, a + points.Count); }
    }
}
