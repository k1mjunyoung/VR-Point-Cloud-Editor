using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using System.IO;
using System;
using System.Collections;

public class pointCloudManagerWindow : EditorWindow
{
    private bool frustumCulling = false;
    private bool LODSystemActive = false;
    private float FPS;
    private float escapedTime;
    private List<float> FPSvalues;
    private bool benchmarkActive = false;
    private bool playModeWasInitByWindow = false;
    private DirectoryInfo currentDirectory = null;
    private List<FileInfo> fileList = new List<FileInfo>();

    private string PrevScene;
    private bool benchmarkFinishedLoadPrevScene = false;

    [MenuItem("Point cloud manager/show main window")]
    public static void ShowWindow()
    {
        GetWindow(typeof(pointCloudManagerWindow));
    }

    private string OpenLAZFileDialog()
    {
        string currentFile = EditorUtility.OpenFilePanel("Choose file", "", "laz,las,cpc");
        if (currentFile == "")
            return currentFile;
        pointCloudManager.loadLAZFile(currentFile);

        return currentFile;
    }

    private void SaveLAZFileDialog(int index, string defaultName)
    {
        string saveToFile = EditorUtility.SaveFilePanel("Save to", "", defaultName, "laz");
        if (saveToFile == "")
            return;

        pointCloudManager.SaveLAZFile(saveToFile, index);
    }

    private void SaveOwnFormatFileDialog(int index, string defaultName)
    {
        string saveToFile = EditorUtility.SaveFilePanel("Save to", "", defaultName, "cpc");
        if (saveToFile == "")
            return;

        pointCloudManager.SaveOwnFormatFile(saveToFile, index);
    }

    void Update()
    {
        if (!benchmarkActive && benchmarkFinishedLoadPrevScene && !EditorApplication.isPlaying)
        {
            benchmarkFinishedLoadPrevScene = false;
            EditorSceneManager.OpenScene(PrevScene/*"Assets/Scenes/BenchmarkScene0.unity"*/);
        }

        if (benchmarkActive && !EditorApplication.isPlaying && !playModeWasInitByWindow)
        {
            escapedTime = 0;
            benchmarkActive = false;
        }

        if (benchmarkActive && !pointCloudManager.isWaitingToLoad && pointCloudManager.isReInitializationObjectsAsyncEmpty())
            escapedTime += Time.unscaledDeltaTime;
    }

    static bool loadStarted = false;

    void OnInspectorUpdate()
    {
        if (pointCloudManager.isWaitingToLoad)
            loadStarted = true;

        if (!benchmarkActive || pointCloudManager.isWaitingToLoad || !pointCloudManager.isReInitializationObjectsAsyncEmpty())
            return;

        if (!Camera.main.GetComponent<Animator>().enabled && loadStarted)
        {
            loadStarted = false;
            Camera.main.GetComponent<Animator>().enabled = true;
            Camera.main.GetComponent<Animator>().Play("BenchmarkAnimation", -1, 0f);
            Camera.main.GetComponent<Animator>().SetBool("BenchmarkAnimation", true);
        }

        if (benchmarkActive && Camera.main.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).normalizedTime > 1 && !Camera.main.GetComponent<Animator>().IsInTransition(0))
        {
            escapedTime = 0;
            benchmarkActive = false;
            Camera.main.GetComponent<Animator>().enabled = false;
            playModeWasInitByWindow = false;

            float totalFPS = 0.0f;
            for (int i = 0; i < FPSvalues.Count; i++)
            {
                totalFPS += FPSvalues[i];
            }

            Debug.Log("avarage fps: " + totalFPS / FPSvalues.Count);

            EditorApplication.isPlaying = false;
            benchmarkFinishedLoadPrevScene = true;
            //SceneManager.LoadScene("Assets/Scenes/BenchmarkScene0.unity", LoadSceneMode.Single);
            //EditorSceneManager.OpenScene("Assets/Scenes/BenchmarkScene0.unity");
        }

        FPS = 1.0f / Time.unscaledDeltaTime;
        FPSvalues.Add(FPS);
        if (FPSvalues.Count > 10000)
            FPSvalues.Clear();
        Repaint();
    }

    static int indexSelected;
    void OnGUI()
    {
        GUILayout.Label("FPS: " + FPS);
        GUILayout.Label("Escaped time: " + escapedTime);

        if (EditorApplication.isPlaying)
        {
            GUI.enabled = false;
        }

        if (GUILayout.Button("Lunch benchmark"))
        {
            if (FPSvalues == null)
                FPSvalues = new List<float>();

            FPSvalues.Clear();
            escapedTime = 0;

            PrevScene = EditorSceneManager.GetActiveScene().path;
            EditorSceneManager.OpenScene("Assets/Scenes/BenchmarkScene1.unity");

            //Camera.main.GetComponent<Animator>().enabled = true;
            //Camera.main.GetComponent<Animator>().Play("BenchmarkAnimation", -1, 0f);
            //Camera.main.GetComponent<Animator>().SetBool("BenchmarkAnimation", true);

            // Only in play mode
            //SceneManager.LoadScene("BenchmarkScene0", LoadSceneMode.Additive);

            benchmarkActive = true;
            if (EditorApplication.isPlaying == false)
            {
                playModeWasInitByWindow = true;
                EditorApplication.isPlaying = true;
            }

        }
        GUI.enabled = true;

        if (!EditorApplication.isPlaying || pointCloudManager.isWaitingToLoad)
        {
            GUI.enabled = false;
        }

        if (GUILayout.Button("Load LAZ file"))
            OpenLAZFileDialog();

        GUI.enabled = true;

        if (pointCloudManager.pointClouds != null)
        {
            string[] availableIndexes = new string[pointCloudManager.pointClouds.Count];
            for (int i = 0; i < pointCloudManager.pointClouds.Count; i++)
            {
                if (pointCloudManager.pointClouds[i].inSceneRepresentation == null)
                    return;
                availableIndexes[i] = pointCloudManager.pointClouds[i].inSceneRepresentation.name;

                if (pointCloudManager.pointClouds[i].UTMZone == 0 && !pointCloudManager.pointClouds[i].North)
                {
                    GUILayout.Label("UTMZone : Information about UTMZone was not found");
                }
                else
                {
                    GUILayout.Label("UTMZone : " + pointCloudManager.pointClouds[i].UTMZone + (pointCloudManager.pointClouds[i].North ? "N" : "S"));
                }
            }

            if (pointCloudManager.pointClouds.Count != 0)
            {
                indexSelected = EditorGUILayout.Popup("Choose file: ", indexSelected, availableIndexes);

                if (GUILayout.Button("Save to LAZ file"))
                    SaveLAZFileDialog(indexSelected, availableIndexes[indexSelected]);

                if (GUILayout.Button("Save to own file format"))
                    SaveOwnFormatFileDialog(indexSelected, availableIndexes[indexSelected]);
            }

            frustumCulling = GUILayout.Toggle(frustumCulling, "use frustum culling");
            pointCloudManager.SetFrustumCulling(frustumCulling);

            LODSystemActive = GUILayout.Toggle(LODSystemActive, "use LOD");
            pointCloudManager.SetLODSystemActive(LODSystemActive);

            if (LODSystemActive)
            {
                for (int j = 0; j < 4; j++)
                {
                    float tempMaxDistance = EditorGUILayout.FloatField("LODs[" + j + "].maxDistance: ", pointCloudManager.LODSettings[j].maxDistance);
                    float tempTargetPercentOFPoints = EditorGUILayout.FloatField("LODs[" + j + "].targetPercentOFPoints: ", pointCloudManager.LODSettings[j].targetPercentOFPoints);

                    if (tempMaxDistance != pointCloudManager.LODSettings[j].maxDistance ||
                        tempTargetPercentOFPoints != pointCloudManager.LODSettings[j].targetPercentOFPoints)
                    {
                        pointCloudManager.SetLODInfo(tempMaxDistance, tempTargetPercentOFPoints, j, 0);
                        pointCloudManager.LODSettings[j].maxDistance = tempMaxDistance;
                        pointCloudManager.LODSettings[j].targetPercentOFPoints = tempTargetPercentOFPoints;
                    }
                }
            }
        }

        GUILayout.Label("Alternative load : ");
        if (currentDirectory == null)
            currentDirectory = new DirectoryInfo(Application.dataPath + "//PointClouds");

        if (!EditorApplication.isPlaying || pointCloudManager.isWaitingToLoad)
        {
            GUI.enabled = false;
        }

        if (GUILayout.Button("Go to parent directory"))
            currentDirectory = currentDirectory.Parent;
        GUILayout.Label("Current directory: " + currentDirectory.FullName);

        foreach (FileInfo file in fileList)
        {
            //GUILayout.Button(file.Name);
            if (GUILayout.Button(file.Name))
                pointCloudManager.loadLAZFile(currentDirectory.FullName + "//" +file.Name);
        }

        GUI.enabled = true;

        timePassed += Time.unscaledDeltaTime;
        if (timePassed > 2.0f)
        {
            timePassed = 0.0f;
            updateFileList();
        }
    }
    static float timePassed = 0.0f;

    void updateFileList()
    {
        fileList.Clear();
        fileList.AddRange(new List<FileInfo>(currentDirectory.GetFiles("*.las", SearchOption.AllDirectories)));
        fileList.AddRange(new List<FileInfo>(currentDirectory.GetFiles("*.laz", SearchOption.AllDirectories)));
    }

    void Start()
    {
        updateFileList();
        benchmarkActive = false;
    }
}