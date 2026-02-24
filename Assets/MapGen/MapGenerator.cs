using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    public bool autoUpdate;
    
    public ComputeShader noiseComputeShader;
    public ComputeShader saltationComputeShader;
    public ComputeShader shadowComputeShader;

    public float angleOfRepose = 34f;
    //public float angleOfYield = 38f;

    public float min;
    public float max;


    

    public int seed;


    public bool showShadow;

    public float initialHeight;



    
    [System.Serializable]
    public class noiseClass 
    {
        [Tooltip("Random seed for the noise.")]
        public int seed;
        [Tooltip("Scale of the noise.")]
        public float scale;
        [Tooltip("Number of layers of noise used.")]
        [Range(1, 10)]
        public int octaves;
        [Tooltip("Persistence.")]
        [Range(0, 1)]
        public float persistence;
        [Tooltip("Lacunarity.")]
        public float lacunarity;
    }
    public noiseClass noise;

    void Start() 
    {
        Random.InitState(seed);
        transform.localScale = new Vector3(LengthDownwind, 1f, WidthAcross);

        avalancheHeightThreshold = Mathf.Tan(angleOfRepose * Mathf.PI / 180f);

        mWidth = WidthAcross - 1;
        mLength = LengthDownwind - 1;

        Elev = new float[WidthAcross, LengthDownwind];
        System.Array.Clear(Elev, 0, LengthDownwind * WidthAcross);
        Shadow = new float[WidthAcross, LengthDownwind];
        System.Array.Clear(Shadow, 0, LengthDownwind * WidthAcross);

        // for (int y = 0; y < WidthAcross; y++)
        // {
        //     for (int x = 0; x < LengthDownwind; x++)
        //     {
        //         Elev[x, y] = initialHeight;
        //         //Elev[w, x] = Random.Range(5, 15);
        //         //Elev[x, y] = (15 + 15 * Mathf.Sin(2 * Mathf.PI * y / (float)LengthDownwind));
        //     }
        // }
        //initializes shadow
        //shadowCheck(false);
        //float t = Time.realtimeSinceStartup;

        Elev = NoiseMapGPU(LengthDownwind, noise.seed, noise.octaves, noise.scale, noise.persistence, noise.lacunarity);

                
        GetComponent<MeshFilter>().sharedMesh = GenerateMesh(Elev);
        //GetComponent<MeshRenderer>().sharedMaterial.mainTexture = TextureFrom1DHeightMap(heightMap, min, max);
        GetComponent<MeshRenderer>().sharedMaterial.mainTexture = TextureFromHeightMap(Elev, min, max);
        //Debug.Log(string.Concat("Render took: ", Time.realtimeSinceStartup - t, " seconds"));
    }

    public void DrawMapInEditor()
    {
        //float t = Time.realtimeSinceStartup;

        //depositGrain(50, 50);
        //Tick();
        // for (int i = 0; i < 20; i++)
        // {
        //     Elev = SaltationGPU();
        //     Shadow = ShadowGPU();
        //     seed++;
        // }
        // Debug.Log(string.Concat("Saltation took: ", Time.realtimeSinceStartup - t, " seconds"));
        // t = Time.realtimeSinceStartup;

        Elev = NoiseMapGPU(LengthDownwind, noise.seed, noise.octaves, noise.scale, noise.persistence, noise.lacunarity);

       
        GetComponent<MeshFilter>().sharedMesh = GenerateMesh(Elev);


       //GetComponent<MeshRenderer>().sharedMaterial.mainTexture = TextureFrom1DHeightMap(Elev, 512, min, max);
        if (showShadow) GetComponent<MeshRenderer>().sharedMaterial.mainTexture = TextureFromHeightMap(Shadow, 0.001f, 0f);
        else GetComponent<MeshRenderer>().sharedMaterial.mainTexture = TextureFromHeightMap(Elev, min, max);
        //Debug.Log(string.Concat("Render took: ", Time.realtimeSinceStartup - t, " seconds"));
    }

    // IEnumerator waiter()
    // {
    // }

    float[,] NoiseMapGPU(int resolution, int seed, int octaves, float scale, float persistence, float lacunarity) 
    {
        float[,] noiseMap = new float[resolution, resolution];
        ComputeBuffer heightBuffer = new ComputeBuffer(resolution * resolution, 2 * sizeof(int));
        heightBuffer.SetData(noiseMap);
        noiseComputeShader.SetBuffer (0, "heightMap", heightBuffer);

        noiseComputeShader.SetInt("xResolution", resolution);
        noiseComputeShader.SetInt("yResolution", resolution);

        noiseComputeShader.SetInt("seed", seed);
        noiseComputeShader.SetInt("octaves", octaves);
        noiseComputeShader.SetFloat("scale", scale);
        noiseComputeShader.SetFloat("persistence", persistence);
        noiseComputeShader.SetFloat("lacunarity", lacunarity);

        noiseComputeShader.Dispatch (0, resolution / 16, resolution / 16, 1);

        heightBuffer.GetData(noiseMap);
        heightBuffer.Release();

        return noiseMap;
    }

    float[,] SaltationGPU() 
    {
        ComputeBuffer heightBufferIn = new ComputeBuffer(LengthDownwind * WidthAcross, 2 * sizeof(int));
        heightBufferIn.SetData(Elev);
        saltationComputeShader.SetBuffer (0, "heightMapIn", heightBufferIn);

        //float[,] heightMapOut = new float[LengthDownwind, resolution];
        ComputeBuffer heightBufferOut = new ComputeBuffer(LengthDownwind * WidthAcross, 2 * sizeof(int));
        heightBufferOut.SetData(Elev);
        saltationComputeShader.SetBuffer (0, "heightMapOut", heightBufferOut);

        ComputeBuffer shadowBuffer = new ComputeBuffer(LengthDownwind * WidthAcross, 2 * sizeof(int));
        shadowBuffer.SetData(Shadow);
        saltationComputeShader.SetBuffer (0, "shadow", shadowBuffer);

        saltationComputeShader.SetInt("xResolution", LengthDownwind);
        saltationComputeShader.SetInt("yResolution", WidthAcross);
        saltationComputeShader.SetInt("xMax", LengthDownwind - 1);
        saltationComputeShader.SetInt("yMax", WidthAcross - 1);

        saltationComputeShader.SetVector("seed", new Vector2(Random.Range(-100f, 100f), Random.Range(-100f, 100f)));
        saltationComputeShader.SetVector("xSeed", new Vector2(Random.Range(-100f, 100f), Random.Range(-100f, 100f)));
        saltationComputeShader.SetVector("ySeed", new Vector2(Random.Range(-100f, 100f), Random.Range(-100f, 100f)));
        //saltationComputeShader.SetVector("seed", new Vector2(seed, seed));

        saltationComputeShader.SetFloat("avalancheHeightThreshold", avalancheHeightThreshold);
        saltationComputeShader.SetFloat("depositAmount", depositAmount);
        saltationComputeShader.SetInt("saltationDistance", HopLength);
        saltationComputeShader.SetFloat("SHADOW_SLOPE", 0.267949192f);

        saltationComputeShader.SetFloat("WindSpeedUpFactor", 1.2f);
        saltationComputeShader.SetFloat("NonlinearFactor", 0.006f);

        saltationComputeShader.SetFloat("averageHeight", initialHeight);

        saltationComputeShader.Dispatch (0, LengthDownwind / 1, WidthAcross / 1, 1);

        heightBufferIn.Release();
        heightBufferOut.GetData(Elev);
        heightBufferOut.Release();
        shadowBuffer.GetData(Shadow);
        shadowBuffer.Release();

        return Elev;
    }

    float[,] ShadowGPU() 
    {
        ComputeBuffer heightBufferIn = new ComputeBuffer(LengthDownwind * WidthAcross, 2 * sizeof(int));
        heightBufferIn.SetData(Elev);
        shadowComputeShader.SetBuffer (0, "heightMap", heightBufferIn);

        float[,] shadowMap = new float[LengthDownwind, WidthAcross];
        ComputeBuffer shadowBufferOut = new ComputeBuffer(LengthDownwind * WidthAcross, 2 * sizeof(int));
        shadowBufferOut.SetData(shadowMap);
        shadowComputeShader.SetBuffer (0, "shadow", shadowBufferOut);

        shadowComputeShader.SetInt("xResolution", LengthDownwind);
        shadowComputeShader.SetInt("yResolution", WidthAcross);
        shadowComputeShader.SetInt("xMax", LengthDownwind - 1);
        shadowComputeShader.SetInt("yMax", WidthAcross - 1);

        shadowComputeShader.SetFloat("SHADOW_SLOPE", 0.267949192f);

        shadowComputeShader.Dispatch (0, LengthDownwind / 1, WidthAcross / 1, 1);

        heightBufferIn.Release();
        shadowBufferOut.GetData(shadowMap);
        shadowBufferOut.Release();

        return shadowMap;
    }

    

    Mesh GenerateMesh(float[,] heightMap)
    {
        int xResolution = heightMap.GetLength(0);
        int yResolution = heightMap.GetLength(1);

        int f = 1;
        if (xResolution > 256)
        {
            f = xResolution / 256;
            xResolution = 256;
            yResolution = 256;
            
        }

        Vector3[] vertices = new Vector3[xResolution * yResolution];
        int[] triangles = new int[(xResolution - 1) * (yResolution - 1) * 6];
        Vector2[] uvs = new Vector2[xResolution * yResolution];

        int triIndex = 0;
        

        for (int y = 0; y < yResolution; y++)
        {
            for (int x = 0; x < xResolution; x++)
            {
                int i = y * xResolution + x;
                
                float normalisedX = (float)x / xResolution;
                float normalisedY = (float)y / yResolution;
                // float normalisedX = (float)x;
                // float normalisedY = (float)y;

                uvs[i] = new Vector2(normalisedX, normalisedY);


                float sampleX = normalisedX;
                float sampleZ = normalisedY;
                float sampleY = heightMap[x * f, y * f];

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
        mesh.RecalculateNormals();

        return mesh;
    }

    public Texture2D TextureFrom1DHeightMap(float[,] heightMap, int resY, float black = 0, float white = 1)
    {
        int resX = heightMap.GetLength(1);

        Color[] colourMap = new Color[resX * resY];
        for (int y = 0; y < resY; y ++)
        {
            for (int x = 0; x < resX; x ++)
            {          
                if (heightMap[0, x] >= Mathf.Lerp(black, white, (float)y / (float)resY)) 
                {
                    if (Shadow[0, x] <= 0) colourMap[y * resX + x] = Color.white;
                    else colourMap[y * resX + x] = Color.black;
                }
                else
                {
                    colourMap[y * resX + x] = Color.blue;
                }
            }
        }

        Texture2D texture = new Texture2D(resX, resY, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels(colourMap);
        texture.Apply();
        
        return texture;
    }

    public Texture2D TextureFromHeightMap(float[,] heightMap, float black = 0, float white = 1)
    {
        int resX = heightMap.GetLength(0);
        int resY = heightMap.GetLength(1);

        Color[] colourMap = new Color[resX * resY];
        for (int y = 0; y < resY; y ++)
        {
            for (int x = 0; x < resX; x ++)
            {       
                colourMap[y * resX + x] = Color.Lerp(Color.black, Color.white, Mathf.InverseLerp(black, white, heightMap[x, y]));       
                // if (heightMap[x, y] > 0)
                // {
                //   //shadow
                //   colourMap[y * resX + x] = Color.black;
                // }
                // else colourMap[y * resX + x] = Color.white;
            }
        }

        Texture2D texture = new Texture2D(resX, resY, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels(colourMap);
        texture.Apply();
        
        return texture;
    }




















    public float[,] Elev;
    public float[,] Shadow;
    public int WidthAcross = 0;    // across wind
    public int LengthDownwind = 0;
    public int HopLength = 5;
    //public float AverageHeight;
    public float pSand = 0.6f;
    public float pNoSand = 0.4f;
    //public IFindSlope FindSlope;
    protected int mWidth, mLength;
    //protected Random rnd = new Random(123);
    //private Form1 parentForm;
    protected bool openEnded = false;
    //public const float SHADOW_SLOPE = 0.803847577f;  //  3 * tan(15 degrees)
    public const float SHADOW_SLOPE = 0.267949192f;  //  tan(15 degrees)


    private const float WindSpeedUpFactor = 0.4f;
    private const float NonlinearFactor = 0.002f;


    //mine
    //public float avalancheHeightThreshold = Mathf.Tan(angleOfRepose * Mathf.PI / 180f);
    float avalancheHeightThreshold = 0.674f;

    public float depositAmount;
    int[,] checkedCells;



    // //public Model(IFindSlope SlopeFinder, int WidthAcross, int LengthDownwind) {
    // public void Model(int WidthAcross, int LengthDownwind) {
    //   //parentForm = ParentForm;
    //   //FindSlope = SlopeFinder;
    //   this.WidthAcross = (int)Mathf.Pow(2, (int)Mathf.Log(WidthAcross, 2));
    //   this.LengthDownwind = (int)Mathf.Pow(2, (int)Mathf.Log(LengthDownwind, 2));
    //   mWidth = this.WidthAcross - 1;
    //   mLength = this.LengthDownwind - 1;
    //   Elev = new int[WidthAcross, LengthDownwind];
    //   System.Array.Clear(Elev, 0, LengthDownwind * WidthAcross);
    //   Shadow = new float[WidthAcross, LengthDownwind];
    //   System.Array.Clear(Shadow, 0, LengthDownwind * WidthAcross);
    //   //FindSlope.Init(ref Elev, WidthAcross, LengthDownwind);
    //   //FindSlope.SetOpenEnded(openEnded);
    // }


    // public int MaxHeight() {
    //   int maxH = 0;
    //   for (int x = 0; x < LengthDownwind; x++)
    //     for (int w = 0; w < WidthAcross; w++)
    //       if (Elev[w, x] > maxH)
    //         maxH = Elev[w, x];
    //   return maxH;
    // }

    // public Range MinMaxHeight() {
    //   int maxH = 0;
    //   int minH = 100000;
    //   for (int x = 0; x < LengthDownwind; x++)
    //     for (int w = 0; w < WidthAcross; w++) {
    //       if (Elev[w, x] > maxH)
    //         maxH = Elev[w, x];
    //       if (Elev[w, x] < minH)
    //         minH = Elev[w, x];
    //     }
    //   Range r = new Range();
    //   r.Min = minH;
    //   r.Max = maxH;
    //   return r;
    // }

    public float AveHeight()
    {
        float sum = 0;
        for (int x = 0; x < LengthDownwind; x++)
            for (int w = 0; w < WidthAcross; w++)
                sum += Elev[w, x];
        return sum / ((float)(LengthDownwind) * (float)WidthAcross);
    }

    // public int Count() {
    //   int sum = 0;
    //   for (int x = 0; x < LengthDownwind; x++)
    //     for (int w = 0; w < WidthAcross; w++)
    //       sum += Elev[w, x];
    //   return sum;
    // }

    // public int[] Profile(int WidthPosition) {
    //   int[] prof = new int[LengthDownwind];
    //   for (int x = 0; x < LengthDownwind; x++)
    //     prof[x] = Elev[WidthPosition, x];
    //   return prof;
    // }


    protected int shadowCheck(bool ReportErrors)
    {  // returns num of fixes
       // Rules:
       // - Shadows start from the downwind edge of slab
       // - A slab is in shadow if its edge is in shadow
       // - If the top slab of a stack not in shadow, that stack is a new peak
       // - If a stack has no accommodation space, it is zero; otherwise it is height of shadow
        float[,] newShadow = new float[Shadow.GetLength(0), Shadow.GetLength(1)];
        //float[,] newShadow = new float[WidthAcross, LengthDownwind];
        System.Array.Clear(newShadow, 0, newShadow.Length);
        float height;
        int xs;
        float hs;
        for (int w = 0; w < WidthAcross; w++)
            for (int x = 0; x < LengthDownwind; x++)
            {
                height = Elev[w, x];
                if (height == 0) continue;
                //max(terrain height, shadow)
                hs = Mathf.Max(((float)height), newShadow[w, (x - 1) & mLength] - SHADOW_SLOPE);
                xs = x;
                while (hs >= ((float)Elev[w, xs]))
                {
                    newShadow[w, xs] = hs;
                    hs -= SHADOW_SLOPE;
                    xs = (xs + 1) & mLength;
                }
            }
        for (int x = 0; x < LengthDownwind; x++)
            for (int w = 0; w < WidthAcross; w++)
                if (newShadow[w, x] == ((float)Elev[w, x]))
                    newShadow[w, x] = 0;
        int errors = 0;
        for (int x = 0; x < LengthDownwind; x++)
            for (int w = 0; w < WidthAcross; w++)
                if (newShadow[w, x] != Shadow[w, x])
                    errors++;
        if (errors > 0)
        {
            if (ReportErrors)
                Debug.Log("shadowCheck error count: " + errors);
            System.Array.Copy(newShadow, Shadow, Shadow.Length);
        }
        for (int x = 0; x < LengthDownwind; x++)
            for (int w = 0; w < WidthAcross; w++)
                if ((Shadow[w, x] > 0) && (Shadow[w, x] < Elev[w, x]))
                    continue;  // bug -- should never get here
        return errors;
    }

    public virtual float erodeGrain(int w, int x)
    {
        float t = Time.realtimeSinceStartup;

        int wSteep, xSteep;
        while (Upslope(w, x, out wSteep, out xSteep) >= 2)
        {
            // if (openEnded && (((xSteep == mLength) && (x == 0)) || ((xSteep == 0) && (x == mLength))))
            //     return 0;  // erosion happens off-field
            w = wSteep;
            x = xSteep;
        }
        //prevents it from being below zero
        Elev[w, x] = Mathf.Max(0, Elev[w, x] - depositAmount);
        //Elev[w, x] = (Elev[w, x] > depositAmount) ? (Elev[w, x] - depositAmount) : 0;

        float h = Elev[w, x];
        float hs;
        // if (openEnded && (x == 0))
        //     hs = h;
        //else
        //{
            int xs = (x - 1) & mLength;
            hs = Mathf.Max(h, Mathf.Max(Elev[w, xs], Shadow[w, xs]) - SHADOW_SLOPE);
        //}
        while (hs >= (h = ((float)Elev[w, x])))
        {
            Shadow[w, x] = (hs == h) ? 0 : hs;
            hs -= SHADOW_SLOPE;
            x = (x + 1) & mLength;
            // if (openEnded && (x == 0))
            //     return 0;
        }
        while (Shadow[w, x] > 0)
        {
            Shadow[w, x] = 0;
            x = (x + 1) & mLength;
            // if (openEnded && (x == 0))
            //     return 0;
            hs = h - SHADOW_SLOPE;
            if (Shadow[w, x] > hs)
                while (hs >= (h = ((float)Elev[w, x])))
                {
                    Shadow[w, x] = (hs == h) ? 0 : hs;
                    hs -= SHADOW_SLOPE;
                    x = (x + 1) & mLength;
                    // if (openEnded && (x == 0))
                    //     return 0;
                }
        }

        return Time.realtimeSinceStartup - t;
    }

    public virtual float depositGrain(int w, int x)
    {
        float t = Time.realtimeSinceStartup;

        int xSteep, wSteep;
        while (Downslope(w, x, out wSteep, out xSteep) >= 2)
        {
            // if (openEnded && (((xSteep == mLength) && (x == 0)) || ((xSteep == 0) && (x == mLength))))
            //     break;  // deposit happens at boundary, to keep grains from rolling off
            w = wSteep;
            x = xSteep;
        }
        Elev[w, x] += depositAmount;

        float h = Elev[w, x];
        float hs;
        // if (openEnded && (x == 0))
        //     hs = h;
        // else
        // {
            int xs = (x - 1) & mLength;
            hs = Mathf.Max(h, Mathf.Max(Elev[w, xs], Shadow[w, xs]) - SHADOW_SLOPE);
        //}
        while (hs >= (h = ((float)Elev[w, x])))
        {
            Shadow[w, x] = (hs == h) ? 0 : hs;
            hs -= SHADOW_SLOPE;
            x = (x + 1) & mLength;
            // if (openEnded && (x == 0))
            //     return 0;
        }

        return Time.realtimeSinceStartup - t;
    }

    // this is WernerEnhanced
    public virtual void Tick()
    {        
        float t, v;
        //float TotalTime = 0;
        float ErodeTime = 0;
        float DepositTime = 0;
        float RandomTime = 0;
        float LoopTime = 0;
        // for (int w = 0; w < WidthAcross; w++)
        // {
        //     for (int x = 0; x < LengthDownwind; x++)
        //     {
        
        int saltationLeap;
        
        float dh;
        float href = AveHeight();

        v = Time.realtimeSinceStartup;
        for (int subticks = LengthDownwind * WidthAcross; subticks > 0; subticks--)
        {
            t = Time.realtimeSinceStartup;
            int x = Random.Range(0, LengthDownwind);
            int w = Random.Range(0, WidthAcross);
            RandomTime += Time.realtimeSinceStartup - t;

            float h = Elev[w, x];

            t = Time.realtimeSinceStartup;
            if (h == 0) continue;
            if (Shadow[w, x] > 0) continue;
            ErodeTime += erodeGrain(w, x);

            //int i = HopLength;
            
            while (true)
            {
                dh = h - href;
                if (dh > 0) saltationLeap = HopLength + Mathf.RoundToInt(WindSpeedUpFactor * dh + NonlinearFactor * dh * dh);
                else saltationLeap = HopLength + Mathf.RoundToInt(WindSpeedUpFactor * dh);

                x = (x + saltationLeap) & mLength;

                //original model calls for x to be incremented by 1 and if there is a shadow, to deposit immediately
                if (Shadow[w, x] > 0)
                {
                    //Debug.Log("grain deposited due to shadow");
                    DepositTime += depositGrain(w, x);
                    break;
                }
                //if (--i <= 0) {
                if (Random.value < (h > 0 ? pSand : pNoSand))
                {
                    //Debug.Log("grain deposited");
                    DepositTime += depositGrain(w, x);
                    break;
                }
                //i = saltationLeap;
                //}
                h = Elev[w, x];
            }
            LoopTime += Time.realtimeSinceStartup - t;
        }
        //}

        Debug.Log(string.Concat("Random time: ", RandomTime, " seconds"));

        Debug.Log(string.Concat("Loop time: ", LoopTime, " seconds"));

        t = Time.realtimeSinceStartup;
        shadowCheck(true);
        Debug.Log(string.Concat("Shadow check in tick: ", Time.realtimeSinceStartup - t, " seconds"));
        
        Debug.Log(string.Concat("Total time: ", Time.realtimeSinceStartup - v, " seconds"));

        //Debug.Log(string.Concat("Deposit time: ", DepositTime, " seconds"));
        //Debug.Log(string.Concat("Erode time: ", ErodeTime, " seconds"));

        
    }

    // public virtual int SaltationLength(int w, int x) {
    //   return HopLength;
    // }













    //#region Von Neumann Deterministic
    //public class FindSlopeVonNeumannDeterministic : IFindSlope {
    // public int[,] Elev;
    // public int mWidth;
    // public int mLength;
    public bool OpenEnded = false;

    // public void Init(ref int[,] Elev, int WidthAcross, int LenghtDownwind) {
    //   this.Elev = Elev;
    //   mWidth = WidthAcross - 1;
    //   mLength = LenghtDownwind - 1;
    // }

    // public void SetOpenEnded(bool NewState) {
    //   OpenEnded = NewState;
    // }

    public int Upslope(int wCenter, int xCenter, out int wSteep, out int xSteep)
    {
        //    2
        //  1   4
        //    3
        int wLeft, wRight, xUp, xDown;
        wSteep = wCenter; xSteep = xCenter;
        float h = Elev[wCenter, xCenter];

        xUp = (xCenter - 1) & mLength;
        if ((!OpenEnded || (xCenter > 0)) && ((Elev[wCenter, xUp] - h) >= avalancheHeightThreshold))
        {
            xSteep = xUp; return 2;
        }
        wRight = (wCenter + 1) & mWidth;
        if ((Elev[wRight, xCenter] - h) >= avalancheHeightThreshold)
        {
            wSteep = wRight; return 2;
        }
        wLeft = (wCenter - 1) & mWidth;
        if ((Elev[wLeft, xCenter] - h) >= avalancheHeightThreshold)
        {
            wSteep = wLeft; return 2;
        }
        xDown = (xCenter + 1) & mLength;
        if ((!OpenEnded || (xCenter != mLength)) && ((Elev[wCenter, xDown] - h) >= avalancheHeightThreshold))
        {
            xSteep = xDown; return 2;
        }
        return 0;
    }

    public int Downslope(int wCenter, int xCenter, out int wSteep, out int xSteep)
    {
        //    2
        //  4   1
        //    3
        int wLeft, wRight, xUp, xDown;
        wSteep = wCenter; xSteep = xCenter;
        float h = Elev[wCenter, xCenter];

        xDown = (xCenter + 1) & mLength;
        if ((!OpenEnded || (xCenter != mLength)) && ((h - Elev[wCenter, xDown]) >= avalancheHeightThreshold))
        {
            xSteep = xDown; return 2;
        }
        wRight = (wCenter + 1) & mWidth;
        if ((h - Elev[wRight, xCenter]) >= avalancheHeightThreshold)
        {
            wSteep = wRight; return 2;
        }
        wLeft = (wCenter - 1) & mWidth;
        if ((h - Elev[wLeft, xCenter]) >= avalancheHeightThreshold)
        {
            wSteep = wLeft; return 2;
        }
        xUp = (xCenter - 1) & mLength;
        if ((!OpenEnded || (xCenter > 0)) && ((h - Elev[wCenter, xUp]) >= avalancheHeightThreshold))
        {
            xSteep = xUp; return 2;
        }
        return 0;
    }
    //}


}