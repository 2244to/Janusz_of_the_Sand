using UnityEngine;
using UnityEditor;

public class ArrayTool : EditorWindow
{
    private GameObject objectToRepeat;
    private int count = 5;
    private Vector3 offset = new Vector3(2f, 0f, 0f);

    [MenuItem("Narzêdzia/Generator Array")]
    public static void ShowWindow()
    {
        // Otwiera ma³e, zgrabne okienko w edytorze
        GetWindow<ArrayTool>("Generator Array");
    }

    void OnGUI()
    {
        GUILayout.Label("Ustawienia modyfikatora Array", EditorStyles.boldLabel);

        // Pola do wpisywania wartoœci w okienku
        objectToRepeat = (GameObject)EditorGUILayout.ObjectField("Obiekt bazowy", objectToRepeat, typeof(GameObject), true);
        count = EditorGUILayout.IntField("Liczba kopii (Count)", count);
        offset = EditorGUILayout.Vector3Field("Odstêp (Offset)", offset);

        GUILayout.Space(10);

        if (GUILayout.Button("Generuj Array"))
        {
            Generate();
        }
    }

    private void Generate()
    {
        if (objectToRepeat == null)
        {
            Debug.LogError("Najpierw przeci¹gnij obiekt do pola 'Obiekt bazowy'!");
            return;
        }

        if (count < 1) count = 1;

        // Tworzymy pusty obiekt-rodzic, ¿eby utrzymaæ porz¹dek w Hierarchy
        GameObject container = new GameObject(objectToRepeat.name + "_Array");
        container.transform.position = objectToRepeat.transform.position;

        // Pêtla generuj¹ca kopie
        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPosition = objectToRepeat.transform.position + (offset * i);

            // Spawnujemy obiekt jako oryginalny Prefab (jeœli nim by³), ¿eby nie straciæ powi¹zania
            GameObject clone = (GameObject)PrefabUtility.InstantiatePrefab(PrefabUtility.GetCorrespondingObjectFromSource(objectToRepeat));

            // Jeœli obiekt nie by³ prefabem, tworzymy zwyk³¹ kopiê
            if (clone == null)
            {
                clone = Instantiate(objectToRepeat);
            }

            clone.transform.position = spawnPosition;
            clone.transform.rotation = objectToRepeat.transform.rotation;
            clone.transform.localScale = objectToRepeat.transform.localScale;

            // Wrzucamy do kontenera
            clone.transform.parent = container.transform;

            // Informujemy Unity, ¿e stworzyliœmy obiekt (¿eby dzia³a³o Ctrl+Z)
            Undo.RegisterCreatedObjectUndo(clone, "Array Generation");
        }

        // Informujemy Unity, ¿e stworzyliœmy kontener
        Undo.RegisterCreatedObjectUndo(container, "Array Container");

        Debug.Log("Pomyœlnie wygenerowano " + count + " obiektów!");
    }
}