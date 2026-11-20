using UnityEngine;
using UnityEditor;

public class HeightmapImporter : EditorWindow
{
    [MenuItem("Narzêdzia/Wgraj Heightmapê")]
    public static void Import()
    {
        // Zak³adamy, ¿e masz zaznaczony obrazek w oknie Project
        Texture2D texture = Selection.activeObject as Texture2D;
        if (texture == null) { Debug.LogError("Zaznacz obrazek PNG w oknie Project!"); return; }

        // Szukamy terenu na scenie
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null) { Debug.LogError("Nie znaleziono Terenu na scenie!"); return; }

        TerrainData data = terrain.terrainData;
        int res = data.heightmapResolution;
        float[,] heights = new float[res, res];

        for (int x = 0; x < res; x++)
        {
            for (int y = 0; y < res; y++)
            {
                // Mapujemy piksele obrazka na siatkê terenu
                float pixelX = (float)x / res * texture.width;
                float pixelY = (float)y / res * texture.height;
                heights[y, x] = texture.GetPixelBilinear(pixelX / texture.width, pixelY / texture.height).grayscale;
            }
        }

        data.SetHeights(0, 0, heights);
        Debug.Log("Pla¿a za³adowana pomyœlnie!");
    }
}