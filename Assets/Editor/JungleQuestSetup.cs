using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;

public static class JungleQuestSetup
{
    private const string ScenePath = "Assets/JungleQuest/Scenes/JungleQuest.unity";

    [InitializeOnLoadMethod]
    private static void CreateOnceAfterCompilation()
    {
        EditorSceneManager.sceneOpened -= RebuildOutdatedSceneWhenOpened;
        EditorSceneManager.sceneOpened += RebuildOutdatedSceneWhenOpened;
        EditorApplication.delayCall += () => RefreshLoadedScene(SceneManager.GetActiveScene());
    }

    private static void RebuildOutdatedSceneWhenOpened(Scene scene, OpenSceneMode mode)
    {
        EditorApplication.delayCall += () => RefreshLoadedScene(scene);
    }

    private static void RefreshLoadedScene(Scene scene)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        ConfigureBackdropImport();
        if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)) { CreateScene(); return; }
        if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath) return;
        var managerObject = GameObject.Find("Jungle Quest Game");
        var game = managerObject ? managerObject.GetComponent<JungleQuestGame>() : null;
        if (!game) return;
        if (game.NeedsSceneRebuild)
        {
            Debug.Log("Jungle Quest scene content changed; rebuilding the saved generated scene.");
            CreateScene();
            return;
        }
        if (!game.HasAuthoredSceneDefaults)
        {
            if (!GameObject.Find("Jungle Quest World")) CreateEditModeWorld(scene, game);
            BakeDefaultObjects(scene, game);
            return;
        }
        bool repairedScripts = RestoreAuthoredBehaviours(scene, game);
        bool repairedBodies = RepairAuthoredEnemyBodies(scene);
        if (repairedScripts || repairedBodies)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Jungle Quest scene scripts and enemy movement settings restored.");
        }
    }

    private static void ConfigureBackdropImport()
    {
        foreach (string name in new[] { "JungleBackdrop.png", "JungleBackdropB.png", "ExplorerSprite.png", "ExplorerSpriteSheet.png", "JungleGround.png" })
        {
            string path = "Assets/JungleQuest/Resources/" + name;
            if (!System.IO.File.Exists(System.IO.Path.Combine(Application.dataPath, "JungleQuest/Resources/" + name))) continue;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                bool sheet = name == "ExplorerSpriteSheet.png";
                TextureImporterType expectedType = sheet ? TextureImporterType.Default : TextureImporterType.Sprite;
                bool changed = false;
                if (importer.textureType != expectedType) { importer.textureType = expectedType; changed = true; }
                if (!sheet && importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; changed = true; }
                if (importer.alphaIsTransparency != true) { importer.alphaIsTransparency = true; changed = true; }
                if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
                if (sheet && !importer.isReadable) { importer.isReadable = true; changed = true; }
                if (importer.filterMode != FilterMode.Bilinear) { importer.filterMode = FilterMode.Bilinear; changed = true; }
                if (importer.maxTextureSize != 2048) { importer.maxTextureSize = 2048; changed = true; }
                if (!changed) continue;
                importer.SaveAndReimport();
            }
        }
        foreach (string name in new[] { "MayaRealisticAnimations.png", "KoaRealisticAnimations.png", "TaviRealisticAnimations.png", "NiaRealisticAnimations.png" })
        {
            string path = "Assets/JungleQuest/Resources/CharacterArt/" + name;
            if (!System.IO.File.Exists(System.IO.Path.Combine(Application.dataPath, "JungleQuest/Resources/CharacterArt/" + name))) continue;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            ConfigureSourceAtlasImporter(path);
        }
        foreach (string name in new[] { "JungleCoinArt.png", "JungleCoinGlow.png", "FallenLogArt.png", "JungleStumpArt.png", "TreeTrunkArt.png", "PowerupLightning.png", "PowerupMagnet.png", "ThornCrownArt.png" })
        {
            string path = "Assets/JungleQuest/Resources/Artwork/" + name;
            if (!System.IO.File.Exists(System.IO.Path.Combine(Application.dataPath, "JungleQuest/Resources/Artwork/" + name))) continue;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            ConfigureArtworkImporter(path, name == "JungleCoinGlow.png" ? 256 : 512);
        }
        EnsureExplorerFrameAssets();
    }

    private static void ConfigureSourceAtlasImporter(string assetPath)
    {
        if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer) return;
        bool changed = false;
        if (importer.textureType != TextureImporterType.Default) { importer.textureType = TextureImporterType.Default; changed = true; }
        if (!importer.isReadable) { importer.isReadable = true; changed = true; }
        if (importer.alphaIsTransparency != true) { importer.alphaIsTransparency = true; changed = true; }
        if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
        if (importer.maxTextureSize != 2048) { importer.maxTextureSize = 2048; changed = true; }
        if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
        if (changed) importer.SaveAndReimport();
    }

    private static void ConfigureArtworkImporter(string assetPath, float pixelsPerUnit)
    {
        if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer) return;
        bool changed = false;
        if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; changed = true; }
        if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; changed = true; }
        if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; changed = true; }
        if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
        if (!importer.isReadable) { importer.isReadable = true; changed = true; }
        if (!Mathf.Approximately(importer.spritePixelsPerUnit, pixelsPerUnit)) { importer.spritePixelsPerUnit = pixelsPerUnit; changed = true; }
        if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        if (settings.spriteMeshType != SpriteMeshType.FullRect) { settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings); changed = true; }
        if (changed) importer.SaveAndReimport();
    }

    private static void EnsureExplorerFrameAssets()
    {
        string[] names = { "Maya", "Koa", "Tavi", "Nia" };
        string[] atlasNames = { "MayaRealisticAnimations", "KoaRealisticAnimations", "TaviRealisticAnimations", "NiaRealisticAnimations" };
        const string outputFolder = "Assets/JungleQuest/Resources/CharacterFrames";
        if (!AssetDatabase.IsValidFolder(outputFolder)) AssetDatabase.CreateFolder("Assets/JungleQuest/Resources", "CharacterFrames");
        for (int character = 0; character < names.Length; character++)
        {
            string sourcePath = "Assets/JungleQuest/Resources/CharacterArt/" + atlasNames[character] + ".png";
            var source = AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath);
            if (!source || source.width != 768 || source.height != 768 || !source.isReadable) continue;
            const int frameSize = 256;
            for (int frameIndex = 0; frameIndex < 9; frameIndex++)
            {
                int column = frameIndex % 3;
                int topRow = frameIndex / 3;
                int sourceY = (2 - topRow) * frameSize;
                var pixels = source.GetPixels(column * frameSize, sourceY, frameSize, frameSize);
                var frame = new Texture2D(frameSize, frameSize, TextureFormat.RGBA32, false);
                frame.SetPixels(pixels); frame.Apply(false, false);
                string relativePath = outputFolder + "/" + names[character] + "Frame" + frameIndex + ".png";
                string fullPath = System.IO.Path.Combine(Application.dataPath, "JungleQuest/Resources/CharacterFrames/" + names[character] + "Frame" + frameIndex + ".png");
                System.IO.File.WriteAllBytes(fullPath, frame.EncodeToPNG());
                Object.DestroyImmediate(frame);
                AssetDatabase.ImportAsset(relativePath, ImportAssetOptions.ForceSynchronousImport);
                ConfigureFrameImporter(relativePath);
            }
        }
    }

    private static void ConfigureFrameImporter(string assetPath)
    {
        if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer) return;
        bool changed = false;
        if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; changed = true; }
        if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; changed = true; }
        if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; changed = true; }
        if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
        if (!importer.isReadable) { importer.isReadable = true; changed = true; }
        if (importer.filterMode != FilterMode.Bilinear) { importer.filterMode = FilterMode.Bilinear; changed = true; }
        if (!Mathf.Approximately(importer.spritePixelsPerUnit, 512)) { importer.spritePixelsPerUnit = 512; changed = true; }
        if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        if (settings.spriteMeshType != SpriteMeshType.FullRect) { settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings); changed = true; }
        if (settings.spriteAlignment != (int)SpriteAlignment.Custom) { settings.spriteAlignment = (int)SpriteAlignment.Custom; importer.SetTextureSettings(settings); changed = true; }
        if (importer.spritePivot != new Vector2(.5f, .035f)) { importer.spritePivot = new Vector2(.5f, .035f); changed = true; }
        if (changed) importer.SaveAndReimport();
    }

    [MenuItem("Jungle Quest/Create or Rebuild Game Scene")]
    public static void CreateScene()
    {
        ConfigureBackdropImport();
        const string folder = "Assets/JungleQuest";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", "JungleQuest");
        if (!AssetDatabase.IsValidFolder(folder + "/Scenes")) AssetDatabase.CreateFolder(folder, "Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("Jungle Quest Game"); var game = root.AddComponent<JungleQuestGame>();
        CreateEditModeWorld(scene, game);
        BakeDefaultObjects(scene, game);
        EditorSceneManager.SaveScene(scene, ScenePath);
        var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log("Jungle Quest scene generated and added as build scene 0.");
    }

    [MenuItem("Jungle Quest/Bake All Default Objects Into Scene")]
    public static void BakeAllDefaultObjectsIntoScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath)
        {
            Debug.LogError("Open Assets/JungleQuest/Scenes/JungleQuest.unity before adding its Edit Mode world.");
            return;
        }
        ConfigureBackdropImport();
        var gameObject = GameObject.Find("Jungle Quest Game");
        var game = gameObject ? gameObject.GetComponent<JungleQuestGame>() : null;
        if (!game)
        {
            gameObject = new GameObject("Jungle Quest Game");
            Undo.RegisterCreatedObjectUndo(gameObject, "Add Jungle Quest game manager");
            game = gameObject.AddComponent<JungleQuestGame>();
        }
        if (!GameObject.Find("Jungle Quest World")) CreateEditModeWorld(scene, game);
        BakeDefaultObjects(scene, game);
    }

    private static void BakeDefaultObjects(Scene scene, JungleQuestGame game)
    {
        if (!game.AuthorDefaultSceneObjectsForEditor()) return;
        var world = GameObject.Find("Jungle Quest World");
        if (world)
        {
            foreach (var rootObject in scene.GetRootGameObjects())
            {
                if (rootObject == world || rootObject == game.gameObject) continue;
                Undo.SetTransformParent(rootObject.transform, world.transform, "Group Jungle Quest scene objects");
            }
        }
        game.PersistProceduralSpritesForEditor();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Jungle Quest defaults baked into and saved with the scene.");
    }

    private static bool RepairAuthoredEnemyBodies(Scene scene)
    {
        bool changed = false;
        foreach (var patrol in Object.FindObjectsByType<JunglePatrol>())
        {
            if (!patrol || patrol.gameObject.scene != scene) continue;
            var body = patrol.GetComponent<Rigidbody2D>();
            if (!body) continue;
            if (body.sleepMode != RigidbodySleepMode2D.NeverSleep || !body.simulated) changed = true;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            body.simulated = true;
            body.WakeUp();
            EditorUtility.SetDirty(body);
        }
        foreach (var patrol in Object.FindObjectsByType<JungleOwlPatrol>())
        {
            if (!patrol || patrol.gameObject.scene != scene) continue;
            var body = patrol.GetComponent<Rigidbody2D>();
            if (!body) continue;
            if (body.sleepMode != RigidbodySleepMode2D.NeverSleep || !body.simulated) changed = true;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            body.simulated = true;
            body.WakeUp();
            EditorUtility.SetDirty(body);
        }
        return changed;
    }

    private static bool RestoreAuthoredBehaviours(Scene scene, JungleQuestGame game)
    {
        bool changed = false;
        foreach (var root in scene.GetRootGameObjects())
        foreach (var child in root.GetComponentsInChildren<Transform>(true))
        {
            var go = child.gameObject;
            switch (go.name)
            {
                case "Explorer":
                    EnsureComponent<JunglePlayerVisual>(go, ref changed);
                    game.ConfigureSceneExplorer(go);
                    break;
                case "Sun coin":
                    var coin = EnsureComponent<JungleCoin>(go, ref changed);
                    var coinVisual = FindChild(go.transform, "Floating embossed gold medallion");
                    var coinGlow = FindChild(go.transform, "Coin aura");
                    if (coinVisual && coinGlow) coin.Configure(coinVisual, coinGlow, go.transform.position.x * .23f);
                    break;
                case "Fallen tree log • dangerous obstacle":
                case "Thorn patch • dangerous ground": // Keep older saved scenes functional until they are rebuilt.
                    EnsureComponent<JungleHazard>(go, ref changed);
                    break;
                case "Powerup • Speed":
                case "Powerup • Magnet":
                    var powerup = EnsureComponent<JunglePowerup>(go, ref changed);
                    powerup.Configure(go.name.EndsWith("Magnet") ? JunglePowerupKind.Magnet : JunglePowerupKind.Speed);
                    break;
                case "Rolling fallen tree log":
                    EnsureComponent<JungleHazard>(go, ref changed);
                    EnsureComponent<JungleRollingLog>(go, ref changed).Configure(go.transform.position.x - 2f, go.transform.position.x + 2f, 1.25f);
                    break;
                case "Mossy tree stump • dangerous obstacle":
                case "Broken branch • dangerous obstacle":
                case "Grass hidden spikes • dangerous obstacle":
                    EnsureComponent<JungleHazard>(go, ref changed);
                    break;
                case "Falling tree branch":
                    EnsureComponent<JungleHazard>(go, ref changed).enabled = false;
                    EnsureComponent<JungleFallingBranch>(go, ref changed).Configure(-2.82f);
                    break;
                case "Parallax jungle background":
                    EnsureComponent<JungleParallax>(go, ref changed).Configure(Camera.main, .34f);
                    break;
                case "Checkpoint flag":
                    EnsureComponent<JungleCheckpoint>(go, ref changed);
                    break;
                case "Ancient temple exit":
                    EnsureComponent<JungleFinish>(go, ref changed);
                    break;
                case "Patrolling jungle beetle":
                    EnsureComponent<JungleEnemy>(go, ref changed);
                    var beetlePatrol = EnsureComponent<JunglePatrol>(go, ref changed); beetlePatrol.left = go.transform.position.x > 100 ? 149 : 51; beetlePatrol.right = go.transform.position.x > 100 ? 155 : 59;
                    var beetleVisual = EnsureComponent<JungleBeetleVisual>(go, ref changed);
                    var beetleArt = FindChild(go.transform, "Beetle animated body");
                    if (beetleArt)
                    {
                        var legs = beetleArt.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Beetle leg joint").ToArray();
                        beetleVisual.Configure(beetleArt, legs, FindChild(beetleArt, "Near antenna"), FindChild(beetleArt, "Far antenna"));
                    }
                    break;
                case "Diving jungle owl":
                    EnsureComponent<JungleEnemy>(go, ref changed);
                    var owlPatrol = EnsureComponent<JungleOwlPatrol>(go, ref changed); owlPatrol.left = go.transform.position.x > 100 ? 158 : 78; owlPatrol.right = go.transform.position.x > 100 ? 175 : 90;
                    var owlVisual = EnsureComponent<JungleOwlVisual>(go, ref changed);
                    var owlArt = FindChild(go.transform, "Owl flight animation pivot");
                    if (owlArt) owlVisual.Configure(owlArt, FindChild(owlArt, "Far feathered wing pivot"), FindChild(owlArt, "Near feathered wing pivot"));
                    break;
                case "Fern stem":
                    var stemSway = EnsureComponent<JungleSway>(go, ref changed); stemSway.Configure(4f, .9f);
                    break;
                case "Lush fern leaflet":
                    var fernSway = EnsureComponent<JungleSway>(go, ref changed); fernSway.Configure(5f, 3.2f);
                    break;
                case "Hanging jungle vine":
                    var vineSway = EnsureComponent<JungleSway>(go, ref changed); vineSway.Configure(2.1f, 2.4f);
                    break;
                case "Vine leaf":
                    var leafSway = EnsureComponent<JungleSway>(go, ref changed); leafSway.Configure(2.7f, 4f);
                    break;
                case "Floating jungle pollen":
                    var firefly = EnsureComponent<JungleFirefly>(go, ref changed);
                    var halo = FindChild(go.transform, "Pollen glow"); if (halo) firefly.Configure(halo, go.transform.position.x * .73f, .52f);
                    break;
                case "Distant jungle bird":
                    var bird = EnsureComponent<JungleBird>(go, ref changed); bird.Configure(go.transform.position.x * .15f, .9f);
                    break;
            }
            if (go.GetComponents<Component>().Any(c => c == null))
            {
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                changed = true;
            }
        }
        return changed;
    }

    private static T EnsureComponent<T>(GameObject go, ref bool changed) where T : Component
    {
        var component = go.GetComponent<T>();
        if (!component) { component = Undo.AddComponent<T>(go); changed = true; }
        return component;
    }

    private static Transform FindChild(Transform root, string exactName)
    {
        if (!root) return null;
        foreach (var child in root.GetComponentsInChildren<Transform>(true)) if (child.name == exactName) return child;
        return null;
    }

    private static void CreateEditModeWorld(Scene scene, JungleQuestGame game)
    {
        var world = new GameObject("Jungle Quest World");
        Undo.RegisterCreatedObjectUndo(world, "Create Jungle Quest Edit Mode world");
        SceneManager.MoveGameObjectToScene(world, scene);

        var camera = Camera.main;
        if (!camera)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.transform.SetParent(world.transform, false);
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
        }
        camera.orthographic = true;
        camera.orthographicSize = 6.1f;
        camera.transform.position = new Vector3(0, 0, -10);
        camera.backgroundColor = new Color(.34f, .73f, .78f);
        camera.enabled = true;
        if (!camera.GetComponent<AudioListener>()) camera.gameObject.AddComponent<AudioListener>();

        var backdropA = LoadResourceSprite("JungleBackdrop");
        var backdropB = LoadResourceSprite("JungleBackdropB") ?? backdropA;
        AddBackdrop(world.transform, backdropA, backdropB);

        var ground = LoadResourceSprite("JungleGround");
        AddPlatform(world.transform, ground, 4, -4, 18, 2);
        AddPlatform(world.transform, ground, 28, -4, 12, 2);
        AddPlatform(world.transform, ground, 48, -4, 18, 2);
        AddPlatform(world.transform, ground, 72, -4, 16, 2);
        AddPlatform(world.transform, ground, 94, -4, 20, 2);
        AddPlatform(world.transform, ground, 111, -4, 14, 2);
        AddPlatform(world.transform, ground, 127, -4, 18, 2);
        AddPlatform(world.transform, ground, 148, -4, 20, 2);
        AddPlatform(world.transform, ground, 170, -4, 22, 2);
        AddPlatform(world.transform, ground, 15, -1.85f, 4, .55f);
        AddPlatform(world.transform, ground, 22, .2f, 4, .55f);
        AddPlatform(world.transform, ground, 34, -.8f, 5, .55f);
        AddPlatform(world.transform, ground, 42, .4f, 5, .55f);
        AddPlatform(world.transform, ground, 57, -.6f, 5, .55f);
        AddPlatform(world.transform, ground, 64, .6f, 5, .55f);
        AddPlatform(world.transform, ground, 82, -.6f, 5, .55f);
        AddPlatform(world.transform, ground, 89, .7f, 5, .55f);
        AddPlatform(world.transform, ground, 103, -.8f, 5, .55f);
        AddPlatform(world.transform, ground, 120, -.9f, 5, .55f);
        AddPlatform(world.transform, ground, 127, .45f, 5, .55f);
        AddPlatform(world.transform, ground, 138, -.7f, 5, .55f);
        AddPlatform(world.transform, ground, 145, .65f, 5, .55f);
        AddPlatform(world.transform, ground, 157, -.5f, 5, .55f);
        AddPlatform(world.transform, ground, 165, .8f, 5, .55f);
        AddPlatform(world.transform, ground, 175, -.5f, 5, .55f);

        var player = new GameObject("Explorer");
        player.transform.SetParent(world.transform, false);
        player.transform.localPosition = new Vector3(2, -2.36f, 0);
        var playerSprite = LoadResourceSprite("ExplorerSprite");
        if (playerSprite)
        {
            var artwork = new GameObject("Painted explorer sprite");
            artwork.transform.SetParent(player.transform, false);
            artwork.transform.localPosition = new Vector3(0, .12f, -.2f);
            artwork.transform.localScale = new Vector3(1.6f / playerSprite.bounds.size.x, 1.75f / playerSprite.bounds.size.y, 1);
            var spriteRenderer = artwork.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = playerSprite;
            spriteRenderer.sortingOrder = 20;
        }
        player.AddComponent<JunglePlayerVisual>();
        player.AddComponent<BoxCollider2D>().size = new Vector2(.72f, 1.28f);
        var body = player.AddComponent<Rigidbody2D>();
        body.freezeRotation = true;
        body.gravityScale = 3.2f;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        game.SetSceneWorld(camera, player);
        EditorUtility.SetDirty(game);
    }

    private static Sprite LoadResourceSprite(string resourceName)
    {
        var sprite = Resources.Load<Sprite>(resourceName);
        if (sprite) return sprite;
        var sprites = Resources.LoadAll<Sprite>(resourceName);
        return sprites.Length > 0 ? sprites[0] : null;
    }

    private static void AddBackdrop(Transform parent, Sprite artA, Sprite artB)
    {
        if (!artA) return;
        if (!artB) artB = artA;
        const float width = 25f;
        var layer = new GameObject("Parallax jungle background");
        layer.transform.SetParent(parent, false);
        var parallax = layer.AddComponent<JungleParallax>();
        parallax.Configure(Camera.main, .34f);
        for (int i = -1; i < 9; i++)
        {
            var sprite = i % 2 == 0 ? artA : artB;
            var backdrop = new GameObject("Painted rainforest panorama");
            backdrop.transform.SetParent(layer.transform, false);
            float height = width / sprite.bounds.size.x * sprite.bounds.size.y;
            backdrop.transform.localPosition = new Vector3(i * width, 0, 20);
            backdrop.transform.localScale = new Vector3(width / sprite.bounds.size.x, height / sprite.bounds.size.y, 1);
            var renderer = backdrop.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = -100;
        }
    }

    private static void AddPlatform(Transform parent, Sprite ground, float x, float y, float width, float height)
    {
        var platform = new GameObject("Jungle earth platform");
        platform.transform.SetParent(parent, false);
        platform.transform.localPosition = new Vector3(x, y, 0);
        platform.AddComponent<BoxCollider2D>().size = new Vector2(width, height);
        if (!ground) return;
        var artwork = new GameObject("Layered soil and moss");
        artwork.transform.SetParent(platform.transform, false);
        artwork.transform.localPosition = new Vector3(0, height * .0625f, .05f);
        artwork.transform.localScale = new Vector3(width / ground.bounds.size.x, height / (ground.bounds.size.y * .64f), 1);
        var renderer = artwork.AddComponent<SpriteRenderer>();
        renderer.sprite = ground;
        renderer.sortingOrder = -1;
    }

    // Unity batch mode hook used for deterministic project setup and validation.
    public static void GenerateForBuild() => CreateScene();
}
