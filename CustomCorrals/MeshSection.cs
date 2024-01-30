using System.Collections.Generic;
using UnityEngine;

namespace CustomCorrals
{
    class MeshSection
    {
        List<Vector3> locations;
        List<int> points;
        List<int> reflexes = new List<int>();
        List<List<int>> reflexgroups = new List<List<int>>();
        List<MeshSection> children = new List<MeshSection>();
        List<int> fixedMesh;
        bool clockwise;
        public bool Clockwise => clockwise;
        public MeshSection(List<Vector3> Locations) : this(Utils.Indices(Locations.Count), Locations) { }
        public MeshSection(List<int> Points, List<Vector3> Locations) : this(Points, Locations, Utils.IsClockwise(Locations)) { }
        public MeshSection(List<int> Points, List<Vector3> Locations, bool Clockwise)
        {
            if (System.Environment.StackTrace.Split(new string[] { "CustomCorrals.MeshSection..ctor" },System.StringSplitOptions.None).Length > 1000)
            {
                Main.LogError("Child stack of 1000 or more mesh sections. Something has probably gone wrong, please contact Aidanamite about this");
                return;
            }
            /*string str = "point indices:";
            foreach (var p in Points)
                str += "\n - " + p;
            Main.Log(str);*/
            locations = Locations;
            points = Points;
            clockwise = Clockwise;
            if (points.Count > 2)
            {
                bool flag = false;
                bool flag2 = false;
                for (int i = 0; i < points.Count; i++)
                    if (CheckReflex(locations[points[i]], locations[points[(i + 1) % points.Count]], locations[points[(i + 2) % points.Count]]))
                    {
                        if (i == 0)
                            flag2 = true;
                        var p = points[(i + 1) % points.Count];
                        reflexes.Add(p);
                        if (flag)
                            reflexgroups.Last().Add(p);
                        else
                        {
                            flag = true;
                            reflexgroups.Add(new List<int> { p });
                        }
                    }
                    else
                        flag = false;
                if (flag && flag2 && reflexgroups.Count > 1)
                {
                    reflexgroups.Last().AddRange(reflexgroups[0]);
                    reflexgroups.RemoveAt(0);
                }
                /*str = "re:";
                foreach (var r in reflexes)
                    str += $"\n point {r} is relfex";
                    Main.Log(str);*/
                if (reflexgroups.Count >= 2)
                {
                    for (int i = 0; i < reflexgroups.Count; i++)
                        children.Add(new MeshSection(points.GetSubset(points.IndexOf(reflexgroups[i][0]), points.IndexOf(reflexgroups[(i + 1) % reflexgroups.Count][0])), locations, clockwise));
                    if (reflexes.Count > 2)
                        children.Add(new MeshSection(reflexes, Locations, clockwise));
                } else if (reflexes.Count >= 2)
                {
                    System.Func<int, int, bool> f = (x, y) =>
                    {
                        x = x.Mod(points.Count);
                        y = y.Mod(points.Count);
                        var xn = (x - 1).Mod(points.Count);
                        var xp = (x + 1).Mod(points.Count);
                        var yn = (y - 1).Mod(points.Count);
                        var yp = (y + 1).Mod(points.Count);
                        var f1 = !CheckReflex(locations[points[yn]], locations[points[y]], locations[points[x]]);
                        var f2 = CheckReflex(locations[points[yp]], locations[points[y]], locations[points[x]]);
                        var f3 = !CheckReflex(locations[points[xn]], locations[points[x]], locations[points[y]]);
                        var f4 = CheckReflex(locations[points[xp]], locations[points[x]], locations[points[y]]);
                        var f5 = reflexes.Contains(points[y]);
                        var f6 = reflexes.Contains(points[x]);
                        //Main.Log($"point 1: {x} - point 2: {y} - flag 1: {f1} - flag 2: {f2} - flag 3: {f3} - flag 4: {f4} - flag 5: {f5} - flag 6: {f6}");
                        return x == yn || x == yp || ((f5 ? (f1 || f2) : (f1 && f2)) && (f6 ? (f3 || f4) : (f3 && f4)));
                    };
                    var c = new List<int>();
                    for (int i = 0; i < points.Count; i++)
                    {
                        var k = new List<int> { i };
                        for (int j = 1; j < points.Count; j++)
                            if (f( i + j,i))
                                k.Add((i + j).Mod(points.Count));
                            else
                                break;
                        var l = k.Count;
                        for (int j = 1; j < points.Count - l + 1; j++)
                            if (f(i - j,i))
                                k.Insert(l,(i - j).Mod(points.Count));
                            else
                                break;

                        if (k.Count > c.Count)
                            c = k;
                    }
                    if (c.Count != points.Count)
                    {
                        var l = -1;
                        if (!c.Contains(0))
                            for (int i = 0; i < points.Count; i++)
                                if (c.Contains(0))
                                {
                                    l = i - 1;
                                    break;
                                }
                        foreach (int i in c)
                        {
                            if (l != -1 && (l + 1).Mod(points.Count) != i)
                                children.Add(new MeshSection(points.GetSubset(l, i), locations, clockwise));
                            l = i;
                        }
                    }
                    fixedMesh = c;
                }
            }
        }
        public List<int> GenerateMesh()
        {
            var l = new List<int>();
            if (locations != null && points.Count > 2)
            {
                if (children.Count > 0)
                    foreach (var m in children)
                        l.AddRange(m.GenerateMesh());
                else if (fixedMesh == null)
                {
                    /*string s = "generating from points:";
                    foreach (var r in points)
                        s += $"\n point {r} ";
                    Main.Log(s);*/
                    var anchor = points[0];
                    if (reflexes.Count == 1)
                        anchor = reflexes[0];
                    for (int i = 0; i < points.Count; i++)
                        if (points[i] != anchor && points[(i + 1) % points.Count] != anchor)
                            l.AddRange(new int[] { anchor, points[i], points[(i + 1) % points.Count] });
                }
                if (fixedMesh != null)
                {
                    /*string s = "generating from fixed points:";
                    foreach (var r in fixedMesh)
                        s += $"\n point {points[r]} ";
                    Main.Log(s);*/
                    var anchor = points[fixedMesh[0]];
                    for (int i = 1; i < fixedMesh.Count - 1; i++)
                        l.AddRange(new int[] { anchor, points[fixedMesh[i]], points[fixedMesh.Get(i + 1)] });
                }
            }
            return l;
        }
        public bool HasOverlaps(int x, int y)
        {
            var a = locations[points[x]];
            var b = locations[points[y]];
            for (int i = 0; i < points.Count; i++)
                if (i != x && i != y && i + 1 % points.Count != x && i + 1 % points.Count != y && Utils.Overlaps(a, b, locations[points[i]], locations[points.Get(i + 1)]))
                    return true;
            return false;
        }
        public bool CheckReflex(Vector3 a, Vector3 b, Vector3 c) => (Utils.Angle(a, b, c) < 0) == clockwise;
    }
}