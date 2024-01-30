using SRML;
using SRML.Console;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using SRML.SR.SaveSystem;
using SRML.SR;
using SRML.Config.Attributes;
using InControl;

namespace CustomCorrals
{
    public class Main : ModEntryPoint
    {
        internal static Assembly modAssembly = Assembly.GetExecutingAssembly();
        internal static string modName = $"{modAssembly.GetName().Name}";
        internal static string modDir = $"{System.Environment.CurrentDirectory}\\SRML\\Mods\\{modName}";
        internal static GameObject wallPrefab;
        internal static GameObject postPrefab;
        internal static GameObject wall2Prefab;
        internal static GameObject post2Prefab;
        internal static GameObject platformPrefab;
        internal static GameObject pointerPrefab;
        internal static CorralUI uiPrefab;
        internal static DroneUIProgramPicker uiPrefab2;
        internal static DroneUIProgramButton buttonPrefab;
        internal static Sprite snappingSrite;
        public const int wallCost = 50;
        internal static float wallLength;
        internal const float platformCost = 5;
        internal const float platformDestroyCost = 1;
        public static PlayerAction confirm;
        public static PlayerAction back;
        public static PlayerAction step;
        public static PlayerAction snap;
        internal static Console.ConsoleInstance Console;

        public override void PreLoad()
        {
            Console = ConsoleInstance;
            HarmonyInstance.PatchAll();

            (confirm = BindingRegistry.RegisterBindedAction("key.corralWallConfirm")).AddDefaultBinding(Key.J);
            TranslationPatcher.AddUITranslation("key.key.corralwallconfirm", "Confirm Wall");
            confirm.OnBindingsChanged += () => PlacementHandler.OnInputChange(confirm);

            (back = BindingRegistry.RegisterBindedAction("key.corralWallBack")).AddDefaultBinding(Key.K);
            TranslationPatcher.AddUITranslation("key.key.corralwallback", "Step Back Wall");
            back.OnBindingsChanged += () => PlacementHandler.OnInputChange(back);

            (step = BindingRegistry.RegisterBindedAction("key.corralWallStep")).AddDefaultBinding(Key.L);
            TranslationPatcher.AddUITranslation("key.key.corralwallstep", "Step Wall");
            step.OnBindingsChanged += () => PlacementHandler.OnInputChange(step);

            (snap = BindingRegistry.RegisterBindedAction("key.corralWallSnap")).AddDefaultBinding(Key.I);
            TranslationPatcher.AddUITranslation("key.key.corralwallsnap", "Toggle Wall Snap");
            snap.OnBindingsChanged += () => PlacementHandler.OnInputChange(snap);

            SRInput.Actions.OnLastInputTypeChanged += (x) => PlacementHandler.OnInputChange(null);
            uiPrefab = Resources.FindObjectsOfTypeAll<CorralUI>().Find((x) => !x.name.EndsWith("(Clone)"));
            uiPrefab2 = Resources.FindObjectsOfTypeAll<DroneUIProgramPicker>().Find((x) => !x.name.EndsWith("(Clone)"));
            buttonPrefab = Resources.FindObjectsOfTypeAll<DroneUIProgramButton>().Find((x) => x.gameObject.name == "DroneUIProgramButton");
            var corralPrefab = Resources.FindObjectsOfTypeAll<LandPlot>().Find((x) => x.name == "patchCorral").transform;
            CreateWallPrefabs(corralPrefab.Find("Base Corral"), out wallPrefab, out postPrefab);
            CreateWallPrefabs(corralPrefab.Find("High Wall Corral"), out wall2Prefab, out post2Prefab);
            platformPrefab = CreatePlatformPrefab(Resources.FindObjectsOfTypeAll<Material>().Find((x) => x.name == "HouseKit01"));
            pointerPrefab = CreatePointerPrefab(Resources.FindObjectsOfTypeAll<Material>().Find((x) => x.name == "HouseKit02"));
            snappingSrite = LoadImage("snapping_sprite.png", 500, 500).CreateSprite();
            
            TranslationPatcher.AddPediaTranslation("t.wall", "Corral Wall");
            TranslationPatcher.AddPediaTranslation("m.upgrade.name.wall.walls", "High Walls");
            TranslationPatcher.AddPediaTranslation("m.upgrade.desc.wall.walls", "Higher corral walls make it harder for anything to get past them.");
            TranslationPatcher.AddUITranslation("l.demolish_wall_segment", "Demolish Single Wall");
            TranslationPatcher.AddUITranslation("m.desc.demolish_wall_segment", "Removes a single segment of the wall.");
            TranslationPatcher.AddUITranslation("l.demolish_wall", "Demolish Wall");
            TranslationPatcher.AddUITranslation("m.desc.demolish_wall", "Removes an entire section of the wall.");
            TranslationPatcher.AddUITranslation("l.demolish_wall2", "Demolish Entire Wall");
            TranslationPatcher.AddUITranslation("m.desc.demolish_wall2", "Removes all sections of the wall");

            TranslationPatcher.AddPediaTranslation("t.platform", "Platform");
            TranslationPatcher.AddUITranslation("l.demolish_platform", "Demolish Platform");
            TranslationPatcher.AddUITranslation("m.desc.demolish_platform", "Removes the platform");

            TranslationPatcher.AddUITranslation("t.placement_handler.mode", "Placement Mode");
            foreach (var v in System.Enum.GetValues(typeof(PlacementMode)))
                TranslationPatcher.AddUITranslation("l.mode_name."+v.ToString(), v.ToString());
        }

        public override void PostLoad()
        {
            SaveRegistry.RegisterWorldDataLoadDelegate(PlacementHandler.ReadData);
            SaveRegistry.RegisterWorldDataSaveDelegate(PlacementHandler.WriteData);
            SRCallbacks.OnActorSpawn += (x,y,z) => {
                if (x == Identifiable.Id.PLAYER)
                {
                    var ui = new GameObject("crosshairUI", typeof(RectTransform), typeof(PlacementUI)).GetComponent<PlacementUI>();
                    ui.transform.SetParent(HudUI.Instance.uiContainer.transform.Find("crossHair"),false);
                    ui.handler = SceneContext.Instance.Player.GetComponent<TeleportablePlayer>().weaponVacuum.gameObject.AddComponent<PlacementHandler>();
                }
                if (y.GetInterfaceComponent<CaveTrigger.Listener>() != null)
                    y.AddComponent<PlatformChecker>();
            };
            SRCallbacks.OnMainMenuLoaded += (x) => PlacementHandler.wallSets.Clear();
            Clues.Loaded = SRModLoader.IsModPresent("contextclues");
        }

        static void CreateWallPrefabs(Transform corralPrefab, out GameObject wallPrefab, out GameObject postPrefab)
        {
            var wp = Object.Instantiate(corralPrefab.Find("Frame").Find("Post 1").gameObject,null,false);
            wallLength = (corralPrefab.Find("Frame").Find("Post 1").localPosition - corralPrefab.Find("Frame").Find("Post 2").localPosition).magnitude;
            wp.AddComponent<WallComponent>();
            var barrierColliderPrefab = corralPrefab.Find("Triggers and Colliders").GetChild(0).gameObject;
            var t = wp.transform.Find("bone_postBase").Find("bone_postTop");
            wp.transform.Find("bone_postBase").localPosition += Vector3.down * 0.2f;
            t.localPosition += Vector3.up * 0.2f;
            t.localRotation *= Quaternion.Euler(0, 45, 0);
            var im = t.childCount / 2;
            for (int i = 0; i < im; i++)
            {
                var s = i == 0 ? "" : $" ({i})";
                var j = t.Find("corralEmmiter" + s);
                var j2 = t.Find("corralField" + s);
                var emitterMesh = Utils.BisectMesh(j.GetComponent<MeshFilter>().mesh);
                var fieldMesh = Utils.BisectMesh(j2.GetComponent<MeshFilter>().mesh);
                var fieldMesh2 = Utils.BisectMesh(j2.GetComponent<MeshFilter>().mesh, false);
                j.GetComponent<MeshFilter>().mesh = emitterMesh;
                j2.GetComponent<MeshFilter>().mesh = fieldMesh;
                j.Rotate(Vector3.up, -45);
                j2.Rotate(Vector3.up, -45);
                var f = -t.right;
                var h = Object.Instantiate(j.gameObject, t, true).transform;
                h.name = $"corralEmmiter ({im + i})";
                h.localPosition += f * wallLength;
                h.Rotate(Vector3.up, 180);
                var h2 = Object.Instantiate(j2.gameObject, t, true).transform;
                h2.name = $"corralField ({im + i})";
                h2.Rotate(Vector3.up, 270);
                h2.localPosition += f * wallLength;
                h2.GetComponent<MeshFilter>().mesh = fieldMesh2;
            }
            var c = Object.Instantiate(barrierColliderPrefab, wp.transform, false).transform;
            c.name = barrierColliderPrefab.name;
            c.localRotation = Quaternion.Euler(0, 90, 0);
            c.localPosition += Vector3.forward * wallLength;
            var c2 = Object.Instantiate(c, c, false);
            foreach (var collider in c2.GetComponentsInChildren<Collider>(true))
            {
                collider.isTrigger = true;
                collider.gameObject.layer = vp_Layer.Launched;
                collider.gameObject.GetOrAddComponent<DelaunchOnLeave>();
            }

            Object.DestroyImmediate(wp.transform.Find("corralBase").gameObject);
            wallPrefab = wp.CreatePrefabCopy();

            while (wp.transform.Find("bone_postBase").Find("bone_postTop").childCount > 0)
                Object.DestroyImmediate(wp.transform.Find("bone_postBase").Find("bone_postTop").GetChild(0).gameObject);
            Object.DestroyImmediate(wp.transform.Find(barrierColliderPrefab.name).gameObject);
            postPrefab = wp.CreatePrefabCopy();
            Object.DestroyImmediate(wp);
        }

        static GameObject CreatePlatformPrefab(Material material)
        {
            var g = new GameObject("Platform");
            g.SetActive(false);
            g.AddComponent<PlatformComponent>();
            var r = new GameObject("collider", typeof(MeshRenderer), typeof(MeshFilter), typeof(MeshCollider)).GetComponent<MeshRenderer>();
            var f = r.GetComponent<MeshFilter>();
            var c = r.GetComponent<MeshCollider>();
            r.material = material;
            f.mesh = null;
            c.sharedMesh = null;
            c.enabled = true;
            c.gameObject.layer = 0;
            r.transform.SetParent(g.transform);
            g.SetActive(true);
            var p = g.CreatePrefabCopy();
            GameObject.DestroyImmediate(g);
            return p;
        }

        static GameObject CreatePointerPrefab(Material material)
        {
            var g = new GameObject("Pointer", typeof(MeshRenderer), typeof(MeshFilter)).GetComponent<MeshRenderer>();
            var f = g.GetComponent<MeshFilter>();
            g.material = material;
            f.mesh = CreatePointerMesh();
            var p = g.gameObject.CreatePrefabCopy();
            Object.DestroyImmediate(g.gameObject);
            return p;
        }

        static Mesh CreatePointerMesh()
        {
            var mesh = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            var s1 = 0.5f;
            var s2 = 0.02f;
            Utils.AddCude(v, t, new Vector3(s2, s2, s1), new Vector3(-s2, -s2, s2), Vector3.back);
            Utils.AddCude(v, t, new Vector3(s2, s2, -s1), new Vector3(-s2, -s2, -s2), Vector3.forward);
            Utils.AddCude(v, t, new Vector3(s1, s2, s2), new Vector3(s2, -s2, -s2), Vector3.left);
            Utils.AddCude(v, t, new Vector3(-s1, s2, s2), new Vector3(-s2, -s2, -s2), Vector3.right);
            Utils.AddCude(v, t, new Vector3(s2, s1, s2), new Vector3(-s2, s2, -s2), Vector3.down);
            Utils.AddCude(v, t, new Vector3(s2, -s1, s2), new Vector3(-s2, -s2, -s2), Vector3.up);
            mesh.vertices = v.ToArray();
            mesh.triangles = t.ToArray();
            mesh.uv = new Vector2[v.Count];
            return mesh;
        }
        public static void Log(string message) => Console.Log($"[{modName}]: " + message);
        public static void LogError(string message) => Console.LogError($"[{modName}]: " + message);
        public static void LogWarning(string message) => Console.LogWarning($"[{modName}]: " + message);
        public static void LogSuccess(string message) => Console.LogSuccess($"[{modName}]: " + message);


        public static void CreatePurchaseUI(WallComponent wall)
        {
            if (wall.Wall.set.zone == null)
            {
                wall.Wall.set.zone = SRSingleton<SceneContext>.Instance.PlayerZoneTracker.GetCurrentZone();
                foreach (var w in wall.Wall.set.walls)
                    foreach (var o in w.objects)
                        o.Zone = wall.Wall.set.zone.Value;
            }
            //Main.Log("Name: " + wall.name + " | Wall: " + (wall.Wall != null));
            var ui = Object.Instantiate(uiPrefab);
            PurchaseUI.Purchasable[] array = new PurchaseUI.Purchasable[4];
            GameObject uiObject = null;
            array[0] = new PurchaseUI.Purchasable(
                "m.upgrade.name.wall.walls",
                ui.walls.icon,
                ui.walls.img,
                "m.upgrade.desc.wall.walls",
                ui.walls.cost * wall.Wall.length / 4,
                new PediaDirector.Id?(PediaDirector.Id.CORRAL),
                () => Utils.Purchase(uiObject, true, wall.Wall.Upgrade, ui.walls.cost * wall.Wall.length / 4),
                () => true,
                () => !wall.Wall.upgraded);
            array[1] = new PurchaseUI.Purchasable(
                MessageUtil.Qualify("ui", "l.demolish_wall_segment"),
                ui.demolish.icon,
                ui.demolish.img,
                MessageUtil.Qualify("ui", "m.desc.demolish_wall_segment"),
                ui.demolish.cost / 4,
                null,
                () => Utils.Purchase(uiObject, false, () => wall.Wall.DestroySegment(wall), ui.demolish.cost / 4),
                () => true,
                () => true, requireHoldToPurchase: Config.holdToConfirmDemolish);
            array[2] = new PurchaseUI.Purchasable(
                MessageUtil.Qualify("ui", "l.demolish_wall"),
                ui.demolish.icon,
                ui.demolish.img,
                MessageUtil.Qualify("ui", "m.desc.demolish_wall"),
                ui.demolish.cost * wall.Wall.length / 4,
                null,
                () => Utils.Purchase(uiObject, false, wall.Wall.Destroy, ui.demolish.cost * wall.Wall.length / 4),
                () => wall.Wall.length > 1,
                () => true, requireHoldToPurchase: Config.holdToConfirmDemolish);
            array[3] = new PurchaseUI.Purchasable(
                MessageUtil.Qualify("ui", "l.demolish_wall2"),
                ui.demolish.icon,
                ui.demolish.img,
                MessageUtil.Qualify("ui", "m.desc.demolish_wall2"),
                ui.demolish.cost * wall.Wall.set.walls.Sum((x) => x.length) / 4,
                null,
                () => Utils.Purchase(uiObject, false, wall.Wall.set.Destroy, ui.demolish.cost * wall.Wall.set.walls.Sum((x) => x.length) / 4),
                () => wall.Wall.set.walls.Count > 1,
                () => true, requireHoldToPurchase: Config.holdToConfirmDemolish);
            uiObject = SRSingleton<GameContext>.Instance.UITemplates.CreatePurchaseUI(ui.titleIcon, "t.wall", array, false, ui.Close, false);
        }
        public static void CreatePurchaseUI(PlatformComponent platform)
        {
            //Main.Log("Name: " + wall.name + " | Wall: " + (wall.Wall != null));
            var ui = Object.Instantiate(uiPrefab);
            PurchaseUI.Purchasable[] array = new PurchaseUI.Purchasable[1];
            GameObject uiObject = null;
            array[0] = new PurchaseUI.Purchasable(
                MessageUtil.Qualify("ui", "l.demolish_platform"),
                ui.demolish.icon,
                ui.demolish.img,
                MessageUtil.Qualify("ui", "m.desc.demolish_platform"),
                platform.DestroyCost,
                null,
                () => Utils.Purchase(uiObject, false, () => Object.Destroy(platform.gameObject), platform.DestroyCost),
                () => true,
                () => true, requireHoldToPurchase: Config.holdToConfirmDemolish);
            uiObject = SRSingleton<GameContext>.Instance.UITemplates.CreatePurchaseUI(ui.titleIcon, "t.platform", array, false, ui.Close, false);
        }

        internal static void CreateModeUI(PlacementHandler handler)
        {
            List<ModeOption> buttons = new List<ModeOption>();
            buttons.Add(new ModeOption(uiPrefab.walls.icon, "l.mode_name.Wall", () => handler.mode = PlacementMode.Wall));
            buttons.Add(new ModeOption(SRSingleton<SceneContext>.Instance.PediaDirector.entries.Find((x) => x.id == PediaDirector.Id.REEF).icon, "l.mode_name.Platform", () => handler.mode = PlacementMode.Platform));
            Utils.CreateSelectionUI("t.placement_handler.mode", uiPrefab.titleIcon, buttons);
        }
        public static Texture2D LoadImage(string filename, int width, int height)
        {
            var spriteData = modAssembly.GetManifestResourceStream(modName + "." + filename);
            var rawData = new byte[spriteData.Length];
            spriteData.Read(rawData, 0, rawData.Length);
            var tex = new Texture2D(width, height);
            tex.LoadImage(rawData);
            return tex;
        }
    }

    [ConfigFile("settings")]
    public static class Config
    {
        public static bool holdToConfirmDemolish = true;
        public static bool platformsSnapToEdges = true;
        public static bool platformsSnapToWalls = true;
        public static bool platformsSnapToCorners = true;
        public static float snapDistance = 0.3f;
    }

    public static class Clues
    {
        public static bool Loaded;
        public static GameObject CreateClue(System.Func<string> text)
        {
            if (Loaded)
                return Create(text);
            return null;
        }
        static GameObject Create(System.Func<string> text) => ContextClues.ContextClue.Create(text);
    }
}