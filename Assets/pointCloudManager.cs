
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Runtime.InteropServices;
using System;
using System.IO;
using UnityEngine.InputSystem;

//public class pointCloudRawDataContainer : MonoBehaviour
//{
//    public String pathToRawData;

//    public pointCloudRawDataContainer(String pathToRawData)
//    {
//        this.pathToRawData = pathToRawData;
//    }
//}

public class debugNodeBox
{
    public Vector3 center;
    public float size;
    public int depth;
    public debugNodeBox(Vector3 center, float size, int depth)
    {
        this.center = center;
        this.size = size;
        this.depth = depth;
    }
}

public class LODInformation
{
    public float maxDistance;
    public float targetPercentOFPoints;
    public int takeEach_Nth_Point;
};

public class pointCloud
{
    public GameObject inSceneRepresentation;
    public Vector3 adjustment;
    public double initialXShift;
    public double initialZShift;
    public string spatialInfo;
    public int UTMZone;
    public bool North;

    public pointCloud(string path, GameObject reinitialization = null)
    {
        if (reinitialization == null)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            inSceneRepresentation = new GameObject(name + "_" + pointCloudManager.pointClouds.Count);
            var newScript = inSceneRepresentation.AddComponent<pointCloudRawDataContainer>();
            newScript.pathToRawData = path;
        }
        else
        {
            inSceneRepresentation = reinitialization;
        }
    }
}
public class pointCloudManager : MonoBehaviour
{
    [DllImport("PointCloudPlugin")]
    private static extern void updateCamera(IntPtr worldMatrix, IntPtr projectionMatrix);
    [DllImport("PointCloudPlugin")]
    static public extern bool updateWorldMatrix(IntPtr worldMatrix, int pointCloudIndex);
    [DllImport("PointCloudPlugin")]
    private static extern IntPtr GetRenderEventFunc();
    [DllImport("PointCloudPlugin")]
    private static extern void SetMeshBuffersFromUnity(IntPtr vertexBuffer, int vertexCount, IntPtr sourceVertex, IntPtr sourceColor);
    [DllImport("PointCloudPlugin")]
    private static extern void RequestToDeleteFromUnity(IntPtr center, float size);
    [DllImport("PointCloudPlugin")]
    private static extern int RequestOctreeBoundsCountFromUnity();
    [DllImport("PointCloudPlugin")]
    private static extern void RequestOctreeBoundsFromUnity(IntPtr arrayToFill);
    [DllImport("PointCloudPlugin")]
    private static extern int RequestOctreeDebugMaxNodeDepthFromUnity();
    [DllImport("PointCloudPlugin")]
    static public extern bool OpenLAZFileFromUnity(IntPtr filePath);
    [DllImport("PointCloudPlugin")]
    static public extern bool IsLastAsyncLoadFinished();
    [DllImport("PointCloudPlugin")]
    static public extern void OnSceneStartFromUnity(IntPtr projectFilePath);
    [DllImport("PointCloudPlugin")]
    static public extern void SaveToLAZFileFromUnity(IntPtr filePath, int pointCloudIndex);
    [DllImport("PointCloudPlugin")]
    static public extern void SaveToOwnFormatFileFromUnity(IntPtr filePath, int pointCloudIndex);
    [DllImport("PointCloudPlugin")]
    static public extern void RequestPointCloudAdjustmentFromUnity(IntPtr adjustment, int pointCloudIndex);
    [DllImport("PointCloudPlugin")]
    static public extern void RequestPointCloudUTMZoneFromUnity(IntPtr UTMZone, IntPtr North, int pointCloudIndex);
    [DllImport("PointCloudPlugin")]
    static public extern void setFrustumCulling(bool active);
    [DllImport("PointCloudPlugin")]
    static public extern void setLODSystemActive(bool active);
    [DllImport("PointCloudPlugin")]
    static public extern void setLODTransitionDistance(IntPtr LODTransitionDistance);

    [DllImport("PointCloudPlugin")]
    static public extern void RequestLODInfoFromUnity(IntPtr maxDistance, IntPtr targetPercentOFPoints, int LODIndex, int pointCloudIndex);
    [DllImport("PointCloudPlugin")]
    static public extern void setLODInfo(IntPtr values, int LODIndex, int pointCloudIndex);

    List<Vector3> vertexData;
    List<Color32> vertexColors;
    public MeshFilter pointCloud;
    public GameObject pointCloudGameObject;
    private List<List<LineRenderer>> octreeDepthViualizations;
    public static List<pointCloud> pointClouds;
    public static List<LODInformation> LODSettings;

    static List<pointCloudRawDataContainer> reInitializationObjectsAsync;
    public static bool isReInitializationObjectsAsyncEmpty()
    {
        if (reInitializationObjectsAsync == null)
            return true;

        return reInitializationObjectsAsync.Count == 0;
    }

    public static bool isWaitingToLoad = false;
    public static string filePathForAsyncLoad = "";
    public static GameObject reInitializationForAsyncLoad = null;
    public static float localLODTransitionDistance = 3500.0f;

    public static void loadLAZFile(string filePath, GameObject reinitialization = null)
    {
        IntPtr strPtr = Marshal.StringToHGlobalAnsi(filePath);
        string LAZFileName = Path.GetFileNameWithoutExtension(filePath);

        if (!OpenLAZFileFromUnity(strPtr))
        {
            Debug.Log("Loading of " + filePath + " failed!");
            return;
        }

        isWaitingToLoad = true;

        if (pointClouds == null)
            pointClouds = new List<pointCloud>();

        LODSettings = new List<LODInformation>();
        IntPtr maxDistance = Marshal.AllocHGlobal(8);
        IntPtr targetPercentOFPoints = Marshal.AllocHGlobal(8);

        for (int i = 0; i < 4; i++)
        {
            LODSettings.Add(new LODInformation());
            RequestLODInfoFromUnity(maxDistance, targetPercentOFPoints, i, 0);
            float[] distance = new float[1];
            Marshal.Copy(maxDistance, distance, 0, 1);
            LODSettings[i].maxDistance = distance[0];

            float[] percentOFPoints = new float[1];
            Marshal.Copy(targetPercentOFPoints, percentOFPoints, 0, 1);
            LODSettings[i].targetPercentOFPoints = percentOFPoints[0];
        }

        Marshal.FreeHGlobal(maxDistance);
        Marshal.FreeHGlobal(targetPercentOFPoints);

        filePathForAsyncLoad = filePath;
        reInitializationForAsyncLoad = reinitialization;

        //pointClouds.Add(new pointCloud(filePath, reinitialization));

        //IntPtr adjustmentArray = Marshal.AllocHGlobal(8 * 5);
        //RequestPointCloudAdjustmentFromUnity(adjustmentArray, pointClouds.Count - 1);

        //IntPtr UTMZone = Marshal.AllocHGlobal(8);
        //IntPtr North = Marshal.AllocHGlobal(8);
        //RequestPointCloudUTMZoneFromUnity(UTMZone, North, pointClouds.Count - 1);
        //int[] zone = new int[1];
        //Marshal.Copy(UTMZone, zone, 0, 1);

        //int[] north = new int[1];
        //Marshal.Copy(North, north, 0, 1);

        //pointClouds[pointClouds.Count - 1].UTMZone = zone[0];
        //pointClouds[pointClouds.Count - 1].North = north[0] == 1;

        //float[] adjustmentResult = new float[5];
        //Marshal.Copy(adjustmentArray, adjustmentResult, 0, 5);
        //pointClouds[pointClouds.Count - 1].adjustment = new Vector3();
        //pointClouds[pointClouds.Count - 1].adjustment.x = adjustmentResult[0];
        //pointClouds[pointClouds.Count - 1].adjustment.y = adjustmentResult[1];
        //pointClouds[pointClouds.Count - 1].adjustment.z = adjustmentResult[2];

        //GameObject geoReference = GameObject.Find("UnityZeroGeoReference");
        //if (geoReference == null)
        //{
        //    geoReference = new GameObject("UnityZeroGeoReference");
        //    geoReference.transform.position = new Vector3(adjustmentResult[3],
        //                                                  pointClouds[pointClouds.Count - 1].UTMZone,
        //                                                  adjustmentResult[4]);
        //}
        //else
        //{
        //    pointClouds[pointClouds.Count - 1].initialXShift = -(geoReference.transform.position.x - adjustmentResult[3]);
        //    pointClouds[pointClouds.Count - 1].initialZShift = -(geoReference.transform.position.z - adjustmentResult[4]);
        //}

        //// Default value for y, it should be calculated but for now it is magic number.
        //float y = 905.0f;

        //// If we are re initializing existing objects, we should preserve y coordinate.
        //if (reinitialization != null)
        //    y = pointClouds[pointClouds.Count - 1].inSceneRepresentation.transform.position.y;

        //pointClouds[pointClouds.Count - 1].inSceneRepresentation.transform.position = new Vector3((float)(pointClouds[pointClouds.Count - 1].initialXShift),
        //                                                                                          y,
        //                                                                                          (float)(pointClouds[pointClouds.Count - 1].initialZShift));
    }

    public static void SaveLAZFile(string filePath, int index)
    {
        IntPtr strPtr = Marshal.StringToHGlobalAnsi(filePath);
        SaveToLAZFileFromUnity(strPtr, index);
    }

    public static void SaveOwnFormatFile(string filePath, int index)
    {
        IntPtr strPtr = Marshal.StringToHGlobalAnsi(filePath);
        SaveToOwnFormatFileFromUnity(strPtr, index);
    }

    private void SendMeshBuffersToPlugin()
    {
        var mesh = pointCloud.sharedMesh;

        GCHandle gcVertices = GCHandle.Alloc(pointCloud.sharedMesh.vertices, GCHandleType.Pinned);
        GCHandle gcColors = GCHandle.Alloc(pointCloud.sharedMesh.colors, GCHandleType.Pinned);

        SetMeshBuffersFromUnity(mesh.GetNativeVertexBufferPtr(0), mesh.vertexCount, gcVertices.AddrOfPinnedObject(), gcColors.AddrOfPinnedObject());

        gcVertices.Free();
        gcColors.Free();
    }

    public void reInitialize()
    {
        Camera.onPostRender += OnPostRenderCallback;

        IntPtr projectPathPtr = Marshal.StringToHGlobalAnsi(Application.dataPath);

        // DLL reinitialization
        OnSceneStartFromUnity(projectPathPtr);

        pointClouds = new List<pointCloud>();
        LODSettings = new List<LODInformation>();
        IntPtr maxDistance = Marshal.AllocHGlobal(8);
        IntPtr targetPercentOFPoints = Marshal.AllocHGlobal(8);

        for (int i = 0; i < 4; i++)
        {
            LODSettings.Add(new LODInformation());
            RequestLODInfoFromUnity(maxDistance, targetPercentOFPoints, i, 0);
            float[] distance = new float[1];
            Marshal.Copy(maxDistance, distance, 0, 1);
            LODSettings[i].maxDistance = distance[0];

            float[] percentOFPoints = new float[1];
            Marshal.Copy(targetPercentOFPoints, percentOFPoints, 0, 1);
            LODSettings[i].targetPercentOFPoints = percentOFPoints[0];
        }

        Marshal.FreeHGlobal(maxDistance);
        Marshal.FreeHGlobal(targetPercentOFPoints);

        // Looking for existing point clouds.
        pointCloudRawDataContainer[] pointCloudGameObjects = (pointCloudRawDataContainer[])GameObject.FindObjectsOfType(typeof(pointCloudRawDataContainer));
        if (pointCloudGameObjects.Length > 0)
        {
            reInitializationObjectsAsync = new List<pointCloudRawDataContainer>();
            for (int i = 0; i < pointCloudGameObjects.Length; i++)
            {
                reInitializationObjectsAsync.Add(pointCloudGameObjects[i]);
                //loadLAZFile(pointCloudGameObjects[i].pathToRawData, pointCloudGameObjects[i].gameObject);
            }

            loadLAZFile(reInitializationObjectsAsync[reInitializationObjectsAsync.Count - 1].pathToRawData, reInitializationObjectsAsync[reInitializationObjectsAsync.Count - 1].gameObject);
        }
    }

    void checkIsAsyncLoadFinished()
    {
        if (!isWaitingToLoad)
            return;

        if (IsLastAsyncLoadFinished())
        {
            pointClouds.Add(new pointCloud(filePathForAsyncLoad, reInitializationForAsyncLoad));

            IntPtr adjustmentArray = Marshal.AllocHGlobal(8 * 5);
            RequestPointCloudAdjustmentFromUnity(adjustmentArray, pointClouds.Count - 1);

            IntPtr UTMZone = Marshal.AllocHGlobal(8);
            IntPtr North = Marshal.AllocHGlobal(8);
            RequestPointCloudUTMZoneFromUnity(UTMZone, North, pointClouds.Count - 1);
            int[] zone = new int[1];
            Marshal.Copy(UTMZone, zone, 0, 1);

            int[] north = new int[1];
            Marshal.Copy(North, north, 0, 1);

            pointClouds[pointClouds.Count - 1].UTMZone = zone[0];
            pointClouds[pointClouds.Count - 1].North = north[0] == 1;

            float[] adjustmentResult = new float[5];
            Marshal.Copy(adjustmentArray, adjustmentResult, 0, 5);
            pointClouds[pointClouds.Count - 1].adjustment = new Vector3();
            pointClouds[pointClouds.Count - 1].adjustment.x = adjustmentResult[0];
            pointClouds[pointClouds.Count - 1].adjustment.y = adjustmentResult[1];
            pointClouds[pointClouds.Count - 1].adjustment.z = adjustmentResult[2];

            //pointClouds[pointClouds.Count - 1].LODs = new List<LODInformation>();
            //IntPtr maxDistance = Marshal.AllocHGlobal(8);
            //IntPtr targetPercentOFPoints = Marshal.AllocHGlobal(8);

            //for (int i = 0; i < 4; i++)
            //{
            //    pointClouds[pointClouds.Count - 1].LODs.Add(new LODInformation());
            //    RequestLODInfoFromUnity(maxDistance, targetPercentOFPoints, i, pointClouds.Count - 1);
            //    float[] distance = new float[1];
            //    Marshal.Copy(maxDistance, distance, 0, 1);
            //    pointClouds[pointClouds.Count - 1].LODs[i].maxDistance = distance[0];

            //    float[] percentOFPoints = new float[1];
            //    Marshal.Copy(targetPercentOFPoints, percentOFPoints, 0, 1);
            //    pointClouds[pointClouds.Count - 1].LODs[i].targetPercentOFPoints = percentOFPoints[0];
            //}

            //Marshal.FreeHGlobal(maxDistance);
            //Marshal.FreeHGlobal(targetPercentOFPoints);

            GameObject geoReference = GameObject.Find("UnityZeroGeoReference");
            if (geoReference == null)
            {
                geoReference = new GameObject("UnityZeroGeoReference");
                geoReference.transform.position = new Vector3(adjustmentResult[3],
                                                              pointClouds[pointClouds.Count - 1].UTMZone,
                                                              adjustmentResult[4]);
            }
            else
            {
                pointClouds[pointClouds.Count - 1].initialXShift = -(geoReference.transform.position.x - adjustmentResult[3]);
                pointClouds[pointClouds.Count - 1].initialZShift = -(geoReference.transform.position.z - adjustmentResult[4]);
            }

            // Default value for y, it should be calculated but for now it is magic number.
            float y = 905.0f;

            // If we are re initializing existing objects, we should preserve y coordinate.
            if (reInitializationForAsyncLoad != null)
                y = pointClouds[pointClouds.Count - 1].inSceneRepresentation.transform.position.y;

            pointClouds[pointClouds.Count - 1].inSceneRepresentation.transform.position = new Vector3((float)(pointClouds[pointClouds.Count - 1].initialXShift),
                                                                                                      y,
                                                                                                      (float)(pointClouds[pointClouds.Count - 1].initialZShift));

            if (reInitializationObjectsAsync != null && reInitializationObjectsAsync.Count > 0)
            {
                loadLAZFile(reInitializationObjectsAsync[reInitializationObjectsAsync.Count - 1].pathToRawData, reInitializationObjectsAsync[reInitializationObjectsAsync.Count - 1].gameObject);
                reInitializationObjectsAsync.RemoveAt(reInitializationObjectsAsync.Count - 1);
            }
            else
            {
                isWaitingToLoad = false;
            }
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        InvokeRepeating("checkIsAsyncLoadFinished", 1.0f, 0.3f);
        //Camera.onPostRender += OnPostRenderCallback;

        //IntPtr projectPathPtr = Marshal.StringToHGlobalAnsi(Application.dataPath);
        //OnSceneStartFromUnity(projectPathPtr);

        //pointClouds = new List<pointCloud>();


        reInitialize();






        //double lastInterval = Time.realtimeSinceStartup;

        //double time = (Time.realtimeSinceStartup - lastInterval) * 1000;
        //Debug.Log("Octree build time: " + time + " ms.");

        //List<debugNodeBox> list = new List<debugNodeBox>();
        //int testSize = RequestOctreeBoundsCountFromUnity();
        //// 8 bytes for 64 bit dll
        //IntPtr testArrayToFill = Marshal.AllocHGlobal(testSize * 8 * 5);
        //RequestOctreeBoundsFromUnity(testArrayToFill);

        //float[] testArray = new float[testSize * 5];
        //Marshal.Copy(testArrayToFill, testArray, 0, testSize * 5);

        //for (int i = 0; i < testSize; i++)
        //{
        //    list.Add(new debugNodeBox(new Vector3(testArray[i * 5], testArray[i * 5 + 1], testArray[i * 5 + 2]), testArray[i * 5 + 3], (int)testArray[i * 5 + 4]));
        //}
        //Marshal.FreeHGlobal(testArrayToFill);



        //int boxCount = list.Count;
        //LineRenderer[] lines = new LineRenderer[boxCount * 12];

        //int debugMaxNodeDepth = RequestOctreeDebugMaxNodeDepthFromUnity();
        //octreeDepthViualizations = new List<List<LineRenderer>>();
        //for (int i = 0; i < debugMaxNodeDepth + 1; i++)
        //{
        //    octreeDepthViualizations.Add(new List<LineRenderer>());
        //}

        ////lastInterval = Time.realtimeSinceStartup;
        //for (int i = 0; i < boxCount; i++)
        //{
        //    float currentSize = list[i].size / 2.0f;
        //    for (int j = 0; j < 12; j++)
        //    {
        //        GameObject gObject = new GameObject("MyGameObject");
        //        lines[i * 12 + j] = gObject.AddComponent<LineRenderer>();
        //        lines[i * 12 + j].material = new Material(Shader.Find("Sprites/Default"));

        //        octreeDepthViualizations[list[i].depth].Add(lines[i * 12 + j]);

        //        if (list[i].depth == 1)
        //        {
        //            lines[i * 12 + j].startColor = Color.green;
        //            lines[i * 12 + j].endColor = Color.green;
        //        }
        //        else if (list[i].depth == 2)
        //        {
        //            lines[i * 12 + j].startColor = Color.blue;
        //            lines[i * 12 + j].endColor = Color.blue;
        //        }
        //        else if (list[i].depth == 3)
        //        {
        //            lines[i * 12 + j].startColor = Color.yellow;
        //            lines[i * 12 + j].endColor = Color.yellow;
        //        }
        //        else if (list[i].depth == 4)
        //        {
        //            lines[i * 12 + j].startColor = Color.cyan;
        //            lines[i * 12 + j].endColor = Color.cyan;
        //        }
        //        else if (list[i].depth == 5)
        //        {
        //            lines[i * 12 + j].startColor = Color.magenta;
        //            lines[i * 12 + j].endColor = Color.magenta;
        //        }
        //        else
        //        {
        //            lines[i * 12 + j].startColor = Color.red;
        //            lines[i * 12 + j].endColor = Color.red;
        //        }

        //        lines[i * 12 + j].widthMultiplier = 0.5f;
        //        lines[i * 12 + j].positionCount = 2;
        //    }

        //    list[i].center = pointClouds[pointClouds.Count - 1].inSceneRepresentation.transform.localToWorldMatrix * new Vector4(list[i].center.x, list[i].center.y, list[i].center.z, 1.0f);

        //    // bottom
        //    lines[i * 12 + 0].SetPosition(0, list[i].center + new Vector3(-currentSize, -currentSize, -currentSize));
        //    lines[i * 12 + 0].SetPosition(1, list[i].center + new Vector3(-currentSize, -currentSize, currentSize));
        //    lines[i * 12 + 1].SetPosition(0, list[i].center + new Vector3(-currentSize, -currentSize, currentSize));
        //    lines[i * 12 + 1].SetPosition(1, list[i].center + new Vector3(currentSize, -currentSize, currentSize));
        //    lines[i * 12 + 2].SetPosition(0, list[i].center + new Vector3(currentSize, -currentSize, currentSize));
        //    lines[i * 12 + 2].SetPosition(1, list[i].center + new Vector3(currentSize, -currentSize, -currentSize));
        //    lines[i * 12 + 3].SetPosition(0, list[i].center + new Vector3(currentSize, -currentSize, -currentSize));
        //    lines[i * 12 + 3].SetPosition(1, list[i].center + new Vector3(-currentSize, -currentSize, -currentSize));

        //    // vertical connections
        //    lines[i * 12 + 4].SetPosition(0, list[i].center + new Vector3(-currentSize, -currentSize, -currentSize));
        //    lines[i * 12 + 4].SetPosition(1, list[i].center + new Vector3(-currentSize, currentSize, -currentSize));
        //    lines[i * 12 + 5].SetPosition(0, list[i].center + new Vector3(-currentSize, -currentSize, currentSize));
        //    lines[i * 12 + 5].SetPosition(1, list[i].center + new Vector3(-currentSize, currentSize, currentSize));
        //    lines[i * 12 + 6].SetPosition(0, list[i].center + new Vector3(currentSize, -currentSize, currentSize));
        //    lines[i * 12 + 6].SetPosition(1, list[i].center + new Vector3(currentSize, currentSize, currentSize));
        //    lines[i * 12 + 7].SetPosition(0, list[i].center + new Vector3(currentSize, -currentSize, -currentSize));
        //    lines[i * 12 + 7].SetPosition(1, list[i].center + new Vector3(currentSize, currentSize, -currentSize));

        //    // top
        //    lines[i * 12 + 8].SetPosition(0, list[i].center + new Vector3(-currentSize, currentSize, -currentSize));
        //    lines[i * 12 + 8].SetPosition(1, list[i].center + new Vector3(-currentSize, currentSize, currentSize));
        //    lines[i * 12 + 9].SetPosition(0, list[i].center + new Vector3(-currentSize, currentSize, currentSize));
        //    lines[i * 12 + 9].SetPosition(1, list[i].center + new Vector3(currentSize, currentSize, currentSize));
        //    lines[i * 12 + 10].SetPosition(0, list[i].center + new Vector3(currentSize, currentSize, currentSize));
        //    lines[i * 12 + 10].SetPosition(1, list[i].center + new Vector3(currentSize, currentSize, -currentSize));
        //    lines[i * 12 + 11].SetPosition(0, list[i].center + new Vector3(currentSize, currentSize, -currentSize));
        //    lines[i * 12 + 11].SetPosition(1, list[i].center + new Vector3(-currentSize, currentSize, -currentSize));
        //}

        //time = (Time.realtimeSinceStartup - lastInterval) * 1000;
        //Debug.Log("line list time: " + time + " ms.");
    }

    int lastVizualizedDepth = -1;
    public int vizualizedDepth = -1;

    // Update is called once per frame
    void Update()
    {
        var kb = Keyboard.current;

        if (Camera.onPostRender != OnPostRenderCallback && pointClouds == null)
        {
            reInitialize();
        }
        else if (Camera.onPostRender != OnPostRenderCallback)
        {
            Camera.onPostRender = OnPostRenderCallback;
        }

        if (vizualizedDepth != lastVizualizedDepth)
        {
            lastVizualizedDepth = vizualizedDepth;
            for (int i = 0; i < octreeDepthViualizations.Count; i++)
            {
                for (int j = 0; j < octreeDepthViualizations[i].Count; j++)
                {
                    if (vizualizedDepth == i)
                    {
                        octreeDepthViualizations[i][j].enabled = true;
                    }
                    else
                    {
                        octreeDepthViualizations[i][j].enabled = false;
                    }
                }
            }
        }

        if (kb.leftArrowKey.isPressed)
        {
            Vector3 position = transform.position;
            position.z += 1.0f;
            transform.SetPositionAndRotation(position, transform.rotation);
        }

        if (kb.rightArrowKey.isPressed)
        {
            Vector3 position = transform.position;
            position.z -= 1.0f;
            transform.SetPositionAndRotation(position, transform.rotation);
        }

        if (kb.upArrowKey.isPressed)
        {
            Vector3 position = transform.position;
            position.x += 1.0f;
            transform.SetPositionAndRotation(position, transform.rotation);
        }

        if (kb.downArrowKey.isPressed)
        {
            Vector3 position = transform.position;
            position.x -= 1.0f;
            transform.SetPositionAndRotation(position, transform.rotation);
        }

        if (kb.eKey.wasReleasedThisFrame)
        {
            //Debug.Log("Input.GetKeyUp(KeyCode.E)");
            float[] center = new float[3];
            center[0] = transform.position.x;
            center[1] = transform.position.y;
            center[2] = transform.position.z;

            GCHandle toDelete = GCHandle.Alloc(center.ToArray(), GCHandleType.Pinned);
            RequestToDeleteFromUnity(toDelete.AddrOfPinnedObject(), transform.localScale.x / 2.0f);
        }
    }

    void OnPostRenderCallback(Camera cam)
    {
        if (cam == Camera.main)
        {
            Matrix4x4 cameraToWorld = cam.cameraToWorldMatrix;
            cameraToWorld = cameraToWorld.inverse;
            float[] cameraToWorldArray = new float[16];
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    cameraToWorldArray[i * 4 + j] = cameraToWorld[i, j];
                }
            }
            GCHandle pointerTocameraToWorld = GCHandle.Alloc(cameraToWorldArray, GCHandleType.Pinned);

            cam.enabled = true;

            //Matrix4x4 projection = cam.nonJitteredProjectionMatrix;
            Matrix4x4 projection = GL.GetGPUProjectionMatrix(cam.projectionMatrix, true);

            float[] projectionArray = new float[16];
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    projectionArray[i * 4 + j] = projection[i, j];
                }
            }
            GCHandle pointerProjection = GCHandle.Alloc(projectionArray, GCHandleType.Pinned);

            updateCamera(pointerTocameraToWorld.AddrOfPinnedObject(), pointerProjection.AddrOfPinnedObject());

            pointerTocameraToWorld.Free();
            pointerProjection.Free();

            Matrix4x4 world;
            float[] worldArray = new float[16];
            GCHandle pointerWorld;

            for (int i = 0; i < pointCloudManager.pointClouds.Count; i++)
            {
                world = pointCloudManager.pointClouds[i].inSceneRepresentation.transform.localToWorldMatrix;
                for (int j = 0; j < 4; j++)
                {
                    for (int k = 0; k < 4; k++)
                    {
                        worldArray[j * 4 + k] = world[j, k];
                    }
                }

                pointerWorld = GCHandle.Alloc(worldArray, GCHandleType.Pinned);
                updateWorldMatrix(pointerWorld.AddrOfPinnedObject(), i);
                pointerWorld.Free();
            }

            GL.IssuePluginEvent(GetRenderEventFunc(), 1);
        }
    }

    private void OnDestroy()
    {
        Camera.onPostRender -= OnPostRenderCallback;
    }

    public static void SetFrustumCulling(bool active)
    {
        setFrustumCulling(active);
    }

    public static void SetLODSystemActive(bool active)
    {
        setLODSystemActive(active);
    }

    public static void SetLODTransitionDistance(float LODTransitionDistance)
    {
        // float[] worldArray = new float[16];
        localLODTransitionDistance = LODTransitionDistance;

        GCHandle valuePointer = GCHandle.Alloc(LODTransitionDistance, GCHandleType.Pinned);
        setLODTransitionDistance(valuePointer.AddrOfPinnedObject());
        valuePointer.Free();
    }

    public static void SetLODInfo(float maxDistance, float targetPercentOFPoints, int LODIndex, int pointCloudIndex)
    {
        float[] valuesArray = new float[2];
        valuesArray[0] = maxDistance;
        valuesArray[1] = targetPercentOFPoints;

        GCHandle valuePointer = GCHandle.Alloc(valuesArray, GCHandleType.Pinned);
        setLODInfo(valuePointer.AddrOfPinnedObject(), LODIndex, pointCloudIndex);
        valuePointer.Free();
    }
}