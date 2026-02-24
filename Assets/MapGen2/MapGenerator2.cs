using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

//TODO adapt editor from cosmology project to make the inspector more usable
public class MapGenerator2 : MonoBehaviour
{
    [System.Serializable]
    public class noiseClass
    {
        [Tooltip("Random seed for the noise.")]
        public int seed;
        [Tooltip("Scale of the noise.")]
        public float scale;
        [Tooltip("Number of layers of noise used.")]
        [Range(1, 20)]
        public int octaves;
        [Tooltip("Persistence.")]
        [Range(0, 1)]
        public float persistence;
        [Tooltip("Lacunarity.")]
        public float lacunarity;
    }
    
    [Range(-1, 1)] public float bias;

    public bool autoUpdate;
    public bool tickWithUpdate;
    public bool meshRender;
    public int stepsPerRender = 10;
    int iterationNumber = 0;

    public ComputeShader noiseComputeShader;
    public ComputeShader blurComputeShader;
    public ComputeShader windComputeShader;
    public ComputeShader shadowComputeShader;
    public ComputeShader slipComputeShader;
    public ComputeShader smoothComputeShader;
    
    public GameObject terrainPrefab;
    public RawImage mapDisplay;

    public enum display {sand, height, normal, gradient, gradientMagnitude, crests, wind, shadow, saltationDistance, saltationAmount, flux, blurAreas};
    public display mapDisplayType; 
    public display meshDisplayType; 

    public GameObject prefab;

    public int seed;

    public int xResolution;
    public int yResolution;
    int xMax;
    int yMax;




    

    public float depositAmount;


    float[,] heightMap;
    float[,] bedrockMap;
    float[,] sandMap;

    Vector3[,] normalMap;
    Vector2[,] gradientMap;
    Vector2[,] windMap;
    float[,] shadowMap;
    float[,] crestMap;

    public float gridSize;

    public float min;
    public float max;

    public int iterations;
    [Range(0, 1)]
    public float b;
    [Range(0, 1)]
    public float sc;


    //Debug stuff
    [HideInInspector] public Vector2Int debugPoint;
    [HideInInspector] public float debugHeight;
    [HideInInspector] public float debugBedrock;
    [HideInInspector] public float debugSand;
    [HideInInspector] public Vector3 debugNormal;
    [HideInInspector] public Vector2 debugGradient;
    [HideInInspector] public float debugAngle;
    [HideInInspector] public float debugCrest;
    [HideInInspector] public float debugHighestDiff;
    [HideInInspector] public float debugSlopeThreshold;
    [HideInInspector] public Vector2 debugSaltationVector;


    //initial settings
    public enum initialSettings {Constant, Random, Noise};
    //bedrock
    [HideInInspector] public initialSettings bedrockInitial;
    [HideInInspector] public float bedrockInitialHeight;
    [HideInInspector] public float bedrockMinHeight;
    [HideInInspector] public float bedrockMaxHeight;
    //bedrock noise
    [HideInInspector] public int bedrockSeed;
    [HideInInspector] public float bedrockScale;
    [HideInInspector] public int bedrockOctaves;
    [HideInInspector] public float bedrockPersistence;
    [HideInInspector] public float bedrockLacunarity;

    //sand
    [HideInInspector] public initialSettings sandInitial;
    [HideInInspector] public float sandInitialHeight;
    [HideInInspector] public float sandMinHeight;
    [HideInInspector] public float sandMaxHeight;
    //sand noise
    [HideInInspector] public int sandSeed;
    [HideInInspector] public float sandScale;
    [HideInInspector] public int sandOctaves;
    [HideInInspector] public float sandPersistence;
    [HideInInspector] public float sandLacunarity;

    //blur
    public enum blurSettings {Blur, None};
    [HideInInspector] public blurSettings blurType;
    [HideInInspector] public float blurStrength;

    //slip
    public enum slipOrders {Sequence, SequenceCycle, Random, AtOnce, GPU};
    //bedrock
    [HideInInspector] public slipOrders slipOrder;
    [HideInInspector] public int slipIterations;
    [HideInInspector] public float crestRoughness;
    [HideInInspector] public float angleOfRepose;
    [HideInInspector] public float spreadAmount;
    float slopeThreshold;
    float slipGradientMagnitude;

    //wind
    [HideInInspector] public Vector2 universalWindVector;
    [HideInInspector] public float deviationCoefficient = 5;
    [HideInInspector] public bool useWindFieldForShadows = false;
    [HideInInspector] float windAccelerationFactor = 0.005f;

    public bool drawWind;





    
    //-----------------------------------------------------------------
    void Start()
    {
        Initialize();
        RenderInEditor();
    }

    void Update()
    {
        if (tickWithUpdate)
        {
            Tick();
        }
    }
    //-----------------------------------------------------------------



    //three main functions---------------------------------------------
    public void Initialize()
    {
        iterationNumber = 0;

        Random.InitState(seed);
        heightMap = new float[xResolution, yResolution];
        bedrockMap = new float[xResolution, yResolution];
        sandMap = new float[xResolution, yResolution];

        normalMap = new Vector3[xResolution, yResolution];
        gradientMap = new Vector2[xResolution, yResolution];
        windMap = new Vector2[xResolution, yResolution];
        shadowMap = new float[xResolution, yResolution];
        crestMap = new float[xResolution, yResolution];

        xMax = xResolution - 1;
        yMax = yResolution - 1;

        slopeThreshold = Mathf.Tan(angleOfRepose * Mathf.PI / 180f) * gridSize;
        slipGradientMagnitude = Mathf.Sin(angleOfRepose * Mathf.PI/180f);

        if (bedrockInitial == initialSettings.Noise) bedrockMap = NoiseMapGPU(bedrockSeed, bedrockOctaves, bedrockScale * gridSize, bedrockPersistence, bedrockLacunarity);
        if (sandInitial == initialSettings.Noise) sandMap = NoiseMapGPU(sandSeed, sandOctaves, sandScale * gridSize, sandPersistence, sandLacunarity);

        for (int x = 0; x < xResolution; x++)
        {
            for (int y = 0; y < yResolution; y++)
            {
                if (bedrockInitial == initialSettings.Constant) bedrockMap[x, y] = bedrockInitialHeight;
                else if (bedrockInitial == initialSettings.Random) bedrockMap[x, y] = Random.Range(bedrockMinHeight, bedrockMaxHeight);
                if (sandInitial == initialSettings.Constant) sandMap[x, y] = sandInitialHeight;
                else if (sandInitial == initialSettings.Random) sandMap[x, y] = Random.Range(sandMinHeight, sandMaxHeight);
                
                heightMap[x, y] = bedrockMap[x, y] + sandMap[x, y];
                //heightMap[x, y] = 10 * texMap.GetPixel(x, y).r;
            }
        }

        //SlipCPU(5);
    }

    public void RenderInEditor()
    {
        // terrain.terrainData.terrainLayers = new TerrainLayer[] {terrainLayer};

        //ShowWind();

        //GetComponent<MeshFilter>().sharedMesh = GenerateMesh(heightMap);
        //GetComponent<MeshRenderer>().sharedMaterial.mainTexture = TextureFromHeightMap(displayType, min, max);
        //GetComponent<MeshRenderer>().sharedMaterial.SetTexture("_BumpMap", TextureFromHeightMap(display.normal));

        GenerateMeshes(heightMap);
    }

    public void Tick()
    {
        float t1 = Time.realtimeSinceStartup;

        //windMap = WindGPU(heightMap, deviationCoefficient);
        float t2 = Time.realtimeSinceStartup;

        ShadowGPU();
        float t3 = Time.realtimeSinceStartup;

        //DotCPU();
        if (iterations > 0 && depositAmount > 0) SaltationCPU(iterations);
        float t4 = Time.realtimeSinceStartup;
        
        
        if (slipOrder == slipOrders.GPU)
        {
            for (int i = 0; i < slipIterations; i++) 
            {
                SlipGPU(); 
                SmoothCrestsGPU();
            }
        } 
        else SlipCPU(slipIterations);        
        float t5 = Time.realtimeSinceStartup;

        if (blurType != blurSettings.None) BlurCPU();
  
        float t6 = Time.realtimeSinceStartup;

        //RecalculateHeights();
        float t7 = Time.realtimeSinceStartup;

        if (meshRender)
        {
            if (iterationNumber % stepsPerRender == 0)
            {
                RenderInEditor();
            } 
        }
        UpdateMap();
        iterationNumber++;
        float t8 = Time.realtimeSinceStartup;

        

        float totalTime = t8 - t1;
        Debug.Log("Iteration #"+iterationNumber+
                "\nTotal:\t\t"  +(Time.realtimeSinceStartup-t1)+
                "\nWind:\t\t"   +(t2-t1).ToString("F3")+"\t" + Mathf.Round((t2-t1)/totalTime * 100f) + "%"+
                "\nShadow:\t"   +(t3-t2).ToString("F3")+"\t" + Mathf.Round((t3-t2)/totalTime * 100f) + "%"+
                "\nSaltation:\t"+(t4-t3).ToString("F3")+"\t" + Mathf.Round((t4-t3)/totalTime * 100f) + "%"+
                "\nSlip:\t\t"   +(t5-t4).ToString("F3")+"\t" + Mathf.Round((t5-t4)/totalTime * 100f) + "%"+
                "\nBlur:\t\t"   +(t6-t5).ToString("F3")+"\t" + Mathf.Round((t6-t5)/totalTime * 100f) + "%"+
                "\nHeight:\t\t" +(t7-t6).ToString("F3")+"\t" + Mathf.Round((t7-t6)/totalTime * 100f) + "%"+
                "\nMap:\t\t"    +(t8-t7).ToString("F3")+"\t" + Mathf.Round((t8-t7)/totalTime * 100f) + "%"
        );
        
    }

    public void UpdateMap()
    {
        mapDisplay.texture = TextureFromHeightMap(mapDisplayType, min, max);
    }

    public void RecalculateHeights()
    {
        for (int x = 0; x < xResolution; x++)
        {
            for (int y = 0; y < yResolution; y++)
            {
               heightMap[x, y] = bedrockMap[x, y] + sandMap[x, y];
            }
        }
    }

    void OnDrawGizmos()
    {
        //check if heightMap has been set
        if (heightMap != null)
        {
            debugHeight = heightMap[debugPoint.x, debugPoint.y];
            debugBedrock = bedrockMap[debugPoint.x, debugPoint.y];
            debugSand = sandMap[debugPoint.x, debugPoint.y];

            debugNormal = CalculateNormal(debugPoint.x, debugPoint.y);
            debugGradient = GetGradient(debugPoint.x, debugPoint.y);     

            debugAngle = GradientToAngle(debugGradient) * 180f / Mathf.PI;

            debugCrest = GetCrest(debugPoint.x, debugPoint.y);
            debugHighestDiff = debugCrest - GradientToHeight(debugGradient);
            debugSlopeThreshold = Mathf.Tan(angleOfRepose * Mathf.PI / 180f) * gridSize;

            debugSaltationVector = GetSaltationVector(debugPoint.x, debugPoint.y);
        
            Vector3 pos = new Vector3(debugPoint.x * gridSize, debugHeight, debugPoint.y * gridSize);

            Vector3 dir = debugNormal;
            Gizmos.color = Color.red;
            //Gizmos.DrawRay(pos, dir);
            DrawArrow(pos, dir);
            dir = new Vector3(debugGradient.x, 0, debugGradient.y);
            Gizmos.color = Color.green;
            DrawArrow(pos, dir);


            if (drawWind)
            {
                for (int x = 0; x < xResolution; x++)
                {
                    for (int y = 0; y < yResolution; y++)
                    {
                        int a = (xResolution / 64);
                        if (x%a == 0 && y%a == 0)
                        {
                            DrawArrow(new Vector3(x, 50, y) * gridSize, new Vector3(windMap[x, y].x, 0, windMap[x, y].y) * 0.3f, 1f);
                            // GameObject f = Instantiate(prefab, new Vector3(x, 50, y) * gridSize, Quaternion.LookRotation(new Vector3(windMap[x, y].x, 0, windMap[x, y].y), Vector3.up), transform);
                            // f.transform.localScale = new Vector3(0.5f, 0.5f, 0.25f * windMap[x, y].magnitude);
                        }
                    }
                }
            }
        }
    }

    public static void DrawArrow(Vector3 pos, Vector3 direction, float arrowHeadLength = 0.25f, float arrowHeadAngle = 20.0f)
    {
        //https://forum.unity.com/threads/debug-drawarrow.85980/
        if (direction.magnitude != 0)
        {
            Gizmos.DrawRay(pos, direction);
        
            Vector3 right = Quaternion.LookRotation(direction) * Quaternion.Euler(0,180+arrowHeadAngle,0) * new Vector3(0,0,1);
            Vector3 left = Quaternion.LookRotation(direction) * Quaternion.Euler(0,180-arrowHeadAngle,0) * new Vector3(0,0,1);
            Gizmos.DrawRay(pos + direction, right * arrowHeadLength);
            Gizmos.DrawRay(pos + direction, left * arrowHeadLength);
        }
    }

    public void ShowWind()
    {
        //destroy all child objects
        //while(transform.childCount != 0) DestroyImmediate(transform.GetChild(0).gameObject);

        // for (int x = 0; x < xResolution; x++)
        // {
        //     for (int y = 0; y < yResolution; y++)
        //     {
        //         if (xResolution > 32)
        //         {
        //             int a = (xResolution / 32);
        //             if (x%a == 0 && y%a == 0)
        //             {
        //                 DrawArrow(new Vector3(x, 50, y) * gridSize, new Vector3(windMap[x, y].x, 0, windMap[x, y].y));
        //                 // GameObject f = Instantiate(prefab, new Vector3(x, 50, y) * gridSize, Quaternion.LookRotation(new Vector3(windMap[x, y].x, 0, windMap[x, y].y), Vector3.up), transform);
        //                 // f.transform.localScale = new Vector3(0.5f, 0.5f, 0.25f * windMap[x, y].magnitude);
        //             }
        //         }
        //         else 
        //         {
        //             GameObject f = Instantiate(prefab, new Vector3(x, 50, y) * gridSize, Quaternion.LookRotation(new Vector3(windMap[x, y].x, 0, windMap[x, y].y), Vector3.up), transform);
        //             f.transform.localScale = new Vector3(0.5f, 0.5f, 0.25f * windMap[x, y].magnitude);
        //         }
        //     }
        // }
    }
    //-----------------------------------------------------------------



    


    float[,] NoiseMapGPU(int seed, int octaves, float scale, float persistence, float lacunarity) 
    {
        float[,] noiseMap = new float[xResolution, yResolution];
        ComputeBuffer heightBuffer = new ComputeBuffer(xResolution * yResolution, sizeof(float));
        heightBuffer.SetData(noiseMap);
        noiseComputeShader.SetBuffer(0, "heightMap", heightBuffer);

        noiseComputeShader.SetInt("xResolution", xResolution);
        noiseComputeShader.SetInt("yResolution", yResolution);

        noiseComputeShader.SetInt("seed", seed);
        noiseComputeShader.SetInt("octaves", octaves);
        noiseComputeShader.SetFloat("scale", scale);
        noiseComputeShader.SetFloat("persistence", persistence);
        noiseComputeShader.SetFloat("lacunarity", lacunarity);

        noiseComputeShader.Dispatch (0, xResolution / 1, yResolution / 1, 1);

        heightBuffer.GetData(noiseMap);
        heightBuffer.Release();

        return noiseMap;
    }

    public void BlurGPU() 
    {
        ComputeBuffer bedrockBufferIn = new ComputeBuffer(xResolution * yResolution, sizeof(float));
        bedrockBufferIn.SetData(bedrockMap);
        blurComputeShader.SetBuffer(0, "bedrockMapIn", bedrockBufferIn);

        ComputeBuffer sandBufferIn = new ComputeBuffer(xResolution * yResolution, sizeof(float));
        sandBufferIn.SetData(sandMap);
        blurComputeShader.SetBuffer(0, "sandMapIn", sandBufferIn);

        ComputeBuffer crestBufferIn = new ComputeBuffer(xResolution * yResolution, sizeof(float));
        crestBufferIn.SetData(crestMap);
        blurComputeShader.SetBuffer(0, "crestMapIn", crestBufferIn);

        float[,] sandMapOut = new float[xResolution, yResolution];
        ComputeBuffer sandBufferOut = new ComputeBuffer(xResolution * yResolution, sizeof(float));
        sandBufferOut.SetData(sandMapOut);
        blurComputeShader.SetBuffer(0, "heightMapOut", sandBufferOut);

        blurComputeShader.SetInt("xResolution", xResolution);
        blurComputeShader.SetInt("yResolution", yResolution);
        blurComputeShader.SetInt("xMax", xResolution - 1);
        blurComputeShader.SetInt("yMax", yResolution - 1);

        blurComputeShader.SetFloat("blurStrength", Mathf.Lerp(1, 1f/9f, blurStrength));
        blurComputeShader.SetFloat("blurThreshold", 0f);

        blurComputeShader.Dispatch(0, xResolution / 16, yResolution / 16, 1);

        bedrockBufferIn.Release();
        sandBufferIn.Release();
        crestBufferIn.Release();
        sandBufferOut.GetData(sandMap);
        sandBufferOut.Release();
    }

    Vector2[,] WindGPU(float[,] heightMap, float deviationCoefficient) 
    {
        ComputeBuffer heightBufferIn = new ComputeBuffer(xResolution * yResolution, sizeof(float));
        heightBufferIn.SetData(heightMap);
        windComputeShader.SetBuffer(0, "heightMapIn", heightBufferIn);

        Vector3[,] normalMapOut = new Vector3[xResolution, yResolution];
        ComputeBuffer normalBufferOut = new ComputeBuffer(xResolution * yResolution, 3 * sizeof(float));
        normalBufferOut.SetData(normalMapOut);
        windComputeShader.SetBuffer(0, "normalMapOut", normalBufferOut);

        Vector2[,] gradientMapOut = new Vector2[xResolution, yResolution];
        ComputeBuffer gradientBufferOut = new ComputeBuffer(xResolution * yResolution, 2 * sizeof(float));
        gradientBufferOut.SetData(gradientMapOut);
        windComputeShader.SetBuffer(0, "gradientMapOut", gradientBufferOut);

        Vector2[,] windMapOut = new Vector2[xResolution, yResolution];
        ComputeBuffer windBufferOut = new ComputeBuffer(xResolution * yResolution, 2 * sizeof(float));
        windBufferOut.SetData(windMapOut);
        windComputeShader.SetBuffer(0, "windMapOut", windBufferOut);

        float[,] crestMapOut = new float[xResolution, yResolution];
        ComputeBuffer crestBufferOut = new ComputeBuffer(xResolution * yResolution, sizeof(float));
        crestBufferOut.SetData(crestMapOut);
        windComputeShader.SetBuffer(0, "crestMapOut", crestBufferOut);

        windComputeShader.SetInt("xResolution", xResolution);
        windComputeShader.SetInt("yResolution", yResolution);
        windComputeShader.SetInt("xMax", xResolution - 1);
        windComputeShader.SetInt("yMax", yResolution - 1);

        windComputeShader.SetFloat("gridSize", gridSize);

        windComputeShader.SetFloat("windAccelerationFactor", windAccelerationFactor);
        windComputeShader.SetFloat("averageHeight", 0);

        windComputeShader.SetVector("universalWindVector", universalWindVector);
        windComputeShader.SetFloat("deviationCoefficient", deviationCoefficient);

        windComputeShader.Dispatch(0, xResolution / 1, yResolution / 1, 1);

        heightBufferIn.Release();
        normalBufferOut.GetData(normalMap);
        normalBufferOut.Release();
        gradientBufferOut.GetData(gradientMap);
        gradientBufferOut.Release();
        windBufferOut.GetData(windMapOut);
        windBufferOut.Release();
        crestBufferOut.GetData(crestMap);
        crestBufferOut.Release();

        return windMapOut;
    }

    void ShadowGPU() 
    {
        ComputeBuffer heightBufferIn = new ComputeBuffer(xResolution * yResolution, sizeof(float));
        heightBufferIn.SetData(heightMap);
        shadowComputeShader.SetBuffer(0, "heightMapIn", heightBufferIn);

        // ComputeBuffer bedrockBufferIn = new ComputeBuffer(xResolution * yResolution, sizeof(float));
        // bedrockBufferIn.SetData(bedrockMap);
        // shadowComputeShader.SetBuffer(0, "bedrockMapIn", bedrockBufferIn);

        // ComputeBuffer sandBufferIn = new ComputeBuffer(xResolution * yResolution, sizeof(float));
        // sandBufferIn.SetData(bedrockMap);
        // shadowComputeShader.SetBuffer(0, "sandMapIn", sandBufferIn);

        ComputeBuffer gradientBufferIn = new ComputeBuffer(xResolution * yResolution, 2 * sizeof(float));
        gradientBufferIn.SetData(gradientMap);
        shadowComputeShader.SetBuffer(0, "gradientMapIn", gradientBufferIn);

        ComputeBuffer windBufferIn = new ComputeBuffer(xResolution * yResolution, 2 * sizeof(float));
        windBufferIn.SetData(windMap);
        shadowComputeShader.SetBuffer(0, "windMapIn", windBufferIn);

        float[,] shadowMapOut = new float[xResolution, yResolution];
        ComputeBuffer shadowBufferOut = new ComputeBuffer(xResolution * yResolution, sizeof(float));
        shadowBufferOut.SetData(shadowMapOut);
        shadowComputeShader.SetBuffer(0, "shadowMapOut", shadowBufferOut);

        shadowComputeShader.SetInt("xResolution", xResolution);
        shadowComputeShader.SetInt("yResolution", yResolution);
        shadowComputeShader.SetInt("xMax", xResolution - 1);
        shadowComputeShader.SetInt("yMax", yResolution - 1);

        shadowComputeShader.SetFloat("gridSize", gridSize);

        shadowComputeShader.SetVector("universalWindVector", universalWindVector);

        shadowComputeShader.SetBool("useWindField", useWindFieldForShadows);

        shadowComputeShader.Dispatch(0, xResolution / 1, yResolution / 1, 1);

        heightBufferIn.Release();
        //bedrockBufferIn.Release();
        //sandBufferIn.Release();
        gradientBufferIn.Release();
        windBufferIn.Release();
        shadowBufferOut.GetData(shadowMap);
        shadowBufferOut.Release();
    }

    void SlipGPU() 
    {
        //IN
        ComputeBuffer heightBufferIn = new ComputeBuffer(xResolution * yResolution, sizeof(float));
        heightBufferIn.SetData(heightMap);
        slipComputeShader.SetBuffer(0, "heightMapIn", heightBufferIn);

        // ComputeBuffer bedrockBufferIn = new ComputeBuffer(xResolution * yResolution, sizeof(float));
        // bedrockBufferIn.SetData(bedrockMap);
        // slipComputeShader.SetBuffer(0, "bedrockMapIn", bedrockBufferIn);

        ComputeBuffer sandBufferIn = new ComputeBuffer(xResolution * yResolution, sizeof(float));
        sandBufferIn.SetData(sandMap);
        slipComputeShader.SetBuffer(0, "sandMapIn", sandBufferIn);

        //OUT
        ComputeBuffer heightBufferOut = new ComputeBuffer(xResolution * yResolution, sizeof(float));
        heightBufferOut.SetData(heightMap);
        slipComputeShader.SetBuffer(0, "heightMapOut", heightBufferOut);

        ComputeBuffer sandBufferOut = new ComputeBuffer(xResolution * yResolution, sizeof(float));
        sandBufferOut.SetData(sandMap);
        slipComputeShader.SetBuffer(0, "sandMapOut", sandBufferOut);

        uint[,] mutex = new uint[xResolution, yResolution];
        ComputeBuffer mutexBufferIn = new ComputeBuffer(xResolution * yResolution, sizeof(uint));
        mutexBufferIn.SetData(mutex);
        slipComputeShader.SetBuffer(0, "mutex", mutexBufferIn);


        slipComputeShader.SetInt("xResolution", xResolution);
        slipComputeShader.SetInt("yResolution", yResolution);
        slipComputeShader.SetInt("xMax", xResolution - 1);
        slipComputeShader.SetInt("yMax", yResolution - 1);

        slipComputeShader.SetFloat("gridSize", gridSize);
        slipComputeShader.SetFloat("slopeThreshold", Mathf.Tan(angleOfRepose * Mathf.PI / 180f) * gridSize);
        slipComputeShader.SetFloat("crestRoughness", crestRoughness);

        slipComputeShader.SetFloat("spreadAmount", spreadAmount / 2f);
        slipComputeShader.SetFloat("spreadAmount2", 1f - spreadAmount);

        

        slipComputeShader.Dispatch(0, xResolution / 1, yResolution / 1, 1);


        heightBufferIn.Release();
        //bedrockBufferIn.Release();
        sandBufferIn.Release();
        heightBufferIn.Release();

        heightBufferOut.GetData(heightMap);
        heightBufferOut.Release();
        sandBufferOut.GetData(sandMap);
        sandBufferOut.Release();

        mutexBufferIn.Release();


    }

    void SmoothCrestsGPU() 
    {
        //IN
        ComputeBuffer heightBufferIn = new ComputeBuffer(xResolution * yResolution, sizeof(float));
        heightBufferIn.SetData(heightMap);
        smoothComputeShader.SetBuffer(0, "heightMapIn", heightBufferIn);

        ComputeBuffer sandBufferIn = new ComputeBuffer(xResolution * yResolution, sizeof(float));
        sandBufferIn.SetData(sandMap);
        smoothComputeShader.SetBuffer(0, "sandMapIn", sandBufferIn);

        //OUT
        ComputeBuffer heightBufferOut = new ComputeBuffer(xResolution * yResolution, sizeof(float));
        heightBufferOut.SetData(heightMap);
        smoothComputeShader.SetBuffer(0, "heightMapOut", heightBufferOut);

        ComputeBuffer sandBufferOut = new ComputeBuffer(xResolution * yResolution, sizeof(float));
        sandBufferOut.SetData(sandMap);
        smoothComputeShader.SetBuffer(0, "sandMapOut", sandBufferOut);

        uint[,] mutex = new uint[xResolution, yResolution];
        ComputeBuffer mutexBufferIn = new ComputeBuffer(xResolution * yResolution, sizeof(uint));
        mutexBufferIn.SetData(mutex);
        smoothComputeShader.SetBuffer(0, "mutex", mutexBufferIn);


        smoothComputeShader.SetInt("xResolution", xResolution);
        smoothComputeShader.SetInt("yResolution", yResolution);
        smoothComputeShader.SetInt("xMax", xResolution - 1);
        smoothComputeShader.SetInt("yMax", yResolution - 1);

        smoothComputeShader.SetFloat("gridSize", gridSize);
        smoothComputeShader.SetFloat("slopeThreshold", Mathf.Tan(angleOfRepose * Mathf.PI / 180f) * gridSize);
        smoothComputeShader.SetFloat("crestRoughness", crestRoughness);
        

        smoothComputeShader.Dispatch(0, xResolution / 1, yResolution / 1, 1);


        heightBufferIn.Release();
        sandBufferIn.Release();
        heightBufferIn.Release();

        heightBufferOut.GetData(heightMap);
        heightBufferOut.Release();
        sandBufferOut.GetData(sandMap);
        sandBufferOut.Release();

        mutexBufferIn.Release();
    }

    































































    void AddSand(int x, int y, float amount)
    {
        sandMap[x, y] += amount;

        sandMap[x, y] = Mathf.Max(0, sandMap[x, y]);
        heightMap[x, y] = bedrockMap[x, y] + sandMap[x, y];
    }

    // void SetSand(int x, int y, float height)
    // {
    //     sandMap[x, y] = amount - bedrockMap[x, y];

    //     sandMap[x, y] = Mathf.Max(0, sandMap[x, y]);
    //     heightMap[x, y] = bedrockMap[x, y] + sandMap[x, y];
    // }




    float ProbabilityFromSand(float sand)
    {
        if (sand > 0) return 1f;
        else return 0.4f;
    }

    void Saltate(Vector2Int id)
    {
        Vector2Int origin = id;
        float originSandAmount = sandMap[id.x, id.y];

        float d = Mathf.Min(originSandAmount, GetSaltationAmount(origin.x, origin.y));
        AddSand(origin.x, origin.y, -d);

        Vector2 pos = origin;
        Vector2Int posIndex = origin;

        // Vector2 gradient = GetGradient(origin.x, origin.y);
        // Vector2 wind = windMap[origin.x, origin.y];
        // float dot = Vector2.Dot(gradient.normalized, wind.normalized);
        // //transforms the jump distance by the gradient
        // //high gradient = jump short distance
        // Vector2 jumpVector = wind;

        uint i = 0;
        while (true)
        {
            //pos += windMap[posIndex.x, posIndex.y];
            pos += GetSaltationVector(posIndex.x, posIndex.y);
            posIndex = new Vector2Int(Mathf.RoundToInt(pos.x) & xMax, Mathf.RoundToInt(pos.y) & yMax);

            float sand = sandMap[posIndex.x, posIndex.y];
            float shadow = shadowMap[posIndex.x, posIndex.y];

            float depositProbability = Mathf.Clamp01(shadow + ProbabilityFromSand(sand));
            if (Random.value <= depositProbability)
            {
                //AddSand(posIndex.x, posIndex.y, d);
                //DepositBetweenAndSlip(pos, d);
                DepositBetween(pos, d);

                //SlipPoint(posIndex.x, posIndex.y, 0f);
                
                break;
            }


            i++;
            if (i >= 100)
            {
                Debug.Log("problem");
                break;
            } 
        }
    }

    Vector2 GetSaltationVector(int x, int y)
    {
        Vector2 gradient = GetGradient(x, y);
        //Vector2 wind = windMap[x, y];
        Vector2 wind = universalWindVector;

        //directional component is the gradient facing towards the wind direction
        float directionalGradient = Vector2.Dot(gradient, wind) / wind.magnitude;

        //METHOD 1: my own
        // float ejectionAngle = Mathf.PI / 4f;
        // float ejectionGradient = Mathf.Sin(ejectionAngle);

        // float saltationMultiplier = Mathf.Min(1f, Mathf.Max(0f, ejectionGradient - directionalGradient)) / ejectionGradient;
        // return saltationMultiplier * wind;

        //METHOD 2: https://www.researchgate.net/publication/2353759_A_Method_for_Modeling_and_Rendering_Dunes_with_Wind-ripples
        float heightDel = Mathf.Tan(Mathf.Asin(directionalGradient)) * gridSize;
        float newWindMagnitude = wind.magnitude * (1f - b * (float)System.Math.Tanh(heightDel));
        //return wind * Mathf.Max(0f, newWindMagnitude / wind.magnitude); 
        return Mathf.Log((heightMap[x, y] + 0.1f) / deviationCoefficient) * wind * Mathf.Max(0f, newWindMagnitude / wind.magnitude); 

        //METHOD 3: log wind profile:
        //return (wind / 0.41f) * Mathf.Log((heightMap[x, y] + 2) / deviationCoefficient);
    }
    float GetSaltationAmount(int x, int y)
    {
        //return Mathf.Min(originSandAmount, depositAmount);

        Vector2 gradient = GetGradient(x, y);
        //Vector2 wind = windMap[x, y];
        Vector2 wind = universalWindVector;

        //directional component is the gradient facing towards the wind direction
        float directionalGradient = Vector2.Dot(gradient, wind) / wind.magnitude;

        //METHOD 1: https://www.researchgate.net/publication/2353759_A_Method_for_Modeling_and_Rendering_Dunes_with_Wind-ripples
        float heightDel = Mathf.Tan(Mathf.Asin(directionalGradient)) * gridSize;
        return depositAmount * (1 + sc * (float)System.Math.Tanh(heightDel));
    }


    void DepositBetweenAndSlip(Vector2 pos, float amount)
    {
        //left
        int x0 = Mathf.FloorToInt(pos.x);
        //top
        int y0 = Mathf.FloorToInt(pos.y);


        float px = pos.x - (float)x0;
        //16.0 - 16 = 0.0
        float py = pos.y - (float)y0;


        //top left
        //sandMap[x0 & xMax, y0 & yMax] += (1 - py) * (1 - px);
        AddSand(x0 & xMax, y0 & yMax, (1 - py) * (1 - px) * amount);
        SlipPoint(x0 & xMax, y0 & yMax, 0f);
        //top right
        //sandMap[(x0 + 1) & xMax, y0 & yMax] += (1 - py) * (px);
        AddSand((x0 + 1) & xMax, y0 & yMax, (1 - py) * (px) * amount);
        SlipPoint((x0 + 1) & xMax, y0 & yMax, 0f);

        //bottom left
        //sandMap[x0 & xMax, (y0 + 1) & yMax] += (py) * (1 - px);
        AddSand(x0 & xMax, (y0 + 1) & yMax, (py) * (1 - px) * amount);
        SlipPoint(x0 & xMax, (y0 + 1) & yMax, 0f);
        //bottom right
        //sandMap[(x0 + 1) & xMax, (y0 + 1) & yMax] += (py) * (px);
        AddSand((x0 + 1) & xMax, (y0 + 1) & yMax, (py) * (px) * amount);
        SlipPoint((x0 + 1) & xMax, (y0 + 1) & yMax, 0f);
    }
    void DepositBetween(Vector2 pos, float amount)
    {
        //left
        int x0 = Mathf.FloorToInt(pos.x);
        //top
        int y0 = Mathf.FloorToInt(pos.y);


        float px = pos.x - (float)x0;
        //16.0 - 16 = 0.0
        float py = pos.y - (float)y0;

        //top left
        //sandMap[x0 & xMax, y0 & yMax] += (1 - py) * (1 - px);
        AddSand(x0 & xMax, y0 & yMax, (1 - py) * (1 - px) * amount);
        //top right
        //sandMap[(x0 + 1) & xMax, y0 & yMax] += (1 - py) * (px);
        AddSand((x0 + 1) & xMax, y0 & yMax, (1 - py) * (px) * amount);

        //bottom left
        //sandMap[x0 & xMax, (y0 + 1) & yMax] += (py) * (1 - px);
        AddSand(x0 & xMax, (y0 + 1) & yMax, (py) * (1 - px) * amount);
        //bottom right
        //sandMap[(x0 + 1) & xMax, (y0 + 1) & yMax] += (py) * (px);
        AddSand((x0 + 1) & xMax, (y0 + 1) & yMax, (py) * (px) * amount);

        //Debug.Log((1 - py) * (1 - px) + (1 - py) * (px) + (py) * (1 - px) + (py) * (px));
    }

    public void SaltationCPU(int iterations)
    {
        //bool[,] doneCells = new bool[xResolution, yResolution];

        // for (int i = 0; i < iterations; i++)
        // {
        //     int x = Mathf.FloorToInt(Random.Range(0, xMax));
        //     int y = Mathf.FloorToInt(Random.Range(0, yMax));

        //     if (shadowMap[x, y] < 1)
        //     {
        //         if (sandMap[x, y] > 0) 
        //         {
        //             Saltate(new Vector2Int(x, y));
        //         }
        //     }
            

        //     // if (!doneCells[x, y])
        //     // {
        //     //     Saltate(new Vector2Int(x, y));
        //     //     doneCells[x, y] = true;
        //     // }
        // }
        for (int x = 0; x < xResolution; x++)
        {
            for (int y = 0; y < yResolution; y++)
            {
                if (shadowMap[x, y] < 1)
                {
                    if (sandMap[x, y] > 0) 
                    {
                        Saltate(new Vector2Int(x, y));
                    }
                }
            }
        }

    }



    void SlipPoint(int x, int y, float incomingSand)
    {
        Vector2Int left = new Vector2Int(-1, 0);
        Vector2Int right = new Vector2Int(1, 0);
        Vector2Int up = new Vector2Int(0, -1);
        Vector2Int down = new Vector2Int(0, 1);

        Vector2Int[] directions = {right, down, left, up};



        slopeThreshold = Mathf.Tan(angleOfRepose * Mathf.PI / 180f) * gridSize;
        slipGradientMagnitude = Mathf.Sin(angleOfRepose * Mathf.PI/180f);

        //for deposit, because this will tweak the values of crest
        //AddSand(x, y, 0);

        float crest = GetCrest(x, y);
        Vector2 gradient = GetGradient(x, y);

    
        //quick method 1: just subtract from current cell, works pretty well but would require smoothing
        // if (gradient.magnitude > slipGradientMagnitude)
        // {
        //     AddSand(x, y, -slipAmount);
        //     DepositBetween(new Vector2(x, y) - gradient, slipAmount);
        // }
        // if (crest > crestRoughness * slopeThreshold)
        // {
        //     AddSand(x, y, -(crest - crestRoughness * slopeThreshold));
        // }

        //best method 2: equalise, this makes it so that the average height difference is equal to the slope threshold for spikes
        if (crest > crestRoughness * slopeThreshold)
        {
            //main thing is, i need to figure out how to make it smart enough to avoid the adjacent tile's spread
            // float equalizeAmount = 4f/5f * (crest - slopeThreshold);
            // //middle
            // AddSand(x, y, -equalizeAmount);

            // // //other directions
            // AddSand((x - 1) & xMax, y, equalizeAmount / 4f);
            // AddSand((x + 1) & xMax, y, equalizeAmount / 4f);
            // AddSand(x, (y - 1) & yMax, equalizeAmount / 4f);
            // AddSand(x, (y + 1) & yMax, equalizeAmount / 4f);

            //slipQueue = new bool[] {true, true, true, true};

            //only distribute to lesser tiles method
            List<Vector2Int> lesserDirections = new List<Vector2Int>();

            foreach (Vector2Int dir in directions)
            {
                Vector2Int pos = new Vector2Int((x + dir.x) & xMax, (y+ dir.y) & yMax);

                if (heightMap[pos.x, pos.y] < heightMap[x, y]) lesserDirections.Add(pos);
           }
           float numSides = (float)lesserDirections.Count;
           float equalizeAmount = (numSides)/(numSides + 1) * (crest - crestRoughness * slopeThreshold);
           equalizeAmount = Mathf.Min(sandMap[x, y], equalizeAmount); //dont want to remove more than we have 

           AddSand(x, y, -equalizeAmount);
           foreach (Vector2Int pos in lesserDirections)
           {
                AddSand(pos.x, pos.y, equalizeAmount / numSides);
           }
        }
        else if (crest < -crestRoughness * slopeThreshold)
        {
            //only distribute to greater tiles method
            List<Vector2Int> greaterDirections = new List<Vector2Int>();

            foreach (Vector2Int dir in directions)
            {
                Vector2Int pos = new Vector2Int((x + dir.x) & xMax, (y+ dir.y) & yMax);

                if (heightMap[pos.x, pos.y] > heightMap[x, y]) greaterDirections.Add(pos); //
           }
           float numSides = (float)greaterDirections.Count;
           float equalizeAmount = (numSides)/(numSides + 1) * (crest + crestRoughness * slopeThreshold); //

           AddSand(x, y, -equalizeAmount);
           foreach (Vector2Int pos in greaterDirections)
           {
                //TODO: prevent removing excess from sides and going below bedrock (or decide if its computationaly nessesary)
                AddSand(pos.x, pos.y, equalizeAmount / numSides);
           }
        }
   


        //recalculate crest gradient and excess
        crest = GetCrest(x, y);
        gradient = GetGradient(x, y);
        float excess = GetExcess(x, y, crest, gradient);  
    
            
        //THIS BIT ACTUALLY WORKS EXCELLENTLY ON ITS OWN BUT STRUGGLES WITH SPIKES AND SUCH
        if (excess == 0f) return;
        else
        {
            foreach (Vector2Int dir in directions)
            {
                Vector2Int pos = new Vector2Int((x + dir.x) & xMax, (y+ dir.y) & yMax);

                //note that gradient points uphill, so dot is +tive when dir is uphill from centre

                // float directionalComponent = ((-dir.x*gradient.x)+(-dir.y*gradient.y) + 1) / 4f;
                // directionalComponent = Mathf.Max(0, directionalComponent - (0.25f * 0.5f));
                // c = directionalComponent * excess;
        
                //sign of excess is here because if excess is negative, sand is added to the centre and removed UPHILL
                //with / gradient.magnitude or without?
                float directionalComponent = Mathf.Max(0, Mathf.Sign(excess) * -Vector2.Dot(gradient, (Vector2)dir));
                
                AddSand(pos.x, pos.y, directionalComponent * excess);
            }   
            AddSand(x, y, -excess); 
        }      
    }

    float GradientToHeight(Vector2 gradient)
    {
        return Mathf.Tan(GradientToAngle(gradient)) * gridSize;
    }

    float GradientToAngle(Vector2 gradient)
    {
        return Mathf.Asin(gradient.magnitude);
    }

    float GetExcess(int x, int y, float crest, Vector2 gradient)
    {
        float excess = 0f;

        // float lowestPointDiff = crest + GradientToHeight(gradient);
        // if (lowestPointDiff > slopeThreshold)
        // {
        //     //0.5* is optional theoretically (but not really)
        //     excess = (lowestPointDiff - slopeThreshold);
        // }           
        


        if (crest > 0)
        {
            //putting this outside the if makes more sweeping curves (NICE!)
            //float lowestPointDiff = crest + GradientToHeight(gradient); //faster
            float lowestPointDiff = Mathf.Max(GradientToHeight(gradient), crest); //nicer looking

            if (lowestPointDiff > slopeThreshold)
            {
                //0.5* is optional theoretically (but not really)
                excess = (lowestPointDiff - (slopeThreshold));
            }      
        }
        else //for negative crests
        {
            //it works fine without negative crests, im just experimenting with removing diamond artifacts (this helps)
            
            //float highestPointDiff = -crest + GradientToHeight(gradient); //faster
            float highestPointDiff = Mathf.Min(-GradientToHeight(gradient), -crest); //nicer looking

            if (highestPointDiff > slopeThreshold)
            {
                //0.5* is optional theoretically (but not really)
                excess += -(highestPointDiff - (slopeThreshold));
            }    
        }
       

        //+tive excess means current cell is too tall
        //-tive excess means current cell is too deep
        return excess;
    }


    public void SlipCPU(int iterations)
    {
        if (slipOrder == slipOrders.Sequence)
        {
            if (iterations == 0) return;
            for (int i = 0; i < iterations; i++)
            {
                for (int x = 0; x < xResolution; x++)
                {
                    for (int y = 0; y < yResolution; y++)
                    {
                        SlipPoint(x, y, 0f);
                    }
                }
            }
        }
        if (slipOrder == slipOrders.SequenceCycle)
        {
            if (iterations == 0) return;

            for (int i = 0; i < iterations; i++)
            {
                
                for (int x = 0; x < xResolution; x++)
                {
                    for (int y = 0; y < yResolution; y++)
                    {
                        SlipPoint(x, y, 0f);
                    }
                }
            
                for (int x = xMax; x >= 0; x--)
                {
                    for (int y = 0; y < yResolution; y++)
                    {
                        SlipPoint(x, y, 0f);
                    }
                }
            
                for (int x = xMax; x >= 0; x--)
                {
                    for (int y = yMax; y >= 0; y--)
                    {
                        SlipPoint(x, y, 0f);
                    }
                }
            
                for (int x = 0; x < xResolution; x++)
                {
                    for (int y = yMax; y >= 0; y--)
                    {
                        SlipPoint(x, y, 0f);
                    }
                }
            
            }
        }
        else if (slipOrder == slipOrders.Random)
        {
            if (iterations == 0) return;

            for (int i = 0; i < iterations; i++)
            {
                List<Vector2Int> cells = new List<Vector2Int>();
                for (int x = 0; x < xResolution; x++)
                {
                    for (int y = 0; y < yResolution; y++)
                    {
                        cells.Add(new Vector2Int(x, y));
                    }
                }

                while (cells.Count> 0)
                {
                    int index = Random.Range(0, cells.Count - 1);

                    SlipPoint(cells[index].x, cells[index].y, 0f);
                    cells.RemoveAt(index);
                }
            }
        }
        //Do everything at once, store it in an array and set
        else if (slipOrder == slipOrders.AtOnce)
        {
            float[,] newSandMap = new float[xResolution, yResolution];

            for (int x = 0; x < xResolution; x++)
            {
                for (int y = 0; y < yResolution; y++)
                {
                    if (sandMap[x, y] > 0) SlipPoint(x, y, 0f);
                }
            }
        }
    }

    void DotCPU()
    {
        for (int x = 0; x < xResolution; x++)
        {
            for (int y = 0; y < yResolution; y++)
            {
                AddSand(x, y, GetSandFlux(x, y));
            }
        }
    }

    float GetSandFlux(int x, int y)
    {
        Vector2 gradient = GetGradient(x, y);
        Vector2 wind = universalWindVector;
        //dont normalize this, we want gradient to affect the dot product
        float dot = Vector2.Dot(gradient, wind) / wind.magnitude;
        float shadow = shadowMap[x, y];


        // float sandFlux = 0;
        // //METHOD 1: if the dot product > 0 (ie uphill is in the same direction as the wind) (windward side)
        // if (dot > 0)
        // {
        //     //any uphill slopes in shadow will have less added/subtracted to them
        //     sandFlux = -(1 - shadow) * depositAmount * dot * gridSize;  
        // }
        // else 
        // {
        //     sandFlux = -depositAmount * dot * gridSize;
        // }
        // return sandFlux;

        //METHOD 2:
        return -depositAmount * dot * gridSize;
    }

    float GetCrest(int x, int y)
    {
        //gets sortof the "average height difference" around a cell

        float centre = heightMap[x, y];

        float left = heightMap[(x - 1) & xMax, y];
        float right = heightMap[(x + 1) & xMax, y];

        float up = heightMap[x, (y - 1) & yMax];
        float down = heightMap[x, (y + 1) & yMax];

        //float total = left + right + up + down;

        //positive values for crest, negative for trough
        //return ((centre - left) + (centre - right) + (centre - up) + (centre - down)) / 4.0 / gridSize;
        return centre - ((left + right + up + down) / 4f);
    }

    Vector3 CalculateNormal(int x, int y)
    {
        float left = heightMap[(x - 1) & xMax, y];
        float right = heightMap[(x + 1) & xMax, y];
        float up = heightMap[x, (y - 1) & yMax];
        float down = heightMap[x, (y + 1) & yMax];

        //Method 1: cross (i think theres something i need to do with proper normalising)
        // Vector3 va = new Vector3(2 * gridSize, (right - left), 0);
        // Vector3 vb = new Vector3(0, (up - down), -2 * gridSize);

        // return Vector3.Normalize(Vector3.Cross(va, vb));

        //Method 2: diff
        return Vector3.Normalize(new Vector3((left - right) / (2 * gridSize), 1, (up - down) / (2 * gridSize)));
    }

    Vector2 GetGradient(int x, int y)
    {
        Vector3 normal = CalculateNormal(x, y);
        return new Vector2(-normal.x, -normal.z);
    }


    void BlurCPU()
    {
        float k = blurStrength / 2f;
        float[,] kernel = {{k/8f, k/8f, k/8f},
                           {k/8f,  1-k, k/8f},
                           {k/8f, k/8f, k/8f}};

        float[,] newMap = new float[xResolution, yResolution];

        for (int x = 0; x < xResolution; x++)
        {
            for (int y = 0; y < yResolution; y++)
            {
                float total = 0;

                for (int ix = -1; ix <= 1; ix++)
                {
                    for (int iy = -1; iy <= 1; iy++)
                    {
                        total += kernel[ix + 1, iy + 1] * (sandMap[(x + ix) & xMax, (y + iy) & yMax] + bedrockMap[(x + ix) & xMax, (y + iy) & yMax]);
                    }
                }
                newMap[x, y] = Mathf.Max(0, total - bedrockMap[x, y]);
            }
        }

        sandMap = newMap;
    }



























    Color GetColor(int x, int y,  display displayType, float min = 0, float max = 1)
    {
        if (x == debugPoint.x && y == debugPoint.y) return Color.cyan;

        //sand
        if (displayType == display.sand)
        {
            if (sandMap[x, y] > 0) return Color.Lerp(Color.gray, new Color(0.9f, 0.9f, 0.8f, 1f), sandMap[x, y] / 0.1f);
            else return Color.gray;
        }

        //height
        else if (displayType == display.height) return Color.Lerp(Color.black, Color.white, Mathf.InverseLerp(min, max, bedrockMap[x, y] + sandMap[x, y]));       
    
        //normal
        else if (displayType == display.normal) 
        {
            Vector3 norm = CalculateNormal(x, y);
            return new Color(norm.x * 0.5f + 0.5f, norm.z * 0.5f + 0.5f, norm.y * 0.5f + 0.5f);
        }
        //gradient
        else if (displayType == display.gradient)
        {
            Vector2 gradientTemp = GetGradient(x, y);
            if (gradientTemp.magnitude >= 1) return Color.blue;
            else return new Color(Mathf.InverseLerp(-1, 1, gradientTemp.x), Mathf.InverseLerp(-1, 1, gradientTemp.y), 0);
        }

        //gradient magnitude (steepness)
        else if (displayType == display.gradientMagnitude) 
        {
            Vector2 gradientTemp = GetGradient(x, y);
            if (gradientTemp.magnitude > slipGradientMagnitude) return Color.red;
            else return Color.Lerp(Color.black, Color.white, gradientTemp.magnitude);
        }

        //crests
        else if (displayType == display.crests) 
        {
            float crest = GetCrest(x, y);
            //return Color.Lerp(Color.black, Color.white, Mathf.InverseLerp(min, max, crestMap[x, y]));
            if (crest > crestRoughness * slopeThreshold) return Color.red;
            else if (crest < -crestRoughness * slopeThreshold) return Color.blue;
            else return Color.Lerp(Color.black, Color.white, crest / (crestRoughness * slopeThreshold));
        }

        //wind direction
        else if (displayType == display.wind) return new Color(windMap[x, y].normalized.x / 2f + 0.5f, windMap[x, y].normalized.y / 2f + 0.5f, 0, 1) * (windMap[x, y].magnitude / universalWindVector.magnitude / 2f);

        //shadow
        //if (displayType == display.shadow) colourMap[x + y * xResolution] = Color.Lerp(Color.black, Color.white, shadowMap[x, y]);
        
        else if (displayType == display.shadow) return Color.Lerp(Color.white, Color.black, shadowMap[x, y]);

        //saltation distance
       
        else if (displayType == display.saltationDistance) 
        {
            float saltationDistance = GetSaltationVector(x, y).magnitude;
            return Color.Lerp(Color.black, Color.white, saltationDistance / universalWindVector.magnitude / 2f);
        }

        //saltation amount
        else if (displayType == display.saltationAmount)
        {
            float saltationAmount = GetSaltationAmount(x, y);
            return Color.Lerp(Color.black, Color.white, Mathf.InverseLerp(min, max, saltationAmount));
        }

        
        //else if (displayType == display.flux) return new Color(Mathf.InverseLerp(min, max, GetFlux(x, y)), 0, 0);

        else if (displayType == display.blurAreas)
        {
            if (crestMap[x, y] > 0)
            {
                if (gradientMap[x, y].magnitude > slipGradientMagnitude) return Color.red;
            }                
            return Color.white;
        }
      
      

        return Color.cyan;
    }


    public Texture2D TextureFromHeightMap(display displayType, float black = 0, float white = 1)
    {
        Color[] colourMap = new Color[xResolution * yResolution];
        for (int y = 0; y < yResolution; y++)
        {
            for (int x = 0; x < xResolution; x++)
            {       
                colourMap[x + y * xResolution] = GetColor(x, y, displayType, black, white);
            }
        }

        //movement arrows should point to associated pixel if oriented correctly
        //x = xMax, y = 0
        colourMap[xMax] = Color.red;
        //x = xMax, y = 0
        colourMap[yMax * yResolution] = Color.blue;

        Texture2D texture = new Texture2D(xResolution, yResolution, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels(colourMap);
        texture.Apply();
        
        return texture;
    }

    void GenerateMeshes(float[,] heightMap)
    {
        float totalHeight = 0f;

        //destroy all child objects (terrain)
        while(transform.childCount != 0) DestroyImmediate(transform.GetChild(0).gameObject);
    

        int xResolutionTotal = heightMap.GetLength(0);
        int yResolutionTotal = heightMap.GetLength(1);

        int xResolution = Mathf.Min(xResolutionTotal, 128) + 1;
        int yResolution = Mathf.Min(yResolutionTotal, 128) + 1;

        for (int yTotal = 0; yTotal < yResolutionTotal; yTotal += yResolution - 1)
        {
            for (int xTotal = 0; xTotal < xResolutionTotal; xTotal += xResolution - 1)
            {
                //each chunk

                Vector3[] vertices = new Vector3[xResolution * yResolution];
                int[] triangles = new int[(xResolution - 1) * (yResolution - 1) * 6];
                Vector2[] uvs = new Vector2[xResolution * yResolution];
                Vector3[] normals = new Vector3[xResolution * yResolution];
                Color[] colors = new Color[xResolution * yResolution];

                int triIndex = 0;
                
                for (int y = 0; y < yResolution; y++)
                {
                    for (int x = 0; x < xResolution; x++)
                    {
                        int i = y * xResolution + x;

                        normals[i] = CalculateNormal((xTotal + x) & xMax, (yTotal + y) & yMax);
                        colors[i] = GetColor((xTotal + x) & xMax, (yTotal + y) & yMax, meshDisplayType, min, max);
                        
                        float normalisedX = (float)x / (xResolution);
                        float normalisedY = (float)y / (yResolution);

                        uvs[i] = new Vector2(normalisedX, normalisedY);

                        float sampleX = normalisedX * xResolution * gridSize;
                        float sampleZ = normalisedY * yResolution * gridSize;
                        float sampleY = heightMap[(xTotal + x) & xMax, (yTotal + y) & yMax];
                        totalHeight += heightMap[(xTotal + x) & xMax, (yTotal + y) & yMax];

                        vertices[i] = new Vector3(sampleX, sampleY, sampleZ);

                        if (y < yResolution - 1 && x < xResolution - 1)
                        {
                            triangles[triIndex] = i;
                            triangles[triIndex + 1] = i + yResolution;
                            triangles[triIndex + 2] = i + yResolution + 1;

                            triangles[triIndex + 3] = i;
                            triangles[triIndex + 4] = i + yResolution + 1;
                            triangles[triIndex + 5] = i + 1;

                            triIndex += 6;   
                        }
                    }
                }

                Mesh mesh = new Mesh();
                mesh.Clear();
                mesh.vertices = vertices;
                mesh.triangles = triangles;
                mesh.uv = uvs;
                mesh.normals = normals;

                mesh.colors = colors;

                //mesh.Optimize();

                GameObject chunk = Instantiate(terrainPrefab, new Vector3(xTotal * gridSize, 0, yTotal * gridSize), Quaternion.identity, transform);
                chunk.GetComponent<MeshFilter>().sharedMesh = mesh;

                chunk.GetComponent<MeshCollider>().sharedMesh = mesh;
                //chunk.GetComponent<MeshRenderer>().sharedMaterial.mainTexture = TextureFromHeightMap(displayType, min, max);
                //GetComponent<MeshRenderer>().sharedMaterial.SetTexture("_BumpMap", TextureFromHeightMap(display.normal));
            }
        }
        Debug.Log("average height:" +totalHeight / xResolution / yResolution);
    }

    

    
}
