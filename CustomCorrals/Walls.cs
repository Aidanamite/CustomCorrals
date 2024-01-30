using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CustomCorrals
{
    public class Walls
    {
        public List<Wall> walls;
        public ZoneDirector.Zone? zone;
        public Walls(List<List<WallComponent>> Walls)
        {
            zone = Walls[0][0].Zone;
            walls = new List<Wall>();
            for (int i = 0; i < Walls.Count; i++)
                walls.Add(new Wall(Walls[i],i == Walls.Count - 1, this));
        }
        public Walls(List<Wall> Walls, ZoneDirector.Zone? Zone)
        {
            zone = Zone;
            walls = Walls;
            foreach (var wall in Walls)
            {
                wall.set = this;
                if (zone != null)
                    foreach (var w in wall.objects)
                        w.Zone = zone.Value;
            }
        }
        public void Destroy(Wall wall)
        {
            if (walls.Count != 1)
                wall.TryAddPostToSibling();
            wall.DestroyObjects();
            walls.Remove(wall);
            if (walls.Count == 0)
            PlacementHandler.wallSets.Remove(this);
        }
        public void Destroy()
        {
            foreach (var wall in walls)
                wall.DestroyObjects();
            walls.Clear();
            PlacementHandler.wallSets.Remove(this);
        }
    }

    public class Wall
    {
        public float angle;
        public int length;
        public bool hasPost;
        public bool upgraded;
        public Vector3 start;
        internal List<WallComponent> objects;
        public Walls set;
        public Wall(List<WallComponent> gameObjects, bool HasPost, Walls Set)
        {
            start = gameObjects[0].transform.position;
            angle = gameObjects[0].transform.rotation.eulerAngles.y;
            hasPost = HasPost;
            length = gameObjects.Count - (hasPost ? 1 : 0);
            upgraded = false;
            objects = gameObjects;
            set = Set;
            foreach (var g in gameObjects)
                g.Wall = this;
        }
        public Wall(Vector3 Start, float Angle, int Length, bool HasPost, bool Upgraded)
        {
            start = Start;
            angle = Angle;
            length = Length;
            hasPost = HasPost;
            upgraded = Upgraded;
            objects = new List<WallComponent>();
            GenerateObjects();
        }
        public void GenerateObjects()
        {
            for (int i = 0; i < length; i++)
                GenerateObject(i);
            if (hasPost)
                GeneratePost();
        }
        public void GenerateObject(int index)
        {
            var g = index == length ? PlacementHandler.CreatePost(null, tallVarient: upgraded) : PlacementHandler.CreateWall(null, tallVarient: upgraded);
            g.transform.rotation = Quaternion.Euler(0, angle, 0);
            objects.Add(g);
            g.transform.position = start + objects[0].transform.forward * Main.wallLength * index;
            g.Wall = this;
        }
        public void GeneratePost() => GenerateObject(length);
        public void Upgrade()
        {
            if (upgraded)
                return;
            DestroyObjects();
            upgraded = true;
            int i = set.walls.IndexOf(this);
            if (i < set.walls.Count - 1 && !set.walls[i + 1].upgraded)
                hasPost = true;
            if (i > 0)
            {
                var s = set.walls[i - 1];
                var l = s.objects.Last();
                if (s.upgraded && s.hasPost && (l.transform.position - start).magnitude < 0.1f)
                {
                    s.hasPost = false;
                    s.objects.Remove(l);
                    Object.Destroy(l.gameObject);
                }
            }
            GenerateObjects();
        }
        public void Destroy() => set.Destroy(this);
        public void DestroyObjects()
        {
            foreach (var g in objects)
                Object.Destroy(g.gameObject);
            objects.Clear();
        }
        public void DestroySegment(WallComponent wall)
        {
            if (length == 1)
            {
                Destroy();
                return;
            }
            var s = objects.FindIndex((x) => x == wall);
            if (s == -1 || length == 0)
            {
                Main.LogError($"Failed to remove wall segment. Wall index was not found\n{System.Environment.StackTrace}");
                return;
            }
            if (s == length)
            {
                length--;
                s--;
                wall = objects[s].GetComponent<WallComponent>();
            }
            if (s == 0)
            {
                Object.Destroy(wall.gameObject);
                objects.RemoveAt(0);
                start = objects[0].transform.position;
                length--;
                TryAddPostToSibling();
                return;
            }
            else
            {
                if (s < length - 1)
                {
                    var n = new Wall(objects.GetRange(s + 1, objects.Count - s - 1), hasPost, set);
                    n.upgraded = upgraded;
                    set.walls.Add(n);
                }
                else if (hasPost)
                    Object.Destroy(objects.Last().gameObject);
                var p = PlacementHandler.CreatePost(null, tallVarient: upgraded);
                p.GetComponent<WallComponent>().Wall = this;
                wall.transform.CopyAllTo(p.transform);
                Object.Destroy(wall.gameObject);
                objects[s] = p;
                hasPost = true;
                objects.RemoveRange(s + 1, objects.Count - s - 1);
                length = s;
            }
        }
        public void TryAddPostToSibling()
        {
            var i = set.walls.IndexOf(this);
            if (i <= 0)
                return;
            var w = set.walls[i - 1];
            if (w.hasPost)
                return;
            w.hasPost = true;
            w.GeneratePost();
        }
    }
}