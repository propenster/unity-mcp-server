using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

public class SceneCreator : EditorWindow
{
    [MenuItem("Tools/Create Simple Scene")]
    public static void CreateSimpleScene()
    {
        // Create a new scene
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        
        // Create floor
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        floor.transform.position = new Vector3(0, 0, 0);
        floor.transform.localScale = new Vector3(5, 1, 5); // 10x10 units
        
        // Create walls
        CreateWalls();
        
        // Create yellow ball
        GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name = "YellowBall";
        ball.transform.position = new Vector3(0, 1, 0); // 1 unit above the floor
        ball.transform.localScale = new Vector3(2, 2, 2);
        
        // Create and assign yellow material to the ball
        Material yellowMaterial = new Material(Shader.Find("Standard"));
        yellowMaterial.color = new Color(1.0f, 0.9f, 0.2f); // Yellowish color
        ball.GetComponent<Renderer>().material = yellowMaterial;
        
        // Add a directional light
        GameObject lightObj = new GameObject("Directional Light");
        Light light = lightObj.AddComponent<Light>();
        light.type = LightType.Directional;
        lightObj.transform.position = new Vector3(0, 10, 0);
        lightObj.transform.rotation = Quaternion.Euler(50, 30, 0);
        
        // Add a camera
        GameObject cameraObj = new GameObject("Main Camera");
        Camera camera = cameraObj.AddComponent<Camera>();
        cameraObj.transform.position = new Vector3(0, 5, -10);
        cameraObj.transform.LookAt(ball.transform);
        
        // Save the scene
        string scenePath = EditorSceneManager.GetActiveScene().path;
        if (string.IsNullOrEmpty(scenePath))
        {
            string directory = "Assets/Scenes";
            
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
                
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), directory + "/SimpleScene.unity");
        }
        
        Debug.Log("Simple scene created with floor, walls, and yellow ball!");
    }
    
    private static void CreateWalls()
    {
        // Create parent object for walls
        GameObject wallsParent = new GameObject("Walls");
        
        // North Wall
        GameObject northWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        northWall.name = "North Wall";
        northWall.transform.parent = wallsParent.transform;
        northWall.transform.position = new Vector3(0, 2.5f, 5);
        northWall.transform.localScale = new Vector3(10, 5, 0.5f);
        
        // South Wall
        GameObject southWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        southWall.name = "South Wall";
        southWall.transform.parent = wallsParent.transform;
        southWall.transform.position = new Vector3(0, 2.5f, -5);
        southWall.transform.localScale = new Vector3(10, 5, 0.5f);
        
        // East Wall
        GameObject eastWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        eastWall.name = "East Wall";
        eastWall.transform.parent = wallsParent.transform;
        eastWall.transform.position = new Vector3(5, 2.5f, 0);
        eastWall.transform.localScale = new Vector3(0.5f, 5, 10);
        
        // West Wall
        GameObject westWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        westWall.name = "West Wall";
        westWall.transform.parent = wallsParent.transform;
        westWall.transform.position = new Vector3(-5, 2.5f, 0);
        westWall.transform.localScale = new Vector3(0.5f, 5, 10);
        
        // Create and assign material to walls
        Material wallMaterial = new Material(Shader.Find("Standard"));
        wallMaterial.color = new Color(0.8f, 0.8f, 0.8f); // Light gray color
        
        // Apply material to all walls
        northWall.GetComponent<Renderer>().material = wallMaterial;
        southWall.GetComponent<Renderer>().material = wallMaterial;
        eastWall.GetComponent<Renderer>().material = wallMaterial;
        westWall.GetComponent<Renderer>().material = wallMaterial;
    }
}

// Execute the scene creation when this script is compiled
[InitializeOnLoadMethod]
static class AutoSceneCreator
{
    static AutoSceneCreator()
    {
        EditorApplication.delayCall += () => {
            SceneCreator.CreateSimpleScene();
        };
    }
}