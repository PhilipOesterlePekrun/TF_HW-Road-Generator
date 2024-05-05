using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using UnityEditor;
using TMPro;
//using System.Diagnostics;

using Utils;

public struct MetaFileData
{
    public float scaleFactorM;
    public int maxElevM;
    public int minElevM;
    public float renderMultiplierM;
}

/// <summary>
/// Generates an RDF (and obj) of the road from the given function in the given domain
/// </summary>

public class RDF_Generator_FromFunction : MonoBehaviour
{
    static string rdfTemplatePath = @"PCD_road_template.rdf";
    static string savedMeshFolderPath =@"Saved Meshes\"; // after "Assets\"
    static string savedName = "rdf_tmp"; // for obj and rdf

    public Material meshMaterial;
    float[] domainX = { -100000,4000};//{ -80000f,1600f}; // all lengths in mm
    float[] domainY = { -2000,2000};//{ -8000f,8000f};

    bool rightHandCoSy = true; // Unity UI is left-handed CoSy for some reason, but Altair is right-handed
    bool zMainAxis = true; // Y up in Unity for some reason

    float resolutionX =10;
    float resolutionY =2000;
    // // // FUNCTIONS
    // // ELEVATION
    float elevation_uniform(float x,float y)
    {
        return 0;
    }
    float elevation_sinX_linY(float x, float y)
    {
        float wavelength = 200;
        float amplitudeMax = 50;
        return (y - domainY[0]) * amplitudeMax /(domainY[1]-domainY[0])* Mathf.Sin(-x* Mathf.PI / wavelength);
    }
    float elevation_sinX(float x, float y)
    {
        float wavelength =400;
        float amplitude =50;
        return amplitude * Mathf.Sin(-x*Mathf.PI/wavelength);
    }
    float elevation_bump(float x, float y)
    {
        if (x < -10000 && x > -10500)
        {
            return 50;
        }
        return 0;
    }
    float elevation_bumpOneSide(float x, float y)
    {
        if (x < -10000 && x > -11000 && y <= -300)
        {
            return 75;
        }
        return 0;
    }
    float elevation_cattleGrids(float x, float y)
    {
        for(int i = 0; i < 30; i++)
        {
            if (x < -20000 - 220 * i && x > -20000 - 220 * i - 70)
            {
                return 20;
            }
        }
        return 0;
    }
    float elevation_drop(float x,float y)
    {
        if (x<-10000)
        {
            return -500;
        }
        return 0;
    }
    float elevation_dropWithSlope(float x, float y)
    {
        if (x < -10000)
        {
            return -500;
        }
        if (x>-10000&&x<-8000)
        {
            return 500/2000*(x+8000);
        }
        return 0;
    }
    float elevation_dropSigmoid(float x,float y)
    {
        float horizontalCoefficient = 0.05f; // as this -> infty, slope approaches vertical
        float verticalCoefficient = 200;
        float critPos = -10000;
        return -verticalCoefficient/ (1 + Mathf.Exp((x-critPos)*horizontalCoefficient));
    }
    float elevation_sinXsinY(float x, float y)
    {
        if (Mathf.Abs(y) < 2000)
        {
            return 0;
        }
        int amplitude = 20;
        int wavelength = 500;
        return amplitude * Mathf.Sin(-x * Mathf.PI / wavelength) * Mathf.Sin(-y * Mathf.PI / wavelength);
    }
    float elevation_pothole(float x, float y)
    {
        if (x<-6000&& x>-6000-2*279.2*2&& y>-1000&&y<1000)
        {
            return -40;
        }
        return 0;
    }
    float elevation_bumpThenPothole(float x, float y)
    {
        /*if (x < -25000 && x > -25000- 2 * 279.2)
        {
            return -50;
        }*/
        if (x < -5000 && x > -5000 - 2 * 279.2)
        {
            return 40;
        }
        return 0;
    }

    // // FRICTION
    float friction_uniform(float x,float y)
    {
        return 100;
    }

    // //
    float elevationFunction(float x, float y)
    {
        if (x > -4000)
        {
            return 0;
        }
        return elevation_uniform(x+2000, y);
    }
    float frictionFunction(float x, float y) // Coefficient of friction mu
    {
        return friction_uniform(x, y);
    }

    short inDomain(float x, float y) // possibly not really needed
    {
        if (x>domainX[0] && x < domainX[1]&&y> domainX[0] &&y< domainX[1])
        {
            return 1;
        }
        else
        {
            return 0;
        }
    }

    int vectToLin(int w, int x, int y)
    {
        return w * y + x;
    }
    int[] linToVect(int pos, int w, int h)
    {
        if (pos < 0 || pos > w * h)
        {
            return new int[] { -1, -1 };
        }
        return new int[] { pos % w, (int)Mathf.Floor((float)pos / (float)w) };
    }

    // // BASIC HELPERS
    int boolToInt(bool b)
    {
        if (b)
        {
            return 1;
        }
        return 0;
    }
    bool intToBool(int i)
    {
        if (i == 0)
        {
            return false;
        }
        return true;
    }
    short boolToShort(bool b)
    {
        if (b)
        {
            return 1;
        }
        return 0;
    }

    void printPartOfArray<T>(T[] arr, float frac, float startFrac)
    {
        for (int i = (int)((float)arr.Length * startFrac); i < (int)((float)arr.Length * startFrac) + (int)((float)arr.Length * frac); i++)
        {
            Debug.Log(arr[i]);
        }
    }
    string Vect3ToString(Vector3 v)
    {
        return v.x + ", " + v.y + ", " + v.z;
    }

    void Start()
    {
        short RHCS = boolToShort(rightHandCoSy);
        short LHCS = boolToShort(!rightHandCoSy);

        short zUp = boolToShort(zMainAxis);
        short yUp = boolToShort(!zMainAxis);

        int gridCountX = (int)Mathf.Floor((domainX[1] - domainX[0]) / resolutionX)+1;
        int gridCountY = (int)Mathf.Floor((domainY[1] - domainY[0]) / resolutionY)+1;

        Vector3[] vertices = new Vector3[gridCountX*gridCountY]; // I could actually make it as long as I want, it does not matter as long as it is large enough
        int verticesLength = vertices.Length;
        Debug.Log("vertices.Length= " + verticesLength);


        int triangleSafetyFactor = 10;
        int[] triangles = new int[triangleSafetyFactor * gridCountX * gridCountY];
        for (int i = 0; i < triangles.Length; i++)
        {
            triangles[i] = -1;
        }

        for (int i = 0; i < gridCountX; i++) // i horizontal, j vertical
        {
            for (int j = 0; j < gridCountY; j++)
            {
                int linPos = vectToLin(gridCountX, i, j);
                vertices[linPos] = new Vector3((float)i * resolutionX + domainX[0],
                    elevationFunction(domainX[0]+(float)i * resolutionX,domainY[0]+(float)j * resolutionY) * yUp + ((float)j * resolutionY + domainY[0])*zUp,
                    elevationFunction(domainX[0]+(float)i * resolutionX,domainY[0]+(float)j * resolutionY) * zUp + ((float)j * resolutionY + domainY[0])*yUp);
            }
        }

        int trianglesPos = 0;
        for (int j = 0; j < gridCountY - 1; j++)
        {
            for (int i = 0; i < gridCountX - 1; i++)
            {
                int linPos = vectToLin(gridCountX, i, j);
                triangles[trianglesPos++] = linPos;

                triangles[trianglesPos++] = vectToLin(gridCountX, i, j + 1)*LHCS+(linPos + 1)*RHCS;
                triangles[trianglesPos++] = vectToLin(gridCountX, i, j + 1) *RHCS + (linPos + 1) *LHCS;


                triangles[trianglesPos++] = linPos + 1;
                triangles[trianglesPos++] = vectToLin(gridCountX, i, j + 1)*LHCS+ vectToLin(gridCountX, i + 1, j + 1)*RHCS;
                triangles[trianglesPos++] = vectToLin(gridCountX, i, j + 1) *RHCS + vectToLin(gridCountX, i + 1, j + 1) *LHCS;

            }
        }
        Debug.Log("Preliminary Triangles Done | t = " + Time.realtimeSinceStartup);

        // Cut down triangles length
        int trianglesLength = 0;
        while (triangles[trianglesLength] != -1)
        {
            trianglesLength++;
        }
        int[] trianglesFinal = new int[trianglesLength];
        for (int i = 0; i < trianglesLength; i++)
        {
            trianglesFinal[i] = triangles[i];
        }

        Debug.Log("Final Triangles Done | t = " + Time.realtimeSinceStartup);

        // // Friction factor (mu) array
        float[] ffMu = new float[trianglesLength];
        for (int i = 0; i < trianglesLength; i++)
        {
            int[] posI = linToVect(i, gridCountX, gridCountY);
            ffMu[i] = frictionFunction(resolutionX * posI[0], resolutionY * posI[1]);
        }
        trianglesLength = trianglesFinal.Length;

        Mesh RDFMesh = new Mesh();
        RDFMesh.indexFormat = IndexFormat.UInt32;
        // Assign vertices and triangles to the mesh
        RDFMesh.vertices = vertices;
        RDFMesh.triangles = trianglesFinal;

        // Calculate normals and other required mesh data
        RDFMesh.RecalculateNormals();
        RDFMesh.RecalculateBounds();
        RDFMesh.Optimize();

        // Create a new GameObject with a MeshFilter and MeshRenderer component
        GameObject meshObject = new GameObject("MeshObject");
        MeshFilter meshFilter = meshObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = meshObject.AddComponent<MeshRenderer>();

        // Assign the created mesh to the MeshFilter component
        meshFilter.mesh = RDFMesh;
        meshRenderer.material = meshMaterial;

        // // Obj File
        string filePathObj = Application.dataPath + @"\" + savedMeshFolderPath + savedName + ".obj";
        //Debug.Log(filePath);
        using (StreamWriter writer = new StreamWriter(filePathObj))
        {
            // Write vertices
            foreach (Vector3 vertex in vertices)
            {
                writer.WriteLine("v " + vertex.x + " " + vertex.y + " " + vertex.z);
            }

            // Write triangles
            for (int i = 0; i < trianglesFinal.Length; i += 3)
            {
                writer.WriteLine("f " + (trianglesFinal[i] + 1) + " " + (trianglesFinal[i + 1] + 1) + " " + (trianglesFinal[i + 2] + 1));
            }
        }

        Debug.Log("Mesh Saved | t = " + Time.realtimeSinceStartup);

        // // RDF File
        // "line before (if add in new line) or at (if overwrite line) x" in template
        short lNUMBER_OF_NODES = 35;
        //short lNUMBER_OF_ELEMENTS = 37;
        short bNodes = 40;
        short bElements = 43;

        string[] rdfText = File.ReadAllLines(Application.dataPath +@"\"+rdfTemplatePath);
        string filePathRDF = Application.dataPath + @"\" + savedMeshFolderPath + savedName + ".rdf";
        using (StreamWriter writer = new StreamWriter(filePathRDF))
        {
            //
            int currentLine = 0;
            while (currentLine < lNUMBER_OF_NODES)
            {
                writer.WriteLine(rdfText[currentLine]); currentLine++;
            }
            writer.WriteLine(" NUMBER_OF_NODES\t\t= " + verticesLength); currentLine++;
            writer.WriteLine(" NUMBER_OF_ELEMENTS    = " + trianglesLength/3); currentLine++;

            while (currentLine <= bNodes)
            {
                writer.WriteLine(rdfText[currentLine]); currentLine++;
            }

            for (int i =0; i < verticesLength; i++)
            {
                writer.WriteLine((i+1)+ "\t" + vertices[i].x + "\t" + vertices[i].y + "\t" + vertices[i].z); currentLine++;
            }

            for(int i=bNodes+1;i<bElements;i++)
            {
                writer.WriteLine(rdfText[i]); currentLine++;
            }
            for(int i =0;i<trianglesLength; i+=3)
            {
                writer.WriteLine((trianglesFinal[i]+1)+ "\t" + (trianglesFinal[i + 1]+1)+ "\t" + (trianglesFinal[i + 2]+1)+ "\t" + ffMu[i / 3]);
            }
        }

        // // ENDING LOGS
        Debug.Log("Finished | t = " + Time.realtimeSinceStartup);

        // // STORE ENDING LOGS/METADATA
        string filePathOUTMETA = Application.dataPath + @"\" + savedMeshFolderPath + savedName + "META.txt";
        using (StreamWriter writerOUTMETA = new StreamWriter(filePathOUTMETA))
        {
            writerOUTMETA.WriteLine("widthFull x heightFull in length units: " + (float)gridCountX + " x " + (float)gridCountY);
            writerOUTMETA.WriteLine("width x height in mm: " + (float)gridCountX * resolutionX + "mm x " + (float)gridCountY * resolutionY + "mm");
            writerOUTMETA.WriteLine("");

            writerOUTMETA.WriteLine("#Vertices: " + vertices.Length);
            writerOUTMETA.WriteLine("#Triangles: " + trianglesFinal.Length / 3);
            writerOUTMETA.WriteLine("Total time taken: " + Time.realtimeSinceStartup);
        }
    }

    // Update is called once per frame
    void Update()
    {

    }
}