using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

public class SceneCreator : EditorWindow
{
    private string sceneName = "New Scene";
    private int gridSize = 10;
    private float cellSize = 1.0f;
    private GameObject floorPrefab;
    private GameObject wallPrefab;
    private GameObject lightPrefab;
    private GameObject playerPrefab;
    
    private bool createFloor = true;
    private bool createWalls = true;
    private bool addLights = true;
    private bool addPlayer = true;
    private bool livePreview = true;
    
    private Color floorColor = Color.gray;
    private Color wallColor = Color.white;
    private Color ambientLightColor = new Color(0.3f, 0.3f, 0.3f);
    
    private Vector2 scrollPosition;
    private string statusMessage = "";
    
    // Preview objects
    private List<GameObject> previewObjects = new List<GameObject>();
    private bool previewNeedsUpdate = true;

    [MenuItem("Tools/Scene Creator")]
    public static void ShowWindow()
    {
        // First, ensure the script is in the proper Editor folder
        EnsureScriptIsInEditorFolder();
        
        // Then open the window
        GetWindow<SceneCreator>("Scene Creator");
    }
    
    private static void EnsureScriptIsInEditorFolder()
    {
        // Get the current script file path
        MonoScript monoScript = MonoScript.FromScriptableObject(CreateInstance<SceneCreator>());
        string currentPath = AssetDatabase.GetAssetPath(monoScript);
        
        // Check if it's already in an Editor folder
        if (currentPath.Contains("/Editor/") || currentPath.Contains("\\Editor\\"))
        {
            return; // Already in an Editor folder
        }
        
        // Create Editor directory if it doesn't exist
        string editorFolder = "Assets/Editor";
        if (!Directory.Exists(editorFolder))
        {
            Directory.CreateDirectory(editorFolder);
            AssetDatabase.Refresh();
        }
        
        // Target path in Editor folder
        string targetPath = Path.Combine(editorFolder, "SceneCreator.cs");
        
        // Move the script to the Editor folder
        if (currentPath != targetPath)
        {
            try
            {
                // Read the script content
                string scriptContent = File.ReadAllText(currentPath);
                
                // Write to the new location
                File.WriteAllText(targetPath, scriptContent);
                
                // Delete the old script after the next asset refresh
                EditorApplication.delayCall += () => {
                    AssetDatabase.DeleteAsset(currentPath);
                    Debug.Log("SceneCreator script moved to Editor folder. Please reopen the editor window.");
                };
                
                AssetDatabase.Refresh();
            }
            catch (System.Exception e)
            {
                Debug.LogError("Failed to move SceneCreator script to Editor folder: " + e.Message);
            }
        }
    }

    void OnEnable()
    {
        // Subscribe to scene view update to refresh our preview
        SceneView.duringSceneGui += OnSceneGUI;
        EditorApplication.update += OnEditorUpdate;
        previewNeedsUpdate = true;
    }
    
    void OnDisable()
    {
        // Unsubscribe when window is closed
        SceneView.duringSceneGui -= OnSceneGUI;
        EditorApplication.update -= OnEditorUpdate;
        ClearPreview();
    }
    
    void OnEditorUpdate()
    {
        if (previewNeedsUpdate && livePreview)
        {
            UpdatePreview();
            previewNeedsUpdate = false;
        }
    }
    
    void OnSceneGUI(SceneView sceneView)
    {
        // We can add custom handles or gizmos here if needed
    }

    void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUILayout.LabelField("Scene Creator Tool", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        
        // Check if any property changed to update preview
        EditorGUI.BeginChangeCheck();
        
        DrawBasicSettings();
        EditorGUILayout.Space();
        
        DrawPrefabSettings();
        EditorGUILayout.Space();
        
        DrawColorSettings();
        EditorGUILayout.Space();
        
        DrawOptionSettings();
        EditorGUILayout.Space();
        
        // Live preview toggle
        bool prevLivePreview = livePreview;
        livePreview = EditorGUILayout.Toggle("Live Preview", livePreview);
        if (prevLivePreview != livePreview)
        {
            if (livePreview)
                previewNeedsUpdate = true;
            else
                ClearPreview();
        }
        
        if (EditorGUI.EndChangeCheck())
        {
            previewNeedsUpdate = true;
        }
        
        if (GUILayout.Button("Create Scene"))
        {
            CreateScene();
        }
        
        if (GUILayout.Button("Clear Scene"))
        {
            ClearScene();
        }
        
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(statusMessage, MessageType.Info);
        
        EditorGUILayout.EndScrollView();
    }
    
    void DrawBasicSettings()
    {
        EditorGUILayout.LabelField("Basic Settings", EditorStyles.boldLabel);
        sceneName = EditorGUILayout.TextField("Scene Name", sceneName);
        gridSize = EditorGUILayout.IntSlider("Grid Size", gridSize, 5, 50);
        cellSize = EditorGUILayout.Slider("Cell Size", cellSize, 0.5f, 5f);
    }
    
    void DrawPrefabSettings()
    {
        EditorGUILayout.LabelField("Prefab Settings", EditorStyles.boldLabel);
        floorPrefab = (GameObject)EditorGUILayout.ObjectField("Floor Prefab", floorPrefab, typeof(GameObject), false);
        wallPrefab = (GameObject)EditorGUILayout.ObjectField("Wall Prefab", wallPrefab, typeof(GameObject), false);
        lightPrefab = (GameObject)EditorGUILayout.ObjectField("Light Prefab", lightPrefab, typeof(GameObject), false);
        playerPrefab = (GameObject)EditorGUILayout.ObjectField("Player Prefab", playerPrefab, typeof(GameObject), false);
    }
    
    void DrawColorSettings()
    {
        EditorGUILayout.LabelField("Color Settings", EditorStyles.boldLabel);
        floorColor = EditorGUILayout.ColorField("Floor Color", floorColor);
        wallColor = EditorGUILayout.ColorField("Wall Color", wallColor);
        ambientLightColor = EditorGUILayout.ColorField("Ambient Light", ambientLightColor);
    }
    
    void DrawOptionSettings()
    {
        EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
        createFloor = EditorGUILayout.Toggle("Create Floor", createFloor);
        createWalls = EditorGUILayout.Toggle("Create Walls", createWalls);
        addLights = EditorGUILayout.Toggle("Add Lights", addLights);
        addPlayer = EditorGUILayout.Toggle("Add Player", addPlayer);
    }
    
    void UpdatePreview()
    {
        ClearPreview();
        
        // Create preview objects with a special tag to identify them
        float totalWidth = gridSize * cellSize;
        
        // Create a preview parent object to organize everything
        GameObject previewParent = new GameObject("PREVIEW_SceneCreator");
        previewParent.hideFlags = HideFlags.DontSave;
        previewObjects.Add(previewParent);
        
        // Create floor
        if (createFloor)
        {
            GameObject floor;
            if (floorPrefab != null)
            {
                floor = Instantiate(floorPrefab, Vector3.zero, Quaternion.identity);
                floor.transform.localScale = new Vector3(totalWidth, 0.1f, totalWidth);
            }
            else
            {
                floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.transform.localScale = new Vector3(totalWidth, 0.1f, totalWidth);
                floor.transform.position = new Vector3(0, -0.05f, 0);
                
                // Apply floor material/color
                Renderer floorRenderer = floor.GetComponent<Renderer>();
                if (floorRenderer != null)
                {
                    Material previewMaterial = new Material(Shader.Find("Standard"));
                    previewMaterial.color = floorColor;
                    floorRenderer.sharedMaterial = previewMaterial;
                }
            }
            
            floor.name = "PREVIEW_Floor";
            floor.hideFlags = HideFlags.DontSave;
            floor.transform.SetParent(previewParent.transform);
            previewObjects.Add(floor);
        }
        
        // Create walls
        if (createWalls)
        {
            float halfWidth = totalWidth * 0.5f;
            float wallHeight = 2.0f;
            
            // Create the 4 walls
            for (int i = 0; i < 4; i++)
            {
                GameObject wall;
                
                if (wallPrefab != null)
                {
                    wall = Instantiate(wallPrefab);
                }
                else
                {
                    wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    
                    // Apply wall material/color
                    Renderer wallRenderer = wall.GetComponent<Renderer>();
                    if (wallRenderer != null)
                    {
                        Material previewMaterial = new Material(Shader.Find("Standard"));
                        previewMaterial.color = wallColor;
                        wallRenderer.sharedMaterial = previewMaterial;
                    }
                }
                
                wall.hideFlags = HideFlags.DontSave;
                
                switch (i)
                {
                    case 0: // North wall
                        wall.transform.position = new Vector3(0, wallHeight * 0.5f, halfWidth);
                        wall.transform.localScale = new Vector3(totalWidth, wallHeight, 0.1f);
                        wall.name = "PREVIEW_North_Wall";
                        break;
                    case 1: // South wall
                        wall.transform.position = new Vector3(0, wallHeight * 0.5f, -halfWidth);
                        wall.transform.localScale = new Vector3(totalWidth, wallHeight, 0.1f);
                        wall.name = "PREVIEW_South_Wall";
                        break;
                    case 2: // East wall
                        wall.transform.position = new Vector3(halfWidth, wallHeight * 0.5f, 0);
                        wall.transform.localScale = new Vector3(0.1f, wallHeight, totalWidth);
                        wall.name = "PREVIEW_East_Wall";
                        break;
                    case 3: // West wall
                        wall.transform.position = new Vector3(-halfWidth, wallHeight * 0.5f, 0);
                        wall.transform.localScale = new Vector3(0.1f, wallHeight, totalWidth);
                        wall.name = "PREVIEW_West_Wall";
                        break;
                }
                
                wall.transform.SetParent(previewParent.transform);
                previewObjects.Add(wall);
            }
        }
        
        // Add lights preview (using gizmos for lights)
        if (addLights)
        {
            // Add visual indicator for lights
            if (lightPrefab == null)
            {
                // Create a simple light representation
                GameObject lightIndicator = new GameObject("PREVIEW_Main_Light");
                lightIndicator.hideFlags = HideFlags.DontSave;
                
                // Add a visible sphere to represent light position
                GameObject lightSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                lightSphere.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
                lightSphere.transform.SetParent(lightIndicator.transform);
                lightSphere.hideFlags = HideFlags.DontSave;
                
                // Make it yellow like a light
                Renderer lightRenderer = lightSphere.GetComponent<Renderer>();
                if (lightRenderer != null)
                {
                    Material lightMaterial = new Material(Shader.Find("Standard"));
                    lightMaterial.color = Color.yellow;
                    lightMaterial.EnableKeyword("_EMISSION");
                    lightMaterial.SetColor("_EmissionColor", Color.yellow);
                    lightRenderer.sharedMaterial = lightMaterial;
                }
                
                lightIndicator.transform.position = new Vector3(2, 4, -2);
                lightIndicator.transform.SetParent(previewParent.transform);
                previewObjects.Add(lightIndicator);
            }
            else
            {
                // Place light prefabs in preview
                for (int i = 0; i < 4; i++)
                {
                    float angle = i * 90f;
                    float radius = totalWidth * 0.3f;
                    float x = Mathf.Cos(angle * Mathf.Deg2Rad) * radius;
                    float z = Mathf.Sin(angle * Mathf.Deg2Rad) * radius;
                    
                    GameObject light = Instantiate(lightPrefab);
                    light.hideFlags = HideFlags.DontSave;
                    light.transform.position = new Vector3(x, 3f, z);
                    light.name = "PREVIEW_Light_" + i;
                    light.transform.SetParent(previewParent.transform);
                    previewObjects.Add(light);
                }
            }
        }
        
        // Add player preview
        if (addPlayer && playerPrefab != null)
        {
            GameObject player = Instantiate(playerPrefab);
            player.hideFlags = HideFlags.DontSave;
            player.transform.position = new Vector3(0, 1f, 0);
            player.name = "PREVIEW_Player";
            player.transform.SetParent(previewParent.transform);
            previewObjects.Add(player);
        }
        
        // Focus scene view on our preview
        if (SceneView.lastActiveSceneView != null)
        {
            SceneView.lastActiveSceneView.FrameSelected();
        }
    }
    
    void ClearPreview()
    {
        // Destroy all preview objects
        foreach (GameObject obj in previewObjects)
        {
            if (obj != null)
            {
                DestroyImmediate(obj);
            }
        }
        
        previewObjects.Clear();
        
        // Also find any stray preview objects that might have been missed
        GameObject[] allObjects = FindObjectsOfType<GameObject>();
        foreach (GameObject obj in allObjects)
        {
            if (obj.name.StartsWith("PREVIEW_"))
            {
                DestroyImmediate(obj);
            }
        }
    }
    
    void CreateScene()
    {
        // Clear any preview objects first
        ClearPreview();
        
        // Create a new empty scene if we're not in a scene already or user wants a new scene
        if (EditorSceneManager.GetActiveScene().name == "")
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
        
        float totalWidth = gridSize * cellSize;
        
        // Store created GameObjects in a list for organization
        List<GameObject> createdObjects = new List<GameObject>();
        
        // Create parent objects for organization
        GameObject environment = new GameObject("Environment");
        GameObject lightsParent = new GameObject("Lights");
        
        // Create floor
        if (createFloor)
        {
            GameObject floor;
            if (floorPrefab != null)
            {
                floor = Instantiate(floorPrefab, Vector3.zero, Quaternion.identity);
                floor.transform.localScale = new Vector3(totalWidth, 0.1f, totalWidth);
            }
            else
            {
                floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.transform.localScale = new Vector3(totalWidth, 0.1f, totalWidth);
                floor.transform.position = new Vector3(0, -0.05f, 0);
                
                // Apply floor material/color
                Renderer floorRenderer = floor.GetComponent<Renderer>();
                if (floorRenderer != null)
                {
                    Material floorMaterial = new Material(Shader.Find("Standard"));
                    floorMaterial.color = floorColor;
                    floorRenderer.sharedMaterial = floorMaterial;
                }
            }
            
            floor.name = "Floor";
            floor.transform.SetParent(environment.transform);
            createdObjects.Add(floor);
        }
        
        // Create walls
        if (createWalls)
        {
            float halfWidth = totalWidth * 0.5f;
            float wallHeight = 2.0f;
            
            // Create the 4 walls
            for (int i = 0; i < 4; i++)
            {
                GameObject wall;
                
                if (wallPrefab != null)
                {
                    wall = Instantiate(wallPrefab);
                }
                else
                {
                    wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    
                    // Apply wall material/color
                    Renderer wallRenderer = wall.GetComponent<Renderer>();
                    if (wallRenderer != null)
                    {
                        Material wallMaterial = new Material(Shader.Find("Standard"));
                        wallMaterial.color = wallColor;
                        wallRenderer.sharedMaterial = wallMaterial;
                    }
                }
                
                switch (i)
                {
                    case 0: // North wall
                        wall.transform.position = new Vector3(0, wallHeight * 0.5f, halfWidth);
                        wall.transform.localScale = new Vector3(totalWidth, wallHeight, 0.1f);
                        wall.name = "North Wall";
                        break;
                    case 1: // South wall
                        wall.transform.position = new Vector3(0, wallHeight * 0.5f, -halfWidth);
                        wall.transform.localScale = new Vector3(totalWidth, wallHeight, 0.1f);
                        wall.name = "South Wall";
                        break;
                    case 2: // East wall
                        wall.transform.position = new Vector3(halfWidth, wallHeight * 0.5f, 0);
                        wall.transform.localScale = new Vector3(0.1f, wallHeight, totalWidth);
                        wall.name = "East Wall";
                        break;
                    case 3: // West wall
                        wall.transform.position = new Vector3(-halfWidth, wallHeight * 0.5f, 0);
                        wall.transform.localScale = new Vector3(0.1f, wallHeight, totalWidth);
                        wall.name = "West Wall";
                        break;
                }
                
                wall.transform.SetParent(environment.transform);
                createdObjects.Add(wall);
            }
        }
        
        // Add lights
        if (addLights)
        {
            // Set ambient light
            RenderSettings.ambientLight = ambientLightColor;
            
            // Create main directional light if no light prefab
            if (lightPrefab == null)
            {
                GameObject mainLight = new GameObject("Main Directional Light");
                Light lightComponent = mainLight.AddComponent<Light>();
                lightComponent.type = LightType.Directional;
                lightComponent.intensity = 1.0f;
                lightComponent.shadows = LightShadows.Soft;
                mainLight.transform.rotation = Quaternion.Euler(50, -30, 0);
                mainLight.transform.SetParent(lightsParent.transform);
                createdObjects.Add(mainLight);
            }
            else
            {
                // Use the light prefab and place a few lights around
                for (int i = 0; i < 4; i++)
                {
                    float angle = i * 90f;
                    float radius = totalWidth * 0.3f;
                    float x = Mathf.Cos(angle * Mathf.Deg2Rad) * radius;
                    float z = Mathf.Sin(angle * Mathf.Deg2Rad) * radius;
                    
                    GameObject light = Instantiate(lightPrefab);
                    light.transform.position = new Vector3(x, 3f, z);
                    light.name = "Light_" + i;
                    light.transform.SetParent(lightsParent.transform);
                    createdObjects.Add(light);
                }
            }
        }
        
        // Add player
        if (addPlayer && playerPrefab != null)
        {
            GameObject player = Instantiate(playerPrefab);
            player.transform.position = new Vector3(0, 1f, 0);
            player.name = "Player";
            createdObjects.Add(player);
        }
        
        // Create a camera if none exists and no player was added
        if (Camera.main == null && (!addPlayer || playerPrefab == null))
        {
            GameObject camera = new GameObject("Main Camera");
            camera.tag = "MainCamera";
            camera.AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 2f, -totalWidth * 0.4f);
            camera.transform.LookAt(new Vector3(0, 1f, 0));
            createdObjects.Add(camera);
        }
        
        statusMessage = "Scene created successfully!";
        
        // Save the scene
        string scenePath = "Assets/" + sceneName + ".unity";
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), scenePath);
        statusMessage += " Saved to " + scenePath;
        
        // Ensure SceneView focuses on the created scene
        if (SceneView.lastActiveSceneView != null)
        {
            SceneView.lastActiveSceneView.FrameSelected();
        }
    }
    
    void ClearScene()
    {
        // Clear preview objects first
        ClearPreview();
        
        // Find all root GameObjects
        List<GameObject> rootObjects = new List<GameObject>();
        Scene currentScene = EditorSceneManager.GetActiveScene();
        currentScene.GetRootGameObjects(rootObjects);
        
        // Destroy all found objects
        foreach (GameObject obj in rootObjects)
        {
            DestroyImmediate(obj);
        }
        
        statusMessage = "Scene cleared!";
    }
}