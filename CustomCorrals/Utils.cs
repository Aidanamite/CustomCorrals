using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CustomCorrals
{
    static class Utils
    {
        public static bool IsClockwise(List<Vector3> Locations)
        {
            if (Locations.Count < 2)
                return true;
            System.Func<Vector3, Vector3, float> f = (v1, v2) => (v2.x - v1.x) * (v2.z + v1.z);
            var a = f(Locations.Last(), Locations[0]);
            for (int i = 1; i < Locations.Count; i++)
                a += f(Locations[i - 1], Locations[i]);
            return a > 0;
        }
        public static float Angle(Vector3 a, Vector3 b, Vector3 c)
        {
            var p1 = new Vector2(b.x, b.z);
            var p4 = new Vector2(a.x, a.z) - p1;
            var p5 = new Vector2(c.x, c.z) - p1;
            return Vector2.SignedAngle(p4, p5);
        }
        public static float SurfaceArea(Vector3 a, Vector3 b, Vector3 c)
        {
            var angleA = Vector3.Angle(b - a, c - a);
            var angleB = Vector3.Angle(a - b, c - b);
            var angleC = Vector3.Angle(a - c, b - c);
            var sideAB = (a - b).magnitude;
            var sideBC = (b - c).magnitude;
            var sideAC = (a - c).magnitude;
            var longest = Mathf.Max(sideAB, sideBC, sideAC);
            if (longest == sideAB)
                return SurfaceArea(angleA, sideAC) + SurfaceArea(angleB, sideBC);
            if (longest == sideAC)
                return SurfaceArea(angleA, sideAB) + SurfaceArea(angleC, sideBC);
            if (longest == sideBC)
                return SurfaceArea(angleC, sideAC) + SurfaceArea(angleB, sideAB);
            return 0;
        }
        public static float SurfaceArea(float angle, float hypotenuse) => Mathf.Sin(angle / 180 * Mathf.PI) * hypotenuse * Mathf.Cos(angle / 180 * Mathf.PI) * hypotenuse * 0.5f;

        public static bool Overlaps(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            var e = Angle(a, c, d);
            var f = Angle(c, a, b);
            return e < Angle(a, c, b) && e >= 0 && f < Angle(c, a, d) && f >= 0;
        }
        public static bool CheckRange(float value, float min, float max) => value <= max && value >= min;
        public static bool CheckAcute(Vector3 a, Vector3 b, Vector3 c) => CheckRange(Angle(a, b, c), -90, 90);
        public static List<int> Indices(int Count)
        {
            var p = new List<int>();
            for (int i = 0; i < Count; i++)
                p.Add(i);
            return p;
        }

        public static void CreateSelectionUI(string titleKey, Sprite titleIcon, List<ModeOption> options)
        {
            var ui = GameObject.Instantiate(Main.uiPrefab2);
            ui.title.text = GameContext.Instance.MessageDirector.Get("ui", titleKey) ;
            ui.icon.sprite = titleIcon;
            List<Button> buttons = new List<Button>();
            foreach (var option in options)
            {
                var button = Object.Instantiate(Main.buttonPrefab, ui.contentGrid).Init(option, null);
                button.button.onClick.AddListener(() => {
                    ui.Close();
                    option.Selected();
                });
                buttons.Add(button.button);
                if (buttons.Count == 1)
                    button.button.gameObject.AddComponent<InitSelected>();
            }
            int num = Mathf.CeilToInt(buttons.Count / 6f);
            for (int j = 0; j < buttons.Count; j++)
            {
                int y = j / 6;
                int x = j % 6;
                Navigation navigation = buttons[j].navigation;
                navigation.mode = Navigation.Mode.Explicit;
                if (y > 0)
                    navigation.selectOnUp = buttons[(y - 1) * 6 + x];
                if (y < num - 1)
                    navigation.selectOnDown = buttons[Mathf.Min((y + 1) * 6 + x, buttons.Count - 1)];
                if (x > 0)
                    navigation.selectOnLeft = buttons[y * 6 + (x - 1)];
                if (x < 5 && j < buttons.Count - 1)
                    navigation.selectOnRight = buttons[y * 6 + (x + 1)];
                buttons[j].navigation = navigation;
            }
        }

        public static void Purchase(GameObject ui, bool refresh, System.Action action, int cost)
        {
            var playerState = SRSingleton<SceneContext>.Instance.PlayerState;
            if (playerState.GetCurrency() >= cost)
            {
                playerState.SpendCurrency(cost, false);
                action?.Invoke();
                if (ui)
                {
                    var u = ui.GetComponent<PurchaseUI>();
                    u.PlayPurchaseFX();
                    if (refresh)
                        u.Rebuild(false);
                    else
                        u.Close();
                }
            }
        }

        static Dictionary<Mesh, Mesh> ibisects = new Dictionary<Mesh, Mesh>();
        static Dictionary<Mesh, Mesh> obisects = new Dictionary<Mesh, Mesh>();
        public static Mesh BisectMesh(Mesh mesh, bool inner = true)
        {
            if ((inner ? ibisects : obisects).ContainsKey(mesh))
                return (inner ? ibisects : obisects)[mesh];
            var m = GameObject.Instantiate(mesh);
            var v = new List<Vector3>();
            m.GetVertices(v);
            for (int i = v.Count - 1; i >= 0; i--)
                if ((Mathf.Abs(v[i].x) > Mathf.Abs(v[i].z)) == inner)
                    v[i] = new Vector3(float.NaN, float.NaN, float.NaN);
            m.SetVertices(v);
            (inner ? ibisects : obisects).Add(mesh, m);
            return m;
        }

        public static string GetLogTree(Transform transform, string prefix = " -")
        {
            string str = "\n" + prefix + transform.name;
            foreach (var obj in transform.GetComponents<Component>())
                str += ": " + obj.GetType().Name;
            foreach (Transform sub in transform)
                str += GetLogTree(sub, prefix + "--");
            return str;
        }

        public static void AddCude(List<Vector3> points, List<int> triangles, Vector3 a, Vector3 b, Vector3 exclude)
        {
            var c = new Vector3(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Min(a.z, b.z));
            b = new Vector3(Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y), Mathf.Max(a.z, b.z));
            var i1 = points.AddOrGetIndex(c);
            var i2 = points.AddOrGetIndex(new Vector3(c.x, c.y, b.z));
            var i3 = points.AddOrGetIndex(new Vector3(c.x, b.y, b.z));
            var i4 = points.AddOrGetIndex(new Vector3(c.x, b.y, c.z));
            var i5 = points.AddOrGetIndex(new Vector3(b.x, c.y, c.z));
            var i6 = points.AddOrGetIndex(new Vector3(b.x, c.y, b.z));
            var i7 = points.AddOrGetIndex(b);
            var i8 = points.AddOrGetIndex(new Vector3(b.x, b.y, c.z));
            if (exclude != Vector3.back)
                triangles.AddSquare(i1, i4, i8, i5);
            if (exclude != Vector3.forward)
                triangles.AddSquare(i2, i6, i7, i3);
            if (exclude != Vector3.left)
                triangles.AddSquare(i1, i2, i3, i4);
            if (exclude != Vector3.right)
                triangles.AddSquare(i8, i7, i6, i5);
            if (exclude != Vector3.down)
                triangles.AddSquare(i2, i1, i5, i6);
            if (exclude != Vector3.up)
                triangles.AddSquare(i4, i3, i7, i8);
        }
        public static Vector3 FindNearestPointOnEdge(Vector3 point, Vector3 lineStart, Vector3 lineEnd) => lineStart + Vector3.Project(point - lineStart, lineEnd- lineStart);//point - Vector3.Cross(point - lineStart, point - lineEnd);
    }
}