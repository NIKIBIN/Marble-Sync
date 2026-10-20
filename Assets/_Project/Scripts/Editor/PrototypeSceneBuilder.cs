using MarbleSync.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MarbleSync.EditorTools
{
    /// <summary>
    /// Builds the greybox prototype: materials, prefabs and the playable scene, with every
    /// [SerializeField] reference wired explicitly (no runtime scene searches).
    /// </summary>
    public static class PrototypeSceneBuilder
    {
        private const string Root = "Assets/_Project";
        private const string MaterialFolder = Root + "/Art/Materials";
        private const string PrefabFolder = Root + "/Prefabs";
        private const string SceneFolder = Root + "/Scenes";
        private const string ScenePath = SceneFolder + "/MarbleSync.unity";

        private static Font _font;

        [MenuItem("Marble Sync/Build Prototype (levels + prefabs + scene)")]
        public static void BuildAll()
        {
            LevelGenerator.Generate();
            BuildScene();
        }

        [MenuItem("Marble Sync/2. Build Scene Only")]
        public static void BuildSceneOnly()
        {
            LevelLibrary library = AssetDatabase.LoadAssetAtPath<LevelLibrary>(LevelGenerator.LibraryPath);
            if (library == null)
            {
                throw new System.InvalidOperationException("Generate the levels first (Marble Sync/1. Generate Levels).");
            }
            BuildScene();
        }

        private static void BuildScene()
        {
            LevelGenerator.EnsureFolder(MaterialFolder);
            LevelGenerator.EnsureFolder(PrefabFolder);
            LevelGenerator.EnsureFolder(SceneFolder);
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            LevelLibrary library = AssetDatabase.LoadAssetAtPath<LevelLibrary>(LevelGenerator.LibraryPath);

            var mats = new Materials();
            HalfPipeView halfPipe = BuildHalfPipePrefab(mats);
            MarbleView marble = BuildMarblePrefab(mats, false);
            MarbleView ghost = BuildMarblePrefab(mats, true);
            ElementView element = BuildElementPrefab(mats);
            HistoryEntryView historyEntry = BuildHistoryEntryPrefab();

            // Lighting & environment
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.47f, 0.52f);

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.position = new Vector3(-8f, -2.5f, 0f);
            floor.transform.localScale = new Vector3(8f, 1f, 6f);
            floor.GetComponent<Renderer>().sharedMaterial = mats.Floor;
            Object.DestroyImmediate(floor.GetComponent<Collider>());

            // Camera
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.13f, 0.14f, 0.18f);
            cam.fieldOfView = 40f;
            cam.farClipPlane = 300f;
            camGo.AddComponent<AudioListener>();
            var gameCamera = camGo.AddComponent<GameCamera>();
            Wire(gameCamera, "cam", cam);

            // Gameplay root
            var game = new GameObject("Game");
            var controller = game.AddComponent<AttemptController>();

            var trackGo = new GameObject("Track");
            trackGo.transform.SetParent(game.transform);
            var trackView = trackGo.AddComponent<TrackView>();
            Transform pipes = Child(trackGo.transform, "Pipes");
            Transform elements = Child(trackGo.transform, "Elements");
            Wire(trackView, "pipePrefab", halfPipe);
            Wire(trackView, "elementPrefab", element);
            Wire(trackView, "pipeContainer", pipes);
            Wire(trackView, "elementContainer", elements);

            MarbleSetView marbles = BuildMarbleSet(game.transform, "Marbles", marble, false);
            MarbleSetView ghosts = BuildMarbleSet(game.transform, "GhostMarbles", ghost, true);

            Wire(controller, "library", library);
            Wire(controller, "trackView", trackView);
            Wire(controller, "marbles", marbles);
            Wire(controller, "ghostMarbles", ghosts);

            SeesawView seesaw = BuildSeesaw(game.transform, controller, trackView, halfPipe, mats);

            // Juice
            var audioGo = new GameObject("Audio");
            audioGo.transform.SetParent(game.transform);
            var oneShots = audioGo.AddComponent<AudioSource>();
            oneShots.playOnAwake = false;
            var creak = audioGo.AddComponent<AudioSource>();
            creak.playOnAwake = false;
            var sfx = audioGo.AddComponent<SfxPlayer>();
            Wire(sfx, "controller", controller);
            Wire(sfx, "oneShots", oneShots);
            Wire(sfx, "creakLoop", creak);

            var tensionGo = new GameObject("TensionDirector");
            tensionGo.transform.SetParent(game.transform);
            var tension = tensionGo.AddComponent<TensionDirector>();
            Wire(tension, "controller", controller);
            Wire(tension, "gameCamera", gameCamera);
            Wire(tension, "sfx", sfx);

            // Input + UI
            var eventSystemGo = new GameObject("EventSystem");
            var eventSystem = eventSystemGo.AddComponent<EventSystem>();
            eventSystemGo.AddComponent<InputSystemUIInputModule>();

            var inputGo = new GameObject("PointerInput");
            inputGo.transform.SetParent(game.transform);
            var pointer = inputGo.AddComponent<PointerInput>();
            Wire(pointer, "controller", controller);
            Wire(pointer, "worldCamera", cam);
            Wire(pointer, "eventSystem", eventSystem);

            BuildHud(controller, historyEntry);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log($"Marble Sync prototype scene built at {ScenePath} (seesaw: {seesaw.name}).");
        }

        // ------------------------------------------------------------ materials

        private sealed class Materials
        {
            public readonly Material Track = Lit("Track", new Color(0.86f, 0.84f, 0.8f), 0.65f);
            public readonly Material Marble = Lit("Marble", Color.white, 0.85f, emission: true);
            public readonly Material Shell = Transparent("MarbleShell", new Color(1f, 1f, 1f, 0.5f), 0.95f);
            public readonly Material Ghost = Transparent("GhostMarble", new Color(1f, 1f, 1f, 0.35f), 0.6f);
            public readonly Material Fire = Unlit("Fire", new Color(1f, 0.55f, 0.1f));
            public readonly Material Puddle = Lit("Puddle", Color.white, 0.95f, emission: true);
            public readonly Material Logic = Lit("LogicPart", Color.white, 0.5f, emission: true);
            public readonly Material Metal = Lit("Metal", new Color(0.35f, 0.37f, 0.4f), 0.7f, metallic: 0.8f);
            public readonly Material Wood = Lit("SeesawWood", new Color(0.72f, 0.5f, 0.3f), 0.3f);
            public readonly Material Marker = Unlit("HalfMarker", new Color(1f, 0.9f, 0.3f));
            public readonly Material Ring = Unlit("SelectionRing", new Color(1f, 1f, 1f));
            public readonly Material Floor = Lit("Floor", new Color(0.22f, 0.24f, 0.28f), 0.1f);
        }

        private static Shader LitShader => Shader.Find("Universal Render Pipeline/Lit");
        private static Shader UnlitShader => Shader.Find("Universal Render Pipeline/Unlit");

        private static Material Lit(string name, Color color, float smoothness, float metallic = 0f, bool emission = false)
        {
            Material mat = LoadOrCreate(name, LitShader);
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallic);
            if (emission)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.black);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material Transparent(string name, Color color, float smoothness)
        {
            Material mat = LoadOrCreate(name, LitShader);
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.SetShaderPassEnabled("ShadowCaster", false);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material Unlit(string name, Color color)
        {
            Material mat = LoadOrCreate(name, UnlitShader);
            mat.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material LoadOrCreate(string name, Shader shader)
        {
            if (shader == null)
            {
                throw new System.InvalidOperationException("URP shaders not found. Is the Universal Render Pipeline active?");
            }
            string path = $"{MaterialFolder}/{name}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            return mat;
        }

        // ------------------------------------------------------------ prefabs

        private static HalfPipeView BuildHalfPipePrefab(Materials mats)
        {
            var go = new GameObject("HalfPipe");
            var filter = go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = mats.Track;
            var view = go.AddComponent<HalfPipeView>();
            Wire(view, "meshFilter", filter);
            Wire(view, "meshRenderer", renderer);
            return SavePrefab(go, "HalfPipe").GetComponent<HalfPipeView>();
        }

        private static MarbleView BuildMarblePrefab(Materials mats, bool ghost)
        {
            var root = new GameObject(ghost ? "GhostMarble" : "Marble");
            var collider = root.AddComponent<SphereCollider>();
            collider.radius = 0.45f;
            collider.isTrigger = true;
            var click = root.AddComponent<ClickTarget>();

            GameObject body = Primitive(PrimitiveType.Sphere, "Body", root.transform, ghost ? mats.Ghost : mats.Marble);
            GameObject shell = Primitive(PrimitiveType.Sphere, "Shell", root.transform, mats.Shell);
            shell.transform.localScale = Vector3.one * 0.6f;

            var fireRoot = new GameObject("Fire");
            fireRoot.transform.SetParent(root.transform, false);
            GameObject flame = Primitive(PrimitiveType.Capsule, "Flame", fireRoot.transform, mats.Fire);
            flame.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            flame.transform.localScale = new Vector3(0.35f, 0.3f, 0.35f);
            var fireLight = new GameObject("FireLight").AddComponent<Light>();
            fireLight.transform.SetParent(fireRoot.transform, false);
            fireLight.type = LightType.Point;
            fireLight.color = new Color(1f, 0.55f, 0.15f);
            fireLight.range = 3f;

            GameObject ring = Primitive(PrimitiveType.Cylinder, "SelectionRing", root.transform, mats.Ring);
            ring.transform.localPosition = new Vector3(0f, -0.24f, 0f);
            ring.transform.localScale = new Vector3(0.8f, 0.01f, 0.8f);

            var view = root.AddComponent<MarbleView>();
            Wire(view, "body", body.transform);
            Wire(view, "bodyRenderer", body.GetComponent<Renderer>());
            Wire(view, "shellRenderer", shell.GetComponent<Renderer>());
            Wire(view, "fireRoot", fireRoot);
            Wire(view, "fireLight", fireLight);
            Wire(view, "selectionRing", ring);
            Wire(view, "clickTarget", click);
            Wire(view, "clickCollider", collider);
            return SavePrefab(root, ghost ? "GhostMarble" : "Marble").GetComponent<MarbleView>();
        }

        private static ElementView BuildElementPrefab(Materials mats)
        {
            var root = new GameObject("Element");
            var collider = root.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(0.9f, 0.7f, 0.9f);
            collider.center = new Vector3(0f, 0.3f, 0f);
            var click = root.AddComponent<ClickTarget>();

            // A flat block whose sides hide inside the pipe wall: reads as liquid pooled in the channel.
            GameObject puddle = Primitive(PrimitiveType.Cube, "Puddle", root.transform, mats.Puddle);
            puddle.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            puddle.transform.localScale = new Vector3(0.44f, 0.1f, 0.9f);

            var flameRoot = new GameObject("Flamethrower");
            flameRoot.transform.SetParent(root.transform, false);
            GameObject nozzle = Primitive(PrimitiveType.Cube, "Nozzle", flameRoot.transform, mats.Logic);
            nozzle.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            nozzle.transform.localScale = new Vector3(0.35f, 0.25f, 0.35f);
            GameObject jet = Primitive(PrimitiveType.Capsule, "Jet", flameRoot.transform, mats.Fire);
            jet.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            var flameLight = new GameObject("FlameLight").AddComponent<Light>();
            flameLight.transform.SetParent(flameRoot.transform, false);
            flameLight.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            flameLight.type = LightType.Point;
            flameLight.color = new Color(1f, 0.5f, 0.1f);
            flameLight.range = 4f;

            GameObject door = Primitive(PrimitiveType.Cube, "DoorPanel", root.transform, mats.Logic);
            door.transform.localScale = new Vector3(0.75f, 0.7f, 0.1f);
            door.transform.localPosition = new Vector3(0f, 0.35f, 0f);

            GameObject button = Primitive(PrimitiveType.Cylinder, "Button", root.transform, mats.Logic);
            button.transform.localPosition = new Vector3(0f, 0.02f, 0f);

            // Sensor: a light bar spanning the top of the half-pipe.
            GameObject sensor = Primitive(PrimitiveType.Cube, "Sensor", root.transform, mats.Logic);
            sensor.transform.localPosition = new Vector3(0f, 0.72f, 0f);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(root.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 0.95f, 0f);
            labelGo.transform.localScale = Vector3.one * 0.08f;
            var label = labelGo.AddComponent<TextMesh>();
            label.font = _font;
            label.fontSize = 48;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.color = Color.white;
            labelGo.GetComponent<MeshRenderer>().sharedMaterial = _font.material;

            var view = root.AddComponent<ElementView>();
            Wire(view, "clickTarget", click);
            Wire(view, "clickCollider", collider);
            Wire(view, "puddle", puddle.GetComponent<Renderer>());
            Wire(view, "flameRoot", flameRoot);
            Wire(view, "flameNozzle", nozzle.GetComponent<Renderer>());
            Wire(view, "flameJet", jet.transform);
            Wire(view, "flameLight", flameLight);
            Wire(view, "doorPanel", door.transform);
            Wire(view, "doorRenderer", door.GetComponent<Renderer>());
            Wire(view, "buttonRenderer", button.GetComponent<Renderer>());
            Wire(view, "sensorRenderer", sensor.GetComponent<Renderer>());
            Wire(view, "label", label);
            return SavePrefab(root, "Element").GetComponent<ElementView>();
        }

        private static MarbleSetView BuildMarbleSet(Transform parent, string name, MarbleView prefab, bool ghost)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            var set = go.AddComponent<MarbleSetView>();
            Transform container = Child(go.transform, "Container");
            Wire(set, "marblePrefab", prefab);
            Wire(set, "container", container);
            WireBool(set, "ghost", ghost);
            return set;
        }

        private static SeesawView BuildSeesaw(Transform parent, AttemptController controller, TrackView trackView, HalfPipeView pipePrefab, Materials mats)
        {
            var go = new GameObject("Seesaw");
            go.transform.SetParent(parent);
            var view = go.AddComponent<SeesawView>();
            Transform deck = Child(go.transform, "Deck");
            GameObject plank = Primitive(PrimitiveType.Cube, "Plank", deck, mats.Wood);
            Transform lanes = Child(deck, "Tracks");
            GameObject half = Primitive(PrimitiveType.Cube, "HalfMarker", deck, mats.Marker);
            GameObject gate = Primitive(PrimitiveType.Cube, "Gate", go.transform, mats.Metal);
            GameObject fulcrum = Primitive(PrimitiveType.Cylinder, "Fulcrum", go.transform, mats.Metal);
            fulcrum.transform.localScale = new Vector3(0.5f, 0.6f, 0.5f);

            Wire(view, "controller", controller);
            Wire(view, "trackView", trackView);
            Wire(view, "deck", deck);
            Wire(view, "laneRoot", lanes);
            Wire(view, "pipePrefab", pipePrefab);
            Wire(view, "plank", plank.transform);
            Wire(view, "gate", gate.transform);
            Wire(view, "halfMarker", half.transform);
            Wire(view, "fulcrum", fulcrum.transform);
            return view;
        }

        // ------------------------------------------------------------ HUD

        private static void BuildHud(AttemptController controller, HistoryEntryView historyEntryPrefab)
        {
            var canvasGo = new GameObject("HUD");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            RectTransform rootRect = canvasGo.GetComponent<RectTransform>();

            // Top-left: title + intro
            RectTransform topLeft = Panel(rootRect, "TopLeft", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(820f, 190f), new Color(0f, 0f, 0f, 0.45f));
            Text title = Label(topLeft, "Title", "1-1 · Tutorial", 40, TextAnchor.UpperLeft, FontStyle.Bold);
            Stretch(title.rectTransform, new Vector2(18f, -12f), new Vector2(-18f, -64f), top: true, height: 52f);
            Text intro = Label(topLeft, "Intro", "", 22, TextAnchor.UpperLeft);
            Stretch(intro.rectTransform, new Vector2(18f, -66f), new Vector2(-18f, 10f), top: true, height: 115f);

            // Top-right: level navigation
            Button prev = MakeButton(rootRect, "PrevLevel", "◀ Fase", new Vector2(1f, 1f), new Vector2(-250f, -24f), new Vector2(110f, 54f));
            Button next = MakeButton(rootRect, "NextLevel", "Fase ▶", new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(110f, 54f));
            ((RectTransform)prev.transform).pivot = new Vector2(1f, 1f);
            ((RectTransform)next.transform).pivot = new Vector2(1f, 1f);
            ((RectTransform)prev.transform).anchoredPosition = new Vector2(-148f, -24f);

            // Right: history
            RectTransform history = Panel(rootRect, "History", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -96f), new Vector2(430f, 560f), new Color(0f, 0f, 0f, 0.45f));
            history.pivot = new Vector2(1f, 1f);
            history.anchoredPosition = new Vector2(-24f, -96f);
            Text historyTitle = Label(history, "HistoryTitle", "Tentativas", 26, TextAnchor.UpperLeft, FontStyle.Bold);
            Stretch(historyTitle.rectTransform, new Vector2(16f, -10f), new Vector2(-16f, 0f), top: true, height: 36f);
            var listGo = new GameObject("Entries", typeof(RectTransform));
            listGo.transform.SetParent(history, false);
            var list = (RectTransform)listGo.transform;
            list.anchorMin = new Vector2(0f, 1f);
            list.anchorMax = new Vector2(1f, 1f);
            list.pivot = new Vector2(0.5f, 1f);
            list.anchoredPosition = new Vector2(0f, -52f);
            list.sizeDelta = new Vector2(-24f, 0f);
            var layout = listGo.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = listGo.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Bottom-left: status, sponge, timers, hint
            RectTransform bottomLeft = Panel(rootRect, "BottomLeft", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 24f), new Vector2(980f, 330f), new Color(0f, 0f, 0f, 0.45f));
            bottomLeft.pivot = Vector2.zero;
            Text status = Label(bottomLeft, "Status", "", 22, TextAnchor.UpperLeft);
            Stretch(status.rectTransform, new Vector2(18f, -12f), new Vector2(-18f, 0f), top: true, height: 84f);
            Text sponge = Label(bottomLeft, "Sponges", "Esponjas: 1/1", 24, TextAnchor.UpperLeft, FontStyle.Bold);
            Stretch(sponge.rectTransform, new Vector2(18f, -98f), new Vector2(-18f, 0f), top: true, height: 30f);
            sponge.color = new Color(1f, 0.92f, 0.5f);
            Text hint = Label(bottomLeft, "HintText", "", 22, TextAnchor.UpperLeft, FontStyle.Italic);
            Stretch(hint.rectTransform, new Vector2(18f, -130f), new Vector2(-18f, 0f), top: true, height: 56f);
            hint.color = new Color(0.7f, 0.95f, 1f);

            var timerPanelGo = new GameObject("TimerPanel", typeof(RectTransform));
            timerPanelGo.transform.SetParent(bottomLeft, false);
            var timerPanel = (RectTransform)timerPanelGo.transform;
            timerPanel.anchorMin = Vector2.zero;
            timerPanel.anchorMax = new Vector2(1f, 0f);
            timerPanel.pivot = new Vector2(0.5f, 0f);
            timerPanel.anchoredPosition = new Vector2(0f, 10f);
            timerPanel.sizeDelta = new Vector2(0f, 170f);
            Text timers = Label(timerPanel, "TimersText", "", 20, TextAnchor.UpperLeft);
            Stretch(timers.rectTransform, new Vector2(18f, -4f), new Vector2(-18f, 0f), top: true, height: 110f);
            timers.supportRichText = true;
            Button up = MakeButton(timerPanel, "TimerUp", "▲ +0,25 s", Vector2.zero, new Vector2(18f, 6f), new Vector2(150f, 46f));
            Button down = MakeButton(timerPanel, "TimerDown", "▼ −0,25 s", Vector2.zero, new Vector2(178f, 6f), new Vector2(150f, 46f));
            Button trigger = MakeButton(timerPanel, "TimerTrigger", "Gatilho (T)", Vector2.zero, new Vector2(338f, 6f), new Vector2(170f, 46f));
            Button clear = MakeButton(timerPanel, "TimerClear", "Sem timer", Vector2.zero, new Vector2(518f, 6f), new Vector2(150f, 46f));
            foreach (Button b in new[] { up, down, trigger, clear })
            {
                ((RectTransform)b.transform).pivot = Vector2.zero;
            }
            Button hintButton = MakeButton(bottomLeft, "HintButton", "Dica", new Vector2(1f, 1f), new Vector2(-14f, -14f), new Vector2(110f, 46f));
            ((RectTransform)hintButton.transform).pivot = new Vector2(1f, 1f);

            // Bottom-centre: start / restart / continue
            Button start = MakeButton(rootRect, "StartButton", "Começar (Espaço)", new Vector2(0.5f, 0f), new Vector2(160f, 40f), new Vector2(300f, 70f));
            Button clearPlan = MakeButton(rootRect, "ClearPlan", "Limpar base", new Vector2(0.5f, 0f), new Vector2(160f, 120f), new Vector2(300f, 50f));
            Button cont = MakeButton(rootRect, "ContinueButton", "Próxima fase ▶", new Vector2(0.5f, 0f), new Vector2(160f, 40f), new Vector2(300f, 70f));
            HoldButton restart = MakeHoldButton(rootRect, new Vector2(0.5f, 0f), new Vector2(480f, 40f), new Vector2(300f, 70f));

            // Centre: outcome
            Text outcome = Label(rootRect, "Outcome", "", 30, TextAnchor.MiddleCenter, FontStyle.Bold);
            outcome.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            outcome.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            outcome.rectTransform.sizeDelta = new Vector2(1000f, 300f);
            outcome.rectTransform.anchoredPosition = new Vector2(0f, 180f);
            var shadow = outcome.gameObject.AddComponent<Shadow>();
            shadow.effectDistance = new Vector2(3f, -3f);

            // Replay bar
            RectTransform replay = Panel(rootRect, "ReplayPanel", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(760f, 70f), new Color(0.1f, 0.2f, 0.35f, 0.85f));
            replay.pivot = new Vector2(0.5f, 1f);
            Text replayText = Label(replay, "ReplayText", "Replay", 24, TextAnchor.MiddleLeft, FontStyle.Bold);
            Stretch(replayText.rectTransform, new Vector2(18f, 0f), new Vector2(-300f, 0f), top: false, height: 0f);
            Button speed = MakeButton(replay, "ReplaySpeed", "Velocidade", new Vector2(1f, 0.5f), new Vector2(-150f, 0f), new Vector2(140f, 50f));
            Button exit = MakeButton(replay, "ExitReplay", "Sair", new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(120f, 50f));
            ((RectTransform)speed.transform).pivot = new Vector2(1f, 0.5f);
            ((RectTransform)exit.transform).pivot = new Vector2(1f, 0.5f);

            var hud = canvasGo.AddComponent<HudView>();
            Wire(hud, "controller", controller);
            Wire(hud, "titleText", title);
            Wire(hud, "introText", intro);
            Wire(hud, "statusText", status);
            Wire(hud, "outcomeText", outcome);
            Wire(hud, "timersText", timers);
            Wire(hud, "spongeText", sponge);
            Wire(hud, "hintText", hint);
            Wire(hud, "replayText", replayText);
            Wire(hud, "startButton", start);
            Wire(hud, "prevLevelButton", prev);
            Wire(hud, "nextLevelButton", next);
            Wire(hud, "continueButton", cont);
            Wire(hud, "restartButton", restart);
            Wire(hud, "timerUpButton", up);
            Wire(hud, "timerDownButton", down);
            Wire(hud, "timerTriggerButton", trigger);
            Wire(hud, "timerClearButton", clear);
            Wire(hud, "hintButton", hintButton);
            Wire(hud, "replaySpeedButton", speed);
            Wire(hud, "exitReplayButton", exit);
            Wire(hud, "clearPlanButton", clearPlan);
            Wire(hud, "timerPanel", timerPanelGo);
            Wire(hud, "replayPanel", replay.gameObject);
            Wire(hud, "historyPanel", history.gameObject);
            Wire(hud, "historyContainer", list);
            Wire(hud, "historyEntryPrefab", historyEntryPrefab);
        }

        private static HistoryEntryView BuildHistoryEntryPrefab()
        {
            var root = new GameObject("HistoryEntry", typeof(RectTransform));
            var bg = root.AddComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.06f);
            var element = root.AddComponent<LayoutElement>();
            element.preferredHeight = 40f;
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(400f, 40f);

            Text label = Label(rect, "Label", "#1 — 0,00 s", 22, TextAnchor.MiddleLeft);
            label.rectTransform.anchorMin = new Vector2(0f, 0f);
            label.rectTransform.anchorMax = new Vector2(0f, 1f);
            label.rectTransform.pivot = new Vector2(0f, 0.5f);
            label.rectTransform.anchoredPosition = new Vector2(10f, 0f);
            label.rectTransform.sizeDelta = new Vector2(160f, 0f);

            Button watch = MakeButton(rect, "Watch", "Ver", new Vector2(1f, 0.5f), new Vector2(-226f, 0f), new Vector2(56f, 34f));
            Button ghost = MakeButton(rect, "Ghost", "Fantasma", new Vector2(1f, 0.5f), new Vector2(-90f, 0f), new Vector2(130f, 34f));
            Button baseButton = MakeButton(rect, "Base", "Base", new Vector2(1f, 0.5f), new Vector2(-4f, 0f), new Vector2(80f, 34f));
            foreach (Button b in new[] { watch, ghost, baseButton })
            {
                ((RectTransform)b.transform).pivot = new Vector2(1f, 0.5f);
                b.GetComponentInChildren<Text>().fontSize = 18;
            }

            var view = root.AddComponent<HistoryEntryView>();
            Wire(view, "label", label);
            Wire(view, "watchButton", watch);
            Wire(view, "ghostButton", ghost);
            Wire(view, "ghostLabel", ghost.GetComponentInChildren<Text>());
            Wire(view, "baseButton", baseButton);
            return SavePrefab(root, "HistoryEntry").GetComponent<HistoryEntryView>();
        }

        // ------------------------------------------------------------ UI helpers

        private static RectTransform Panel(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = anchorMin;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static Text Label(RectTransform parent, string name, string text, int size, TextAnchor anchor, FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<Text>();
            label.font = _font;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.alignment = anchor;
            label.color = Color.white;
            label.supportRichText = true;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
        }

        private static void Stretch(RectTransform rect, Vector2 offsetTopLeft, Vector2 offsetRight, bool top, float height)
        {
            if (top)
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.offsetMin = new Vector2(offsetTopLeft.x, offsetTopLeft.y - height);
                rect.offsetMax = new Vector2(offsetRight.x, offsetTopLeft.y);
            }
            else
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(offsetTopLeft.x, 0f);
                rect.offsetMax = new Vector2(offsetRight.x, 0f);
            }
        }

        private static Button MakeButton(RectTransform parent, string name, string text, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = new Color(0.22f, 0.25f, 0.32f, 0.95f);
            var button = go.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.35f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.8f);
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.4f);
            button.colors = colors;
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Text label = Label(rect, "Text", text, 22, TextAnchor.MiddleCenter, FontStyle.Bold);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
            return button;
        }

        private static HoldButton MakeHoldButton(RectTransform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var go = new GameObject("RestartHoldButton", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = new Color(0.35f, 0.18f, 0.18f, 0.95f);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(rect, false);
            var fill = fillGo.GetComponent<Image>();
            fill.color = new Color(1f, 0.45f, 0.35f, 0.9f);
            fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 0f;
            fill.raycastTarget = false;
            var fillRect = (RectTransform)fillGo.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            Text label = Label(rect, "Text", "Segure para reiniciar (R)", 22, TextAnchor.MiddleCenter, FontStyle.Bold);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;

            var hold = go.AddComponent<HoldButton>();
            Wire(hold, "fill", fill);
            return hold;
        }

        // ------------------------------------------------------------ generic helpers

        private static GameObject Primitive(PrimitiveType type, string name, Transform parent, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        private static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static GameObject SavePrefab(GameObject go, string name)
        {
            string path = $"{PrefabFolder}/{name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static void Wire(Object target, string field, Object value)
        {
            if (value == null)
            {
                throw new System.InvalidOperationException($"Cannot wire {target.GetType().Name}.{field}: value is null.");
            }
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(field)
                ?? throw new System.InvalidOperationException($"{target.GetType().Name} has no serialized field '{field}'.");
            prop.objectReferenceValue = value;
            if (prop.objectReferenceValue != value)
            {
                throw new System.InvalidOperationException($"{target.GetType().Name}.{field} rejected a {value.GetType().Name}.");
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireBool(Object target, string field, bool value)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(field)
                ?? throw new System.InvalidOperationException($"{target.GetType().Name} has no serialized field '{field}'.");
            prop.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddSceneToBuildSettings()
        {
            var scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            EditorBuildSettings.scenes = scenes;
        }
    }
}
