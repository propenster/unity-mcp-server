#!/usr/bin/env node

import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import {
    CallToolRequestSchema,
    ListToolsRequestSchema,
    Tool,
} from "@modelcontextprotocol/sdk/types.js";
import * as fs from 'fs';
import * as path from 'path';

/**
 * We want to get the QUERY from Claude and the Query could be a command to run in Unity.
 * 
 * We want to return the output of the command to Claude.
 * 
 * We can use the Unity C# API to run commands in Unity. That means we need to have/generate a C# script that can be used in Unity.
 * 
 * @param query: The query to interact with Unity
 * @param unity_project_path: The path to the Unity project -> I think this is the path to the project folder in the file system e.g C:\Users\<username>\Documents\<project_name>
 * @returns: The output of the command
 * 
 * So we get the query from Claude, type of the query is it CREATE, READ, UPDATE, DELETE
 * 
 * Before running/iexecuting the command, we need to check for a UnityEditor script file... if not create it, create an empty scene in it that we will
 * dump whatever OBJECTS created in this current Claude Session in...And if it already exists, we will update it. 
 * 
 * 
 * 
 * 
 * 
 * 
 * 
 * If it is CREATE, we need to generate a C# script and save it to the unity_project_path/Assets/Scripts folder.
 * 
 * 
 */



/**
 * How about I receive request from Claude and I just generate a boilerPlate C# script send that back to Claude to modify based on 
 * the query and then I execute the modified script in Unity.
 * This could work better than the current approach.
 * 
 * 
 * */

const UNITY_SCENE_DESIGNER_TOOL = {
    name: "unity_scene_designer",
    description: "Use this tool to interact with Unity, to design a scene in Unity." +
        "How about I receive request from Claude and I just generate a boilerPlate C# script" +
        " send that back to Claude to modify based on the query and then I execute the modified script in Unity." +
        "I always rerender the scene after executing the script." +
        "And also note the Scene file has to have a unique name and " +
        "it has to be a valid Unity Scene file name that myself and Claude can pass around " +
        "so we don't have to create new scene files in the same Claude Session." +
        "The script Claude will generate/modify will be a UnityEditor script file that will be real-time to show how the scenes is getting designed." +
        "This could work better than the current approach.",
    inputSchema: {
        type: "object",
        properties: {
            query: {
                type: "string",
                description: "The query to interact with Unity. I don't really do anything serious with the query," +
                    "I just generate a boilerPlate C# script and send it back to Claude to modify based on the query." +
                    "And I execute the modified script in Unity. and always rerender the scene after executing the script."
            },
            unity_project_path: {
                type: "string",
                description: "The path to the Unity project. You MUST ask the user for this path before continuing."
            }
        },
        required: ["query", "unity_project_path"],
    },
};

const SEND_MODIFIED_SCRIPT_BACK_TO_SERVER_TOOL: Tool = {
    name: "send_modified_script_back_to_server",
    description: "Claude use this tool to send the modified script back to the mcp server to final execution in UnityEditor" +
        "don't forget the to include the usings in the boilerplate script I gave you."
        + "don't forget that OnLoadMethod is always applied to the method instead of the class like this." +
        `static class AutoSceneCreator
{
    [InitializeOnLoadMethod] // This must be placed on a method, not the class itself
    private static void Initialize()
    {
        EditorApplication.delayCall += () => {
            SceneCreator.CreateSimpleScene();
        };
    }
}`
    ,
    inputSchema: {
        type: "object",
        properties: {
            modified_script: {
                type: "string",
                description: "The modified script source code that will be executed in UnityEditor"
            },
            unity_project_path: {
                type: "string",
                description: "The path to the Unity project"
            }
        },
        required: ["modified_script", "unity_project_path"],
    },
}

const UNITY_CREATE_OBJECT_TOOL: Tool = {
    name: "unity_create_object",
    description: "Use this tool to create an object in Unity",
    inputSchema: {
        type: "object",
        properties: {
            object_type: {
                type: "string",
                description: "The type of object to create" +
                    "Can be one of the following: " +
                    //"Floor, Wall, Light, Player, Enemy, Pickup, etc."
                    "Scene, Object, GameObject, Component, Material, Texture, etc."
            },
            object_uid: {
                type: "string",
                description: "The unique identifier for the object to create, " +
                    "this is necessary to identify the objects first in the Unity Editor and also to make updates to object easier. Like if I know the " +
                    "id of the object you Claude or any other AI agent wants me to move position of or add to a parent object, I can just use this uid to do that."
            },
            object_name: {
                type: "string",
                description: "The name of the object to create, give it any descriptive name you like"
            },
            object_description: {
                type: "string",
                description: "A description of the object to create, this will be used to describe the object in the Unity Editor. Nobody actually gives a shit about description so this is totally optional"
            },
            object_position: {
                type: "string",
                description: "The position of the object to create, I think this is actually Vector3d or Vector2D shit in the Unity lingua. Just pass X, Y, Z I think should suffice."
            },
            object_count: {
                type: "number",
                description: "The number of objects to create, this is optional and defaults to 1"
            },
            object_parent_uid: {
                type: "string",
                description: "The unique identifier of the parent object to create the object as a child of, this is optional and defaults to the root object"
            },


        },
        required: ["object_type", "object_uid", "object_count"],
    },
};


const LOCAL_SEARCH_TOOL: Tool = {
    name: "brave_local_search",
    description:
        "Searches for local businesses and places using Brave's Local Search API. " +
        "Best for queries related to physical locations, businesses, restaurants, services, etc. " +
        "Returns detailed information including:\n" +
        "- Business names and addresses\n" +
        "- Ratings and review counts\n" +
        "- Phone numbers and opening hours\n" +
        "Use this when the query implies 'near me' or mentions specific locations. " +
        "Automatically falls back to web search if no local results are found.",
    inputSchema: {
        type: "object",
        properties: {
            query: {
                type: "string",
                description: "Local search query (e.g. 'pizza near Central Park')"
            },
            count: {
                type: "number",
                description: "Number of results (1-20, default 5)",
                default: 5
            },
        },
        required: ["query"]
    }
};

// Server implementation
const server = new Server(
    {
        name: "unity-scene-designer",
        version: "0.1.0",
    },
    {
        capabilities: {
            tools: {},
        },
    },
);


interface UnitySceneDesignerResponse {
    real_time_scene_design_script_source_code: string;
    scene_file_path: string;
    file_uid: string;

}

function generateRealTimeSceneDesignScriptSourceCode(query: string): string {
    return `
    using UnityEngine;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;

    public class SceneCreator : EditorWindow
    {
        private string sceneName = "New Scene";

    }
    `
}

function realTimeSceneDesignScriptSourceCodeToUnityEditorScript(claudeModifiedditorScriptSourceCode: string, unity_project_path: string) {
    // console.log("Writing modified script to Unity Editor Script");
    const scriptPath = path.join(unity_project_path, "Assets", "Scripts", "SceneCreator.cs");
    const fs = require("node:fs");
    fs.writeFileSync(scriptPath, claudeModifiedditorScriptSourceCode);
}
function createUnityEditorScript(scriptPath: string, scriptContent: string) {
    const scriptDirectory = path.dirname(scriptPath);
    // Ensure the directory exists
    if (!fs.existsSync(scriptDirectory)) {
        fs.mkdirSync(scriptDirectory, { recursive: true });
    }
    scriptContent = scriptContent.replace(/\\"/g, '"');
    // Write the file
    fs.writeFileSync(scriptPath, scriptContent, "utf8");
}
async function unityDesign(query: string, unity_project_path: string) {
    // console.log("Generating real time scene design script source code");
    const realTimeSceneDesignScriptSourceCode = generateRealTimeSceneDesignScriptSourceCode(query);
    // const claudeModifiedditorScriptSourceCode = await claude.modify(realTimeSceneDesignScriptSourceCode);
    // realTimeSceneDesignScriptSourceCodeToUnityEditorScript(claudeModifiedditorScriptSourceCode, unity_project_path);
    // executeUnityEditorScript(scriptPath, unity_project_path);
    return realTimeSceneDesignScriptSourceCode;
}
// async function executeUnityEditorScript(scriptPath: string, unity_project_path: string) {
//     const child_process = require("node:child_process");
//     const process = child_process.spawn("unity", ["-batchmode", "-nographics", "-executeMethod", "SceneCreator.ShowWindow"]);
//     process.stdout.on("data", (data: string) => {
//         console.log(data);
//     });

//     process.stderr.on("data", (data: string) => {
//         console.error(data);
//     });
//     process.on("close", (code: number) => {
//         console.log("Unity Editor script executed with code " + code);
//     });
// }

function isUnitySceneDesignerArgs(args: unknown): args is { query: string; unity_project_path: string } {
    return (
        typeof args === "object" &&
        args !== null &&
        "query" in args &&
        "unity_project_path" in args &&
        typeof (args as { query: string }).query === "string" &&
        typeof (args as { unity_project_path: string }).unity_project_path === "string"
    );
}
function isSendModifiedScriptBackToServerArgs(args: unknown): args is { modified_script: string; unity_project_path: string } {
    return (
        typeof args === "object" &&
        args !== null &&
        "modified_script" in args &&
        typeof (args as { modified_script: string }).modified_script === "string" &&
        "unity_project_path" in args &&
        typeof (args as { unity_project_path: string }).unity_project_path === "string"
    );
}

// Tool handlers
server.setRequestHandler(ListToolsRequestSchema, async () => ({
    tools: [UNITY_SCENE_DESIGNER_TOOL, SEND_MODIFIED_SCRIPT_BACK_TO_SERVER_TOOL],
}));

server.setRequestHandler(CallToolRequestSchema, async (request) => {
    try {
        const { name, arguments: args } = request.params;

        if (!args) {
            throw new Error("No arguments provided");
        }
        switch (name) {
            case "unity_scene_designer": {
                if (!isUnitySceneDesignerArgs(args)) {
                    throw new Error("Invalid arguments for unity_scene_designer");
                }
                const { query, unity_project_path } = args;
                const results = await unityDesign(args.query, args.unity_project_path);
                // console.log(Unity Scene Designer results: ${results});
                return {
                    content: [{ type: "text", text: results }],
                    isError: false,
                };
            }
            case "send_modified_script_back_to_server": {
                if (!isSendModifiedScriptBackToServerArgs(args)) {
                    throw new Error("Invalid arguments for send_modified_script_back_to_server");
                }
                const { modified_script, unity_project_path } = args;
                // console.log(modified_script);
                const scriptPath = path.join(unity_project_path, "Assets", "Scripts", "SceneCreator.cs");
                // console.log(Writing modified script to Unity Editor Script ${scriptPath});
                createUnityEditorScript(scriptPath, modified_script);
                return {
                    content: [{ type: "text", text: modified_script }],
                    isError: false,
                };
            }
        }

    } catch (error) {
        return {
            content: [{
                type: "text",
                text: "Error: " + (error instanceof Error ? error.message : String(error)),
            }],
            isError: true,
        };
    }
});

async function runServer() {
    // console.log("Starting Unity Scene Designer MCP Server");
    const transport = new StdioServerTransport();
    await server.connect(transport);
    // console.error("Unity Scene Designer MCP Server running on stdio");
}

runServer().catch((error) => {
    console.error("Fatal error running server:", error);
    process.exit(1);
});