using UnityEngine;
using System.Collections;
using UnityEngine.Networking;
using UnityEngine.InputSystem;

public class ENCManager : MonoBehaviour
{
    public GameObject controller;
    public GameObject HMD;
    public GameObject pointCloudRoot;

    public Shader ENCShader;

    public Texture2D errorTexture;

    public InputActionProperty adjustENCAction;

    public int resolution = 4096;

    public bool create;

    public GEOReference geoReference;
    public pointCloud pointCloud;

    private GameObject ENC;
    private bool adjusting;

    void Start()
    {
        adjustENCAction.action.started += ctx => BeginAdjustENCHeight();
        adjustENCAction.action.canceled += ctx => EndAdjustENCHeight();

        create = false;
        adjusting = false;
    }

    void Update()
    {
        if (geoReference == null)
            geoReference = FindObjectOfType<GEOReference>();

        if (create)
        {
            if (geoReference != null && pointCloud != null)
                StartCoroutine(CreateENC(geoReference, pointCloud));

            create = false;
        }
        
        var gaze = HMD.transform.forward;
        var ctrlrDown = -controller.transform.up;

        if (ENC != null)
        {
            if (Vector3.Dot(gaze, ctrlrDown) < 0f)
            {
                ENC.SetActive(true);
            }
            else
            {
                adjusting = false;
                ENC.SetActive(false);
            }

            if (adjusting)
            {
                var newheight = pointCloudRoot.transform.InverseTransformPoint(controller.transform.position).y;
                ENC.transform.localPosition = new Vector3(ENC.transform.localPosition.x, newheight, ENC.transform.localPosition.z);
            }
        }

    }

    void OnEnable()
    {
        adjustENCAction.action.Enable();
    }

    void OnDisable()
    {
        adjustENCAction.action.Disable();
    }

    public IEnumerator CreateENC(GEOReference geoRef, pointCloud pc)
    {
        DestroyENC();

        var encMat = new Material(ENCShader);

        Debug.Log("Processing ENC image... ");
        float tick = Time.realtimeSinceStartup;
        yield return GetTexture(geoRef, pc, encMat);
        Debug.Log("ENC image processed in " + (Time.realtimeSinceStartup - tick) + " seconds");

        ENC = GameObject.CreatePrimitive(PrimitiveType.Quad);
        ENC.name = "ENC_" + pc.name;

        ENC.transform.parent = pc.transform;
        ENC.transform.localRotation = Quaternion.Euler(90f, 0, 0);
        ENC.transform.localScale = new Vector3(pc.bounds.extents.x * 2f, pc.bounds.extents.z * 2f, 1f);
        ENC.transform.localPosition = new Vector3(pc.bounds.center.x, pc.groundLevel, pc.bounds.center.z);

        Renderer rend = ENC.GetComponent<Renderer>();
        rend.material = encMat;
    }

    private void DestroyENC()
    {
        if (ENC != null)
            Destroy(ENC);
    }

    public IEnumerator GetTexture(GEOReference geoRef, pointCloud pc, Material encMat)
    {
        double minBBx = geoRef.realWorldX + pc.bounds.min.x;
        double maxBBx = geoRef.realWorldX + pc.bounds.max.x;
        double minBBz = geoRef.realWorldZ + pc.bounds.min.z;
        double maxBBz = geoRef.realWorldZ + pc.bounds.max.z;

        int epsg = pc.EPSG;

        if (epsg == 6344 || epsg == 0 || epsg == 32767)
            epsg = 26915;

        string url = $"https://gis.charttools.noaa.gov/arcgis/rest/services/MCS/ENCOnline/MapServer/exts/MaritimeChartService/WMSServer?LAYERS=0,1,2,3,4,5,6,7&FORMAT=image%2Fpng&CRS=EPSG:{epsg}&SERVICE=WMS&REQUEST=GetMap&WIDTH={resolution}&HEIGHT={resolution}&BBOX={minBBx},{minBBz},{maxBBx},{maxBBz}";

        Debug.Log(url);
        UnityWebRequest www = UnityWebRequestTexture.GetTexture(url);

        Debug.Log("Downloading ENC image... ");
        float tick = Time.realtimeSinceStartup;
        www.SendWebRequest();

        while (!www.isDone)
        {
            Debug.Log(www.downloadProgress);
            yield return null;
        }

        Debug.Log("ENC image downloaded in " + (Time.realtimeSinceStartup - tick) + " seconds");

        bool success = www.result == UnityWebRequest.Result.Success;

        if (success)
        {
            Debug.Log("Converting image to Texture2D... ");
            tick = Time.realtimeSinceStartup;
            Texture2D encTex = ((DownloadHandlerTexture)www.downloadHandler).texture;
            encMat.mainTexture = encTex;
            Debug.Log("Image converted to Texture2D in " + (Time.realtimeSinceStartup - tick) + " seconds");

            Debug.Log("Saving texture to disk... ");
            tick = Time.realtimeSinceStartup;
            yield return SaveTexture(encTex, pc.name);
            Debug.Log("Texture saved in " + (Time.realtimeSinceStartup - tick) + " seconds");
        }
        else
        {
            Debug.Log(www.error);

            encMat.mainTexture = errorTexture;
        }
    }

    IEnumerator SaveTexture(Texture2D texture, string name)
    {
        byte[] bytes = texture.EncodeToPNG();
        var dirPath = Application.dataPath + "/ENCs";
        if (!System.IO.Directory.Exists(dirPath))
        {
            System.IO.Directory.CreateDirectory(dirPath);
        }
        //System.IO.File.WriteAllBytes(dirPath + "/ENC_" + name + ".png", bytes);
        yield return System.IO.File.WriteAllBytesAsync(dirPath + "/ENC_" + name + ".png", bytes);
        Debug.Log(bytes.Length / 1024 + "Kb was saved as: " + dirPath);
#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
#endif
    }

    private void BeginAdjustENCHeight()
    {
        if (ENC == null || !ENC.activeInHierarchy)
            return;

        adjusting = true;
    }

    private void EndAdjustENCHeight()
    {
        adjusting = false;
    }
}