using System.Collections.Generic;
using UnityEngine;
using InControl;
using System.Reflection;

namespace CustomCorrals
{
    static class ExtentionMethods
    {
        public static Sprite CreateSprite(this Texture2D texture) => Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 1);
        public static T Find<T>(this T[] array, System.Predicate<T> predicate)
        {
            foreach (var i in array)
                if (predicate.Invoke(i))
                    return i;
            return default(T);
        }
        public static T Last<T>(this List<T> list) => list[list.Count - 1];
        public static float XZMagnitude(this Vector3 v) => new Vector3(v.x, 0, v.z).magnitude;
        public static List<T> Merge<T>(this List<T> a, List<T> b)
        {
            a.AddRange(b);
            return a;
        }
        public static List<T> GetSubset<T>(this List<T> s, int firstInd, int lastInd)
        {
            firstInd = firstInd.Mod(s.Count);
            lastInd = lastInd.Mod(s.Count);
            return firstInd <= lastInd ? s.GetRange(firstInd, lastInd - firstInd + 1) : s.GetRange(firstInd, s.Count - firstInd).Merge(s.GetRange(0, lastInd + 1));
        }
        public static void RemoveSubset<T>(this List<T> s, int firstInd, int lastInd)
        {
            firstInd = firstInd.Mod(s.Count);
            lastInd = lastInd.Mod(s.Count);
            if (firstInd <= lastInd)
                s.RemoveRange(firstInd, lastInd - firstInd + 1);
            else
            {
                s.RemoveRange(firstInd, s.Count - firstInd);
                s.RemoveRange(0, lastInd + 1);
            }
        }
        public static int FindIndexLoop<T>(this List<T> s, int start, System.Predicate<T> predicate)
        {
            start = start.Mod(s.Count);
            var e = s.FindIndex(start, predicate);
            var e2 = -1;
            if (e == -1 || e == s.Count - 1)
                e2 = s.FindIndex(predicate);
            if (e2 != -1)
                return e2;
            return e;
        }

        public static T Get<T>(this List<T> s, int index) => s[index.Mod(s.Count)];
        public static int Mod(this int o, int v) => o % v + (o < 0 ? v : 0);
        public static void AddSquare(this List<int> t, int a, int b, int c, int d) => t.AddRange(new int[] { a, b, d, b, c, d });

        public static int AddOrGetIndex<T>(this List<T> t, T value)
        {
            var i = t.IndexOf(value);
            if (i == -1)
            {
                i = t.Count;
                t.Add(value);
            }
            return i;
        }

        public static T FindSmallest<T>(this List<T> s, System.Func<T,float> value)
        {
            var m = float.PositiveInfinity;
            T l = default;
            foreach (var i in s)
            {
                var d = value(i);
                if (d < m)
                {
                    m = d;
                    l = i;
                }
            }
            return l;
        }

        public static List<Transform> FindChildrenRecursively(this Transform transform, string ChildName)
        {
            var t = new List<Transform>();
            transform.GetChildren_Internal(ChildName, t);
            return t;
        }

        static void GetChildren_Internal(this Transform transform, string ChildName, List<Transform> transforms)
        {
            if (transform.name == ChildName)
                transforms.Add(transform);
            foreach (Transform t in transform)
                t.GetChildren_Internal(ChildName, transforms);
        }
    }
}