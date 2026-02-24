using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapGenerator3 : MonoBehaviour
{
    public ComputeShader noiseComputeShader;

    public bool autoUpdate;

    public int xResolution;
    int yResolution = 1;
    int xMax;
    int yMax;

    public float sizeHorizontal;
    float pixelSizeH;
    public float sizeVertical;

    public float min;
    public float max;

    public enum display {sand, height, shadow, saltationDistance, subtracted};
    public display displayType; 

    public bool sliding;
    public float angleOfRepose;

    public bool shadows;
    public float shadowGradient;

    //multiply is equivalent to adding, but it scales with the deposit amount
    //we dont want that since gradient should be independant
    public enum depositType {constant, upWindAdd, upWindAddTan, gradientAdd, peter2002};
    public depositType depositMethod; 
    public bool clampGradient;
    public float depositAmount;
    public float depositConstant;

    //public float maximumAngle;
    public enum windType {constant, height, peter2002, logHeight, gradient};
    public windType windMethod; 
    public float universalWindVector;
    public float windAccelerationFactor;
    public float averageHeight;
    public float maxAngle;


    float[,] bedrockMap;
    float[,] sandMap;

    bool[,] shadowMap;

    

    void Start()
    {
        DrawMapInEditor();
    }

    void Update()
    {
        Simulate();
    }

    public void DrawMapInEditor()
    {
        xMax = xResolution - 1;
        yMax = yResolution - 1;

        pixelSizeH = sizeHorizontal / xResolution;
        
        bedrockMap = NoiseMapGPU(0, 6, 0.01f * sizeHorizontal, 0.6f, 1.75f);
        sandMap = new float[xResolution, yResolution];

        for (int x = 0; x < xResolution; x++) sandMap[x, 0] = 1;
        //sandMap[0, 0] = 5;

        shadowMap = new bool[xResolution, yResolution];

        transform.localScale = new Vector3(1, 1, sizeVertical / sizeHorizontal);
        GetComponent<MeshRenderer>().sharedMaterial.mainTexture = TextureFrom1DHeightMap(displayType, min, max);
    }

    public void Simulate()
    {
        for(int x = 0; x < xResolution; x++)
        {
            shadowMap[x, 0] = ShadowCheck(x);
        }

        
        //will cause nice patterns when wind is flowing against the direction
        // for(int i = xMax; i >= 0; i--)
        // {
        //     int x = i;

        //     Saltate(x);
        // }

        bool[,] doneCells = new bool[xResolution, yResolution];
        int i = 0;
        while (i < xResolution)
        {
            int x = Mathf.FloorToInt(Random.Range(0, xResolution));

            if (!doneCells[x, 0])
            {
                Saltate(x);
                doneCells[x, 0] = true;
                i++;
            }
        }

        //Deposit(new Vector2Int(512, 0), depositAmount);


        transform.localScale = new Vector3(1, 1, sizeVertical / sizeHorizontal);
        GetComponent<MeshRenderer>().sharedMaterial.mainTexture = TextureFrom1DHeightMap(displayType, min, max);
    }

    void Saltate(int x)
    {
        int origin = x;
        float originSandAmount = sandMap[x, 0];

        if (originSandAmount <= 0) goto End;
        //if (ShadowCheck(x)) goto End;
        if (shadowMap[x, 0]) goto End;



        //https://en.wikipedia.org/wiki/Bagnold_formula
        //https://en.wikipedia.org/wiki/Log_wind_profile
        //https://en.wikipedia.org/wiki/Shear_velocity 
        float wind = GetWind(x);

        float d = GetDepositAmount(x);

        Erode(new Vector2Int(x, 0), d);


        float newPos = x + wind;
        if (newPos != Mathf.Round(newPos))
        {
            int floor = Mathf.FloorToInt(newPos);
            int ceil = Mathf.CeilToInt(newPos);

            Deposit(new Vector2Int(floor & xMax, 0), d * (ceil - newPos));
            Deposit(new Vector2Int(ceil & xMax, 0), d * (newPos - floor));
        }
        else
        {
            Deposit(new Vector2Int(Mathf.RoundToInt(newPos) & xMax, 0), d);
        }
        //sandMap[x, 0] += d;
        //for 2d, do
        //topLeft = (ceil - x) * (y - floor)
        //topRight = (x - floor) * (y - floor)
        //bottomLeft = (ceil - x) * (ceil - y)
        //bottomRight = (x - floor) * (ceil - y)

        //sandMap[x, 0] = sandMap[(x - 5) & xMax, 0];

        End:;
    }

    void Erode(Vector2Int origin, float amount)
    {
        Vector2Int pos = origin;

        if (sliding)
        {
            Vector2Int nextPos;
            float centreHeight = bedrockMap[origin.x, origin.y] + sandMap[origin.x, origin.y];
            while(MaxSlope(pos, centreHeight, out nextPos) > 0)
            {
                pos = nextPos;
                centreHeight = bedrockMap[pos.x, pos.y] + sandMap[pos.x, pos.y];
            }
        }
        sandMap[pos.x, pos.y] -= amount;
    }

    void Deposit(Vector2Int origin, float amount)
    {   
        Vector2Int pos = origin;

        if (sliding)
        {
            Vector2Int nextPos;
            float centreHeight = bedrockMap[origin.x, origin.y] + sandMap[origin.x, origin.y];
            while(MinSlope(pos, centreHeight, pos, out nextPos) < 0)
            {
                pos = nextPos;
                centreHeight =  bedrockMap[pos.x, pos.y] + sandMap[pos.x, pos.y];
            }
        }
        sandMap[pos.x, pos.y] += amount;
    }

    float GetWind(int x)
    {
        if (windMethod == windType.constant) 
        {
            return universalWindVector;
        }
        if (windMethod == windType.height) 
        {
            float height = bedrockMap[x, 0] + sandMap[x, 0] - averageHeight;

            return universalWindVector * (1 + windAccelerationFactor * height);
        }
        else if (windMethod == windType.peter2002)
        {
            //A Computer Simulation of Sand Ripple Formation
            float gradient = GetGradient(x);

            float height = bedrockMap[x, 0] + sandMap[x, 0] - averageHeight;

            return (universalWindVector + windAccelerationFactor * height) * (1 - HyperbolicTan(gradient));
        }
        else if (windMethod == windType.logHeight)
        {
            float originHeight = sandMap[x, 0] + bedrockMap[x, 0];

            return universalWindVector * (windAccelerationFactor * Mathf.Log(originHeight - 0));
        }
        else if (windMethod == windType.gradient)
        {
            float originHeight = sandMap[x, 0] + bedrockMap[x, 0];
            float downWindHeight = sandMap[(x + 1) & xMax, 0] + bedrockMap[(x + 1) & xMax, 0];
            float upwindHeight = sandMap[(x - 1) & xMax, 0] + bedrockMap[(x - 1) & xMax, 0];

            float downWindGradient = (downWindHeight - originHeight) / pixelSizeH;

            return universalWindVector * Mathf.InverseLerp(maxAngle, 0, downWindGradient);
        }

        return 0;
    }

    float GetDepositAmount(int x)
    {
        //ideas:
        //minimum activation angle

        if (depositMethod == depositType.constant)
        {
            return Mathf.Min(sandMap[x, 0], depositAmount);
        }
        else if (depositMethod == depositType.upWindAdd)
        {
            float originHeight = sandMap[x, 0] + bedrockMap[x, 0];
            float upwindHeight = sandMap[(x - 1) & xMax, 0] + bedrockMap[(x - 1) & xMax, 0];
            float gradient = (originHeight - upwindHeight) / pixelSizeH;

            if (clampGradient) gradient = Mathf.Max(gradient, 0);

            float excess = depositConstant * gradient;    

            return Mathf.Min(sandMap[x, 0], Mathf.Max(depositAmount + excess, 0));
        }
        else if (depositMethod == depositType.upWindAddTan)
        {
            float originHeight = sandMap[x, 0] + bedrockMap[x, 0];
            float upwindHeight = sandMap[(x - 1) & xMax, 0] + bedrockMap[(x - 1) & xMax, 0];
            float gradient = (originHeight - upwindHeight) / pixelSizeH;

            if (clampGradient) gradient = Mathf.Max(gradient, 0);

            float excess = depositConstant * HyperbolicTan(gradient);    

            return Mathf.Min(sandMap[x, 0], Mathf.Max(depositAmount + excess, 0));
        }        
        else if (depositMethod == depositType.gradientAdd)
        {
            //add
            float gradient = GetGradient(x);

            if (clampGradient) gradient = Mathf.Max(gradient, 0);

            float excess = depositConstant * gradient; 
            return Mathf.Min(sandMap[x, 0], depositAmount + excess);
        }
        else if (depositMethod == depositType.peter2002)
        {
            //A Computer Simulation of Sand Ripple Formation
            float gradient = GetGradient(x);

            if (clampGradient) gradient = Mathf.Max(gradient, 0);

            float multiplier = 1 + HyperbolicTan(gradient);   
            return Mathf.Min(sandMap[x, 0], Mathf.Max(depositAmount * multiplier, 0));
        }        
        
        return 0;
    }

    float GetGradient(int x)
    {
        //3d
        int x1 = (x + 1) & xMax;
        int x2 = (x - 1) & xMax;
        // int y1 = (y + 1) & yMax;
        // int y2 = (y - 1) & yMax;
        // float dhdx = ((sandMap[x1, y] + bedrockMap[x1, y]) - (sandMap[x2, y] + bedrockMap[x2, y])) / 2f / pixelSizeH;
        // float dhdy = ((sandMap[x, y1] + bedrockMap[x, y1]) - (sandMap[x, y2] + bedrockMap[x, y2])) / 2f / pixelSizeH;

        // //makes all gradients positive (from A Computer Simulation of Sand Ripple Formation, but it uses the sign of the x gradient)
        // return Mathf.Sqrt(dhdx * dhdx + dhdy * dhdy);
        // //doesn't (my own)
        // //return Vector2.Dot(wind, new Vector2(dhdx, dhdy))

        //2d
        float dhdx = ((sandMap[x1, 0] + bedrockMap[x1, 0]) - (sandMap[x2, 0] + bedrockMap[x2, 0])) / 2f / pixelSizeH;

        return dhdx;
    }

    bool ShadowCheck(int x)
    {
        //int direction = Mathf.RoundToInt(Mathf.Sign(universalWindVector));
        float originHeight = bedrockMap[x, 0] + sandMap[x, 0];

        if (shadows)
        {
            if (universalWindVector > 0)
            {
                for (int i = 0; i < 300; i++)
                {
                    int pos = x - i;
                    float posHeight = bedrockMap[pos & xMax, 0] + sandMap[pos & xMax, 0];
                    if (posHeight > originHeight + (i * shadowGradient * pixelSizeH)) return true;
                }
            }
            else
            {
                for (int i = 0; i < 300; i++)
                {
                    int pos = x + i;
                    float posHeight = bedrockMap[pos & xMax, 0] + sandMap[pos & xMax, 0];
                    if (posHeight > originHeight + (i * shadowGradient * pixelSizeH)) return true;
                }
            }
        }

        return false;
    }

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

        noiseComputeShader.Dispatch (0, xResolution, yResolution, 1);

        heightBuffer.GetData(noiseMap);
        heightBuffer.Release();

        return noiseMap;
    }


    public Texture2D TextureFrom1DHeightMap(display displayType, float min = 0, float max = 1)
    {
        Color[] colourMap = new Color[xResolution * xResolution];
        for (int x = 0; x < xResolution; x++)
        {
            float bedrockHeight = bedrockMap[x, 0];
            float sandHeight = bedrockHeight + sandMap[x, 0];
            bool shadow = ShadowCheck(x);


            float wind = GetWind(x);

            for (int y = 0; y < xResolution; y++)
            {       
                float pixelHeight = sizeVertical * y / (float)xResolution;

                //sand
                if (displayType == display.sand)
                {
                    if (pixelHeight <= bedrockHeight) colourMap[x + y * xResolution] = Color.gray;
                    else if (pixelHeight <= sandHeight) colourMap[x + y * xResolution] = new Color(0.9f, 0.9f, 0.8f, 1f);
                    else colourMap[x + y * xResolution] = Color.cyan;
                }

                if (displayType == display.saltationDistance)
                {
                    if (pixelHeight <= bedrockHeight) colourMap[x + y * xResolution] = Color.gray;
                    else if (pixelHeight <= sandHeight)
                    {
                        if (pixelHeight >= sandHeight - GetDepositAmount(x)) colourMap[x + y * xResolution] = Color.blue;
                        //else if (pixelHeight >= sandHeight - ((1 + (windAccelerationFactor * (bedrockMap[x, 0] + sandMap[x, 0] - averageHeight))) * GetDepositAmount(x))) colourMap[x + y * xResolution] = Color.green;
                        
                        else colourMap[x + y * xResolution] = new Color(0.9f, 0.9f, 0.8f, 1f);
                    } 
                    else if (shadow) colourMap[x + y * xResolution] = Color.red;
                    else colourMap[x + y * xResolution] = Color.Lerp(Color.black, Color.white, Mathf.InverseLerp(0, universalWindVector, wind));
                }

                if (displayType == display.subtracted)
                {
                    if (pixelHeight <= bedrockHeight) colourMap[x + y * xResolution] = Color.gray;
                    else if (pixelHeight <= sandHeight)
                    {
                        if (pixelHeight >= sandHeight - GetDepositAmount(x)) colourMap[x + y * xResolution] = Color.blue;                       
                        else colourMap[x + y * xResolution] = new Color(0.9f, 0.9f, 0.8f, 1f);
                    } 
                    else if (shadow) colourMap[x + y * xResolution] = Color.red;
                    else colourMap[x + y * xResolution] = Color.Lerp(Color.black, Color.white, Mathf.InverseLerp(min, max, wind));
                }
            }
        }

        //movement arrows should point to associated pixel if oriented correctly
        //x = 0, y = 0: bottom left of texture, but can appear in different places on screen
        colourMap[0] = Color.red;
        //x = xMax, y = yMax: top right of texture, but can appear in different places on screen
        colourMap[xResolution * xResolution - 1] = Color.blue;

        Texture2D texture = new Texture2D(xResolution, xResolution, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels(colourMap);
        texture.Apply();
        
        return texture;
    }


















    float MaxSlope(Vector2Int centre, float centreHeight, out Vector2Int maxSlopePos)
    {
        float slopeThreshold = Mathf.Tan(angleOfRepose * Mathf.PI / 180f) * pixelSizeH;

        float maxSlope = 0;
        maxSlopePos = centre;

        Vector2Int left = new Vector2Int((centre.x - 1) & xMax, centre.y);
        float leftSlope = (bedrockMap[left.x, left.y] + sandMap[left.x, left.y] - centreHeight) - slopeThreshold;
        if (leftSlope > maxSlope)
        {
            maxSlope = leftSlope;
            maxSlopePos = left;
        }
        Vector2Int right = new Vector2Int((centre.x + 1) & xMax, centre.y);
        float rightSlope = (bedrockMap[right.x, right.y] + sandMap[right.x, right.y] - centreHeight) - slopeThreshold;
        if (rightSlope > maxSlope)
        {
            maxSlope = rightSlope;
            maxSlopePos = right;
        }

        return maxSlope;
    }

    float MinSlope(Vector2Int centre, float centreHeight, Vector2Int exclusion, out Vector2Int minSlopePos)
    {
        float slopeThreshold = Mathf.Tan(angleOfRepose * Mathf.PI / 180f) * pixelSizeH;

        float minSlope = 0;
        minSlopePos = centre;

        
        Vector2Int left = new Vector2Int((centre.x - 1) & xMax, centre.y);
        float leftSlope = (bedrockMap[left.x, left.y] + sandMap[left.x, left.y] - centreHeight) + slopeThreshold;
        if (leftSlope < minSlope && left != exclusion)
        {
            minSlope = leftSlope;
            minSlopePos = left;
        }
        Vector2Int right = new Vector2Int((centre.x + 1) & xMax, centre.y);
        float rightSlope = (bedrockMap[right.x, right.y] + sandMap[right.x, right.y] - centreHeight) + slopeThreshold;
        if (rightSlope < minSlope && right != exclusion)
        {
            minSlope = rightSlope;
            minSlopePos = right;
        }

        return minSlope;
    }

















    float HyperbolicTan(float x)
    {
        return (Mathf.Exp(2 * x) - 1) / (Mathf.Exp(2 * x) + 1);
    }


}